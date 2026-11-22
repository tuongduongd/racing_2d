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
        [SerializeField] private MapTheme defaultTheme;
        [SerializeField] private BikeSkinData referenceMultiPoseSkin;

        [Header("Pseudo-3D Projection")]
        [SerializeField] private int sliceCount = 104;
        [SerializeField] private float sliceStep = 1.9f;
        [SerializeField] private float firstSliceZ = 1.0f;
        [SerializeField] private float playerCameraZ = 5.4f;
        [SerializeField] private float cameraHeight = 2.55f;
        [SerializeField] private float cameraDepth = 0.86f;
        [SerializeField] private float curveVisualGain = 2.10f;
        [SerializeField] private float hillVisualGain = 0.048f;
        [SerializeField] private float bikeVisualScale = 2.05f;

        [Header("Camera Feel")]
        [SerializeField] private float baseOrthoSize = 5.0f;
        [SerializeField] private float maxSpeedOrthoBoost = 0.22f;
        [SerializeField] private float maxCameraRollDeg = 1.5f;

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
        private Material _defaultBikeMaterial;
        private SpriteRenderer _shadowRenderer;
        private SpriteRenderer _tailLightRenderer;

        public const float RoadEdgeNormalizedInset = 0.92f;

        private GameManager _gameManager;
        private EnvironmentDecorator _environmentDecorator;
        private SliceData[] _slices;
        private SliceData _playerSlice;
        private float _horizonScroll;
        private float _wheelAnimPhase;
        private float _shakeTrauma;
        private float _idleDemoScroll;
        private float _currentOrthoSize;
        private float _currentRollDeg;
        private int _cachedDividerLaneCount = -1;
        private float[] _cachedDividerOffsets;
        private float _firstActiveSliceZ;
        private float _smoothedVisualLatNorm;
        private float _smoothedHeadingError;
        private float _lastRenderBaseS;
        private bool _hasVisualBaseline;
        private float _renderAlpha = 1f;

        private struct ActiveThemeColors
        {
            public Color skyTop, skyMid, skyHorizon, sunGlow;
            public Color farRidgeTop, farRidgeBase, midHillLight, midHillDark;
            public Color terrainEarthLight, terrainEarthDark, terrainGrassLight, terrainGrassDark;
            public Color shoulderLight, shoulderDark, asphaltLight, asphaltDark;
            public Color centerYellowLine, laneWhiteLine, startCheckDark, startCheckLight;
            public float fogDensity, sunScale;
            public bool showSun, showStars;
            public HorizonSilhouetteStyle horizonStyle;
        }

        public MapTheme DefaultTheme => defaultTheme;
        public MapTheme EffectiveTheme => track != null && track.ActiveTheme != null ? track.ActiveTheme : defaultTheme;

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
            public TrackFeatureKind featureKind;
            public bool occluded;
        }

        private class OpponentSpriteSlot
        {
            public SpriteRenderer bike;
            public SpriteRenderer shadow;
            public SpriteRenderer tailLight;
            public SpriteRenderer sirenBeacon;
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

        /// <summary>
        /// Computes normalized horizontal offsets (within [-RoadEdgeNormalizedInset, +RoadEdgeNormalizedInset])
        /// for the (laneCount - 1) lane dividers of a 2..6 lane highway.
        /// </summary>
        public static float[] GetNormalizedLaneDividerOffsets(int laneCount)
        {
            int clamped = Mathf.Clamp(laneCount, 2, 6);
            int dividerCount = clamped - 1;
            float[] offsets = new float[dividerCount];
            for (int i = 0; i < dividerCount; i++)
            {
                int laneBoundary = i + 1;
                offsets[i] = ((2f * laneBoundary - clamped) / clamped) * RoadEdgeNormalizedInset;
            }
            return offsets;
        }

        /// <summary>
        /// Returns true when the 0-based divider index is the center double-yellow line (even lane counts only).
        /// </summary>
        public static bool IsCenterDoubleYellowDivider(int laneCount, int dividerIndex)
        {
            int clamped = Mathf.Clamp(laneCount, 2, 6);
            int laneBoundary = dividerIndex + 1;
            return (clamped % 2 == 0) && (laneBoundary == clamped / 2);
        }

        private float[] GetCachedLaneDividerOffsets(int laneCount)
        {
            int clamped = Mathf.Clamp(laneCount, 2, 6);
            if (_cachedDividerOffsets == null || _cachedDividerLaneCount != clamped)
            {
                _cachedDividerLaneCount = clamped;
                _cachedDividerOffsets = GetNormalizedLaneDividerOffsets(clamped);
            }
            return _cachedDividerOffsets;
        }

        private float ComputeNormalizedLateralOffset(float lateralOffset)
        {
            float halfTrackWidth = track != null ? Mathf.Max(1f, track.TrackWidth * 0.5f) : 1f;
            return lateralOffset / halfTrackWidth;
        }

        private float ProjectLateralScreenX(in SliceData slice, float lateralOffset)
        {
            return slice.centerX + ComputeNormalizedLateralOffset(lateralOffset) * slice.roadHalfW * RoadEdgeNormalizedInset;
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
                float relSpeed = (a != null && b != null)
                    ? (b.Physics.Velocity - a.Physics.Velocity).magnitude
                    : 10f;
                float severity = Mathf.InverseLerp(2f, 22f, relSpeed);
                AddCameraShake(Mathf.Lerp(0.35f, 0.78f, severity));
                EmitSparksForCars(a, b, Mathf.Lerp(0.65f, 1.25f, severity));
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
                targetCamera.backgroundColor = EffectiveTheme != null ? EffectiveTheme.SkyHorizon : RoadRashVisualTheme.SkyHorizon;
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
                _defaultBikeMaterial = _bikeRenderer.sharedMaterial;

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
                _renderAlpha = 1f;
            }
            else if (Time.timeScale <= 0.0001f || (_gameManager != null && _gameManager.CurrentFlowState != GameFlowState.Racing))
            {
                _renderAlpha = 1f;
            }
            else
            {
                _renderAlpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Mathf.Max(0.0001f, Time.fixedDeltaTime));
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
                ? 0.016f * speedNorm
                : 0f;
            float shakeAmp = (_shakeTrauma * _shakeTrauma) * 0.24f + offRoadShake;

            float sx = (Mathf.PerlinNoise(Time.time * 38f, 1.3f) - 0.5f) * 2f * shakeAmp;
            float sy = (Mathf.PerlinNoise(2.7f, Time.time * 38f) - 0.5f) * 2f * shakeAmp;

            targetCamera.transform.position = new Vector3(sx, sy, -10f);
            targetCamera.transform.rotation = Quaternion.Euler(0f, 0f, _currentRollDeg);
        }

        private ActiveThemeColors ResolveThemeColors()
        {
            MapTheme t = EffectiveTheme;
            if (t == null)
            {
                return new ActiveThemeColors
                {
                    skyTop = RoadRashVisualTheme.SkyTop,
                    skyMid = RoadRashVisualTheme.SkyMid,
                    skyHorizon = RoadRashVisualTheme.SkyHorizon,
                    sunGlow = RoadRashVisualTheme.SunGlow,
                    farRidgeTop = RoadRashVisualTheme.FarRidgeTop,
                    farRidgeBase = RoadRashVisualTheme.FarRidgeBase,
                    midHillLight = RoadRashVisualTheme.MidHillLight,
                    midHillDark = RoadRashVisualTheme.MidHillDark,
                    terrainEarthLight = RoadRashVisualTheme.TerrainEarthLight,
                    terrainEarthDark = RoadRashVisualTheme.TerrainEarthDark,
                    terrainGrassLight = RoadRashVisualTheme.TerrainGrassLight,
                    terrainGrassDark = RoadRashVisualTheme.TerrainGrassDark,
                    shoulderLight = RoadRashVisualTheme.ShoulderLight,
                    shoulderDark = RoadRashVisualTheme.ShoulderDark,
                    asphaltLight = RoadRashVisualTheme.AsphaltLight,
                    asphaltDark = RoadRashVisualTheme.AsphaltDark,
                    centerYellowLine = RoadRashVisualTheme.CenterYellowLine,
                    laneWhiteLine = RoadRashVisualTheme.LaneWhiteLine,
                    startCheckDark = RoadRashVisualTheme.StartCheckDark,
                    startCheckLight = RoadRashVisualTheme.StartCheckLight,
                    fogDensity = 0.86f,
                    sunScale = 1.0f,
                    showSun = true,
                    showStars = false,
                    horizonStyle = HorizonSilhouetteStyle.ValleyMountains
                };
            }

            return new ActiveThemeColors
            {
                skyTop = t.SkyTop,
                skyMid = t.SkyMid,
                skyHorizon = t.SkyHorizon,
                sunGlow = t.SunGlow,
                farRidgeTop = t.FarRidgeTop,
                farRidgeBase = t.FarRidgeBase,
                midHillLight = t.MidHillLight,
                midHillDark = t.MidHillDark,
                terrainEarthLight = t.TerrainEarthLight,
                terrainEarthDark = t.TerrainEarthDark,
                terrainGrassLight = t.TerrainGrassLight,
                terrainGrassDark = t.TerrainGrassDark,
                shoulderLight = t.ShoulderLight,
                shoulderDark = t.ShoulderDark,
                asphaltLight = t.AsphaltLight,
                asphaltDark = t.AsphaltDark,
                centerYellowLine = t.CenterYellowLine,
                laneWhiteLine = t.LaneWhiteLine,
                startCheckDark = t.StartCheckDark,
                startCheckLight = t.StartCheckLight,
                fogDensity = t.FogDensity,
                sunScale = t.SunScale,
                showSun = t.ShowSun,
                showStars = t.ShowStars,
                horizonStyle = t.HorizonStyle
            };
        }

        private void RenderFrame(bool isMenuBackdrop)
        {
            _verts.Clear();
            _uvs.Clear();
            _uv1s.Clear();
            _tris.Clear();
            _colors.Clear();

            ActiveThemeColors theme = ResolveThemeColors();
            float halfH = targetCamera.orthographicSize;
            float halfW = halfH * targetCamera.aspect;

            float baseS = isMenuBackdrop ? _idleDemoScroll : playerCar.GetInterpolatedTrackDistance(_renderAlpha, track);
            float interpLat = isMenuBackdrop ? 0f : playerCar.GetInterpolatedLateralOffset(_renderAlpha);
            float interpHeading = isMenuBackdrop ? 0f : playerCar.GetInterpolatedHeading(_renderAlpha);
            Vector2 interpForward = new Vector2(Mathf.Cos(interpHeading), Mathf.Sin(interpHeading));

            TrackSample playerSample = track.SampleAtDistance(baseS);
            float rawHeadingError = isMenuBackdrop ? 0f : SignedAngleRad(playerSample.tangent, interpForward);
            float rawLatNorm = isMenuBackdrop ? 0f : ComputeNormalizedLateralOffset(interpLat);
            float baseElevation = playerSample.elevation;

            float sJump = WrappedDeltaDistance(baseS, _lastRenderBaseS, track.TotalLength);
            if (isMenuBackdrop || !_hasVisualBaseline || Mathf.Abs(sJump) > 12f)
            {
                _smoothedHeadingError = rawHeadingError;
                _smoothedVisualLatNorm = rawLatNorm;
                _hasVisualBaseline = !isMenuBackdrop;
            }
            else
            {
                float dtRender = Mathf.Max(0f, Time.deltaTime);
                _smoothedHeadingError = Mathf.Lerp(_smoothedHeadingError, rawHeadingError, 1f - Mathf.Exp(-18f * dtRender));
                _smoothedVisualLatNorm = Mathf.Lerp(_smoothedVisualLatNorm, rawLatNorm, 1f - Mathf.Exp(-24f * dtRender));
            }
            _lastRenderBaseS = baseS;

            float headingError = _smoothedHeadingError;
            float horizonY = halfH * 0.22f;

            // 1. Sky gradient (top -> mid sky -> horizon haze)
            float skyMidY = Mathf.Lerp(horizonY, halfH, 0.52f);
            AddQuad(
                new Vector2(-halfW - 1f, skyMidY),
                new Vector2(halfW + 1f, skyMidY),
                new Vector2(halfW + 1f, halfH + 1f),
                new Vector2(-halfW - 1f, halfH + 1f),
                theme.skyMid, theme.skyMid,
                theme.skyTop, theme.skyTop,
                0f
            );
            AddQuad(
                new Vector2(-halfW - 1f, horizonY),
                new Vector2(halfW + 1f, horizonY),
                new Vector2(halfW + 1f, skyMidY),
                new Vector2(-halfW - 1f, skyMidY),
                theme.skyHorizon, theme.skyHorizon,
                theme.skyMid, theme.skyMid,
                0f
            );

            // Base ground horizon cap so no background sky strip ever leaks below hills or at corners
            AddQuad(
                new Vector2(-halfW - 1f, -halfH - 1.5f),
                new Vector2(halfW + 1f, -halfH - 1.5f),
                new Vector2(halfW + 1f, horizonY),
                new Vector2(-halfW - 1f, horizonY),
                theme.terrainGrassDark, theme.terrainGrassDark,
                theme.skyHorizon, theme.skyHorizon,
                0f
            );

            float scrollSpeed = isMenuBackdrop ? 14f : playerCar.Physics.ForwardSpeed;
            _horizonScroll -= scrollSpeed * (playerSample.curvature * 18f - headingError) * Time.deltaTime * 0.14f;

            // 2. Sky stars / sun & Parallax mountains / skyline
            if (theme.showStars)
            {
                DrawNightStars(halfW, halfH, horizonY, theme);
            }
            if (theme.showSun)
            {
                DrawSunCircle(halfW, horizonY, theme);
            }
            DrawParallaxMountains(halfW, horizonY, theme);

            // 3. Project world-locked road slices anchored smoothly across baseS (eliminates stripe/projection judder)
            float segLen = Mathf.Max(0.5f, sliceStep);
            float minStartZ = Mathf.Max(0.55f, firstSliceZ - segLen * 0.25f);
            float rawStartS = baseS + (minStartZ - playerCameraZ);
            int startSliceWorldIdx = Mathf.FloorToInt(rawStartS / segLen);
            float firstWorldS = startSliceWorldIdx * segLen;
            _firstActiveSliceZ = playerCameraZ + (firstWorldS - baseS);
            while (_firstActiveSliceZ < 0.45f)
            {
                startSliceWorldIdx++;
                firstWorldS += segLen;
                _firstActiveSliceZ += segLen;
            }

            float curveAcc = 0f;
            float xOffset = 0f;
            float playerLatNorm = isMenuBackdrop ? 0f : _smoothedVisualLatNorm;
            float headingShift = Mathf.Tan(Mathf.Clamp(headingError, -0.7f, 0.7f)) * halfW * 0.85f;
            float maxAheadZ = Mathf.Max(1f, (_firstActiveSliceZ + (sliceCount - 1) * segLen) - playerCameraZ);
            float fogMaxZ = Mathf.Max(1f, sliceCount * segLen);

            int validSlices = 0;
            float maxDrawnY = -halfH - 4.0f;

            for (int i = 0; i < sliceCount; i++)
            {
                int worldSliceIdx = startSliceWorldIdx + i;
                float worldSUnwrapped = worldSliceIdx * segLen;
                float dzFromPlayer = worldSUnwrapped - baseS;
                float worldZ = playerCameraZ + dzFromPlayer;
                float scale = cameraDepth / (worldZ * 0.135f + 0.34f);
                float sampleS = track.WrapDistance(worldSUnwrapped);
                TrackSample sample = track.SampleAtDistance(sampleS);

                if (dzFromPlayer > 0f)
                {
                    float segDz = Mathf.Min(segLen, dzFromPlayer);
                    curveAcc += sample.curvature * segDz * curveVisualGain;
                    xOffset += curveAcc * (segDz / segLen) * 0.045f;
                }

                float hillDelta = (sample.elevation - baseElevation) * hillVisualGain * scale;
                float projY = horizonY - cameraHeight * scale * 2.25f + hillDelta;

                bool occluded = projY < maxDrawnY;
                float clampedY = Mathf.Max(projY, maxDrawnY);

                float roadHalfW = (track.TrackWidth * 0.5f) * scale * 0.54f;
                float shoulderHalfW = roadHalfW * 1.22f;
                float forwardT = Mathf.Clamp01(dzFromPlayer / maxAheadZ);
                float centerX = xOffset - playerLatNorm * roadHalfW * RoadEdgeNormalizedInset + headingShift * forwardT;

                int stripe = Mathf.FloorToInt(worldSliceIdx * 0.25f);
                bool isStartLine = sampleS < 4.5f || sampleS > track.TotalLength - 2.5f;
                track.TryGetFeatureAtDistance(sampleS, out TrackFeatureKind featKind, out _);

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
                    featureKind = featKind,
                    occluded = occluded
                };

                if (projY > maxDrawnY) maxDrawnY = projY;
                validSlices++;
            }

            if (!TrySampleAtCameraZ(playerCameraZ, out _playerSlice))
            {
                _playerSlice = _slices[0];
            }

            // 4. Draw road, gravel shoulders, flanking valley embankments, and dynamic N-lane markings (2-6 lanes)
            int laneCount = Mathf.Clamp(track.LaneCount, 2, 6);
            float[] dividerOffsets = GetCachedLaneDividerOffsets(laneCount);
            for (int i = 0; i < validSlices - 1; i++)
            {
                SliceData a = _slices[i];
                SliceData b = _slices[i + 1];
                if (b.screenY <= a.screenY + 0.0001f) continue;

                float tA = Mathf.Clamp01((a.worldZ - firstSliceZ) / fogMaxZ);
                float tB = Mathf.Clamp01((b.worldZ - firstSliceZ) / fogMaxZ);
                float fogA = SmoothFactor(0.45f, 1.0f, tA) * theme.fogDensity;
                float fogB = SmoothFactor(0.45f, 1.0f, tB) * theme.fogDensity;

                bool even = (a.stripeIndex % 2) == 0;
                bool isOverpass = a.featureKind == TrackFeatureKind.Overpass;
                bool isTunnel = a.featureKind == TrackFeatureKind.Tunnel;
                bool isCrosswalk = a.featureKind == TrackFeatureKind.Crosswalk;

                Color horizonTarget = isTunnel ? new Color(0.03f, 0.04f, 0.07f) : theme.skyHorizon;
                float sliceFogA = isTunnel ? Mathf.Clamp01(fogA * 1.15f + 0.12f) : fogA;
                float sliceFogB = isTunnel ? Mathf.Clamp01(fogB * 1.15f + 0.12f) : fogB;

                Color earthA = Color.Lerp(even ? theme.terrainEarthLight : theme.terrainEarthDark, horizonTarget, sliceFogA);
                Color earthB = Color.Lerp(even ? theme.terrainEarthLight : theme.terrainEarthDark, horizonTarget, sliceFogB);
                Color grassA = Color.Lerp(even ? theme.terrainGrassLight : theme.terrainGrassDark, horizonTarget, sliceFogA);
                Color grassB = Color.Lerp(even ? theme.terrainGrassLight : theme.terrainGrassDark, horizonTarget, sliceFogB);
                Color shoulderA = Color.Lerp(even ? theme.shoulderLight : theme.shoulderDark, horizonTarget, sliceFogA);
                Color shoulderB = Color.Lerp(even ? theme.shoulderLight : theme.shoulderDark, horizonTarget, sliceFogB);
                Color roadA = Color.Lerp(even ? theme.asphaltLight : theme.asphaltDark, horizonTarget, sliceFogA);
                Color roadB = Color.Lerp(even ? theme.asphaltLight : theme.asphaltDark, horizonTarget, sliceFogB);

                float uvYa = a.worldS * 0.15f;
                float uvYb = b.worldS * 0.15f;

                if (isOverpass)
                {
                    // Cầu vượt: nền trời hở phía dưới hai bên & dải lan can bê tông dọc vai cầu
                    float railOutA = a.shoulderHalfW * 1.24f;
                    float railOutB = b.shoulderHalfW * 1.24f;
                    float railH_A = 0.28f * a.scale;
                    float railH_B = 0.28f * b.scale;
                    Color deckColA = Color.Lerp(new Color(0.42f, 0.45f, 0.52f), horizonTarget, sliceFogA);
                    Color deckColB = Color.Lerp(new Color(0.42f, 0.45f, 0.52f), horizonTarget, sliceFogB);
                    Color deckVoidA = Color.Lerp(theme.skyMid, horizonTarget, 0.45f);
                    Color deckVoidB = Color.Lerp(theme.skyMid, horizonTarget, 0.45f);

                    AddQuad(
                        new Vector2(-halfW - 1.5f, a.screenY),
                        new Vector2(a.centerX - railOutA, a.screenY),
                        new Vector2(b.centerX - railOutB, b.screenY),
                        new Vector2(-halfW - 1.5f, b.screenY),
                        deckVoidA, deckVoidA, deckVoidB, deckVoidB, 0f);
                    AddQuad(
                        new Vector2(a.centerX + railOutA, a.screenY),
                        new Vector2(halfW + 1.5f, a.screenY),
                        new Vector2(halfW + 1.5f, b.screenY),
                        new Vector2(b.centerX + railOutB, b.screenY),
                        deckVoidA, deckVoidA, deckVoidB, deckVoidB, 0f);

                    // Lan can trái & phải cầu vượt
                    AddQuad(
                        new Vector2(a.centerX - railOutA, a.screenY),
                        new Vector2(a.centerX - a.shoulderHalfW, a.screenY),
                        new Vector2(b.centerX - b.shoulderHalfW, b.screenY + railH_B),
                        new Vector2(b.centerX - railOutB, b.screenY + railH_B),
                        deckColA, deckColA, deckColB, deckColB, 0f);
                    AddQuad(
                        new Vector2(a.centerX + a.shoulderHalfW, a.screenY),
                        new Vector2(a.centerX + railOutA, a.screenY),
                        new Vector2(b.centerX + railOutB, b.screenY + railH_B),
                        new Vector2(b.centerX + b.shoulderHalfW, b.screenY + railH_B),
                        deckColA, deckColA, deckColB, deckColB, 0f);
                }
                else
                {
                    // Left sloping hillside embankment
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
                }

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
                    DrawStartLineCheckers(a, b, sliceFogA, sliceFogB, laneCount, theme);
                }
                else if (isCrosswalk)
                {
                    DrawCrosswalkStripes(a, b, sliceFogA, sliceFogB, laneCount, theme);
                }
                else
                {
                    Color whiteA = Color.Lerp(theme.laneWhiteLine, horizonTarget, sliceFogA);
                    Color whiteB = Color.Lerp(theme.laneWhiteLine, horizonTarget, sliceFogB);
                    Color yellowA = Color.Lerp(theme.centerYellowLine, horizonTarget, sliceFogA);
                    Color yellowB = Color.Lerp(theme.centerYellowLine, horizonTarget, sliceFogB);

                    float laneUnitA = a.roadHalfW / laneCount;
                    float laneUnitB = b.roadHalfW / laneCount;

                    // Solid white edge lines
                    float edgeW_A = Mathf.Max(0.012f, laneUnitA * 0.10f);
                    float edgeW_B = Mathf.Max(0.008f, laneUnitB * 0.10f);
                    float edgeOffA = a.roadHalfW * RoadEdgeNormalizedInset;
                    float edgeOffB = b.roadHalfW * RoadEdgeNormalizedInset;
                    AddStripQuad(a.centerX - edgeOffA, a.screenY, edgeW_A, b.centerX - edgeOffB, b.screenY, edgeW_B, whiteA, whiteB);
                    AddStripQuad(a.centerX + edgeOffA, a.screenY, edgeW_A, b.centerX + edgeOffB, b.screenY, edgeW_B, whiteA, whiteB);

                    // Dynamic N-lane dividers (2 to 6 lanes)
                    float yelW_A = Mathf.Max(0.010f, laneUnitA * 0.088f);
                    float yelW_B = Mathf.Max(0.007f, laneUnitB * 0.088f);
                    float yelGapA = Mathf.Max(0.014f, laneUnitA * 0.112f);
                    float yelGapB = Mathf.Max(0.009f, laneUnitB * 0.112f);
                    float dashW_A = Mathf.Max(0.010f, laneUnitA * 0.072f);
                    float dashW_B = Mathf.Max(0.007f, laneUnitB * 0.072f);

                    for (int divIdx = 0; divIdx < dividerOffsets.Length; divIdx++)
                    {
                        float divNorm = dividerOffsets[divIdx];
                        bool isCenter = IsCenterDoubleYellowDivider(laneCount, divIdx);

                        if (isCenter)
                        {
                            float cxA = a.centerX + a.roadHalfW * divNorm;
                            float cxB = b.centerX + b.roadHalfW * divNorm;
                            AddStripQuad(cxA - yelGapA, a.screenY, yelW_A, cxB - yelGapB, b.screenY, yelW_B, yellowA, yellowB);
                            AddStripQuad(cxA + yelGapA, a.screenY, yelW_A, cxB + yelGapB, b.screenY, yelW_B, yellowA, yellowB);
                        }
                        else if (even)
                        {
                            float lxA = a.centerX + a.roadHalfW * divNorm;
                            float lxB = b.centerX + b.roadHalfW * divNorm;
                            AddStripQuad(lxA, a.screenY, dashW_A, lxB, b.screenY, dashW_B, whiteA, whiteB);
                        }
                    }
                }

                if (isTunnel)
                {
                    DrawTunnelEnclosure(a, b, halfW, halfH, sliceFogA, sliceFogB, even);
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetUVs(1, _uv1s);
            _mesh.SetTriangles(_tris, 0);
            _mesh.SetColors(_colors);
            _mesh.RecalculateBounds();

            // 5. Render pooled roadside sprite props & obstacles
            UpdateRoadsideProps(validSlices, isMenuBackdrop, theme);
        }

        private void DrawStartLineCheckers(SliceData a, SliceData b, float fogA, float fogB, int laneCount, in ActiveThemeColors theme)
        {
            int cols = Mathf.Clamp(laneCount, 2, 6) * 2;
            float stepA = (a.roadHalfW * 2f) / cols;
            float stepB = (b.roadHalfW * 2f) / cols;

            for (int c = 0; c < cols; c++)
            {
                bool light = (c + a.stripeIndex) % 2 == 0;
                Color baseCol = light ? theme.startCheckLight : theme.startCheckDark;
                Color ca = Color.Lerp(baseCol, theme.skyHorizon, fogA);
                Color cb = Color.Lerp(baseCol, theme.skyHorizon, fogB);

                float ax0 = a.centerX - a.roadHalfW + c * stepA;
                float ax1 = ax0 + stepA;
                float bx0 = b.centerX - b.roadHalfW + c * stepB;
                float bx1 = bx0 + stepB;
                AddQuad(new Vector2(ax0, a.screenY), new Vector2(ax1, a.screenY), new Vector2(bx1, b.screenY), new Vector2(bx0, b.screenY), ca, ca, cb, cb, 0f);
            }
        }

        private void DrawCrosswalkStripes(SliceData a, SliceData b, float fogA, float fogB, int laneCount, in ActiveThemeColors theme)
        {
            int stripes = Mathf.Clamp(laneCount, 2, 6) * 2;
            float usableA = a.roadHalfW * 2f * RoadEdgeNormalizedInset;
            float usableB = b.roadHalfW * 2f * RoadEdgeNormalizedInset;
            float cellA = usableA / stripes;
            float cellB = usableB / stripes;

            Color ca = Color.Lerp(theme.laneWhiteLine, theme.skyHorizon, fogA);
            Color cb = Color.Lerp(theme.laneWhiteLine, theme.skyHorizon, fogB);

            for (int c = 0; c < stripes; c++)
            {
                float padA = cellA * 0.22f;
                float padB = cellB * 0.22f;
                float ax0 = a.centerX - usableA * 0.5f + c * cellA + padA;
                float ax1 = a.centerX - usableA * 0.5f + (c + 1) * cellA - padA;
                float bx0 = b.centerX - usableB * 0.5f + c * cellB + padB;
                float bx1 = b.centerX - usableB * 0.5f + (c + 1) * cellB - padB;
                AddQuad(new Vector2(ax0, a.screenY), new Vector2(ax1, a.screenY), new Vector2(bx1, b.screenY), new Vector2(bx0, b.screenY), ca, ca, cb, cb, 0f);
            }
        }

        private void DrawTunnelEnclosure(SliceData a, SliceData b, float halfW, float halfH, float fogA, float fogB, bool litStripe)
        {
            Color wallColA = Color.Lerp(new Color(0.08f, 0.09f, 0.14f), new Color(0.02f, 0.03f, 0.06f), fogA);
            Color wallColB = Color.Lerp(new Color(0.08f, 0.09f, 0.14f), new Color(0.02f, 0.03f, 0.06f), fogB);
            Color ceilCol = new Color(0.03f, 0.03f, 0.07f);

            float wallHeightA = 2.6f * a.scale;
            float wallHeightB = 2.6f * b.scale;
            float topA = Mathf.Min(halfH + 1.5f, a.screenY + wallHeightA);
            float topB = Mathf.Min(halfH + 1.5f, b.screenY + wallHeightB);

            // Tường trái & phải che kín hai bên hầm
            AddQuad(
                new Vector2(-halfW - 1.5f, a.screenY),
                new Vector2(a.centerX - a.shoulderHalfW, a.screenY),
                new Vector2(b.centerX - b.shoulderHalfW, topB),
                new Vector2(-halfW - 1.5f, topA),
                wallColA, wallColA, wallColB, wallColB, 0f);
            AddQuad(
                new Vector2(a.centerX + a.shoulderHalfW, a.screenY),
                new Vector2(halfW + 1.5f, a.screenY),
                new Vector2(halfW + 1.5f, topB),
                new Vector2(b.centerX + b.shoulderHalfW, topA),
                wallColA, wallColA, wallColB, wallColB, 0f);

            // Vòm trần hầm che phần bầu trời phía trên
            AddQuad(
                new Vector2(-halfW - 1.5f, topA),
                new Vector2(halfW + 1.5f, topA),
                new Vector2(halfW + 1.5f, halfH + 1.5f),
                new Vector2(-halfW - 1.5f, halfH + 1.5f),
                ceilCol, ceilCol, ceilCol, ceilCol, 0f);

            // Đèn trần hầm hắt sáng theo từng nhịp
            if (litStripe)
            {
                Color lampCol = new Color(1.00f, 0.90f, 0.56f, 0.92f);
                float lwA = a.roadHalfW * 0.28f;
                float lwB = b.roadHalfW * 0.28f;
                float lhA = 0.14f * a.scale;
                float lhB = 0.14f * b.scale;
                AddQuad(
                    new Vector2(a.centerX - lwA, topA - lhA),
                    new Vector2(a.centerX + lwA, topA - lhA),
                    new Vector2(b.centerX + lwB, topB - lhB),
                    new Vector2(b.centerX - lwB, topB - lhB),
                    lampCol, lampCol, lampCol, lampCol, 0f);
            }
        }

        private void DrawNightStars(float halfW, float halfH, float horizonY, in ActiveThemeColors theme)
        {
            float skyMinY = horizonY + 0.8f;
            float skyMaxY = halfH - 0.15f;
            for (int i = 0; i < 28; i++)
            {
                float h1 = Mathf.Sin(i * 12.9898f) * 43758.5453f;
                float u = h1 - Mathf.Floor(h1);
                float h2 = Mathf.Sin(i * 78.233f) * 24634.6345f;
                float v = h2 - Mathf.Floor(h2);

                float sx = Mathf.Lerp(-halfW * 0.95f, halfW * 0.95f, Mathf.Repeat(u + _horizonScroll * 0.015f, 1f));
                float sy = Mathf.Lerp(skyMinY, skyMaxY, v);
                float sz = (i % 4 == 0) ? 0.045f : 0.028f;
                float twinkle = 0.65f + 0.35f * Mathf.Sin(Time.time * (2.5f + (i % 5)) + i);
                Color c = Color.Lerp(theme.sunGlow, Color.white, 0.72f);
                c.a = twinkle;

                AddQuad(
                    new Vector2(sx - sz, sy - sz),
                    new Vector2(sx + sz, sy - sz),
                    new Vector2(sx + sz, sy + sz),
                    new Vector2(sx - sz, sy + sz),
                    c, c, c, c, 0f
                );
            }
        }

        private void DrawSunCircle(float halfW, float horizonY, in ActiveThemeColors theme)
        {
            float sunX = -halfW * 0.42f + (_horizonScroll * 0.12f % 3.5f);
            float sunY = horizonY + 1.65f;
            float radius = 0.72f * theme.sunScale;
            Color centerCol = new Color(theme.sunGlow.r, theme.sunGlow.g, theme.sunGlow.b, 0.62f);
            Color edgeCol = new Color(theme.skyMid.r, theme.skyMid.g, theme.skyMid.b, 0f);

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

        private void DrawParallaxMountains(float halfW, float horizonY, in ActiveThemeColors theme)
        {
            if (theme.horizonStyle == HorizonSilhouetteStyle.NeonCitySkyline)
            {
                const int bldgCount = 26;
                float bldgW = (halfW * 2f + 2.0f) / bldgCount;
                for (int i = 0; i < bldgCount; i++)
                {
                    float scrollIdx = i + Mathf.Floor(_horizonScroll * 3.2f);
                    float x0 = -halfW - 1.0f + i * bldgW - Mathf.Repeat(_horizonScroll * 3.2f, 1f) * bldgW;
                    float x1 = x0 + bldgW * 0.92f;
                    float hash = Mathf.Abs(Mathf.Sin(scrollIdx * 17.3f));
                    float height = 0.35f + hash * 1.15f;
                    Color topCol = (i % 3 == 0) ? Color.Lerp(theme.farRidgeTop, theme.sunGlow, 0.35f) : theme.farRidgeTop;

                    AddQuad(
                        new Vector2(x0, horizonY),
                        new Vector2(x1, horizonY),
                        new Vector2(x1, horizonY + height),
                        new Vector2(x0, horizonY + height),
                        theme.farRidgeBase, theme.farRidgeBase,
                        topCol, topCol,
                        0f
                    );
                }

                const int lowCount = 20;
                float lowW = (halfW * 2f + 2.0f) / lowCount;
                for (int i = 0; i < lowCount; i++)
                {
                    float scrollIdx = i + Mathf.Floor(_horizonScroll * 5.0f);
                    float x0 = -halfW - 1.0f + i * lowW - Mathf.Repeat(_horizonScroll * 5.0f, 1f) * lowW;
                    float x1 = x0 + lowW * 0.88f;
                    float h = 0.16f + Mathf.Abs(Mathf.Cos(scrollIdx * 11.7f)) * 0.45f;
                    AddQuad(
                        new Vector2(x0, horizonY),
                        new Vector2(x1, horizonY),
                        new Vector2(x1, horizonY + h),
                        new Vector2(x0, horizonY + h),
                        theme.midHillDark, theme.midHillDark,
                        theme.midHillLight, theme.midHillLight,
                        0f
                    );
                }
                return;
            }

            float farAmp = theme.horizonStyle == HorizonSilhouetteStyle.CoastalHills ? 0.22f : 0.42f;
            float midAmp = theme.horizonStyle == HorizonSilhouetteStyle.CoastalHills ? 0.18f : 0.30f;

            const int farSegs = 22;
            float farW = (halfW * 2f + 2.0f) / farSegs;
            for (int i = 0; i < farSegs; i++)
            {
                float x0 = -halfW - 1.0f + i * farW;
                float x1 = x0 + farW;
                float u0 = i * 0.65f + _horizonScroll * 0.45f;
                float u1 = (i + 1) * 0.65f + _horizonScroll * 0.45f;
                float h0 = horizonY + 0.35f + Mathf.Max(0f, Mathf.Sin(u0) * farAmp + Mathf.Cos(u0 * 2.1f) * (farAmp * 0.55f));
                float h1 = horizonY + 0.35f + Mathf.Max(0f, Mathf.Sin(u1) * farAmp + Mathf.Cos(u1 * 2.1f) * (farAmp * 0.55f));

                AddQuad(
                    new Vector2(x0, horizonY),
                    new Vector2(x1, horizonY),
                    new Vector2(x1, h1),
                    new Vector2(x0, h0),
                    theme.farRidgeBase, theme.farRidgeBase,
                    theme.farRidgeTop, theme.farRidgeTop,
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
                float h0 = horizonY + 0.14f + Mathf.Max(0f, Mathf.Sin(u0) * midAmp + Mathf.Sin(u0 * 0.45f) * 0.16f);
                float h1 = horizonY + 0.14f + Mathf.Max(0f, Mathf.Sin(u1) * midAmp + Mathf.Sin(u1 * 0.45f) * 0.16f);

                AddQuad(
                    new Vector2(x0, horizonY),
                    new Vector2(x1, horizonY),
                    new Vector2(x1, h1),
                    new Vector2(x0, h0),
                    theme.midHillDark, theme.midHillDark,
                    theme.midHillLight, theme.midHillLight,
                    0f
                );
            }
        }

        private void UpdateRoadsideProps(int validSlices, bool isMenuBackdrop, in ActiveThemeColors theme)
        {
            int propIdx = 0;
            float totalLen = Mathf.Max(1f, track.TotalLength);
            float playerS = isMenuBackdrop ? _idleDemoScroll : playerCar.GetInterpolatedTrackDistance(_renderAlpha, track);
            float segLen = Mathf.Max(0.5f, sliceStep);
            float maxAheadDz = (_firstActiveSliceZ + (sliceCount - 1) * segLen) - playerCameraZ - segLen;
            float minBehindDz = Mathf.Min(-(playerCameraZ - 1.2f), _firstActiveSliceZ - playerCameraZ);
            float maxSliceZ = _firstActiveSliceZ + sliceCount * segLen;

            if (_environmentDecorator == null)
            {
                _environmentDecorator = _gameManager != null
                    ? _gameManager.EnvironmentDecorator
                    : FindAnyObjectByType<EnvironmentDecorator>();
            }

            if (_environmentDecorator != null && _environmentDecorator.EnableExtraProps)
            {
                _environmentDecorator.EnsureBuiltForTrack(track);
                IReadOnlyList<RoadsideDecoration> decorations = _environmentDecorator.Decorations;

                for (int i = 0; i < decorations.Count; i++)
                {
                    RoadsideDecoration dec = decorations[i];
                    float dz = dec.TrackDistance - playerS;
                    if (dz < -totalLen * 0.5f) dz += totalLen;
                    if (dz > totalLen * 0.5f) dz -= totalLen;

                    if (dz >= minBehindDz && dz <= maxAheadDz)
                    {
                        float propZ = playerCameraZ + dz;
                        if (TrySampleAtCameraZ(propZ, out SliceData s) && !s.occluded)
                        {
                            float fog = SmoothFactor(0.45f, 1.0f, propZ / maxSliceZ) * theme.fogDensity;
                            Color tint = Color.Lerp(Color.white, theme.skyHorizon, fog);
                            int order = Mathf.Clamp(475 - Mathf.RoundToInt(propZ * 2.5f), 15, 475);
                            float px = s.centerX + dec.SideSign * (s.shoulderHalfW + s.scale * dec.ExtraShoulderOffset);
                            float py = s.screenY + s.scale * 0.08f;
                            PlaceProp(propIdx++, dec.Kind, px, py, s.scale * dec.ScaleMultiplier, order, tint, dec.CustomOverrideSprite);
                        }
                    }
                }
            }
            else
            {
                for (int i = validSlices - 2; i >= 1; i--)
                {
                    SliceData s = _slices[i];
                    if (s.occluded) continue;

                    int slot9 = Mathf.FloorToInt(s.worldS / 9f);
                    int prevSlot9 = Mathf.FloorToInt(_slices[i - 1].worldS / 9f);
                    if (slot9 == prevSlot9) continue;

                    float fog = SmoothFactor(0.45f, 1.0f, i / (float)(validSlices - 1)) * theme.fogDensity;
                    Color tint = Color.Lerp(Color.white, theme.skyHorizon, fog);
                    int baseOrder = Mathf.Clamp(475 - Mathf.RoundToInt(s.worldZ * 2.5f), 15, 475);

                    float leftX = s.centerX - s.shoulderHalfW - s.scale * 0.85f;
                    PlaceProp(propIdx++, RoadsidePropKind.FencePost, leftX, s.screenY + s.scale * 0.08f, s.scale * 1.15f, baseOrder, tint);

                    if ((slot9 % 2) == 0)
                    {
                        float poleX = s.centerX - s.shoulderHalfW - s.scale * 1.45f;
                        RoadsidePropKind leftTallKind = ((slot9 / 2) % 2 == 0) ? RoadsidePropKind.StreetLamp : RoadsidePropKind.UtilityPole;
                        PlaceProp(propIdx++, leftTallKind, poleX, s.screenY + s.scale * 0.14f, s.scale * 1.45f, baseOrder + 1, tint);
                    }

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
            }

            // Render active on-track obstacles (traffic cones, oil barrels, road barriers, oil slicks)
            if (_gameManager != null && _gameManager.ObstacleSystem != null && track != null && playerCar != null)
            {
                var obstacles = _gameManager.ObstacleSystem.Obstacles;
                for (int i = 0; i < obstacles.Count; i++)
                {
                    var obs = obstacles[i];
                    if (obs == null || obs.IsOnCooldown) continue;

                    float dz = obs.TrackDistance - playerS;
                    if (dz < -totalLen * 0.5f) dz += totalLen;
                    if (dz > totalLen * 0.5f) dz -= totalLen;

                    if (dz >= minBehindDz && dz <= maxAheadDz)
                    {
                        float obsZ = playerCameraZ + dz;
                        if (TrySampleAtCameraZ(obsZ, out SliceData obsSlice) && !obsSlice.occluded)
                        {
                            float screenX = ProjectLateralScreenX(obsSlice, obs.LateralOffset);
                            int order = Mathf.Clamp(478 - Mathf.RoundToInt(obsZ * 2.5f), 18, 492);
                            float fog = SmoothFactor(0.45f, 1.0f, obsZ / maxSliceZ) * 0.72f;
                            Color obsTint = Color.Lerp(Color.white, theme.skyHorizon, fog);
                            float obsScale = obs.Kind == ObstacleKind.ParkedBus ? obsSlice.scale * 1.45f : obsSlice.scale * 1.20f;
                            PlaceProp(propIdx++, obs.VisualPropKind, screenX, obsSlice.screenY, obsScale, order, obsTint, obs.CustomSprite);
                        }
                    }
                }
            }

            for (int i = propIdx; i < _propPool.Count; i++)
            {
                _propPool[i].gameObject.SetActive(false);
            }
        }

        private void PlaceProp(int index, RoadsidePropKind kind, float x, float y, float scale, int sortingOrder, Color tint, Sprite overrideSprite = null)
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
            propSr.sprite = overrideSprite != null ? overrideSprite : RoadRashPropSprites.GetPropSprite(kind);
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
                vibe += Mathf.Sin(Time.time * 75f) * 0.022f;
            }

            float pScale = _playerSlice.scale * bikeVisualScale;
            float pGroundY = _playerSlice.screenY;
            int playerOrder = Mathf.Clamp(480 - Mathf.RoundToInt(playerCameraZ * 2.5f), 20, 480);

            BikeSkinData activeSkin = playerCar.ActiveSkin;
            if (activeSkin != null && activeSkin.CustomMaterial != null)
            {
                _bikeRenderer.sharedMaterial = activeSkin.CustomMaterial;
            }
            else if (_defaultBikeMaterial != null)
            {
                _bikeRenderer.sharedMaterial = _defaultBikeMaterial;
            }

            float interpLean = isMenuBackdrop ? 0f : playerCar.GetInterpolatedLeanAngle(_renderAlpha);

            Sprite overrideSprite = activeSkin != null ? activeSkin.GetOverrideSprite() : null;
            bool isMultiPoseArt = false;
            if (overrideSprite != null)
            {
                _bikeRenderer.sprite = overrideSprite;
                _bikeRenderer.flipX = false;
            }
            else
            {
                BikeSpriteSet set;
                if (activeSkin != null && activeSkin.HasMultiPoseSprites)
                {
                    set = BikePaletteSwapper.GetOrCreateSet(activeSkin);
                }
                else
                {
                    BikeSkinData refSkin = ResolveReferenceMultiPoseSkin();
                    if (refSkin != null)
                    {
                        BikePaletteKind palette = activeSkin != null
                            ? BikePaletteSwapper.ToPaletteKind(activeSkin.SkinId)
                            : BikePaletteKind.XanhSM;
                        set = BikePaletteSwapper.GetOrCreateSet(palette, refSkin);
                    }
                    else
                    {
                        set = activeSkin != null
                            ? RoadRashBikeSprites.GetOrCreateSet(activeSkin)
                            : RoadRashBikeSprites.GetOrCreateSet(playerCar.BikeColor);
                    }
                }

                BikePose pose = ResolveBikePose(playerCar, interpLean);
                isMultiPoseArt = set != null && set.FlipKickRight;
                _bikeRenderer.sprite = RoadRashBikeSprites.SelectPoseSprite(set, pose, animFrame);
                _bikeRenderer.flipX = isMultiPoseArt && pose == BikePose.KickRight;
            }
            _bikeRenderer.color = Color.white;
            _bikeRenderer.sortingOrder = playerOrder;

            float airH = isMenuBackdrop ? 0f : playerCar.GetInterpolatedAirHeight(_renderAlpha);
            Vector3 bikePos = new Vector3(0f, pGroundY + vibe + airH * _playerSlice.scale * 0.42f, -0.2f);
            _bikeRenderer.transform.position = bikePos;
            float baseRoll = isMultiPoseArt
                ? (playerCar.IsKicking ? -playerCar.KickDirection * 4f : -interpLean * 5f)
                : (playerCar.IsKicking ? -playerCar.KickDirection * 6f : -interpLean * 14f);
            float knockRoll = (!isMenuBackdrop && playerCar.IsCollisionKnockback)
                ? Mathf.Clamp(playerCar.AngularVelocity * 5f, -22f, 22f)
                : 0f;
            _bikeRenderer.transform.rotation = Quaternion.Euler(0f, 0f, baseRoll + knockRoll);
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
                float shadowScale = 1f / (1f + airH * 0.35f);
                _shadowRenderer.transform.position = new Vector3(interpLean * 0.10f * _playerSlice.scale, pGroundY - 0.02f * _playerSlice.scale, -0.1f);
                _shadowRenderer.transform.localScale = new Vector3(1.55f * pScale * 0.52f * shadowScale, 0.82f * pScale * 0.52f * shadowScale, 1f);
                _shadowRenderer.sortingOrder = playerOrder - 1;
            }
        }

        private void UpdateOpponentBikeVisuals(bool isMenuBackdrop)
        {
            float segLen = Mathf.Max(0.5f, sliceStep);
            float maxAheadDz = (_firstActiveSliceZ + (sliceCount - 1) * segLen) - playerCameraZ - segLen;
            float minBehindDz = Mathf.Min(-(playerCameraZ - 1.5f), _firstActiveSliceZ - playerCameraZ);
            float totalLen = track.TotalLength;
            float playerS = isMenuBackdrop ? _idleDemoScroll : playerCar.GetInterpolatedTrackDistance(_renderAlpha, track);
            int animFrame = Mathf.FloorToInt(_wheelAnimPhase);
            int slotIdx = 0;

            for (int i = 0; i < _allCars.Count; i++)
            {
                Car other = _allCars[i];
                if (other == null || other == playerCar) continue;

                float otherS = other.GetInterpolatedTrackDistance(_renderAlpha, track);
                float dz = otherS - playerS;
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

                        float oppLat = other.GetInterpolatedLateralOffset(_renderAlpha);
                        float oppLean = other.GetInterpolatedLeanAngle(_renderAlpha);
                        float screenX = ProjectLateralScreenX(slice, oppLat);
                        float spriteScale = slice.scale * bikeVisualScale;

                        int order = Mathf.Clamp(480 - Mathf.RoundToInt(oppZ * 2.5f), 20, 495);
                        float fog = SmoothFactor(0.45f, 1.0f, oppZ / (_firstActiveSliceZ + sliceCount * segLen)) * 0.72f;
                        Color horizonCol = EffectiveTheme != null ? EffectiveTheme.SkyHorizon : RoadRashVisualTheme.SkyHorizon;
                        Color fogTint = Color.Lerp(Color.white, horizonCol, fog);

                        bool isPolice = _gameManager != null && _gameManager.PoliceController != null &&
                                        other == _gameManager.PoliceController.PoliceCar;

                        BikeSpriteSet set;
                        BikeSkinData refSkin = (other.ActiveSkin != null && other.ActiveSkin.HasMultiPoseSprites)
                            ? other.ActiveSkin
                            : ResolveReferenceMultiPoseSkin();
                        if (refSkin != null)
                        {
                            BikePaletteKind palette = isPolice
                                ? BikePaletteKind.Police
                                : (other.ActiveSkin != null
                                    ? BikePaletteSwapper.ToPaletteKind(other.ActiveSkin.SkinId)
                                    : (BikePaletteKind)((i + 1) % 4));
                            set = BikePaletteSwapper.GetOrCreateSet(palette, refSkin);
                        }
                        else
                        {
                            set = RoadRashBikeSprites.GetOrCreateSet(other.BikeColor);
                        }

                        BikePose pose = ResolveBikePose(other, oppLean);
                        bool isMultiPoseArt = set != null && set.FlipKickRight;
                        slot.bike.sprite = RoadRashBikeSprites.SelectPoseSprite(set, pose, animFrame + i);
                        slot.bike.flipX = isMultiPoseArt && pose == BikePose.KickRight;
                        slot.bike.color = other.IsStaggered && (Mathf.FloorToInt(Time.time * 24f) % 2 == 0)
                            ? new Color(1f, 0.85f, 0.75f, 1f)
                            : fogTint;

                        float oppAirH = other.GetInterpolatedAirHeight(_renderAlpha);
                        Vector3 pos = new Vector3(screenX, slice.screenY + oppAirH * slice.scale * 0.42f, -0.15f);
                        slot.lastScreenPos = pos;
                        slot.bike.transform.position = pos;
                        float roll = other.IsStaggered
                            ? Mathf.Sin(Time.time * 36f) * 16f
                            : -oppLean * (isMultiPoseArt ? 5f : 14f);
                        if (other.IsCollisionKnockback)
                        {
                            roll = Mathf.Clamp(roll + other.AngularVelocity * 6f, -32f, 32f);
                        }
                        slot.bike.transform.rotation = Quaternion.Euler(0f, 0f, roll);
                        slot.bike.transform.localScale = Vector3.one * spriteScale;
                        slot.bike.sortingOrder = order;

                        if (slot.tailLight != null)
                        {
                            bool braking = other.LastInput.brake > 0.1f;
                            slot.tailLight.color = braking ? RoadRashVisualTheme.TailLightBrakeHdr : RoadRashVisualTheme.TailLightIdle;
                            slot.tailLight.sortingOrder = order + 1;
                        }

                        if (slot.sirenBeacon != null)
                        {
                            slot.sirenBeacon.gameObject.SetActive(isPolice);
                            if (isPolice)
                            {
                                slot.sirenBeacon.color = _gameManager.PoliceController.CurrentSirenColor;
                                slot.sirenBeacon.sortingOrder = order + 2;
                            }
                        }

                        float oppShadowScale = 1f / (1f + oppAirH * 0.35f);
                        slot.shadow.transform.position = new Vector3(screenX + oppLean * 0.10f * slice.scale, slice.screenY - 0.02f * slice.scale, -0.1f);
                        slot.shadow.transform.localScale = new Vector3(1.55f * spriteScale * 0.52f * oppShadowScale, 0.82f * spriteScale * 0.52f * oppShadowScale, 1f);
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

        private BikeSkinData ResolveReferenceMultiPoseSkin()
        {
            if (referenceMultiPoseSkin != null && referenceMultiPoseSkin.HasMultiPoseSprites)
            {
                return referenceMultiPoseSkin;
            }

            if (playerCar != null && playerCar.ActiveSkin != null && playerCar.ActiveSkin.HasMultiPoseSprites)
            {
                return playerCar.ActiveSkin;
            }

            if (_gameManager != null && _gameManager.SkinManager != null)
            {
                BikeSkinData xanhSm = _gameManager.SkinManager.GetSkin(DriverBrandSkin.XanhSM);
                if (xanhSm != null && xanhSm.HasMultiPoseSprites)
                {
                    return xanhSm;
                }
            }

            return null;
        }

        private static BikePose ResolveBikePose(Car car, float leanOverride)
        {
            if (car.IsKicking)
            {
                return car.KickDirection < 0 ? BikePose.KickLeft : BikePose.KickRight;
            }
            if (car.IsStaggered)
            {
                return BikePose.HitStagger;
            }

            if (leanOverride <= -0.58f) return BikePose.LeanLeftDeep;
            if (leanOverride <= -0.16f) return BikePose.LeanLeftLight;
            if (leanOverride >= 0.58f) return BikePose.LeanRightDeep;
            if (leanOverride >= 0.16f) return BikePose.LeanRightLight;
            return BikePose.Upright;
        }

        private static float WrappedDeltaDistance(float currentS, float previousS, float totalLen)
        {
            float delta = currentS - previousS;
            if (totalLen > 0.01f)
            {
                if (delta < -totalLen * 0.5f) delta += totalLen;
                else if (delta > totalLen * 0.5f) delta -= totalLen;
            }
            return delta;
        }

        private bool TrySampleAtCameraZ(float cameraZ, out SliceData result)
        {
            float fIdx = (cameraZ - _firstActiveSliceZ) / Mathf.Max(0.001f, sliceStep);
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

                GameObject sirenObj = new GameObject($"OpponentSiren_{_opponentPool.Count}");
                sirenObj.transform.SetParent(bikeObj.transform, false);
                sirenObj.transform.localPosition = new Vector3(0f, 1.48f, -0.03f);
                sirenObj.transform.localScale = new Vector3(0.65f, 0.65f, 1f);
                SpriteRenderer sirenSr = sirenObj.AddComponent<SpriteRenderer>();
                sirenSr.sprite = RoadRashPropSprites.GetPropSprite(RoadsidePropKind.PoliceSirenBeacon);
                sirenObj.SetActive(false);

                _opponentPool.Add(new OpponentSpriteSlot
                {
                    bike = bikeSr,
                    shadow = shadowSr,
                    tailLight = tailSr,
                    sirenBeacon = sirenSr
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
