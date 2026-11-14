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
        private const string VehicleStatsPath = DataFolder + "/DefaultVehicleStats.asset";
        private const string EasyProfilePath = AiFolder + "/AIProfile_Easy.asset";
        private const string NormalProfilePath = AiFolder + "/AIProfile_Normal.asset";
        private const string HardProfilePath = AiFolder + "/AIProfile_Hard.asset";

        [MenuItem("RacingGame/Create Default Data Assets")]
        public static void CreateDefaultDataAssets()
        {
            EnsureDefaultDataAssets(out _, out _, out _, out _);
        }

        public static void EnsureDefaultDataAssets(
            out VehicleStatsAsset vehicleStats,
            out AIProfile easyProfile,
            out AIProfile normalProfile,
            out AIProfile hardProfile)
        {
            EnsureFolderExists("Assets", "ScriptableObjects");
            EnsureFolderExists(DataFolder, "AI");

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

        private static void EnsureFolderExists(string parentFolder, string childFolderName)
        {
            string fullPath = $"{parentFolder}/{childFolderName}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parentFolder, childFolderName);
            }
        }

        [MenuItem("RacingGame/Setup Road Rash 2D Scene")]
        public static void SetupSampleScene()
        {
            EnsureDefaultDataAssets(
                out VehicleStatsAsset vehicleStats,
                out AIProfile easyProfile,
                out AIProfile normalProfile,
                out AIProfile hardProfile);

            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);

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

            SerializedObject carSo = new SerializedObject(playerCar);
            carSo.FindProperty("baseStatsAsset").objectReferenceValue = vehicleStats;
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
            RaceAudioController audioCtrl = gmObj.AddComponent<RaceAudioController>();

            SerializedObject gmSo = new SerializedObject(gm);
            gmSo.FindProperty("track").objectReferenceValue = track;
            gmSo.FindProperty("playerCar").objectReferenceValue = playerCar;
            gmSo.FindProperty("inputHandler").objectReferenceValue = inputHandler;
            gmSo.FindProperty("playerDriver").objectReferenceValue = playerDriver;
            gmSo.FindProperty("roadRenderer").objectReferenceValue = roadRenderer;
            gmSo.FindProperty("lapManager").objectReferenceValue = lapManager;
            gmSo.FindProperty("raceHud").objectReferenceValue = raceHud;
            gmSo.FindProperty("menuUi").objectReferenceValue = menuUi;
            gmSo.FindProperty("audioController").objectReferenceValue = audioCtrl;
            gmSo.FindProperty("defaultVehicleStats").objectReferenceValue = vehicleStats;
            gmSo.FindProperty("easyProfile").objectReferenceValue = easyProfile;
            gmSo.FindProperty("normalProfile").objectReferenceValue = normalProfile;
            gmSo.FindProperty("hardProfile").objectReferenceValue = hardProfile;
            gmSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[RoadRashSceneSetup] SampleScene configured with URP Post-Processing, Road Rash Graphics, and Kick Combat.");
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
