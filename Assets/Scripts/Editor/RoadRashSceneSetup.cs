using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RacingGame.Editor
{
    public static class RoadRashSceneSetup
    {
        [MenuItem("RacingGame/Setup Phase 1-2 Scene")]
        public static void SetupSampleScene()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);

            // Ensure Main Camera exists and is configured for 2D
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

            // Clean up any previous RacingGame root objects to keep setup idempotent
            DestroyIfExists("GameManager");
            DestroyIfExists("WaypointTrack");
            DestroyIfExists("PlayerCar");
            DestroyIfExists("RoadRashRenderer2D");

            // Create WaypointTrack
            GameObject trackObj = new GameObject("WaypointTrack");
            WaypointTrack track = trackObj.AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();

            // Create PlayerCar
            GameObject carObj = new GameObject("PlayerCar");
            Car playerCar = carObj.AddComponent<Car>();
            playerCar.isPlayer = true;

            // Create RoadRashRenderer2D
            GameObject rendererObj = new GameObject("RoadRashRenderer2D");
            RoadRashRenderer2D roadRenderer = rendererObj.AddComponent<RoadRashRenderer2D>();
            roadRenderer.targetCamera = mainCam;
            roadRenderer.track = track;
            roadRenderer.playerCar = playerCar;

            // Create GameManager + InputHandler
            GameObject gmObj = new GameObject("GameManager");
            InputHandler inputHandler = gmObj.AddComponent<InputHandler>();
            GameManager gm = gmObj.AddComponent<GameManager>();
            gm.track = track;
            gm.playerCar = playerCar;
            gm.inputHandler = inputHandler;
            gm.roadRenderer = roadRenderer;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[RoadRashSceneSetup] SampleScene configured for Phase 1 & 2.");
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
