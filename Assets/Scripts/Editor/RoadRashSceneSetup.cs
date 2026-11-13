using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RacingGame.Editor
{
    public static class RoadRashSceneSetup
    {
        [MenuItem("RacingGame/Setup Road Rash 2D Scene")]
        public static void SetupSampleScene()
        {
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

            DestroyIfExists("GameManager");
            DestroyIfExists("WaypointTrack");
            DestroyIfExists("PlayerCar");
            DestroyIfExists("RoadRashRenderer2D");

            GameObject trackObj = new GameObject("WaypointTrack");
            WaypointTrack track = trackObj.AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();

            GameObject carObj = new GameObject("PlayerCar");
            Car playerCar = carObj.AddComponent<Car>();
            playerCar.ConfigureIdentity(true, new Color(0.95f, 0.24f, 0.24f));

            GameObject rendererObj = new GameObject("RoadRashRenderer2D");
            RoadRashRenderer2D roadRenderer = rendererObj.AddComponent<RoadRashRenderer2D>();
            roadRenderer.SetSceneReferences(mainCam, track, playerCar, new[] { playerCar });

            GameObject gmObj = new GameObject("GameManager");
            InputHandler inputHandler = gmObj.AddComponent<InputHandler>();
            PlayerDriver playerDriver = gmObj.AddComponent<PlayerDriver>();
            playerDriver.SetDriverName("Player");
            playerDriver.SetInputHandler(inputHandler);

            GameManager gm = gmObj.AddComponent<GameManager>();
            LapCheckpointManager lapManager = gmObj.AddComponent<LapCheckpointManager>();
            RaceHUD raceHud = gmObj.AddComponent<RaceHUD>();
            SerializedObject gmSo = new SerializedObject(gm);
            gmSo.FindProperty("track").objectReferenceValue = track;
            gmSo.FindProperty("playerCar").objectReferenceValue = playerCar;
            gmSo.FindProperty("inputHandler").objectReferenceValue = inputHandler;
            gmSo.FindProperty("playerDriver").objectReferenceValue = playerDriver;
            gmSo.FindProperty("roadRenderer").objectReferenceValue = roadRenderer;
            gmSo.FindProperty("lapManager").objectReferenceValue = lapManager;
            gmSo.FindProperty("raceHud").objectReferenceValue = raceHud;
            gmSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[RoadRashSceneSetup] SampleScene configured for Loop 5 (LapCheckpointManager + RaceHUD).");
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
