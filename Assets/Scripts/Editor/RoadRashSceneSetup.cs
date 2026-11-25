using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RacingGame.Editor
{
    public static class RoadRashSceneSetup
    {
        private const string DataFolder = "Assets/ScriptableObjects";
        private const string AiFolder = "Assets/ScriptableObjects/AI";
        private const string SkinsFolder = "Assets/ScriptableObjects/Skins";
        private const string MapsFolder = "Assets/ScriptableObjects/Maps";
        private const string VehicleStatsPath = DataFolder + "/DefaultVehicleStats.asset";
        private const string EasyProfilePath = AiFolder + "/AIProfile_Easy.asset";
        private const string NormalProfilePath = AiFolder + "/AIProfile_Normal.asset";
        private const string HardProfilePath = AiFolder + "/AIProfile_Hard.asset";

        private const string SkinXanhSmPath = SkinsFolder + "/Skin_XanhSM.asset";
        private const string SkinShopeeFoodPath = SkinsFolder + "/Skin_ShopeeFood.asset";
        private const string SkinGrabPath = SkinsFolder + "/Skin_Grab.asset";
        private const string SkinAhamovePath = SkinsFolder + "/Skin_Ahamove.asset";

        private const string MapThemeValleyPath = MapsFolder + "/MapTheme_Valley.asset";
        private const string MapThemeCityPath = MapsFolder + "/MapTheme_City.asset";
        private const string ThemeCityMorningPath = MapsFolder + "/Theme_CityMorning.asset";
        private const string ThemeCoastalHighwayPath = MapsFolder + "/Theme_CoastalHighway.asset";
        private const string ThemeNeonNightPath = MapsFolder + "/Theme_NeonNightExpress.asset";

        private const string MapCityMorningPath = MapsFolder + "/Map_01_CityMorning.asset";
        private const string MapCoastalHighwayPath = MapsFolder + "/Map_02_CoastalHighway.asset";
        private const string MapNeonNightPath = MapsFolder + "/Map_03_NeonNightExpress.asset";
        private const string MapCityDistrictPath = MapsFolder + "/Map_04_CityNightDistrict.asset";

        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        private static MapTheme _cachedValleyTheme;

        [MenuItem("RacingGame/Create Default Data Assets")]
        public static void CreateDefaultDataAssets()
        {
            EnsureDefaultDataAssets(out _, out _, out _, out _, out _, out _);
        }

        public static void EnsureDefaultDataAssets(
            out VehicleStatsAsset vehicleStats,
            out AIProfile easyProfile,
            out AIProfile normalProfile,
            out AIProfile hardProfile)
        {
            EnsureDefaultDataAssets(
                out vehicleStats,
                out easyProfile,
                out normalProfile,
                out hardProfile,
                out _,
                out _);
        }

        public static void EnsureDefaultDataAssets(
            out VehicleStatsAsset vehicleStats,
            out AIProfile easyProfile,
            out AIProfile normalProfile,
            out AIProfile hardProfile,
            out BikeSkinData[] skinAssets)
        {
            EnsureDefaultDataAssets(
                out vehicleStats,
                out easyProfile,
                out normalProfile,
                out hardProfile,
                out skinAssets,
                out _);
        }

        public static void EnsureDefaultDataAssets(
            out VehicleStatsAsset vehicleStats,
            out AIProfile easyProfile,
            out AIProfile normalProfile,
            out AIProfile hardProfile,
            out BikeSkinData[] skinAssets,
            out TrackDefinition[] mapAssets)
        {
            EnsureFolderExists("Assets", "ScriptableObjects");
            EnsureFolderExists(DataFolder, "AI");
            EnsureFolderExists(DataFolder, "Skins");
            EnsureFolderExists(DataFolder, "Maps");

            vehicleStats = AssetDatabase.LoadAssetAtPath<VehicleStatsAsset>(VehicleStatsPath);
            if (vehicleStats == null)
            {
                vehicleStats = ScriptableObject.CreateInstance<VehicleStatsAsset>();
                vehicleStats.SetStats(VehicleStats.Default);
                AssetDatabase.CreateAsset(vehicleStats, VehicleStatsPath);
            }

            easyProfile = LoadOrCreateAiProfile(EasyProfilePath, DifficultyLevel.Easy);
            normalProfile = LoadOrCreateAiProfile(NormalProfilePath, DifficultyLevel.Normal);
            hardProfile = LoadOrCreateAiProfile(HardProfilePath, DifficultyLevel.Hard);

            skinAssets = new[]
            {
                LoadOrCreateSkinAsset(SkinXanhSmPath, DriverBrandSkin.XanhSM),
                LoadOrCreateSkinAsset(SkinShopeeFoodPath, DriverBrandSkin.ShopeeFood),
                LoadOrCreateSkinAsset(SkinGrabPath, DriverBrandSkin.Grab),
                LoadOrCreateSkinAsset(SkinAhamovePath, DriverBrandSkin.Ahamove)
            };

            MapTheme valleyTheme = LoadOrCreateThemeAsset(MapThemeValleyPath, MapTheme.CreateValleyPreset);
            MapTheme cityDistrictTheme = LoadOrCreateThemeAsset(MapThemeCityPath, MapTheme.CreateCityDistrictPreset);
            MapTheme cityTheme = LoadOrCreateThemeAsset(ThemeCityMorningPath, MapTheme.CreateCityMorningPreset);
            MapTheme coastalTheme = LoadOrCreateThemeAsset(ThemeCoastalHighwayPath, MapTheme.CreateCoastalHighwayPreset);
            MapTheme neonTheme = LoadOrCreateThemeAsset(ThemeNeonNightPath, MapTheme.CreateNeonNightExpressPreset);
            _cachedValleyTheme = valleyTheme;

            mapAssets = new[]
            {
                LoadOrCreateMapAsset(MapCityMorningPath, () => TrackDefinition.CreateCityMorningPreset(cityTheme)),
                LoadOrCreateMapAsset(MapCoastalHighwayPath, () => TrackDefinition.CreateCoastalHighwayPreset(coastalTheme)),
                LoadOrCreateMapAsset(MapNeonNightPath, () => TrackDefinition.CreateNeonNightExpressPreset(neonTheme)),
                LoadOrCreateMapAsset(MapCityDistrictPath, () => TrackDefinition.CreateCityDistrictPreset(cityDistrictTheme))
            };

            Stage2ArtWiringEditor.WireDataAssetsOnly();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static AIProfile LoadOrCreateAiProfile(string assetPath, DifficultyLevel level)
        {
            AIProfile profile = AssetDatabase.LoadAssetAtPath<AIProfile>(assetPath);
            if (profile != null) return profile;

            profile = AIProfile.CreateDefault(level);
            profile.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(profile, assetPath);
            return profile;
        }

        private static BikeSkinData LoadOrCreateSkinAsset(string assetPath, DriverBrandSkin brand)
        {
            BikeSkinData skin = AssetDatabase.LoadAssetAtPath<BikeSkinData>(assetPath);
            if (skin != null) return skin;

            skin = BikeSkinData.CreateDefault(brand);
            skin.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(skin, assetPath);
            return skin;
        }

        private static MapTheme LoadOrCreateThemeAsset(string assetPath, System.Func<MapTheme> factory)
        {
            MapTheme theme = AssetDatabase.LoadAssetAtPath<MapTheme>(assetPath);
            if (theme != null) return theme;

            theme = factory();
            theme.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(theme, assetPath);
            return theme;
        }

        private static TrackDefinition LoadOrCreateMapAsset(string assetPath, System.Func<TrackDefinition> factory)
        {
            TrackDefinition map = AssetDatabase.LoadAssetAtPath<TrackDefinition>(assetPath);
            if (map != null) return map;

            map = factory();
            map.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(map, assetPath);
            return map;
        }

        private static void EnsureFolderExists(string parentFolder, string childFolderName)
        {
            string fullPath = $"{parentFolder}/{childFolderName}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parentFolder, childFolderName);
            }
        }

        [MenuItem("RacingGame/Setup All Scenes & Skin Assets")]
        public static void SetupAllScenesAndAssets()
        {
            SetupLobbyScene();
            SetupSampleScene();
            RegisterScenesInBuildSettings();
        }

        [MenuItem("RacingGame/Setup Startup Lobby Scene")]
        public static void SetupLobbyScene()
        {
            EnsureDefaultDataAssets(
                out VehicleStatsAsset vehicleStats,
                out _,
                out _,
                out _,
                out BikeSkinData[] skinAssets,
                out TrackDefinition[] mapAssets);

            EnsureFolderExists("Assets", "Scenes");
            Scene lobbyScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera mainCam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            mainCam.orthographic = true;
            mainCam.orthographicSize = 5f;
            mainCam.transform.position = new Vector3(0f, 0f, -10f);

            UniversalAdditionalCameraData camData = camObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.volumeLayerMask = ~0;

            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
            if (profile != null)
            {
                GameObject volObj = new GameObject("Global PostProcess Volume");
                Volume vol = volObj.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.sharedProfile = profile;
            }

            // Tạo đường đua nền + xe mẫu người chơi hiển thị trực tiếp skin & map đang chọn ở sảnh chờ
            GameObject trackObj = new GameObject("WaypointTrack");
            WaypointTrack track = trackObj.AddComponent<WaypointTrack>();
            if (mapAssets != null && mapAssets.Length > 0)
            {
                track.ApplyTrackDefinition(mapAssets[0]);
            }
            else
            {
                track.BuildDefaultCircuit();
            }

            GameObject carObj = new GameObject("PlayerCar");
            Car playerCar = carObj.AddComponent<Car>();
            playerCar.ConfigureIdentity(true, RoadRashVisualTheme.PlayerJacketBlue);
            playerCar.SetBaseStatsAsset(vehicleStats);
            if (skinAssets != null && skinAssets.Length > 0)
            {
                SerializedObject lobbyCarSo = new SerializedObject(playerCar);
                lobbyCarSo.FindProperty("activeSkin").objectReferenceValue = skinAssets[0];
                lobbyCarSo.ApplyModifiedPropertiesWithoutUndo();
                playerCar.ApplySkin(skinAssets[0]);
            }

            GameObject rendererObj = new GameObject("RoadRashRenderer2D");
            RoadRashRenderer2D roadRenderer = rendererObj.AddComponent<RoadRashRenderer2D>();
            rendererObj.AddComponent<RaceVfxController>();
            EnsureCrtControllerConfigured(rendererObj);
            AssignDefaultThemeSerialized(roadRenderer, _cachedValleyTheme);
            Stage2ArtWiringEditor.WireSceneObjects(roadRenderer, null, skinAssets != null && skinAssets.Length > 0 ? skinAssets[0] : null);
            roadRenderer.SetSceneReferences(mainCam, track, playerCar, new[] { playerCar });

            GameObject lobbyObj = new GameObject("LobbyController");
            SkinManager skinManager = lobbyObj.AddComponent<SkinManager>();
            AssignSkinAssetsSerialized(skinManager, skinAssets);
            MapManager mapManager = lobbyObj.AddComponent<MapManager>();
            AssignMapAssetsSerialized(mapManager, mapAssets);
            EnvironmentDecorator envDecorator = lobbyObj.AddComponent<EnvironmentDecorator>();
            if (mapAssets != null && mapAssets.Length > 0)
            {
                envDecorator.RebuildEnvironment(track, mapAssets[0].Theme);
            }

            GameMenuUI menuUi = lobbyObj.AddComponent<GameMenuUI>();
            SerializedObject menuSo = new SerializedObject(menuUi);
            menuSo.FindProperty("isStandaloneLobbyScene").boolValue = true;
            menuSo.FindProperty("gameplaySceneName").stringValue = "SampleScene";
            menuSo.ApplyModifiedPropertiesWithoutUndo();
            EnsureLobbyBackgroundConfigured(menuUi);

            EditorSceneManager.SaveScene(lobbyScene, LobbyScenePath);
            RegisterScenesInBuildSettings();
            Debug.Log("[RoadRashSceneSetup] LobbyScene created and configured at " + LobbyScenePath);
        }

        [MenuItem("RacingGame/Setup Road Rash 2D Scene")]
        public static void SetupSampleScene()
        {
            EnsureDefaultDataAssets(
                out VehicleStatsAsset vehicleStats,
                out AIProfile easyProfile,
                out AIProfile normalProfile,
                out AIProfile hardProfile,
                out BikeSkinData[] skinAssets,
                out TrackDefinition[] mapAssets);

            Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                mainCam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }
            mainCam.orthographic = true;
            mainCam.orthographicSize = 5f;
            mainCam.transform.position = new Vector3(0f, 0f, -10f);

            UniversalAdditionalCameraData camData = mainCam.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null) camData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.volumeLayerMask = ~0;

            DestroyIfExists("GameManager");
            DestroyIfExists("WaypointTrack");
            DestroyIfExists("PlayerCar");
            DestroyIfExists("RoadRashRenderer2D");
            DestroyIfExists("Global PostProcess Volume");
            DestroyIfExists("EventSystem");

            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
            GameObject volObj = new GameObject("Global PostProcess Volume");
            Volume vol = volObj.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            GameObject trackObj = new GameObject("WaypointTrack");
            WaypointTrack track = trackObj.AddComponent<WaypointTrack>();
            if (mapAssets != null && mapAssets.Length > 0)
            {
                track.ApplyTrackDefinition(mapAssets[0]);
            }
            else
            {
                track.BuildDefaultCircuit();
            }

            GameObject carObj = new GameObject("PlayerCar");
            Car playerCar = carObj.AddComponent<Car>();
            playerCar.ConfigureIdentity(true, RoadRashVisualTheme.PlayerJacketBlue);
            playerCar.SetBaseStatsAsset(vehicleStats);
            if (skinAssets != null && skinAssets.Length > 0)
            {
                SerializedObject lobbyCarSo = new SerializedObject(playerCar);
                lobbyCarSo.FindProperty("activeSkin").objectReferenceValue = skinAssets[0];
                lobbyCarSo.ApplyModifiedPropertiesWithoutUndo();
                playerCar.ApplySkin(skinAssets[0]);
            }

            SerializedObject carSo = new SerializedObject(playerCar);
            carSo.FindProperty("baseStatsAsset").objectReferenceValue = vehicleStats;
            if (skinAssets != null && skinAssets.Length > 0)
            {
                carSo.FindProperty("activeSkin").objectReferenceValue = skinAssets[0];
            }
            carSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject rendererObj = new GameObject("RoadRashRenderer2D");
            RoadRashRenderer2D roadRenderer = rendererObj.AddComponent<RoadRashRenderer2D>();
            rendererObj.AddComponent<RaceVfxController>();
            EnsureCrtControllerConfigured(rendererObj);
            AssignDefaultThemeSerialized(roadRenderer, _cachedValleyTheme);
            Stage2ArtWiringEditor.WireSceneObjects(roadRenderer, null, skinAssets != null && skinAssets.Length > 0 ? skinAssets[0] : null);
            roadRenderer.SetSceneReferences(mainCam, track, playerCar, new[] { playerCar });

            GameObject gmObj = new GameObject("GameManager");
            InputHandler inputHandler = gmObj.AddComponent<InputHandler>();
            PlayerDriver playerDriver = gmObj.AddComponent<PlayerDriver>();
            playerDriver.SetDriverName("Player");
            playerDriver.SetInputHandler(inputHandler);

            GameManager gm = gmObj.AddComponent<GameManager>();
            LapCheckpointManager lapManager = gmObj.AddComponent<LapCheckpointManager>();
            RaceHUD raceHud = gmObj.AddComponent<RaceHUD>();
            GameMenuUI menuUi = gmObj.AddComponent<GameMenuUI>();
            PauseMenuUI pauseMenuUi = gmObj.AddComponent<PauseMenuUI>();
            RaceAudioController audioCtrl = gmObj.AddComponent<RaceAudioController>();
            SkinManager skinManager = gmObj.AddComponent<SkinManager>();
            MapManager mapManager = gmObj.AddComponent<MapManager>();
            EnvironmentDecorator envDecorator = gmObj.AddComponent<EnvironmentDecorator>();
            TrackObstacleSystem obstacleSystem = gmObj.AddComponent<TrackObstacleSystem>();
            PoliceChaseController policeCtrl = gmObj.AddComponent<PoliceChaseController>();
            AssignSkinAssetsSerialized(skinManager, skinAssets);
            AssignMapAssetsSerialized(mapManager, mapAssets);
            Stage2ArtWiringEditor.WireSceneObjects(
                roadRenderer,
                obstacleSystem,
                skinAssets != null && skinAssets.Length > 0 ? skinAssets[0] : null);

            SerializedObject gmSo = new SerializedObject(gm);
            gmSo.FindProperty("track").objectReferenceValue = track;
            gmSo.FindProperty("playerCar").objectReferenceValue = playerCar;
            gmSo.FindProperty("inputHandler").objectReferenceValue = inputHandler;
            gmSo.FindProperty("playerDriver").objectReferenceValue = playerDriver;
            gmSo.FindProperty("roadRenderer").objectReferenceValue = roadRenderer;
            gmSo.FindProperty("lapManager").objectReferenceValue = lapManager;
            gmSo.FindProperty("raceHud").objectReferenceValue = raceHud;
            gmSo.FindProperty("menuUi").objectReferenceValue = menuUi;
            gmSo.FindProperty("pauseMenuUi").objectReferenceValue = pauseMenuUi;
            gmSo.FindProperty("audioController").objectReferenceValue = audioCtrl;
            gmSo.FindProperty("skinManager").objectReferenceValue = skinManager;
            gmSo.FindProperty("mapManager").objectReferenceValue = mapManager;
            gmSo.FindProperty("environmentDecorator").objectReferenceValue = envDecorator;
            gmSo.FindProperty("obstacleSystem").objectReferenceValue = obstacleSystem;
            gmSo.FindProperty("policeController").objectReferenceValue = policeCtrl;
            gmSo.FindProperty("defaultVehicleStats").objectReferenceValue = vehicleStats;
            gmSo.FindProperty("easyProfile").objectReferenceValue = easyProfile;
            gmSo.FindProperty("normalProfile").objectReferenceValue = normalProfile;
            gmSo.FindProperty("hardProfile").objectReferenceValue = hardProfile;
            gmSo.ApplyModifiedPropertiesWithoutUndo();
            EnsureLobbyBackgroundConfigured(menuUi);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RegisterScenesInBuildSettings();
            Debug.Log("[RoadRashSceneSetup] SampleScene configured with SkinManager (Xanh SM, Shopee Food, Grab, Ahamove) and Lobby UI.");
        }

        private static void AssignSkinAssetsSerialized(SkinManager skinManager, BikeSkinData[] skinAssets)
        {
            if (skinManager == null || skinAssets == null) return;
            SerializedObject skinSo = new SerializedObject(skinManager);
            SerializedProperty arrProp = skinSo.FindProperty("skinAssets");
            arrProp.arraySize = skinAssets.Length;
            for (int i = 0; i < skinAssets.Length; i++)
            {
                arrProp.GetArrayElementAtIndex(i).objectReferenceValue = skinAssets[i];
            }
            skinSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignDefaultThemeSerialized(RoadRashRenderer2D renderer, MapTheme defaultTheme)
        {
            if (renderer == null || defaultTheme == null) return;
            SerializedObject rendererSo = new SerializedObject(renderer);
            rendererSo.FindProperty("defaultTheme").objectReferenceValue = defaultTheme;
            rendererSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignMapAssetsSerialized(MapManager mapManager, TrackDefinition[] mapAssets)
        {
            if (mapManager == null || mapAssets == null) return;
            SerializedObject mapSo = new SerializedObject(mapManager);
            SerializedProperty arrProp = mapSo.FindProperty("mapAssets");
            arrProp.arraySize = mapAssets.Length;
            for (int i = 0; i < mapAssets.Length; i++)
            {
                arrProp.GetArrayElementAtIndex(i).objectReferenceValue = mapAssets[i];
            }
            mapSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterScenesInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            if (File.Exists(LobbyScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(LobbyScenePath, true));
            }
            if (File.Exists(SampleScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(SampleScenePath, true));
            }
            if (scenes.Count > 0)
            {
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static void DestroyIfExists(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        [MenuItem("Tools/Road Rash/Wire Stage 3 VFX & CRT Overlay")]
        public static void WireStage3VfxInScenes()
        {
            WireStage3InScene(LobbyScenePath);
            WireStage3InScene(SampleScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RoadRashSceneSetup] Wired CrtScanlineController into LobbyScene and SampleScene.");
        }

        private static void WireStage3InScene(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[RoadRashSceneSetup] Scene not found: {scenePath}");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            RoadRashRenderer2D renderer = Object.FindAnyObjectByType<RoadRashRenderer2D>();
            if (renderer != null)
            {
                EnsureCrtControllerConfigured(renderer.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureCrtControllerConfigured(GameObject targetObj)
        {
            if (targetObj == null) return;
            CrtScanlineController crt = targetObj.GetComponent<CrtScanlineController>();
            if (crt == null)
            {
                crt = targetObj.AddComponent<CrtScanlineController>();
            }

            Shader crtShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/CrtScanlineOverlay.shader");
            if (crtShader != null)
            {
                SerializedObject so = new SerializedObject(crt);
                so.FindProperty("crtShader").objectReferenceValue = crtShader;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(crt);
            }
        }
        [MenuItem("Tools/Road Rash/Wire Stage 4A Lobby UI & TMP Essentials")]
        public static void WireStage4ALobbyAndTmp()
        {
            ImportTmpEssentialsIfMissing();
            WireLobbyBackgroundInScene(LobbyScenePath);
            WireLobbyBackgroundInScene(SampleScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RoadRashSceneSetup] Wired Stage 4A Lobby UI background & TMP Essential Resources.");
        }

        private static void ImportTmpEssentialsIfMissing()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                return;
            }

            string pkgCacheDir = Path.GetFullPath("Library/PackageCache");
            if (!Directory.Exists(pkgCacheDir))
            {
                Debug.LogWarning("[RoadRashSceneSetup] Library/PackageCache not found; skipping TMP Essentials import.");
                return;
            }

            string[] packages = Directory.GetFiles(pkgCacheDir, "TMP Essential Resources.unitypackage", SearchOption.AllDirectories);
            if (packages.Length == 0)
            {
                Debug.LogWarning("[RoadRashSceneSetup] TMP Essential Resources.unitypackage not found in PackageCache.");
                return;
            }

            UnityEditor.AssetPackage.Package.Import(packages[0], false);
            AssetDatabase.Refresh();
        }

        private static void WireLobbyBackgroundInScene(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[RoadRashSceneSetup] Scene not found: {scenePath}");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameMenuUI menuUi = Object.FindAnyObjectByType<GameMenuUI>();
            if (menuUi != null)
            {
                EnsureLobbyBackgroundConfigured(menuUi);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("RacingGame/Build Windows Standalone")]
        public static void BuildWindowsRelease()
        {
            RegisterScenesInBuildSettings();

            var enabledScenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene != null && scene.enabled && File.Exists(scene.path))
                {
                    enabledScenes.Add(scene.path);
                }
            }

            if (enabledScenes.Count == 0)
            {
                throw new System.InvalidOperationException("[RoadRashSceneSetup] No enabled scenes found in EditorBuildSettings for Windows build.");
            }

            string buildDir = Path.GetFullPath("Builds/RoadRash2D");
            Directory.CreateDirectory(buildDir);
            string exePath = Path.Combine(buildDir, "RoadRash2D.exe");

            var options = new BuildPlayerOptions
            {
                scenes = enabledScenes.ToArray(),
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception($"[RoadRashSceneSetup] Windows Standalone build failed: {summary.result} (errors: {summary.totalErrors}).");
            }

            Debug.Log($"[RoadRashSceneSetup] Windows Standalone build succeeded: {exePath} ({summary.totalSize / (1024f * 1024f):F2} MB)");
        }

        private static void EnsureLobbyBackgroundConfigured(GameMenuUI menuUi)
        {
            if (menuUi == null) return;
            Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Bg_Lobby_SaigonNight.png");
            if (bgSprite == null) return;

            SerializedObject so = new SerializedObject(menuUi);
            SerializedProperty prop = so.FindProperty("lobbyBackgroundSprite");
            if (prop != null)
            {
                prop.objectReferenceValue = bgSprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(menuUi);
            }
        }
    }
}
