using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Renders a Road Rash-style 2D pseudo-3D behind-the-vehicle perspective view
    /// driven by the underlying 2D WaypointTrack and VehiclePhysics simulation.
    /// Also renders a compact 2D top-down minimap in the top-right corner.
    /// </summary>
    public class RoadRashRenderer2D : MonoBehaviour
    {
        [Header("References")]
        public Camera targetCamera;
        public WaypointTrack track;
        public Car playerCar;

        [Header("Pseudo-3D Projection")]
        public int sliceCount = 85;
        public float sliceStep = 2.0f;
        public float cameraHeight = 2.6f;
        public float cameraDepth = 0.85f;
        public float curveVisualGain = 2.4f;
        public float hillVisualGain = 0.045f;

        [Header("Colors")]
        public Color skyTopColor = new Color(0.12f, 0.36f, 0.78f);
        public Color skyHorizonColor = new Color(0.62f, 0.84f, 0.98f);
        public Color hillColor = new Color(0.16f, 0.35f, 0.25f);
        public Color grassLight = new Color(0.18f, 0.56f, 0.24f);
        public Color grassDark = new Color(0.14f, 0.46f, 0.19f);
        public Color roadLight = new Color(0.24f, 0.25f, 0.28f);
        public Color roadDark = new Color(0.20f, 0.21f, 0.24f);
        public Color curbLight = new Color(0.95f, 0.95f, 0.95f);
        public Color curbDark = new Color(0.86f, 0.18f, 0.18f);

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private readonly List<Vector3> _verts = new List<Vector3>(4096);
        private readonly List<int> _tris = new List<int>(6144);
        private readonly List<Color> _colors = new List<Color>(4096);

        private SpriteRenderer _bikeRenderer;
        private SpriteRenderer _shadowRenderer;
        private float _horizonScroll;

        private struct SliceData
        {
            public float screenY;
            public float centerX;
            public float roadHalfW;
            public float curbHalfW;
            public float scale;
            public float worldS;
            public int stripeIndex;
            public bool isStartLine;
        }

        private SliceData[] _slices;

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
                _shadowRenderer.sprite = CreateShadowSprite();
                _shadowRenderer.color = new Color(0f, 0f, 0f, 0.45f);
                _shadowRenderer.sortingOrder = 9;

                GameObject bikeObj = new GameObject("PlayerBikeSprite");
                bikeObj.transform.SetParent(transform, false);
                _bikeRenderer = bikeObj.AddComponent<SpriteRenderer>();
                _bikeRenderer.sprite = CreateBikeSprite();
                _bikeRenderer.sortingOrder = 10;
            }
        }

        private void LateUpdate()
        {
            if (track == null || playerCar == null || targetCamera == null) return;
            if (_slices == null || _slices.Length != sliceCount) _slices = new SliceData[sliceCount];

            RenderFrame();
            UpdatePlayerBikeVisual();
        }

        private void RenderFrame()
        {
            _verts.Clear();
            _tris.Clear();
            _colors.Clear();

            float halfH = targetCamera.orthographicSize;
            float halfW = halfH * targetCamera.aspect;
            float screenW = halfW * 2f;

            // Player track state
            float baseS = playerCar.trackDistance;
            TrackSample playerSample = track.SampleAtDistance(baseS);
            float headingError = SignedAngleRad(playerSample.tangent, playerCar.physics.Forward);
            float baseElevation = playerSample.elevation;

            // Horizon line sits slightly above screen center
            float horizonY = halfH * 0.18f;

            // 1. Sky gradient
            AddQuad(
                new Vector2(-halfW, horizonY),
                new Vector2(halfW, horizonY),
                new Vector2(halfW, halfH),
                new Vector2(-halfW, halfH),
                skyHorizonColor, skyHorizonColor, skyTopColor, skyTopColor, 0f
            );

            // Distant parallax hills
            _horizonScroll -= playerCar.physics.ForwardSpeed * headingError * Time.deltaTime * 0.15f;
            DrawDistantHills(halfW, horizonY, halfH);

            // 2. Project road slices from near (0) to far (sliceCount - 1)
            float curveAcc = 0f;
            float xOffset = 0f;
            float playerLatNorm = playerCar.lateralOffset / Mathf.Max(1f, track.trackWidth * 0.5f);

            // Heading error tilts the road vanishing point left/right
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

                // Clamp above bottom of screen and enforce painter's horizon occlusion
                if (i == 0) projY = Mathf.Min(projY, -halfH * 0.99f);
                float clampedY = Mathf.Max(projY, maxDrawnY);

                float roadHalfW = (track.trackWidth * 0.5f) * scale * 0.42f;
                float curbHalfW = roadHalfW * 1.16f;
                float centerX = xOffset - playerLatNorm * roadHalfW * 0.95f + headingShift * (i / (float)sliceCount);

                int stripe = Mathf.FloorToInt(sampleS / 6f);
                bool isStartLine = sampleS < 5f || sampleS > track.TotalLength - 3f;

                _slices[i] = new SliceData
                {
                    screenY = clampedY,
                    centerX = centerX,
                    roadHalfW = roadHalfW,
                    curbHalfW = curbHalfW,
                    scale = scale,
                    worldS = sampleS,
                    stripeIndex = stripe,
                    isStartLine = isStartLine
                };

                if (projY > maxDrawnY) maxDrawnY = projY;
                validSlices++;
            }

            // Draw road strips from near to far (or far to near; non-overlapping in Y)
            for (int i = 0; i < validSlices - 1; i++)
            {
                SliceData a = _slices[i];
                SliceData b = _slices[i + 1];
                if (b.screenY <= a.screenY + 0.0001f) continue;

                bool even = (a.stripeIndex % 2) == 0;
                Color grassCol = even ? grassLight : grassDark;
                Color curbCol = even ? curbLight : curbDark;
                Color roadCol = a.isStartLine ? Color.white : (even ? roadLight : roadDark);

                // Grass spanning full screen width
                AddQuad(
                    new Vector2(-halfW, a.screenY),
                    new Vector2(halfW, a.screenY),
                    new Vector2(halfW, b.screenY),
                    new Vector2(-halfW, b.screenY),
                    grassCol, grassCol, grassCol, grassCol, 0f
                );

                // Rumble curbs
                AddQuad(
                    new Vector2(a.centerX - a.curbHalfW, a.screenY),
                    new Vector2(a.centerX + a.curbHalfW, a.screenY),
                    new Vector2(b.centerX + b.curbHalfW, b.screenY),
                    new Vector2(b.centerX - b.curbHalfW, b.screenY),
                    curbCol, curbCol, curbCol, curbCol, 0f
                );

                // Asphalt road
                AddQuad(
                    new Vector2(a.centerX - a.roadHalfW, a.screenY),
                    new Vector2(a.centerX + a.roadHalfW, a.screenY),
                    new Vector2(b.centerX + b.roadHalfW, b.screenY),
                    new Vector2(b.centerX - b.roadHalfW, b.screenY),
                    roadCol, roadCol, roadCol, roadCol, 0f
                );

                // Center dashed lane marker
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

            // 3. Draw roadside trees & posts back-to-front for strong sense of speed
            for (int i = validSlices - 2; i >= 1; i--)
            {
                SliceData s = _slices[i];
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

            // 4. Top-right 2D Minimap showing the underlying WaypointTrack & player position
            DrawMinimap(halfW, halfH);

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetTriangles(_tris, 0);
            _mesh.SetColors(_colors);
            _mesh.RecalculateBounds();
        }

        private void DrawDistantHills(float halfW, float horizonY, float halfH)
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
                // Trunk
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

                // Foliage
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
                // Highway reflector post
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
            if (track == null || track.waypoints == null || track.waypoints.Length < 2) return;

            // Compute track bounding box
            Vector2 min = track.waypoints[0];
            Vector2 max = track.waypoints[0];
            for (int i = 1; i < track.waypoints.Length; i++)
            {
                min = Vector2.Min(min, track.waypoints[i]);
                max = Vector2.Max(max, track.waypoints[i]);
            }

            Vector2 size = max - min;
            float maxDim = Mathf.Max(1f, Mathf.Max(size.x, size.y));
            float mapSize = 2.1f;
            Vector2 mapOrigin = new Vector2(halfW - mapSize - 0.35f, halfH - mapSize - 0.35f);

            // Semi-transparent backing box
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

            // Track loop segments
            int count = track.waypoints.Length;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = ToMap(track.waypoints[i]);
                Vector2 b = ToMap(track.waypoints[(i + 1) % count]);
                AddLineQuad(a, b, 0.045f, new Color(0.85f, 0.85f, 0.9f, 0.95f));
            }

            // Player dot
            Vector2 p = ToMap(playerCar.physics.position);
            float r = 0.085f;
            Color pCol = playerCar.isOnTrack ? Color.yellow : Color.red;
            AddQuad(
                p + new Vector2(-r, -r),
                p + new Vector2(r, -r),
                p + new Vector2(r, r),
                p + new Vector2(-r, r),
                pCol, pCol, pCol, pCol, 0f
            );
        }

        private void UpdatePlayerBikeVisual()
        {
            if (_bikeRenderer == null || playerCar == null) return;

            float speedNorm = Mathf.Clamp01(playerCar.physics.Speed / playerCar.physics.maxSpeed);
            float vibe = Mathf.Sin(Time.time * 45f) * 0.025f * speedNorm;
            if (!playerCar.isOnTrack && playerCar.physics.Speed > 3f)
            {
                vibe += Mathf.Sin(Time.time * 75f) * 0.055f;
            }

            Vector3 bikePos = new Vector3(0f, -3.1f + vibe, -0.2f);
            _bikeRenderer.transform.position = bikePos;
            _bikeRenderer.transform.rotation = Quaternion.Euler(0f, 0f, -playerCar.leanAngle * 24f);
            _bikeRenderer.transform.localScale = Vector3.one * 2.8f;

            // Flash slightly red-tinted when braking
            _bikeRenderer.color = playerCar.lastInput.brake > 0.1f
                ? new Color(1f, 0.82f, 0.82f, 1f)
                : Color.white;

            if (_shadowRenderer != null)
            {
                _shadowRenderer.transform.position = new Vector3(0f, -3.75f, -0.1f);
                _shadowRenderer.transform.localScale = new Vector3(2.4f, 0.65f, 1f);
            }
        }

        private void AddLineQuad(Vector2 a, Vector2 b, float thickness, Color col)
        {
            Vector2 dir = (b - a);
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
            Color bikeBody = new Color(0.88f, 0.18f, 0.18f);
            Color tailLight = new Color(1f, 0.25f, 0.15f);
            Color leather = new Color(0.18f, 0.20f, 0.26f);
            Color jacket = new Color(0.15f, 0.45f, 0.90f);
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

            // Rear tire
            Rect(13, 1, 18, 9, tire);
            // Exhaust pipes
            Rect(11, 3, 12, 7, chrome);
            Rect(19, 3, 20, 7, chrome);
            // Fairing & tail
            Rect(11, 8, 20, 15, bikeBody);
            Rect(14, 9, 17, 11, tailLight);
            // Handlebars
            Rect(8, 16, 23, 17, chrome);
            // Rider legs
            Rect(11, 10, 13, 17, leather);
            Rect(18, 10, 20, 17, leather);
            // Rider torso / jacket
            Rect(11, 16, 20, 23, jacket);
            // Helmet
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
