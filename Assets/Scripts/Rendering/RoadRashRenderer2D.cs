using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Renders a Road Rash-style 2D pseudo-3D behind-the-vehicle perspective view
    /// including the scrolling curved track, player motorcycle, AI opponent motorcycles
    /// scaled by perspective distance, and a 2D minimap of all racers.
    /// </summary>
    public class RoadRashRenderer2D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private WaypointTrack track;
        [SerializeField] private Car playerCar;

        [Header("Pseudo-3D Projection")]
        [SerializeField] private int sliceCount = 85;
        [SerializeField] private float sliceStep = 2.0f;
        [SerializeField] private float cameraHeight = 2.6f;
        [SerializeField] private float cameraDepth = 0.85f;
        [SerializeField] private float curveVisualGain = 2.4f;
        [SerializeField] private float hillVisualGain = 0.045f;

        [Header("Colors")]
        [SerializeField] private Color skyTopColor = new Color(0.12f, 0.36f, 0.78f);
        [SerializeField] private Color skyHorizonColor = new Color(0.62f, 0.84f, 0.98f);
        [SerializeField] private Color hillColor = new Color(0.16f, 0.35f, 0.25f);
        [SerializeField] private Color grassLight = new Color(0.18f, 0.56f, 0.24f);
        [SerializeField] private Color grassDark = new Color(0.14f, 0.46f, 0.19f);
        [SerializeField] private Color roadLight = new Color(0.24f, 0.25f, 0.28f);
        [SerializeField] private Color roadDark = new Color(0.20f, 0.21f, 0.24f);
        [SerializeField] private Color curbLight = new Color(0.95f, 0.95f, 0.95f);
        [SerializeField] private Color curbDark = new Color(0.86f, 0.18f, 0.18f);

        private readonly List<Car> _allCars = new List<Car>();
        private readonly List<OpponentSpriteSlot> _opponentPool = new List<OpponentSpriteSlot>();

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private readonly List<Vector3> _verts = new List<Vector3>(4096);
        private readonly List<int> _tris = new List<int>(6144);
        private readonly List<Color> _colors = new List<Color>(4096);

        private Sprite _bikeSprite;
        private Sprite _shadowSprite;
        private SpriteRenderer _bikeRenderer;
        private SpriteRenderer _shadowRenderer;
        private float _horizonScroll;

        private struct SliceData
        {
            public float worldZ;
            public float screenY;
            public float centerX;
            public float roadHalfW;
            public float curbHalfW;
            public float scale;
            public float worldS;
            public int stripeIndex;
            public bool isStartLine;
            public bool occluded;
        }

        private class OpponentSpriteSlot
        {
            public SpriteRenderer bike;
            public SpriteRenderer shadow;
        }

        private SliceData[] _slices;

        public void SetSceneReferences(Camera cam, WaypointTrack trackRef, Car player, IReadOnlyList<Car> cars)
        {
            targetCamera = cam != null ? cam : Camera.main;
            track = trackRef;
            playerCar = player;
            _allCars.Clear();
            if (cars != null)
            {
                for (int i = 0; i < cars.Count; i++)
                {
                    if (cars[i] != null) _allCars.Add(cars[i]);
                }
            }
        }

        private void Awake()
        {
            EnsureRenderObjects();
            _slices = new SliceData[sliceCount];
        }

        private void EnsureRenderObjects()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null)
            {
                targetCamera.orthographic = true;
                targetCamera.orthographicSize = 5f;
                targetCamera.transform.position = new Vector3(0f, 0f, -10f);
                targetCamera.transform.rotation = Quaternion.identity;
                targetCamera.backgroundColor = skyTopColor;
            }

            if (_bikeSprite == null) _bikeSprite = CreateBikeSprite();
            if (_shadowSprite == null) _shadowSprite = CreateShadowSprite();

            if (_meshFilter == null)
            {
                GameObject roadObj = new GameObject("RoadRashMesh");
                roadObj.transform.SetParent(transform, false);
                roadObj.transform.localPosition = Vector3.zero;
                _meshFilter = roadObj.AddComponent<MeshFilter>();
                _meshRenderer = roadObj.AddComponent<MeshRenderer>();

                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                Material mat = new Material(shader);
                _meshRenderer.sharedMaterial = mat;
                _meshRenderer.sortingOrder = 0;

                _mesh = new Mesh { name = "RoadRashPseudo3DMesh" };
                _mesh.MarkDynamic();
                _meshFilter.sharedMesh = _mesh;
            }

            if (_bikeRenderer == null)
            {
                GameObject shadowObj = new GameObject("PlayerBikeShadow");
                shadowObj.transform.SetParent(transform, false);
                _shadowRenderer = shadowObj.AddComponent<SpriteRenderer>();
                _shadowRenderer.sprite = _shadowSprite;
                _shadowRenderer.color = new Color(0f, 0f, 0f, 0.45f);
                _shadowRenderer.sortingOrder = 499;

                GameObject bikeObj = new GameObject("PlayerBikeSprite");
                bikeObj.transform.SetParent(transform, false);
                _bikeRenderer = bikeObj.AddComponent<SpriteRenderer>();
                _bikeRenderer.sprite = _bikeSprite;
                _bikeRenderer.sortingOrder = 500;
            }
        }

        private void LateUpdate()
        {
            if (track == null || playerCar == null || targetCamera == null) return;
            if (_slices == null || _slices.Length != sliceCount) _slices = new SliceData[sliceCount];

            RenderFrame();
            UpdatePlayerBikeVisual();
            UpdateOpponentBikeVisuals();
        }

        private void RenderFrame()
        {
            _verts.Clear();
            _tris.Clear();
            _colors.Clear();

            float halfH = targetCamera.orthographicSize;
            float halfW = halfH * targetCamera.aspect;

            float baseS = playerCar.TrackDistance;
            TrackSample playerSample = track.SampleAtDistance(baseS);
            float headingError = SignedAngleRad(playerSample.tangent, playerCar.Physics.Forward);
            float baseElevation = playerSample.elevation;

            float horizonY = halfH * 0.18f;

            // 1. Sky gradient
            AddQuad(
                new Vector2(-halfW, horizonY),
                new Vector2(halfW, horizonY),
                new Vector2(halfW, halfH),
                new Vector2(-halfW, halfH),
                skyHorizonColor, skyHorizonColor, skyTopColor, skyTopColor, 0f
            );

            _horizonScroll -= playerCar.Physics.ForwardSpeed * headingError * Time.deltaTime * 0.15f;
            DrawDistantHills(halfW, horizonY);

            // 2. Project road slices
            float curveAcc = 0f;
            float xOffset = 0f;
            float playerLatNorm = playerCar.LateralOffset / Mathf.Max(1f, track.TrackWidth * 0.5f);
            float headingShift = Mathf.Tan(Mathf.Clamp(headingError, -0.7f, 0.7f)) * halfW * 0.85f;

            int validSlices = 0;
            float maxDrawnY = -halfH;

            for (int i = 0; i < sliceCount; i++)
            {
                float worldZ = (i + 1) * sliceStep;
                float scale = cameraDepth / (worldZ * 0.14f + 0.35f);
                float sampleS = track.WrapDistance(baseS + worldZ);
                TrackSample sample = track.SampleAtDistance(sampleS);

                curveAcc += sample.curvature * sliceStep * curveVisualGain;
                xOffset += curveAcc * 0.045f;

                float hillDelta = (sample.elevation - baseElevation) * hillVisualGain * scale;
                float projY = horizonY - cameraHeight * scale * 2.3f + hillDelta;

                if (i == 0) projY = Mathf.Min(projY, -halfH * 0.99f);
                bool occluded = projY < maxDrawnY;
                float clampedY = Mathf.Max(projY, maxDrawnY);

                float roadHalfW = (track.TrackWidth * 0.5f) * scale * 0.42f;
                float curbHalfW = roadHalfW * 1.16f;
                float centerX = xOffset - playerLatNorm * roadHalfW * 0.95f + headingShift * (i / (float)sliceCount);

                int stripe = Mathf.FloorToInt(sampleS / 6f);
                bool isStartLine = sampleS < 5f || sampleS > track.TotalLength - 3f;

                _slices[i] = new SliceData
                {
                    worldZ = worldZ,
                    screenY = clampedY,
                    centerX = centerX,
                    roadHalfW = roadHalfW,
                    curbHalfW = curbHalfW,
                    scale = scale,
                    worldS = sampleS,
                    stripeIndex = stripe,
                    isStartLine = isStartLine,
                    occluded = occluded
                };

                if (projY > maxDrawnY) maxDrawnY = projY;
                validSlices++;
            }

            for (int i = 0; i < validSlices - 1; i++)
            {
                SliceData a = _slices[i];
                SliceData b = _slices[i + 1];
                if (b.screenY <= a.screenY + 0.0001f) continue;

                bool even = (a.stripeIndex % 2) == 0;
                Color grassCol = even ? grassLight : grassDark;
                Color curbCol = even ? curbLight : curbDark;
                Color roadCol = a.isStartLine ? Color.white : (even ? roadLight : roadDark);

                AddQuad(
                    new Vector2(-halfW, a.screenY),
                    new Vector2(halfW, a.screenY),
                    new Vector2(halfW, b.screenY),
                    new Vector2(-halfW, b.screenY),
                    grassCol, grassCol, grassCol, grassCol, 0f
                );

                AddQuad(
                    new Vector2(a.centerX - a.curbHalfW, a.screenY),
                    new Vector2(a.centerX + a.curbHalfW, a.screenY),
                    new Vector2(b.centerX + b.curbHalfW, b.screenY),
                    new Vector2(b.centerX - b.curbHalfW, b.screenY),
                    curbCol, curbCol, curbCol, curbCol, 0f
                );

                AddQuad(
                    new Vector2(a.centerX - a.roadHalfW, a.screenY),
                    new Vector2(a.centerX + a.roadHalfW, a.screenY),
                    new Vector2(b.centerX + b.roadHalfW, b.screenY),
                    new Vector2(b.centerX - b.roadHalfW, b.screenY),
                    roadCol, roadCol, roadCol, roadCol, 0f
                );

                if (even && !a.isStartLine)
                {
                    float dashA = Mathf.Max(0.015f, a.roadHalfW * 0.045f);
                    float dashB = Mathf.Max(0.01f, b.roadHalfW * 0.045f);
                    AddQuad(
                        new Vector2(a.centerX - dashA, a.screenY),
                        new Vector2(a.centerX + dashA, a.screenY),
                        new Vector2(b.centerX + dashB, b.screenY),
                        new Vector2(b.centerX - dashB, b.screenY),
                        Color.white, Color.white, Color.white, Color.white, 0f
                    );
                }
            }

            // 3. Roadside props
            for (int i = validSlices - 2; i >= 1; i--)
            {
                SliceData s = _slices[i];
                if (s.occluded) continue;
                int propSlot = Mathf.FloorToInt(s.worldS / 16f);
                int prevSlot = Mathf.FloorToInt(_slices[i - 1].worldS / 16f);
                if (propSlot != prevSlot)
                {
                    float propScale = s.scale * 0.7f;
                    bool isTree = (propSlot % 2) == 0;
                    float sideOffset = s.curbHalfW + propScale * 1.1f;

                    DrawRoadsideProp(s.centerX - sideOffset, s.screenY, propScale, isTree);
                    DrawRoadsideProp(s.centerX + sideOffset, s.screenY, propScale, !isTree);
                }
            }

            // 4. Top-right 2D Minimap showing all racers
            DrawMinimap(halfW, halfH);

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetTriangles(_tris, 0);
            _mesh.SetColors(_colors);
            _mesh.RecalculateBounds();
        }

        private void UpdateOpponentBikeVisuals()
        {
            float maxVisibleZ = sliceCount * sliceStep;
            float totalLen = track.TotalLength;
            int slotIdx = 0;

            for (int i = 0; i < _allCars.Count; i++)
            {
                Car other = _allCars[i];
                if (other == null || other == playerCar) continue;

                float dz = other.TrackDistance - playerCar.TrackDistance;
                if (dz < -totalLen * 0.5f) dz += totalLen;
                if (dz > totalLen * 0.5f) dz -= totalLen;

                // Show opponent bikes within visible range ahead (and slightly beside player)
                if (dz >= 0.8f && dz <= maxVisibleZ - sliceStep)
                {
                    if (TrySampleProjectedSlice(dz, out SliceData slice) && !slice.occluded)
                    {
                        OpponentSpriteSlot slot = GetOrCreateOpponentSlot(slotIdx++);
                        slot.bike.gameObject.SetActive(true);
                        slot.shadow.gameObject.SetActive(true);

                        float otherLatNorm = other.LateralOffset / Mathf.Max(1f, track.TrackWidth * 0.5f);
                        float screenX = slice.centerX + otherLatNorm * slice.roadHalfW * 0.95f;
                        float spriteScale = slice.scale * 2.55f;

                        int order = Mathf.Clamp(480 - Mathf.RoundToInt(dz * 2.5f), 20, 480);

                        slot.bike.transform.position = new Vector3(screenX, slice.screenY, -0.15f);
                        slot.bike.transform.rotation = Quaternion.Euler(0f, 0f, -other.LeanAngle * 22f);
                        slot.bike.transform.localScale = Vector3.one * spriteScale;
                        slot.bike.color = other.BikeColor;
                        slot.bike.sortingOrder = order;

                        slot.shadow.transform.position = new Vector3(screenX, slice.screenY - 0.04f * slice.scale, -0.1f);
                        slot.shadow.transform.localScale = new Vector3(2.1f * spriteScale * 0.36f, 0.55f * spriteScale * 0.36f, 1f);
                        slot.shadow.sortingOrder = order - 1;
                    }
                }
            }

            for (int i = slotIdx; i < _opponentPool.Count; i++)
            {
                _opponentPool[i].bike.gameObject.SetActive(false);
                _opponentPool[i].shadow.gameObject.SetActive(false);
            }
        }

        private bool TrySampleProjectedSlice(float dz, out SliceData result)
        {
            float fIdx = (dz / sliceStep) - 1f;
            int idx0 = Mathf.Clamp(Mathf.FloorToInt(fIdx), 0, sliceCount - 2);
            int idx1 = idx0 + 1;
            float t = Mathf.Clamp01(fIdx - idx0);

            SliceData a = _slices[idx0];
            SliceData b = _slices[idx1];

            result = new SliceData
            {
                worldZ = dz,
                screenY = Mathf.Lerp(a.screenY, b.screenY, t),
                centerX = Mathf.Lerp(a.centerX, b.centerX, t),
                roadHalfW = Mathf.Lerp(a.roadHalfW, b.roadHalfW, t),
                curbHalfW = Mathf.Lerp(a.curbHalfW, b.curbHalfW, t),
                scale = Mathf.Lerp(a.scale, b.scale, t),
                occluded = a.occluded && b.occluded
            };
            return true;
        }

        private OpponentSpriteSlot GetOrCreateOpponentSlot(int index)
        {
            while (_opponentPool.Count <= index)
            {
                GameObject shadowObj = new GameObject($"OpponentShadow_{_opponentPool.Count}");
                shadowObj.transform.SetParent(transform, false);
                SpriteRenderer shadowSr = shadowObj.AddComponent<SpriteRenderer>();
                shadowSr.sprite = _shadowSprite;
                shadowSr.color = new Color(0f, 0f, 0f, 0.42f);

                GameObject bikeObj = new GameObject($"OpponentBike_{_opponentPool.Count}");
                bikeObj.transform.SetParent(transform, false);
                SpriteRenderer bikeSr = bikeObj.AddComponent<SpriteRenderer>();
                bikeSr.sprite = _bikeSprite;

                _opponentPool.Add(new OpponentSpriteSlot { bike = bikeSr, shadow = shadowSr });
            }
            return _opponentPool[index];
        }

        private void DrawDistantHills(float halfW, float horizonY)
        {
            int hillSegments = 12;
            float segW = (halfW * 2f) / hillSegments;
            for (int i = 0; i < hillSegments; i++)
            {
                float x0 = -halfW + i * segW;
                float x1 = x0 + segW;
                float h0 = horizonY + 0.35f + Mathf.Sin(i * 0.9f + _horizonScroll) * 0.28f;
                float h1 = horizonY + 0.35f + Mathf.Sin((i + 1) * 0.9f + _horizonScroll) * 0.28f;
                AddQuad(
                    new Vector2(x0, horizonY),
                    new Vector2(x1, horizonY),
                    new Vector2(x1, Mathf.Max(horizonY, h1)),
                    new Vector2(x0, Mathf.Max(horizonY, h0)),
                    hillColor, hillColor, hillColor * 0.85f, hillColor * 0.85f, 0f
                );
            }
        }

        private void DrawRoadsideProp(float x, float y, float scale, bool isTree)
        {
            if (isTree)
            {
                float tw = 0.14f * scale;
                float th = 0.65f * scale;
                Color trunk = new Color(0.38f, 0.24f, 0.12f);
                AddQuad(
                    new Vector2(x - tw, y),
                    new Vector2(x + tw, y),
                    new Vector2(x + tw, y + th),
                    new Vector2(x - tw, y + th),
                    trunk, trunk, trunk, trunk, 0f
                );

                float fw = 0.65f * scale;
                float fh = 1.25f * scale;
                Color leaf = new Color(0.10f, 0.42f, 0.16f);
                Color leafTop = new Color(0.18f, 0.58f, 0.22f);
                AddQuad(
                    new Vector2(x - fw, y + th * 0.8f),
                    new Vector2(x + fw, y + th * 0.8f),
                    new Vector2(x + fw * 0.65f, y + th * 0.8f + fh),
                    new Vector2(x - fw * 0.65f, y + th * 0.8f + fh),
                    leaf, leaf, leafTop, leafTop, 0f
                );
            }
            else
            {
                float pw = 0.09f * scale;
                float ph = 0.55f * scale;
                Color postCol = new Color(0.9f, 0.9f, 0.9f);
                Color topCol = new Color(0.95f, 0.45f, 0.1f);
                AddQuad(
                    new Vector2(x - pw, y),
                    new Vector2(x + pw, y),
                    new Vector2(x + pw, y + ph),
                    new Vector2(x - pw, y + ph),
                    postCol, postCol, postCol, postCol, 0f
                );
                AddQuad(
                    new Vector2(x - pw * 1.2f, y + ph * 0.72f),
                    new Vector2(x + pw * 1.2f, y + ph * 0.72f),
                    new Vector2(x + pw * 1.2f, y + ph),
                    new Vector2(x - pw * 1.2f, y + ph),
                    topCol, topCol, topCol, topCol, 0f
                );
            }
        }

        private void DrawMinimap(float halfW, float halfH)
        {
            IReadOnlyList<Vector2> wps = track.Waypoints;
            if (wps == null || wps.Count < 2) return;

            Vector2 min = wps[0];
            Vector2 max = wps[0];
            for (int i = 1; i < wps.Count; i++)
            {
                min = Vector2.Min(min, wps[i]);
                max = Vector2.Max(max, wps[i]);
            }

            Vector2 size = max - min;
            float maxDim = Mathf.Max(1f, Mathf.Max(size.x, size.y));
            float mapSize = 2.1f;
            Vector2 mapOrigin = new Vector2(halfW - mapSize - 0.35f, halfH - mapSize - 0.35f);

            Color bg = new Color(0f, 0f, 0f, 0.55f);
            AddQuad(
                mapOrigin + new Vector2(-0.15f, -0.15f),
                mapOrigin + new Vector2(mapSize + 0.15f, -0.15f),
                mapOrigin + new Vector2(mapSize + 0.15f, mapSize + 0.15f),
                mapOrigin + new Vector2(-0.15f, mapSize + 0.15f),
                bg, bg, bg, bg, 0f
            );

            Vector2 ToMap(Vector2 worldPos)
            {
                Vector2 norm = (worldPos - min) / maxDim;
                return mapOrigin + norm * mapSize;
            }

            int count = wps.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = ToMap(wps[i]);
                Vector2 b = ToMap(wps[(i + 1) % count]);
                AddLineQuad(a, b, 0.045f, new Color(0.85f, 0.85f, 0.9f, 0.95f));
            }

            // Draw AI dots first
            for (int i = 0; i < _allCars.Count; i++)
            {
                Car c = _allCars[i];
                if (c == null || c == playerCar) continue;
                Vector2 p = ToMap(c.Physics.Position);
                float r = 0.065f;
                AddQuad(
                    p + new Vector2(-r, -r),
                    p + new Vector2(r, -r),
                    p + new Vector2(r, r),
                    p + new Vector2(-r, r),
                    c.BikeColor, c.BikeColor, c.BikeColor, c.BikeColor, 0f
                );
            }

            // Draw player dot on top
            Vector2 playerPt = ToMap(playerCar.Physics.Position);
            float pr = 0.088f;
            Color pCol = playerCar.IsOnTrack ? Color.yellow : Color.red;
            AddQuad(
                playerPt + new Vector2(-pr, -pr),
                playerPt + new Vector2(pr, -pr),
                playerPt + new Vector2(pr, pr),
                playerPt + new Vector2(-pr, pr),
                pCol, pCol, pCol, pCol, 0f
            );
        }

        private void UpdatePlayerBikeVisual()
        {
            if (_bikeRenderer == null || playerCar == null) return;

            float speedNorm = Mathf.Clamp01(playerCar.Physics.Speed / playerCar.Physics.MaxSpeed);
            float vibe = Mathf.Sin(Time.time * 45f) * 0.025f * speedNorm;
            if (!playerCar.IsOnTrack && playerCar.Physics.Speed > 3f)
            {
                vibe += Mathf.Sin(Time.time * 75f) * 0.055f;
            }

            Vector3 bikePos = new Vector3(0f, -3.1f + vibe, -0.2f);
            _bikeRenderer.transform.position = bikePos;
            _bikeRenderer.transform.rotation = Quaternion.Euler(0f, 0f, -playerCar.LeanAngle * 24f);
            _bikeRenderer.transform.localScale = Vector3.one * 2.8f;

            _bikeRenderer.color = playerCar.LastInput.brake > 0.1f
                ? new Color(1f, 0.82f, 0.82f, 1f)
                : playerCar.BikeColor;

            if (_shadowRenderer != null)
            {
                _shadowRenderer.transform.position = new Vector3(0f, -3.75f, -0.1f);
                _shadowRenderer.transform.localScale = new Vector3(2.4f, 0.65f, 1f);
            }
        }

        private void AddLineQuad(Vector2 a, Vector2 b, float thickness, Color col)
        {
            Vector2 dir = b - a;
            if (dir.sqrMagnitude < 0.00001f) return;
            Vector2 perp = new Vector2(-dir.y, dir.x).normalized * (thickness * 0.5f);
            AddQuad(a - perp, a + perp, b + perp, b - perp, col, col, col, col, 0f);
        }

        private void AddQuad(Vector2 bl, Vector2 br, Vector2 tr, Vector2 tl, Color cbl, Color cbr, Color ctr, Color ctl, float z)
        {
            int idx = _verts.Count;
            _verts.Add(new Vector3(bl.x, bl.y, z));
            _verts.Add(new Vector3(br.x, br.y, z));
            _verts.Add(new Vector3(tr.x, tr.y, z));
            _verts.Add(new Vector3(tl.x, tl.y, z));

            _colors.Add(cbl);
            _colors.Add(cbr);
            _colors.Add(ctr);
            _colors.Add(ctl);

            _tris.Add(idx);
            _tris.Add(idx + 2);
            _tris.Add(idx + 1);
            _tris.Add(idx);
            _tris.Add(idx + 3);
            _tris.Add(idx + 2);
        }

        private static float SignedAngleRad(Vector2 from, Vector2 to)
        {
            float cross = from.x * to.y - from.y * to.x;
            float dot = Vector2.Dot(from, to);
            return Mathf.Atan2(cross, dot);
        }

        private static Sprite CreateBikeSprite()
        {
            int w = 32;
            int h = 32;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color tire = new Color(0.12f, 0.12f, 0.14f);
            Color bikeBody = new Color(0.95f, 0.95f, 0.95f);
            Color tailLight = new Color(1f, 0.25f, 0.15f);
            Color leather = new Color(0.18f, 0.20f, 0.26f);
            Color jacket = new Color(0.85f, 0.88f, 0.96f);
            Color helmet = new Color(0.96f, 0.96f, 0.98f);
            Color visor = new Color(0.1f, 0.14f, 0.2f);
            Color chrome = new Color(0.75f, 0.78f, 0.82f);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, clear);

            void Rect(int x0, int y0, int x1, int y1, Color c)
            {
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        if (x >= 0 && x < w && y >= 0 && y < h) tex.SetPixel(x, y, c);
            }

            Rect(13, 1, 18, 9, tire);
            Rect(11, 3, 12, 7, chrome);
            Rect(19, 3, 20, 7, chrome);
            Rect(11, 8, 20, 15, bikeBody);
            Rect(14, 9, 17, 11, tailLight);
            Rect(8, 16, 23, 17, chrome);
            Rect(11, 10, 13, 17, leather);
            Rect(18, 10, 20, 17, leather);
            Rect(11, 16, 20, 23, jacket);
            Rect(13, 24, 18, 29, helmet);
            Rect(14, 25, 17, 26, visor);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.2f), 32f);
        }

        private static Sprite CreateShadowSprite()
        {
            Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float dx = (x - 7.5f) / 7.5f;
                    float dy = (y - 7.5f) / 7.5f;
                    float a = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
