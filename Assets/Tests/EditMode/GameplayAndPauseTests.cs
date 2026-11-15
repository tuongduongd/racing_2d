using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class GameplayAndPauseTests
    {
        [Test]
        public void EnvironmentDecorator_GeneratesZonePropsAlongCircuit()
        {
            var trackGo = new GameObject("TestTrack");
            var envGo = new GameObject("TestEnvironment");
            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                var decorator = envGo.AddComponent<EnvironmentDecorator>();
                decorator.RebuildEnvironment(track, 1995);

                Assert.Greater(decorator.Decorations.Count, 20, "EnvironmentDecorator should populate roadside props along the circuit.");

                bool hasStreetLamp = false;
                bool hasPalmOrBillboard = false;
                for (int i = 0; i < decorator.Decorations.Count; i++)
                {
                    RoadsidePropKind kind = decorator.Decorations[i].Kind;
                    if (kind == RoadsidePropKind.StreetLamp) hasStreetLamp = true;
                    if (kind == RoadsidePropKind.PalmTree || kind == RoadsidePropKind.Billboard) hasPalmOrBillboard = true;

                    Sprite spr = RoadRashPropSprites.GetPropSprite(kind);
                    Assert.IsNotNull(spr, $"Sprite for {kind} must not be null.");
                }

                Assert.IsTrue(hasStreetLamp, "Environment should include urban StreetLamp props.");
                Assert.IsTrue(hasPalmOrBillboard, "Environment should include PalmTree or Billboard props.");
            }
            finally
            {
                Object.DestroyImmediate(envGo);
                Object.DestroyImmediate(trackGo);
            }
        }

        [Test]
        public void TrackObstacleSystem_SpawnsObstaclesAndAppliesCollisionPenalty()
        {
            var trackGo = new GameObject("TestTrack");
            var obsGo = new GameObject("TestObstacles");
            var carGo = new GameObject("TestCar");
            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                var obstacleSystem = obsGo.AddComponent<TrackObstacleSystem>();
                obstacleSystem.SpawnObstacles(track, 2026);
                Assert.Greater(obstacleSystem.Obstacles.Count, 0, "TrackObstacleSystem should spawn random obstacles.");

                var car = carGo.AddComponent<Car>();
                var driver = carGo.AddComponent<AIRacerDriver>();
                Vector2 wp0 = track.GetWaypoint(0);
                Vector2 dir0 = track.GetTrackDirection(0);
                float heading = Mathf.Atan2(dir0.y, dir0.x);
                car.Initialize(driver, wp0, heading, track);

                // Step car forward to build speed
                for (int i = 0; i < 30; i++)
                {
                    car.StepPhysics(0.02f, track);
                }
                float speedBeforeHit = car.Physics.Speed;
                Assert.Greater(speedBeforeHit, 5f);

                // Place a manual obstacle directly at the car's current track position
                TrackObstacle manualObs = obstacleSystem.AddManualObstacle(
                    ObstacleKind.OilBarrel,
                    car.TrackDistance,
                    car.LateralOffset,
                    2.5f);

                Car hitCar = null;
                TrackObstacle hitObstacle = null;
                obstacleSystem.ObstacleHit += (c, o) =>
                {
                    hitCar = c;
                    hitObstacle = o;
                };

                obstacleSystem.StepObstacles(track, new[] { car }, 0.02f);

                Assert.AreEqual(car, hitCar, "ObstacleHit event should fire for the colliding car.");
                Assert.AreEqual(manualObs, hitObstacle, "ObstacleHit event should report the collided obstacle.");
                Assert.Less(car.Physics.Speed, speedBeforeHit, "Colliding with an obstacle must reduce the car's speed.");
                Assert.IsTrue(manualObs.IsOnCooldown, "Collided obstacle should enter respawn cooldown.");
            }
            finally
            {
                Object.DestroyImmediate(carGo);
                Object.DestroyImmediate(obsGo);
                Object.DestroyImmediate(trackGo);
            }
        }

        [Test]
        public void PoliceChaseController_SpawnsPoliceRacerAndPursuesPlayer()
        {
            var trackGo = new GameObject("TestTrack");
            var playerGo = new GameObject("TestPlayer");
            var policeCtrlGo = new GameObject("TestPoliceController");
            Car spawnedPoliceCar = null;
            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                var playerCar = playerGo.AddComponent<Car>();
                var playerDriver = playerGo.AddComponent<PlayerDriver>();
                Vector2 wp0 = track.GetWaypoint(0);
                Vector2 dir0 = track.GetTrackDirection(0);
                float heading = Mathf.Atan2(dir0.y, dir0.x);
                playerCar.Initialize(playerDriver, wp0, heading, track);

                var policeCtrl = policeCtrlGo.AddComponent<PoliceChaseController>();
                spawnedPoliceCar = policeCtrl.EnsurePoliceUnit(playerCar, track);
                Assert.IsNotNull(spawnedPoliceCar, "EnsurePoliceUnit must create a Police Car.");
                Assert.IsNotNull(policeCtrl.PoliceDriver, "EnsurePoliceUnit must attach a PoliceRacerDriver.");
                Assert.IsTrue(policeCtrl.PoliceDriver is Driver, "PoliceRacerDriver must inherit from Driver.");

                policeCtrl.ResetPoliceOnGrid(playerCar, track);
                Assert.IsTrue(policeCtrl.IsPursuing, "Police unit should be actively pursuing after ResetPoliceOnGrid.");

                bool initialRedPhase = policeCtrl.IsSirenRedPhase;
                bool phaseChanged = false;

                for (int i = 0; i < 60; i++)
                {
                    spawnedPoliceCar.StepPhysics(0.02f, track);
                    policeCtrl.TickPursuit(0.02f);
                    if (policeCtrl.IsSirenRedPhase != initialRedPhase)
                    {
                        phaseChanged = true;
                    }
                }

                Assert.Greater(spawnedPoliceCar.Physics.Speed, 5f, "Police car should accelerate in pursuit of the player.");
                Assert.IsTrue(phaseChanged, "Police siren should alternate between red and blue phases.");
            }
            finally
            {
                if (spawnedPoliceCar != null) Object.DestroyImmediate(spawnedPoliceCar.gameObject);
                Object.DestroyImmediate(policeCtrlGo);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(trackGo);
            }
        }

        [Test]
        public void PauseMenuUI_AndGameManager_TogglePauseAndBuildQuitButtons()
        {
            var gmGo = new GameObject("TestPauseGM");
            try
            {
                var gm = gmGo.AddComponent<GameManager>();
                var pauseUi = gmGo.AddComponent<PauseMenuUI>();
                pauseUi.Bind(gm);

                gm.TransitionTo(GameFlowState.Racing);
                Assert.IsFalse(gm.IsPaused);
                Assert.IsFalse(pauseUi.IsShowing);

                gm.PauseGame();
                Assert.IsTrue(gm.IsPaused, "GameManager.IsPaused must be true after PauseGame().");
                Assert.AreEqual(0f, Time.timeScale, 1e-4f, "Time.timeScale must be 0 while paused.");
                Assert.IsTrue(pauseUi.IsShowing, "PauseMenuUI must be visible while paused in Racing state.");
                Assert.IsNotNull(pauseUi.ResumeButton);
                Assert.IsNotNull(pauseUi.RestartButton);
                Assert.IsNotNull(pauseUi.LobbyButton);
                Assert.IsNotNull(pauseUi.QuitButton);

                gm.ResumeGame();
                Assert.IsFalse(gm.IsPaused, "GameManager.IsPaused must be false after ResumeGame().");
                Assert.AreEqual(1f, Time.timeScale, 1e-4f, "Time.timeScale must be restored to 1 after ResumeGame().");
                Assert.IsFalse(pauseUi.IsShowing, "PauseMenuUI must hide after ResumeGame().");
            }
            finally
            {
                Time.timeScale = 1f;
                Object.DestroyImmediate(gmGo);
            }
        }
    }
}
