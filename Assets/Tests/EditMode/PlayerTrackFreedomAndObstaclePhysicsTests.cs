using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class PlayerTrackFreedomAndObstaclePhysicsTests
    {
        private sealed class FakeInputProvider : IInputProvider
        {
            public VehicleInput Current;
            public bool RecoverPressed;

            public VehicleInput ReadInput() => Current;
            public bool WasPausePressed() => false;
            public bool WasRestartPressed() => false;
            public bool WasRecoverPressed()
            {
                bool v = RecoverPressed;
                RecoverPressed = false;
                return v;
            }
            public bool WasMutePressed() => false;
            public void ResetState()
            {
                RecoverPressed = false;
            }
        }

        private readonly List<Object> _cleanup = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _cleanup.Count - 1; i >= 0; i--)
            {
                if (_cleanup[i] != null) Object.DestroyImmediate(_cleanup[i]);
            }
            _cleanup.Clear();
        }

        private T TrackCleanup<T>(T obj) where T : Object
        {
            _cleanup.Add(obj);
            return obj;
        }

        [Test]
        public void Player_NormalAndOffRoadDriving_NeverTeleportsOrPullsToCenter_AndKeepsPositiveCappedOffRoadSpeed()
        {
            var trackGo = TrackCleanup(new GameObject("FreedomTrack"));
            var gmGo = TrackCleanup(new GameObject("FreedomGM"));
            var playerGo = TrackCleanup(new GameObject("FreedomPlayer"));
            var inputHost = TrackCleanup(new GameObject("FreedomInput"));

            TrackDefinition cityDef = TrackCleanup(TrackDefinition.CreateCityMorningPreset(null));
            var track = trackGo.AddComponent<WaypointTrack>();
            track.ApplyTrackDefinition(cityDef);

            var car = playerGo.AddComponent<Car>();
            car.ConfigureIdentity(true, Color.cyan);
            var playerDriver = playerGo.AddComponent<PlayerDriver>();
            var fakeInput = new FakeInputProvider { Current = new VehicleInput(1f, 0f, 0f) };
            playerDriver.SetInputProvider(fakeInput);

            var inputHandler = inputHost.AddComponent<InputHandler>();
            Assert.IsFalse(inputHandler.WasRecoverPressed(), "InputHandler must never queue recover teleport presses.");

            var gm = gmGo.AddComponent<GameManager>();
            SetPrivateField(gm, "track", track);
            SetPrivateField(gm, "playerCar", car);
            SetPrivateField(gm, "playerDriver", playerDriver);
            SetPrivateField(gm, "aiOpponentCount", 0);
            gm.ConfigureServices(inputProvider: fakeInput);
            InvokePrivateMethod(gm, "SpawnOrSyncOpponents");
            gm.TransitionTo(GameFlowState.Racing);

            int recoveryEvents = 0;
            gm.PlayerRecoveredToTrack += _ => recoveryEvents++;

            // 1. Driving in an outer lane with ZERO steering across curves must NOT pull back to centerline 0
            Vector2 wp0 = track.GetWaypoint(0);
            Vector2 dir0 = track.GetTrackDirection(0);
            Vector2 right0 = new Vector2(dir0.y, -dir0.x);
            float heading0 = Mathf.Atan2(dir0.y, dir0.x);
            float outerLaneOffset = track.GetLaneCenterOffset(track.LaneCount - 1);

            car.Initialize(playerDriver, wp0 + right0 * outerLaneOffset, heading0, track);
            fakeInput.Current = new VehicleInput(1f, 0f, 0f);
            const float dt = 0.02f;
            for (int step = 0; step < 250; step++) // 5.0 seconds around curves
            {
                gm.StepRaceSimulation(dt);
            }

            Assert.IsTrue(car.IsPlayer);
            Assert.IsTrue(car.IsRoadRashArcadeActive);
            Assert.IsTrue(car.IsOnTrack);
            Assert.AreEqual(0, recoveryEvents);
            Assert.AreEqual(outerLaneOffset, car.LateralOffset, 0.05f,
                "Player cruising in an outer lane with zero steering must hold its lateral offset instead of drifting toward center.");
            Assert.Greater(car.Physics.ForwardSpeed, 35f);

            // 2. Driving OFF-ROAD on left shoulder with ZERO steering must NOT auto-pull or teleport back onto road
            float offRoadLeft = -(track.TrackWidth * 0.5f + 4.0f);
            car.Initialize(playerDriver, wp0 + right0 * offRoadLeft, heading0, track);
            fakeInput.Current = new VehicleInput(1f, 0f, 0f);
            car.Physics.SetVelocity(dir0 * 24f);
            Assert.IsFalse(car.IsOnTrack, "Player must begin off-road.");

            for (int step = 0; step < 250; step++) // 5.0 seconds around curves off-road
            {
                gm.StepRaceSimulation(dt);
            }

            Assert.IsFalse(gm.RecoverPlayerToTrack(automatic: false), "RecoverPlayerToTrack must be disabled.");
            Assert.IsFalse(gm.RecoverPlayerToTrack(automatic: true), "Automatic RecoverPlayerToTrack must be disabled.");
            Assert.AreEqual(0, recoveryEvents, "Off-road driving must never fire teleport recovery.");
            Assert.IsFalse(car.IsOnTrack, "Player driving straight off-road must stay off-road until steering back.");
            Assert.AreEqual(offRoadLeft, car.LateralOffset, 0.05f,
                "Off-road player with zero steering must not be pulled toward road center.");
            Assert.Greater(car.Physics.ForwardSpeed, 15f, "Off-road throttle must stay > 0 and never stall to zero.");
            Assert.LessOrEqual(car.Physics.ForwardSpeed, car.Physics.MaxSpeed * 0.6f,
                "Off-road speed must remain capped at <= 60% MaxSpeed.");

            // 3. Releasing throttle momentarily preserves positive forward momentum; manual steering returns smoothly
            fakeInput.Current = new VehicleInput(0f, 0f, 0f);
            for (int step = 0; step < 20; step++)
            {
                gm.StepRaceSimulation(dt);
            }
            Assert.Greater(car.Physics.ForwardSpeed, 5f, "Coasting briefly off-road should preserve momentum > 0.");

            fakeInput.Current = new VehicleInput(1f, 0f, 1f); // Steer right (inward from left shoulder)
            float returnTime = -1f;
            for (int step = 0; step < 80; step++)
            {
                gm.StepRaceSimulation(dt);
                if (car.IsOnTrack)
                {
                    returnTime = (step + 1) * dt;
                    break;
                }
            }
            Assert.Greater(returnTime, 0f, "Player steering inward manually from off-road must return smoothly.");
        }

        [TestCase(ObstacleKind.Pothole)]
        [TestCase(ObstacleKind.OilSlick)]
        public void TrackObstacleSystem_PotholeAndOilSlick_OnlySlowDownWithoutLateralKnockbackOrBackwardPush(ObstacleKind hazardKind)
        {
            var trackGo = TrackCleanup(new GameObject("HazardTrack"));
            var obsGo = TrackCleanup(new GameObject("HazardObstacles"));
            var carGo = TrackCleanup(new GameObject("HazardPlayer"));

            var track = trackGo.AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();

            var obsSystem = obsGo.AddComponent<TrackObstacleSystem>();
            obsSystem.EnableObstacles = true;

            var car = carGo.AddComponent<Car>();
            car.ConfigureIdentity(true, Color.red);
            var playerDriver = carGo.AddComponent<PlayerDriver>();
            var fakeInput = new FakeInputProvider { Current = new VehicleInput(1f, 0f, 0f) };
            playerDriver.SetInputProvider(fakeInput);

            Vector2 wp0 = track.GetWaypoint(0);
            Vector2 dir0 = track.GetTrackDirection(0);
            float heading0 = Mathf.Atan2(dir0.y, dir0.x);
            car.Initialize(playerDriver, wp0, heading0, track);
            car.Physics.SetVelocity(dir0 * 35f);
            car.StepPhysics(0.02f, track);

            float preSpeed = car.Physics.ForwardSpeed;
            float preDist = car.TrackDistance;
            float preLat = car.LateralOffset;

            TrackObstacle hazard = obsSystem.AddManualObstacle(hazardKind, preDist + 0.3f, preLat + 0.1f, 2.5f);
            Assert.IsTrue(hazard.IsSurfaceHazard, $"{hazardKind} must be tagged as surface hazard.");

            int hitCount = 0;
            obsSystem.ObstacleHit += (_, __) => hitCount++;

            obsSystem.StepObstacles(track, new[] { car }, 0.02f);

            Assert.AreEqual(1, hitCount, $"{hazardKind} should fire ObstacleHit once.");
            Assert.IsTrue(car.IsHazardSlowed, $"{hazardKind} must trigger temporary hazard slowdown.");
            Assert.Greater(car.Physics.ForwardSpeed, 0f, $"{hazardKind} should reduce speed without stopping or reversing the car.");
            Assert.Less(car.Physics.ForwardSpeed, preSpeed * 0.90f, $"{hazardKind} must noticeably slow forward speed.");
            Assert.GreaterOrEqual(car.TrackDistance, preDist - 0.05f, $"{hazardKind} must NOT push the car backward.");
            Assert.AreEqual(preLat, car.LateralOffset, 0.01f, $"{hazardKind} must NOT knock the car sideways.");
            Assert.IsFalse(car.IsStaggered, $"{hazardKind} must NOT trigger stagger.");
            Assert.IsFalse(car.IsCollisionKnockback, $"{hazardKind} must NOT trigger sideways knockback skid.");

            for (int step = 0; step < 10; step++)
            {
                car.StepPhysics(0.02f, track);
                obsSystem.StepObstacles(track, new[] { car }, 0.02f);
            }
            Assert.AreEqual(1, hitCount);
            Assert.Greater(car.Physics.ForwardSpeed, 5f);
            Assert.AreEqual(preLat, car.LateralOffset, 0.05f);
        }

        [TestCase(ObstacleKind.RoadBarrier)]
        [TestCase(ObstacleKind.ParkedBus)]
        public void TrackObstacleSystem_SolidObstacles_FlingSidewaysAndPushBackwardWithImmunityCooldown(ObstacleKind solidKind)
        {
            var trackGo = TrackCleanup(new GameObject("SolidTrack"));
            var obsGo = TrackCleanup(new GameObject("SolidObstacles"));
            var carGo = TrackCleanup(new GameObject("SolidPlayer"));

            var track = trackGo.AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();

            var obsSystem = obsGo.AddComponent<TrackObstacleSystem>();
            obsSystem.EnableObstacles = true;

            var car = carGo.AddComponent<Car>();
            car.ConfigureIdentity(true, Color.yellow);
            var playerDriver = carGo.AddComponent<PlayerDriver>();
            var fakeInput = new FakeInputProvider { Current = new VehicleInput(1f, 0f, 0f) };
            playerDriver.SetInputProvider(fakeInput);

            Vector2 wp0 = track.GetWaypoint(0);
            Vector2 dir0 = track.GetTrackDirection(0);
            Vector2 right0 = new Vector2(dir0.y, -dir0.x);
            float heading0 = Mathf.Atan2(dir0.y, dir0.x);

            // Start 40m down segment 0 so backward push does not wrap around index 0
            car.Initialize(playerDriver, wp0 + dir0 * 40f + right0 * 0.5f, heading0, track);
            car.Physics.SetVelocity(dir0 * 35f);
            car.StepPhysics(0.02f, track);

            float preTotalSpeed = car.Physics.Speed;
            float preDist = car.TrackDistance;
            float preLat = car.LateralOffset;

            // Place two overlapping solid obstacles slightly left of center so knock pushSide == +1
            TrackObstacle primaryObs = obsSystem.AddManualObstacle(solidKind, preDist + 0.4f, preLat - 0.3f, 2.5f);
            TrackObstacle overlapObs = obsSystem.AddManualObstacle(solidKind, preDist - 2.5f, preLat + 0.3f, 3.0f);
            Assert.IsFalse(primaryObs.IsSurfaceHazard, $"{solidKind} must NOT be tagged as surface hazard.");
            Assert.Greater(primaryObs.BackwardKnockbackMeters, 1.5f);

            int hitCount = 0;
            obsSystem.ObstacleHit += (_, __) => hitCount++;

            obsSystem.StepObstacles(track, new[] { car }, 0.02f);

            Assert.AreEqual(1, hitCount, "Solid collision should trigger once and immediately grant immunity.");
            Assert.IsTrue(car.IsObstacleImmune, "Car must gain short obstacle immunity cooldown to prevent sticky re-hits.");
            Assert.IsTrue(car.IsStaggered, $"{solidKind} must stagger the rider.");
            Assert.IsTrue(car.IsCollisionKnockback, $"{solidKind} must initiate lateral knockback skid.");
            Assert.Less(car.Physics.ForwardSpeed, 0f, $"{solidKind} must rebound forward velocity backward (< 0).");
            Assert.Less(car.Physics.Speed, preTotalSpeed, $"{solidKind} must dissipate pre-impact total speed.");
            Assert.Less(car.TrackDistance, preDist - 1.5f, $"{solidKind} must visibly shove car backward along the track.");
            Assert.Greater(Mathf.Abs(car.LateralOffset - preLat), 0.3f, $"{solidKind} must kick car laterally away from impact.");
            Assert.IsFalse(overlapObs.IsOnCooldown, "Overlapping second obstacle must remain ignored while car is immune.");

            // Step 20 frames through both obstacles: immunity prevents sticky infinite re-hit while bike slides laterally
            for (int step = 0; step < 20; step++)
            {
                car.StepPhysics(0.02f, track);
                obsSystem.StepObstacles(track, new[] { car }, 0.02f);
            }
            Assert.AreEqual(1, hitCount, "Immunity cooldown must prevent infinite sticky re-hits during rebound.");
            Assert.Greater(Mathf.Abs(car.LateralOffset - preLat), 1.5f,
                $"{solidKind} must visibly fling the bike sideways across the track.");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing private field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing private method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, null);
        }
    }
}
