using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Renders a PS1 Road Rash-inspired pseudo-3D behind-the-bike view with unified
    /// chase-camera projection so player bike, opponent bikes, road markings, and roadside props
    /// share exact perspective scale, vertical ground placement, lateral spacing, and depth order.
    /// </summary>
    public class RoadRashRenderer2D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private WaypointTrack track;
        [SerializeField] private Car playerCar;
        [SerializeField] private RaceVfxController vfxController;

        [Header("Pseudo-3D Projection")]
        [SerializeField] private int sliceCount = 104;
        [SerializeField] private float sliceStep = 1.9f;
        [SerializeField] private float firstSliceZ = 1.0f;
        [SerializeField] private float playerCameraZ = 5.4f;
        [SerializeField] private float cameraHeight = 2.55f;
        [SerializeField] private float cameraDepth = 0.86f;
        [SerializeField] private float curveVisualGain = 2.55f;
        [SerializeField] private float hillVisualGain = 0.048f;
        [SerializeField] private float bikeVisualScale = 2.05f;

        [Header("Camera Feel")]
        [SerializeField] private float baseOrthoSize = 5.0f;
        [SerializeField] private float maxSpeedOrthoBoost = 0.34f;
        [SerializeField] private float maxCameraRollDeg = 2.0f;

        private readonly List<Car> _allCars = new List<Car>();
        private readonly List<OpponentSpriteSlot> _opponentPool = new List<OpponentSpriteSlot>();
        private readonly List<SpriteRenderer> _propPool = new List<SpriteRenderer>();

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Texture2D _grainTexture;

        private readonly List<Vector3> _verts = new List<Vector3>(8192);
        private readonly List<Vector2> _uvs = new List<Vector2>(8192);
        private readonly List<Vector2> _uv1s = new List<Vector2>(8192);
        private readonly List<int> _tris = new List<int>(12288);
        private readonly List<Color> _colors = new List<Color>(8192);

        private SpriteRenderer _bikeRenderer;
        private SpriteRenderer _shadowRenderer;
        private SpriteRenderer _tailLightRenderer;

        private GameManager _gameManager;
        private SliceData[] _slices;
        private SliceData _playerSlice;
        private float _horizonScroll;
        private float _wheelAnimPhase;
        private float _shakeTrauma;
        private float _idleDemoScroll;
        private float _currentOrthoSize;
        private float _currentRollDeg;

        private struct SliceData
        {
            public float worldZ;
            public float screenY;
            public float centerX;
            public float roadHalfW;
            public float shoulderHalfW;
            public float scale;
            public float worldS;
            public float curvature;
            public int stripeIndex;
            public bool isStartLine;
            public bool occluded;
        }

        private class OpponentSpriteSlot
        {
            public SpriteRenderer bike;
            public SpriteRenderer shadow;
            public SpriteRenderer tailLight;
            public Car boundCar;
            public Vector3 lastScreenPos;
            public bool isVisible;
        }

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

            BindGameManagerEvents();
            if (vfxController != null)
            {
                vfxController.Initialize(targetCamera);
            }
        }

        public void AddCameraShake(float amount)
        {
            _shakeTrauma = Mathf.Clamp01(_shakeTrauma + amount);
        }

        private void Awake()
        {
            EnsureRenderObjects();
            _slices = new SliceData[sliceCount];
            _currentOrthoSize = baseOrthoSize;
        }

        private void OnEnable()
        {
            BindGameManagerEvents();
        }

        private void OnDisable()
        {
            UnbindGameManagerEvents();
        }

        private void BindGameManagerEvents()
        {
            if (_gameManager == null)
            {
                _gameManager = FindAnyObjectByType<GameManager>();
            }
            if (_gameManager != null)
            {
                _gameManager.RacerKicked -= HandleRacerKicked;
                _gameManager.RacerKicked += HandleRacerKicked;
                _gameManager.RacersCollided -= HandleRacersCollided;
                _gameManager.RacersCollided += HandleRacersCollided;
            }
        }

        private void UnbindGameManagerEvents()
        {
            if (_gameManager != null)
            {
                _gameManager.RacerKicked -= HandleRacerKicked;
                _gameManager.RacersCollided -= HandleRacersCollided;
            }
        }

        private void HandleRacerKicked(Car attacker, Car target, Vector2 worldHitPos)
        {
            AddCameraShake(0.65f);
            EmitSparksForCars(attacker, target, 1.0f);
        }

        private void HandleRacersCollided(Car a, Car b, Vector2 worldMid)
        {
            if (a == playerCar || b == playerCar)
            {
                AddCameraShake(0.42f);
                EmitSparksForCars(a, b, 0.72f);
            }
        }

        private void EmitSparksForCars(Car a, Car b, float intensity)
        {
            if (vfxController == null) return;
            Car other = a == playerCar ? b : a;
            if (other == playerCar)
            {
                vfxController.EmitImpactSparks(new Vector3(0f, _playerSlice.screenY + 0.8f, -0.3f), intensity);
                return;
            }

            for (int i = 0; i < _opponentPool.Count; i++)
            {
                OpponentSpriteSlot slot = _opponentPool[i];
                if (slot.isVisible && slot.boundCar == other)
                {
                    Vector3 playerPos = _bikeRenderer != null ? _bikeRenderer.transform.position : new Vector3(0f, _playerSlice.screenY, -0.2f);
                    Vector3 mid = Vector3.Lerp(playerPos, slot.lastScreenPos, 0.5f) + Vector3.up * 0.75f;
                    vfxController.EmitImpactSparks(mid, intensity);
                    return;
                }
            }

            if (a == playerCar || b == playerCar)
            {
                vfxController.EmitImpactSparks(new Vector3(0f, _playerSlice.screenY + 0.8f, -0.3f), intensity);
            }
        }

        private void EnsureRenderObjects()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null)
            {
                targetCamera.orthographic = true;
                targetCamera.orthographicSize = baseOrthoSize;
                targetCamera.transform.position = new Vector3(0f, 0f, -10f);
                targetCamera.transform.rotation = Quaternion.identity;
                targetCamera.backgroundColor = RoadRashVisualTheme.SkyHorizon;
            }

            if (vfxController == null)
            {
                vfxController = GetComponent<RaceVfxController>();
                if (vfxController == null) vfxController = gameObject.AddComponent<RaceVfxController>();
            }
            vfxController.Initialize(targetCamera);

            if (_grainTexture == null)
            {
                _grainTexture = CreateGrainTexture();
            }

            if (_meshFilter == null)
            {
                GameObject roadObj = new GameObject("RoadRashMesh");
                roadObj.transform.SetParent(transform, false);
                roadObj.transform.localPosition = Vector3.zero;
                _meshFilter = roadObj.AddComponent<MeshFilter>();
                _meshRenderer = roadObj.AddComponent<MeshRenderer>();

                Shader shader = Shader.Find("RacingGame/RoadRashHighway");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                Material mat = new Material(shader);
                mat.mainTexture = _grainTexture;
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
                _shadowRenderer.sprite = RoadRashBikeSprites.GetShadowSprite();
                _shadowRenderer.color = new Color(0f, 0f, 0f, 0.58f);

                GameObject bikeObj = new GameObject("PlayerBikeSprite");
                bikeObj.transform.SetParent(transform, false);
                _bikeRenderer = bikeObj.AddComponent<SpriteRenderer>();

                GameObject tailObj = new GameObject("PlayerTailLight");
                tailObj.transform.SetParent(bikeObj.transform, false);
                tailObj.transform.localPosition = new Vector3(0f, 0.62f, -0.02f);
                tailObj.transform.localScale = new Vector3(0.42f, 0.22f, 1f);
                _tailLightRenderer = tailObj.AddComponent<SpriteRenderer>();
                _tailLightRenderer.sprite = RoadRashBikeSprites.GetTailLightSprite();
            }
        }

        private void LateUpdate()
        {
            if (track == null || playerCar == null || targetCamera == null) return;
            if (_slices == null || _slices.Length != sliceCount) _slices = new SliceData[sliceCount];

            bool isMenuBackdrop = _gameManager != null &&
                (_gameManager.CurrentFlowState == GameFlowState.MainMenu ||
                 _gameManager.CurrentFlowState == GameFlowState.UpgradeShop);

            if (isMenuBackdrop)
            {
                _idleDemoScroll = track.WrapDistance(_idleDemoScroll + Time.unscaledDeltaTime * 16f);
            }

            UpdateCameraJuice(isMenuBackdrop);
            RenderFrame(isMenuBackdrop);
            UpdatePlayerBikeVisual(isMenuBackdrop);
            UpdateOpponentBikeVisuals(isMenuBackdrop);

            if (vfxController != null)
            {
                float speedNorm = Mathf.Clamp01(playerCar.Physics.Speed / Mathf.Max(1f, playerCar.Physics.MaxSpeed));
                bool isRacing = _gameManager == null || _gameManager.CurrentFlowState == GameFlowState.Racing;
                Vector3 tirePos = _bikeRenderer != null
                    ? _bikeRenderer.transform.position + new Vector3(0f, 0.12f, -0.05f)
                    : new Vector3(0f, -3.5f, -0.25f);
                vfxController.UpdateFrameEffects(Time.deltaTime, speedNorm, !playerCar.IsOnTrack, isRacing, tirePos);
            }
        }

        private void UpdateCameraJuice(bool isMenuBackdrop)
        {
            float dt = Time.deltaTime;
            float speedNorm = isMenuBackdrop ? 0.35f : Mathf.Clamp01(playerCar.Physics.Speed / Mathf.Max(1f, playerCar.Physics.MaxSpeed));

            float targetOrtho = baseOrthoSize + speedNorm * maxSpeedOrthoBoost;
            _currentOrthoSize = Mathf.Lerp(_currentOrthoSize, targetOrtho, 1f - Mathf.Exp(-6f * dt));
            targetCamera.orthographicSize = _currentOrthoSize;

            float targetRoll = isMenuBackdrop ? 0f : -playerCar.LeanAngle * maxCameraRollDeg * speedNorm;
            _currentRollDeg = Mathf.Lerp(_currentRollDeg, targetRoll, 1f - Mathf.Exp(-8f * dt));

            _shakeTrauma = Mathf.MoveTowards(_shakeTrauma, 0f, dt * 2.4f);
            float offRoadShake = (!isMenuBackdrop && !playerCar.IsOnTrack && playerCar.Physics.Speed > 4f)
                ? 0.035f * speedNorm
                : 0f;
            float shakeAmp = (_shakeTrauma * _shakeTrauma) * 0.24f + offRoadShake;

            float sx = (Mathf.PerlinNoise(Time.time * 38f, 1.3f) - 0.5f) * 2f * shakeAmp;
            float sy = (Mathf.PerlinNoise(2.7f, Time.time * 38f) - 0.5f) * 2f * shakeAmp;

            targetCamera.transform.position = new Vector3(sx, sy, -10f);
            targetCamera.transform.rotation = Quaternion.Euler(0f, 0f, _currentRollDeg);
        }

        private void RenderFrame(bool isMenuBackdrop)
        {
            _verts.Clear();
            _uvs.Clear();
            _uv1s.Clear();
            _tris.Clear();
            _colors.Clear();

            float halfH = targetCamera.orthographicSize;
            float halfW = halfH * targetCamera.aspect;

            float baseS = isMenuBackdrop ? _idleDemoScroll : playerCar.TrackDistance;
            TrackSample playerSample = track.SampleAtDistance(baseS);
            float headingError = isMenuBackdrop ? 0f : SignedAngleRad(playerSample.tangent, playerCar.Physics.Forward);
            float baseElevation = playerSample.elevation;

            float horizonY = halfH * 0.22f;

            // 1. Sky gradient (cobalt top -> mid sky -> pale horizon haze)
            float skyMidY = Mathf.Lerp(horizonY, halfH, 0.52f);
            AddQuad(
                new Vector2(-halfW - 1f, skyMidY),
                new Vector2(halfW + 1f, skyMidY),
                new Vector2(halfW + 1f, halfH + 1f),
                new Vector2(-halfW - 1f, halfH + 1f),
                RoadRashVisualTheme.SkyMid, RoadRashVisualTheme.SkyMid,
                RoadRashVisualTheme.SkyTop, RoadRashVisualTheme.SkyTop,
                0f
            );
            AddQuad(
                new Vector2(-halfW - 1f, horizonY),
                new Vector2(halfW + 1f, horizonY),
                new Vector2(halfW + 1f, skyMidY),
                new Vector2(-halfW - 1f, skyMidY),
                RoadRashVisualTheme.SkyHorizon, RoadRashVisualTheme.SkyHorizon,
                RoadRashVisualTheme.SkyMid, RoadRashVisualTheme.SkyMid,
                0f
            );

            // Base ground horizon cap so no background sky strip ever leaks below hills or at corners
            AddQuad(
                new Vector2(-halfW - 1f, -halfH - 1.5f),
                new Vector2(halfW + 1f, -halfH - 1.5f),
                new Vector2(halfW + 1f, horizonY),
                new Vector2(-halfW - 1f, horizonY),
                RoadRashVisualTheme.TerrainGrassDark, RoadRashVisualTheme.TerrainGrassDark,
                RoadRashVisualTheme.SkyHorizon, RoadRashVisualTheme.SkyHorizon,
                0f
            );

            float scrollSpeed = isMenuBackdrop ? 14f : playerCar.Physics.ForwardSpeed;
            _horizonScroll -= scrollSpeed * (headingError + playerSample.curvature * 18f) * Time.deltaTime * 0.14f;

            // 2. Parallax mountains & valley hills
            DrawSunCircle(halfW, horizonY);
            DrawParallaxMountains(halfW, horizonY);

            // 3. Project road slices anchored cleanly at playerCameraZ so baseS aligns under player's rear tire
            float curveAcc = 0f;
            float xOffset = 0f;
            float playerLatNorm = isMenuBackdrop ? 0f : playerCar.LateralOffset / Mathf.Max(1f, track.TrackWidth * 0.5f);
            float headingShift = Mathf.Tan(Mathf.Clamp(headingError, -0.7f, 0.7f)) * halfW * 0.85f;
            float maxAheadZ = Mathf.Max(1f, (firstSliceZ + (sliceCount - 1) * sliceStep) - playerCameraZ);

            int validSlices = 0;
            float maxDrawnY = -halfH - 4.0f;

            for (int i = 0; i < sliceCount; i++)
            {
                float worldZ = firstSliceZ + i * sliceStep;
                float dzFromPlayer = worldZ - playerCameraZ;
                float scale = cameraDepth / (worldZ * 0.135f + 0.34f);
                float sampleS = track.WrapDistance(baseS + dzFromPlayer);
                TrackSample sample = track.SampleAtDistance(sampleS);

                if (dzFromPlayer > 0f)
                {
                    float segDz = Mathf.Min(sliceStep, dzFromPlayer);
                    curveAcc += sample.curvature * segDz * curveVisualGain;
                    xOffset += curveAcc * (segDz / sliceStep) * 0.045f;
                }

                float hillDelta = (sample.elevation - baseElevation) * hillVisualGain * scale;
                float projY = horizonY - cameraHeight * scale * 2.25f + hillDelta;

                bool occluded = projY < maxDrawnY;
                float clampedY = Mathf.Max(projY, maxDrawnY);

                float roadHalfW = (track.TrackWidth * 0.5f) * scale * 0.44f;
                float shoulderHalfW = roadHalfW * 1.19f;
                float forwardT = Mathf.Clamp01(dzFromPlayer / maxAheadZ);
                float centerX = xOffset - playerLatNorm * roadHalfW * 0.92f + headingShift * forwardT;

                int stripe = Mathf.FloorToInt(sampleS / 5.5f);
                bool isStartLine = sampleS < 4.5f || sampleS > track.TotalLength - 2.5f;

                _slices[i] = new SliceData
                {
                    worldZ = worldZ,
                    screenY = clampedY,
                    centerX = centerX,
                    roadHalfW = roadHalfW,
                    shoulderHalfW = shoulderHalfW,
                    scale = scale,
                    worldS = sampleS,
                    curvature = sample.curvature,
                    stripeIndex = stripe,
                    isStartLine = isStartLine,
                    occluded = occluded
                };

                if (projY > maxDrawnY) maxDrawnY = projY;
                validSlices++;
            }

            if (!TrySampleAtCameraZ(playerCameraZ, out _playerSlice))
            {
                _playerSlice = _slices[0];
            }

            // 4. Draw road, gravel shoulders, flanking valley embankments, and 4-lane markings
            for (int i = 0; i < validSlices - 1; i++)
            {
                SliceData a = _slices[i];
                SliceData b = _slices[i + 1];
                if (b.screenY <= a.screenY + 0.0001f) continue;

                float tA = i / (float)(validSlices - 1);
                float tB = (i + 1) / (float)(validSlices - 1);
                float fogA = SmoothFactor(0.45f, 1.0f, tA) * 0.88f;
                float fogB = SmoothFactor(0.45f, 1.0f, tB) * 0.88f;

                bool even = (a.stripeIndex % 2) == 0;
                Color earthA = Color.Lerp(even ? RoadRashVisualTheme.TerrainEarthLight : RoadRashVisualTheme.TerrainEarthDark, RoadRashVisualTheme.SkyHorizon, fogA);
                Color earthB = Color.Lerp(even ? RoadRashVisualTheme.TerrainEarthLight : RoadRashVisualTheme.TerrainEarthDark, RoadRashVisualTheme.SkyHorizon, fogB);
                Color grassA = Color.Lerp(even ? RoadRashVisualTheme.TerrainGrassLight : RoadRashVisualTheme.TerrainGrassDark, RoadRashVisualTheme.SkyHorizon, fogA);
                Color grassB = Color.Lerp(even ? RoadRashVisualTheme.TerrainGrassLight : RoadRashVisualTheme.TerrainGrassDark, RoadRashVisualTheme.SkyHorizon, fogB);
                Color shoulderA = Color.Lerp(even ? RoadRashVisualTheme.ShoulderLight : RoadRashVisualTheme.ShoulderDark, RoadRashVisualTheme.SkyHorizon, fogA);
                Color shoulderB = Color.Lerp(even ? RoadRashVisualTheme.ShoulderLight : RoadRashVisualTheme.ShoulderDark, RoadRashVisualTheme.SkyHorizon, fogB);
                Color roadA = Color.Lerp(even ? RoadRashVisualTheme.AsphaltLight : RoadRashVisualTheme.AsphaltDark, RoadRashVisualTheme.SkyHorizon, fogA);
                Color roadB = Color.Lerp(even ? RoadRashVisualTheme.AsphaltLight : RoadRashVisualTheme.AsphaltDark, RoadRashVisualTheme.SkyHorizon, fogB);

                float uvYa = a.worldS * 0.15f;
                float uvYb = b.worldS * 0.15f;

                // Left sloping hillside embankment (earthy brown like screenshot left slope)
                float leftSlopeRiseA = i == 0 ? 0f : 0.72f * a.scale;
                float leftSlopeRiseB = 0.72f * b.scale;
                AddTexturedQuad(
                    new Vector2(-halfW - 1.5f, a.screenY + leftSlopeRiseA),
                    new Vector2(a.centerX - a.shoulderHalfW, a.screenY),
                    new Vector2(b.centerX - b.shoulderHalfW, b.screenY),
                    new Vector2(-halfW - 1.5f, b.screenY + leftSlopeRiseB),
                    earthA, grassA, grassB, earthB,
                    new Vector2(-3.5f, uvYa), new Vector2(-1f, uvYa), new Vector2(-1f, uvYb), new Vector2(-3.5f, uvYb),
                    0.28f
                );

                // Right valley grass & rolling slope
                float rightSlopeRiseA = i == 0 ? 0f : 0.34f * a.scale;
                float rightSlopeRiseB = 0.34f * b.scale;
                AddTexturedQuad(
                    new Vector2(a.centerX + a.shoulderHalfW, a.screenY),
                    new Vector2(halfW + 1.5f, a.screenY + rightSlopeRiseA),
                    new Vector2(halfW + 1.5f, b.screenY + rightSlopeRiseB),
                    new Vector2(b.centerX + b.shoulderHalfW, b.screenY),
                    grassA, earthA, earthB, grassB,
                    new Vector2(1f, uvYa), new Vector2(3.5f, uvYa), new Vector2(3.5f, uvYb), new Vector2(1f, uvYb),
                    0.26f
                );

                // Gravel/dirt road shoulders
                AddTexturedQuad(
                    new Vector2(a.centerX - a.shoulderHalfW, a.screenY),
                    new Vector2(a.centerX + a.shoulderHalfW, a.screenY),
                    new Vector2(b.centerX + b.shoulderHalfW, b.screenY),
                    new Vector2(b.centerX - b.shoulderHalfW, b.screenY),
                    shoulderA, shoulderA, shoulderB, shoulderB,
                    new Vector2(-1.2f, uvYa), new Vector2(1.2f, uvYa), new Vector2(1.2f, uvYb), new Vector2(-1.2f, uvYb),
                    0.24f
                );

                // Textured asphalt highway
                AddTexturedQuad(
                    new Vector2(a.centerX - a.roadHalfW, a.screenY),
                    new Vector2(a.centerX + a.roadHalfW, a.screenY),
                    new Vector2(b.centerX + b.roadHalfW, b.screenY),
                    new Vector2(b.centerX - b.roadHalfW, b.screenY),
                    roadA, roadA, roadB, roadB,
                    new Vector2(-1f, uvYa), new Vector2(1f, uvYa), new Vector2(1f, uvYb), new Vector2(-1f, uvYb),
                    0.20f
                );

                if (a.isStartLine)
                {
                    DrawStartLineCheckers(a, b, fogA, fogB);
                }
                else
                {
                    Color whiteA = Color.Lerp(RoadRashVisualTheme.LaneWhiteLine, RoadRashVisualTheme.SkyHorizon, fogA);
                    Color whiteB = Color.Lerp(RoadRashVisualTheme.LaneWhiteLine, RoadRashVisualTheme.SkyHorizon, fogB);
                    Color yellowA = Color.Lerp(RoadRashVisualTheme.CenterYellowLine, RoadRashVisualTheme.SkyHorizon, fogA);
                    Color yellowB = Color.Lerp(RoadRashVisualTheme.CenterYellowLine, RoadRashVisualTheme.SkyHorizon, fogB);

                    // Solid white edge lines
                    float edgeW_A = Mathf.Max(0.012f, a.roadHalfW * 0.025f);
                    float edgeW_B = Mathf.Max(0.008f, b.roadHalfW * 0.025f);
                    float edgeOffA = a.roadHalfW * 0.92f;
                    float edgeOffB = b.roadHalfW * 0.92f;
                    AddStripQuad(a.centerX - edgeOffA, a.screenY, edgeW_A, b.centerX - edgeOffB, b.screenY, edgeW_B, whiteA, whiteB);
                    AddStripQuad(a.centerX + edgeOffA, a.screenY, edgeW_A, b.centerX + edgeOffB, b.screenY, edgeW_B, whiteA, whiteB);

                    // Double yellow center lines
                    float yelW_A = Mathf.Max(0.010f, a.roadHalfW * 0.022f);
                    float yelW_B = Mathf.Max(0.007f, b.roadHalfW * 0.022f);
                    float yelGapA = Mathf.Max(0.014f, a.roadHalfW * 0.028f);
                    float yelGapB = Mathf.Max(0.009f, b.roadHalfW * 0.028f);
                    AddStripQuad(a.centerX - yelGapA, a.screenY, yelW_A, b.centerX - yelGapB, b.screenY, yelW_B, yellowA, yellowB);
                    AddStripQuad(a.centerX + yelGapA, a.screenY, yelW_A, b.centerX + yelGapB, b.screenY, yelW_B, yellowA, yellowB);

                    // Dashed white lane dividers (4-lane highway)
                    if (even)
                    {
                        float laneOffA = a.roadHalfW * 0.46f;
                        float laneOffB = b.roadHalfW * 0.46f;
                        float dashW_A = Mathf.Max(0.010f, a.roadHalfW * 0.018f);
                        float dashW_B = Mathf.Max(0.007f, b.roadHalfW * 0.018f);
                        AddStripQuad(a.centerX - laneOffA, a.screenY, dashW_A, b.centerX - laneOffB, b.screenY, dashW_B, whiteA, whiteB);
                        AddStripQuad(a.centerX + laneOffA, a.screenY, dashW_A, b.centerX + laneOffB, b.screenY, dashW_B, whiteA, whiteB);
                    }
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetUVs(1, _uv1s);
            _mesh.SetTriangles(_tris, 0);
            _mesh.SetColors(_colors);
            _mesh.RecalculateBounds();

            // 5. Render pooled roadside sprite props
            UpdateRoadsideProps(validSlices);
        }

        private void DrawStartLineCheckers(SliceData a, SliceData b, float fogA, float fogB)
        {
            const int cols = 10;
            float stepA = (a.roadHalfW * 2f) / cols;
            float stepB = (b.roadHalfW * 2f) / cols;

            for (int c = 0; c < cols; c++)
            {
                bool light = (c + a.stripeIndex) % 2 == 0;
                Color baseCol = light ? RoadRashVisualTheme.StartCheckLight : RoadRashVisualTheme.StartCheckDark;
                Color ca = Color.Lerp(baseCol, RoadRashVisualTheme.SkyHorizon, fogA);
                Color cb = Color.Lerp(baseCol, RoadRashVisualTheme.SkyHorizon, fogB);

                float ax0 = a.centerX - a.roadHalfW + c * stepA;
                float ax1 = ax0 + stepA;
                float bx0 = b.centerX - b.roadHalfW + c * stepB;
                float bx1 = bx0 + stepB;
                AddQuad(new Vector2(ax0, a.screenY), new Vector2(ax1, a.screenY), new Vector2(bx1, b.screenY), new Vector2(bx0, b.screenY), ca, ca, cb, cb, 0f);
            }
        }

        private void DrawSunCircle(float halfW, float horizonY)
        {
            float sunX = -halfW * 0.42f + (_horizonScroll * 0.12f % 3.5f);
            float sunY = horizonY + 1.65f;
            float radius = 0.72f;
            Color centerCol = new Color(RoadRashVisualTheme.SunGlow.r, RoadRashVisualTheme.SunGlow.g, RoadRashVisualTheme.SunGlow.b, 0.55f);
            Color edgeCol = new Color(RoadRashVisualTheme.SkyMid.r, RoadRashVisualTheme.SkyMid.g, RoadRashVisualTheme.SkyMid.b, 0f);

            const int segs = 16;
            int centerIdx = _verts.Count;
            _verts.Add(new Vector3(sunX, sunY, 0f));
            _uvs.Add(Vector2.zero);
            _uv1s.Add(Vector2.zero);
            _colors.Add(centerCol);

            for (int i = 0; i <= segs; i++)
            {
                float ang = (i / (float)segs) * Mathf.PI * 2f;
                _verts.Add(new Vector3(sunX + Mathf.Cos(ang) * radius, sunY + Mathf.Sin(ang) * radius, 0f));
                _uvs.Add(Vector2.zero);
                _uv1s.Add(Vector2.zero);
                _colors.Add(edgeCol);
            }

            for (int i = 0; i < segs; i++)
            {
                _tris.Add(centerIdx);
                _tris.Add(centerIdx + 1 + i);
                _tris.Add(centerIdx + 2 + i);
            }
        }

        private void DrawParallaxMountains(float halfW, float horizonY)
        {
            const int farSegs = 22;
            float farW = (halfW * 2f + 2.0f) / farSegs;
            for (int i = 0; i < farSegs; i++)
            {
                float x0 = -halfW - 1.0f + i * farW;
                float x1 = x0 + farW;
                float u0 = i * 0.65f + _horizonScroll * 0.45f;
                float u1 = (i + 1) * 0.65f + _horizonScroll * 0.45f;
                float h0 = horizonY + 0.55f + Mathf.Max(0f, Mathf.Sin(u0) * 0.42f + Mathf.Cos(u0 * 2.1f) * 0.24f);
                float h1 = horizonY + 0.55f + Mathf.Max(0f, Mathf.Sin(u1) * 0.42f + Mathf.Cos(u1 * 2.1f) * 0.24f);

                AddQuad(
                    new Vector2(x0, horizonY),
                    new Vector2(x1, horizonY),
                    new Vector2(x1, h1),
                    new Vector2(x0, h0),
                    RoadRashVisualTheme.FarRidgeBase, RoadRashVisualTheme.FarRidgeBase,
                    RoadRashVisualTheme.FarRidgeTop, RoadRashVisualTheme.FarRidgeTop,
                    0f
                );
            }

            const int midSegs = 18;
            float midW = (halfW * 2f + 2.0f) / midSegs;
            for (int i = 0; i < midSegs; i++)
            {
                float x0 = -halfW - 1.0f + i * midW;
                float x1 = x0 + midW;
                float u0 = i * 0.72f + _horizonScroll * 0.85f + 1.4f;
                float u1 = (i + 1) * 0.72f + _horizonScroll * 0.85f + 1.4f;
                float h0 = horizonY + 0.18f + Mathf.Max(0f, Mathf.Sin(u0) * 0.30f + Mathf.Sin(u0 * 0.45f) * 0.22f);
                float h1 = horizonY + 0.18f + Mathf.Max(0f, Mathf.Sin(u1) * 0.30f + Mathf.Sin(u1 * 0.45f) * 0.22f);

                AddQuad(
                    new Vector2(x0, horizonY),
                    new Vector2(x1, horizonY),
                    new Vector2(x1, h1),
                    new Vector2(x0, h0),
                    RoadRashVisualTheme.MidHillDark, RoadRashVisualTheme.MidHillDark,
                    RoadRashVisualTheme.MidHillLight, RoadRashVisualTheme.MidHillLight,
                    0f
                );
            }
        }

        private void UpdateRoadsideProps(int validSlices)
        {
            int propIdx = 0;

            for (int i = validSlices - 2; i >= 1; i--)
            {
                SliceData s = _slices[i];
                if (s.occluded) continue;

                int slot9 = Mathf.FloorToInt(s.worldS / 9f);
                int prevSlot9 = Mathf.FloorToInt(_slices[i - 1].worldS / 9f);
                if (slot9 == prevSlot9) continue;

                float fog = SmoothFactor(0.45f, 1.0f, i / (float)(validSlices - 1)) * 0.84f;
                Color tint = Color.Lerp(Color.white, RoadRashVisualTheme.SkyHorizon, fog);
                int baseOrder = Mathf.Clamp(475 - Mathf.RoundToInt(s.worldZ * 2.5f), 15, 475);

                // Left embankment: wooden fence posts every 9m
                float leftX = s.centerX - s.shoulderHalfW - s.scale * 0.85f;
                PlaceProp(propIdx++, RoadsidePropKind.FencePost, leftX, s.screenY + s.scale * 0.08f, s.scale * 1.15f, baseOrder, tint);

                // Every 18m: tall wooden telephone pole on left roadside (like screenshot)
                if ((slot9 % 2) == 0)
                {
                    float poleX = s.centerX - s.shoulderHalfW - s.scale * 1.45f;
                    PlaceProp(propIdx++, RoadsidePropKind.UtilityPole, poleX, s.screenY + s.scale * 0.14f, s.scale * 1.45f, baseOrder + 1, tint);
                }

                // Sharp curve chevron warning signs on outer edge
                if (Mathf.Abs(s.curvature) > 0.012f && (slot9 % 2) == 0)
                {
                    bool rightTurn = s.curvature > 0f;
                    RoadsidePropKind signKind = rightTurn ? RoadsidePropKind.ChevronRight : RoadsidePropKind.ChevronLeft;
                    float signSide = rightTurn ? -1f : 1f;
                    float signX = s.centerX + signSide * (s.shoulderHalfW + s.scale * 0.65f);
                    PlaceProp(propIdx++, signKind, signX, s.screenY + s.scale * 0.04f, s.scale * 1.3f, baseOrder + 2, tint);
                }
                else if ((slot9 % 3) == 0)
                {
                    float rightX = s.centerX + s.shoulderHalfW + s.scale * 1.35f;
                    RoadsidePropKind rightKind = (slot9 % 6) == 0 ? RoadsidePropKind.Tree : RoadsidePropKind.MilePost;
                    PlaceProp(propIdx++, rightKind, rightX, s.screenY + s.scale * 0.06f, s.scale * (rightKind == RoadsidePropKind.Tree ? 1.6f : 1.05f), baseOrder, tint);
                }
            }

            for (int i = propIdx; i < _propPool.Count; i++)
            {
                _propPool[i].gameObject.SetActive(false);
            }
        }

        private void PlaceProp(int index, RoadsidePropKind kind, float x, float y, float scale, int sortingOrder, Color tint)
        {
            while (_propPool.Count <= index)
            {
                GameObject obj = new GameObject($"RoadsideProp_{_propPool.Count}");
                obj.transform.SetParent(transform, false);
                SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
                _propPool.Add(sr);
            }

            SpriteRenderer propSr = _propPool[index];
            propSr.gameObject.SetActive(true);
            propSr.sprite = RoadRashPropSprites.GetPropSprite(kind);
            propSr.transform.position = new Vector3(x, y, -0.05f);
            propSr.transform.localScale = Vector3.one * scale;
            propSr.color = tint;
            propSr.sortingOrder = sortingOrder;
        }

        private void UpdatePlayerBikeVisual(bool isMenuBackdrop)
        {
            if (_bikeRenderer == null || playerCar == null) return;

            float speedNorm = isMenuBackdrop ? 0.35f : Mathf.Clamp01(playerCar.Physics.Speed / Mathf.Max(1f, playerCar.Physics.MaxSpeed));
            _wheelAnimPhase += Time.deltaTime * Mathf.Lerp(2f, 22f, speedNorm);
            int animFrame = Mathf.FloorToInt(_wheelAnimPhase);

            float vibe = Mathf.Sin(Time.time * 42f) * 0.015f * speedNorm;
            if (!isMenuBackdrop && !playerCar.IsOnTrack && playerCar.Physics.Speed > 3f)
            {
                vibe += Mathf.Sin(Time.time * 75f) * 0.050f;
            }

            float pScale = _playerSlice.scale * bikeVisualScale;
            float pGroundY = _playerSlice.screenY;
            int playerOrder = Mathf.Clamp(480 - Mathf.RoundToInt(playerCameraZ * 2.5f), 20, 480);

            BikeSpriteSet set = RoadRashBikeSprites.GetOrCreateSet(playerCar.BikeColor);
            BikePose pose = ResolveBikePose(playerCar);
            _bikeRenderer.sprite = RoadRashBikeSprites.SelectPoseSprite(set, pose, animFrame);
            _bikeRenderer.color = Color.white;
            _bikeRenderer.sortingOrder = playerOrder;

            Vector3 bikePos = new Vector3(0f, pGroundY + vibe, -0.2f);
            _bikeRenderer.transform.position = bikePos;
            float rollDeg = playerCar.IsKicking ? -playerCar.KickDirection * 6f : -playerCar.LeanAngle * 14f;
            _bikeRenderer.transform.rotation = Quaternion.Euler(0f, 0f, rollDeg);
            _bikeRenderer.transform.localScale = Vector3.one * pScale;

            if (_tailLightRenderer != null)
            {
                bool braking = !isMenuBackdrop && playerCar.LastInput.brake > 0.1f;
                _tailLightRenderer.color = braking ? RoadRashVisualTheme.TailLightBrakeHdr : RoadRashVisualTheme.TailLightIdle;
                _tailLightRenderer.transform.localScale = braking ? new Vector3(0.55f, 0.30f, 1f) : new Vector3(0.38f, 0.18f, 1f);
                _tailLightRenderer.sortingOrder = playerOrder + 1;
            }

            if (_shadowRenderer != null)
            {
                _shadowRenderer.transform.position = new Vector3(playerCar.LeanAngle * 0.10f * _playerSlice.scale, pGroundY - 0.02f * _playerSlice.scale, -0.1f);
                _shadowRenderer.transform.localScale = new Vector3(1.55f * pScale * 0.52f, 0.82f * pScale * 0.52f, 1f);
                _shadowRenderer.sortingOrder = playerOrder - 1;
            }
        }

        private void UpdateOpponentBikeVisuals(bool isMenuBackdrop)
        {
            float maxAheadDz = (firstSliceZ + (sliceCount - 1) * sliceStep) - playerCameraZ - sliceStep;
            float minBehindDz = -(playerCameraZ - 1.5f);
            float totalLen = track.TotalLength;
            float playerS = isMenuBackdrop ? _idleDemoScroll : playerCar.TrackDistance;
            int animFrame = Mathf.FloorToInt(_wheelAnimPhase);
            int slotIdx = 0;

            for (int i = 0; i < _allCars.Count; i++)
            {
                Car other = _allCars[i];
                if (other == null || other == playerCar) continue;

                float dz = other.TrackDistance - playerS;
                if (dz < -totalLen * 0.5f) dz += totalLen;
                if (dz > totalLen * 0.5f) dz -= totalLen;

                if (dz >= minBehindDz && dz <= maxAheadDz)
                {
                    float oppZ = playerCameraZ + dz;
                    if (TrySampleAtCameraZ(oppZ, out SliceData slice) && !slice.occluded)
                    {
                        OpponentSpriteSlot slot = GetOrCreateOpponentSlot(slotIdx++);
                        slot.boundCar = other;
                        slot.isVisible = true;
                        slot.bike.gameObject.SetActive(true);
                        slot.shadow.gameObject.SetActive(true);

                        float otherLatNorm = other.LateralOffset / Mathf.Max(1f, track.TrackWidth * 0.5f);
                        float screenX = slice.centerX + otherLatNorm * slice.roadHalfW * 0.92f;
                        float spriteScale = slice.scale * bikeVisualScale;

                        int order = Mathf.Clamp(480 - Mathf.RoundToInt(oppZ * 2.5f), 20, 495);
                        float fog = SmoothFactor(0.45f, 1.0f, oppZ / (firstSliceZ + sliceCount * sliceStep)) * 0.72f;
                        Color fogTint = Color.Lerp(Color.white, RoadRashVisualTheme.SkyHorizon, fog);

                        BikeSpriteSet set = RoadRashBikeSprites.GetOrCreateSet(other.BikeColor);
                        BikePose pose = ResolveBikePose(other);
                        slot.bike.sprite = RoadRashBikeSprites.SelectPoseSprite(set, pose, animFrame + i);
                        slot.bike.color = other.IsStaggered && (Mathf.FloorToInt(Time.time * 24f) % 2 == 0)
                            ? new Color(1f, 0.85f, 0.75f, 1f)
                            : fogTint;

                        Vector3 pos = new Vector3(screenX, slice.screenY, -0.15f);
                        slot.lastScreenPos = pos;
                        slot.bike.transform.position = pos;
                        float roll = other.IsStaggered ? Mathf.Sin(Time.time * 36f) * 16f : -other.LeanAngle * 14f;
                        slot.bike.transform.rotation = Quaternion.Euler(0f, 0f, roll);
                        slot.bike.transform.localScale = Vector3.one * spriteScale;
                        slot.bike.sortingOrder = order;

                        if (slot.tailLight != null)
                        {
                            bool braking = other.LastInput.brake > 0.1f;
                            slot.tailLight.color = braking ? RoadRashVisualTheme.TailLightBrakeHdr : RoadRashVisualTheme.TailLightIdle;
                            slot.tailLight.sortingOrder = order + 1;
                        }

                        slot.shadow.transform.position = new Vector3(screenX + other.LeanAngle * 0.10f * slice.scale, slice.screenY - 0.02f * slice.scale, -0.1f);
                        slot.shadow.transform.localScale = new Vector3(1.55f * spriteScale * 0.52f, 0.82f * spriteScale * 0.52f, 1f);
                        slot.shadow.sortingOrder = order - 1;
                    }
                }
            }

            for (int i = slotIdx; i < _opponentPool.Count; i++)
            {
                _opponentPool[i].isVisible = false;
                _opponentPool[i].boundCar = null;
                _opponentPool[i].bike.gameObject.SetActive(false);
                _opponentPool[i].shadow.gameObject.SetActive(false);
            }
        }

        private static BikePose ResolveBikePose(Car car)
        {
            if (car.IsKicking)
            {
                return car.KickDirection < 0 ? BikePose.KickLeft : BikePose.KickRight;
            }
            if (car.IsStaggered)
            {
                return BikePose.HitStagger;
            }

            float lean = car.LeanAngle;
            if (lean <= -0.58f) return BikePose.LeanLeftDeep;
            if (lean <= -0.16f) return BikePose.LeanLeftLight;
            if (lean >= 0.58f) return BikePose.LeanRightDeep;
            if (lean >= 0.16f) return BikePose.LeanRightLight;
            return BikePose.Upright;
        }

        private bool TrySampleAtCameraZ(float cameraZ, out SliceData result)
        {
            float fIdx = (cameraZ - firstSliceZ) / sliceStep;
            int idx0 = Mathf.Clamp(Mathf.FloorToInt(fIdx), 0, sliceCount - 2);
            int idx1 = idx0 + 1;
            float t = Mathf.Clamp01(fIdx - idx0);

            SliceData a = _slices[idx0];
            SliceData b = _slices[idx1];

            result = new SliceData
            {
                worldZ = cameraZ,
                screenY = Mathf.Lerp(a.screenY, b.screenY, t),
                centerX = Mathf.Lerp(a.centerX, b.centerX, t),
                roadHalfW = Mathf.Lerp(a.roadHalfW, b.roadHalfW, t),
                shoulderHalfW = Mathf.Lerp(a.shoulderHalfW, b.shoulderHalfW, t),
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
                shadowSr.sprite = RoadRashBikeSprites.GetShadowSprite();
                shadowSr.color = new Color(0f, 0f, 0f, 0.52f);

                GameObject bikeObj = new GameObject($"OpponentBike_{_opponentPool.Count}");
                bikeObj.transform.SetParent(transform, false);
                SpriteRenderer bikeSr = bikeObj.AddComponent<SpriteRenderer>();

                GameObject tailObj = new GameObject($"OpponentTailLight_{_opponentPool.Count}");
                tailObj.transform.SetParent(bikeObj.transform, false);
                tailObj.transform.localPosition = new Vector3(0f, 0.62f, -0.02f);
                tailObj.transform.localScale = new Vector3(0.42f, 0.22f, 1f);
                SpriteRenderer tailSr = tailObj.AddComponent<SpriteRenderer>();
                tailSr.sprite = RoadRashBikeSprites.GetTailLightSprite();

                _opponentPool.Add(new OpponentSpriteSlot
                {
                    bike = bikeSr,
                    shadow = shadowSr,
                    tailLight = tailSr
                });
            }
            return _opponentPool[index];
        }

        private void AddStripQuad(float ax, float ay, float halfWidthA, float bx, float by, float halfWidthB, Color colA, Color colB)
        {
            AddQuad(
                new Vector2(ax - halfWidthA, ay),
                new Vector2(ax + halfWidthA, ay),
                new Vector2(bx + halfWidthB, by),
                new Vector2(bx - halfWidthB, by),
                colA, colA, colB, colB, 0f
            );
        }

        private void AddQuad(Vector2 bl, Vector2 br, Vector2 tr, Vector2 tl, Color cbl, Color cbr, Color ctr, Color ctl, float z)
        {
            AddTexturedQuad(bl, br, tr, tl, cbl, cbr, ctr, ctl, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, 0f, z);
        }

        private void AddTexturedQuad(
            Vector2 bl, Vector2 br, Vector2 tr, Vector2 tl,
            Color cbl, Color cbr, Color ctr, Color ctl,
            Vector2 uvBl, Vector2 uvBr, Vector2 uvTr, Vector2 uvTl,
            float grainStrength, float z = 0f)
        {
            int idx = _verts.Count;
            _verts.Add(new Vector3(bl.x, bl.y, z));
            _verts.Add(new Vector3(br.x, br.y, z));
            _verts.Add(new Vector3(tr.x, tr.y, z));
            _verts.Add(new Vector3(tl.x, tl.y, z));

            _uvs.Add(uvBl);
            _uvs.Add(uvBr);
            _uvs.Add(uvTr);
            _uvs.Add(uvTl);

            Vector2 uv1Val = new Vector2(grainStrength, 0f);
            _uv1s.Add(uv1Val);
            _uv1s.Add(uv1Val);
            _uv1s.Add(uv1Val);
            _uv1s.Add(uv1Val);

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

        private static float SmoothFactor(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float SignedAngleRad(Vector2 from, Vector2 to)
        {
            float cross = from.x * to.y - from.y * to.x;
            float dot = Vector2.Dot(from, to);
            return Mathf.Atan2(cross, dot);
        }

        private static Texture2D CreateGrainTexture()
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            System.Random rng = new System.Random(1995);
            Color[] px = new Color[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                float v = 0.72f + (float)rng.NextDouble() * 0.50f;
                px[i] = new Color(v, v, v, 1f);
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
