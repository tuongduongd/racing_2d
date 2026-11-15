using System.Collections.Generic;
using System.IO;
using UnityEditor;
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
        private const string VehicleStatsPath = DataFolder + "/DefaultVehicleStats.asset";
        private const string EasyProfilePath = AiFolder + "/AIProfile_Easy.asset";
        private const string NormalProfilePath = AiFolder + "/AIProfile_Normal.asset";
        private const string HardProfilePath = AiFolder + "/AIProfile_Hard.asset";

        private const string SkinXanhSmPath = SkinsFolder + "/Skin_XanhSM.asset";
        private const string SkinShopeeFoodPath = SkinsFolder + "/Skin_ShopeeFood.asset";
        private const string SkinGrabPath = SkinsFolder + "/Skin_Grab.asset";
        private const string SkinAhamovePath = SkinsFolder + "/Skin_Ahamove.asset";

        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("RacingGame/Create Default Data Assets")]
        public static void CreateDefaultDataAssets()
        {
            EnsureDefaultDataAssets(out _, out _, out _, out _, out _);
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
                out _);
        }

        public static void EnsureDefaultDataAssets(
            out VehicleStatsAsset vehicleStats,
            out AIProfile easyProfile,
            out AIProfile normalProfile,
            out AIProfile hardProfile,
            out BikeSkinData[] skinAssets)
        {
            EnsureFolderExists("Assets", "ScriptableObjects");
            EnsureFolderExists(DataFolder, "AI");
            EnsureFolderExists(DataFolder, "Skins");

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
                out BikeSkinData[] skinAssets);

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

            // Tạo đường đua nền + xe mẫu người chơi hiển thị trực tiếp skin đang chọn ở sảnh chờ
            GameObject trackObj = new GameObject("WaypointTrack");
            WaypointTrack track = trackObj.AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();

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
            roadRenderer.SetSceneReferences(mainCam, track, playerCar, new[] { playerCar });

            GameObject lobbyObj = new GameObject("LobbyController");
            SkinManager skinManager = lobbyObj.AddComponent<SkinManager>();
            AssignSkinAssetsSerialized(skinManager, skinAssets);

            GameMenuUI menuUi = lobbyObj.AddComponent<GameMenuUI>();
            SerializedObject menuSo = new SerializedObject(menuUi);
            menuSo.FindProperty("isStandaloneLobbyScene").boolValue = true;
            menuSo.FindProperty("gameplaySceneName").stringValue = "SampleScene";
            menuSo.ApplyModifiedPropertiesWithoutUndo();

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
                out BikeSkinData[] skinAssets);

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
            track.BuildDefaultCircuit();

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
            EnvironmentDecorator envDecorator = gmObj.AddComponent<EnvironmentDecorator>();
            TrackObstacleSystem obstacleSystem = gmObj.AddComponent<TrackObstacleSystem>();
            PoliceChaseController policeCtrl = gmObj.AddComponent<PoliceChaseController>();
            AssignSkinAssetsSerialized(skinManager, skinAssets);

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
            gmSo.FindProperty("environmentDecorator").objectReferenceValue = envDecorator;
            gmSo.FindProperty("obstacleSystem").objectReferenceValue = obstacleSystem;
            gmSo.FindProperty("policeController").objectReferenceValue = policeCtrl;
            gmSo.FindProperty("defaultVehicleStats").objectReferenceValue = vehicleStats;
            gmSo.FindProperty("easyProfile").objectReferenceValue = easyProfile;
            gmSo.FindProperty("normalProfile").objectReferenceValue = normalProfile;
            gmSo.FindProperty("hardProfile").objectReferenceValue = hardProfile;
            gmSo.ApplyModifiedPropertiesWithoutUndo();

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
    }
}
