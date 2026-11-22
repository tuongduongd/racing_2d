using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class GameplayFeelAndOffRoadRecoveryTests
    {
        private class ScriptedDriver : Driver
        {
            public VehicleInput Input;
            public override VehicleInput DecideInput(Car car, WaypointTrack track) => Input;
        }

        private class ScriptedPlayerDriver : PlayerDriver
        {
            public VehicleInput Input;
            public override VehicleInput DecideInput(Car car, WaypointTrack track) => Input;
        }

        private class PassiveAiDriver : AIRacerDriver
        {
            public VehicleInput Input;
            public override VehicleInput DecideInput(Car car, WaypointTrack track) => Input;
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

        private T Track<T>(T obj) where T : Object
        {
            _cleanup.Add(obj);
            return obj;
        }

        [Test]
        public void VehicleStats_Default_IsCalmerAndEasierToControl()
        {
            VehicleStats defaults = VehicleStats.Default;
            Assert.LessOrEqual(defaults.maxSpeed, 50f, "Default maxSpeed should be slower for easier control.");
            Assert.GreaterOrEqual(defaults.maxSpeed, 42f, "Default maxSpeed should remain lively.");
            Assert.LessOrEqual(defaults.enginePower, 5200f, "Default enginePower should ramp smoothly.");
            Assert.GreaterOrEqual(defaults.grip, 9.0f, "Default tire grip should stay crisp.");
        }

        [Test]
        public void OffRoad_ThrottleAcceleratesBrisklyAndSteeringReturnsToTrackQuickly()
        {
            var trackGo = Track(new GameObject("OffRoadTestTrack"));
            var carGo = Track(new GameObject("OffRoadTestCar"));
            var track = trackGo.AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();

            var car = carGo.AddComponent<Car>();
            var driver = carGo.AddComponent<ScriptedDriver>();
            driver.Input = new VehicleInput(1f, 0f, -1f); // Full throttle + steer left back toward center from right shoulder

            Vector2 dir = track.GetTrackDirection(0);
            Vector2 right = new Vector2(dir.y, -dir.x);
            float startLat = track.TrackWidth * 0.5f + 3.5f;
            Vector2 offRoadStart = track.GetWaypoint(0) + right * startLat;
            float heading = Mathf.Atan2(dir.y, dir.x);

            car.Initialize(driver, offRoadStart, heading, track);
            Assert.IsFalse(car.IsOnTrack, "Car must begin off-road.");

            const float dt = 0.02f;
            float timeToReturn = -1f;
            for (int step = 0; step < 100; step++) // up to 2.0s
            {
                car.StepPhysics(dt, track);
                if (car.IsOnTrack)
                {
                    timeToReturn = (step + 1) * dt;
                    break;
                }
            }

            Assert.Greater(timeToReturn, 0f, "Car should return onto the track within 2.0s when throttling and steering back.");
            Assert.LessOrEqual(timeToReturn, 1.2f, $"Returning to track took too long ({timeToReturn:F2}s).");
            Assert.Greater(car.Physics.ForwardSpeed, 16f, "Off-road throttle should accelerate briskly instead of bogging down.");
        }

        [Test]
        public void OuterBarrier_PreventsExceedingMaxOffRoadWithoutKillingForwardMomentum()
        {
            var trackGo = Track(new GameObject("BarrierTestTrack"));
            var carGo = Track(new GameObject("BarrierTestCar"));
            var track = trackGo.AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();

            var car = carGo.AddComponent<Car>();
            var driver = carGo.AddComponent<ScriptedDriver>();
            driver.Input = new VehicleInput(1f, 0f, 0f);

            Vector2 dir = track.GetTrackDirection(0);
            Vector2 right = new Vector2(dir.y, -dir.x);
            float maxHalfOffRoad = track.MaxOffRoadWidth * 0.5f;
            Vector2 edgePos = track.GetWaypoint(0) + right * (maxHalfOffRoad - 0.1f);
            float headingOutward = Mathf.Atan2(dir.y, dir.x) - 0.15f; // slight angle toward right outer barrier

            car.Initialize(driver, edgePos, headingOutward, track);
            car.Physics.ApplyVelocityImpulse(car.Physics.Forward * 24f);

            const float dt = 0.02f;
            for (int step = 0; step < 25; step++)
            {
                car.StepPhysics(dt, track);
            }

            Assert.LessOrEqual(Mathf.Abs(car.LateralOffset), maxHalfOffRoad + 0.05f,
                "Car must stay inside outer barrier.");
            Assert.Greater(car.Physics.ForwardSpeed, 18f,
                "Brushing the outer barrier should not kill forward speed.");
        }

#if UNITY_EDITOR
        [Test]
        public void MapAssets_HaveWidenedLaneAndTrackWidths()
        {
            string[] assetPaths =
            {
                "Assets/ScriptableObjects/Maps/Map_01_CityMorning.asset",
                "Assets/ScriptableObjects/Maps/Map_02_CoastalHighway.asset",
                "Assets/ScriptableObjects/Maps/Map_03_NeonNightExpress.asset",
                "Assets/ScriptableObjects/Maps/Map_04_CityNightDistrict.asset",
                "Assets/ScriptableObjects/Maps/TrackDefinition_City.asset"
            };

            for (int i = 0; i < assetPaths.Length; i++)
            {
                TrackDefinition def = UnityEditor.AssetDatabase.LoadAssetAtPath<TrackDefinition>(assetPaths[i]);
                Assert.IsNotNull(def, $"Missing map asset: {assetPaths[i]}");
                Assert.GreaterOrEqual(def.LaneWidth, 5.4f, $"Expected widened LaneWidth on {assetPaths[i]}");
                Assert.GreaterOrEqual(def.TotalTrackWidth, 24.0f, $"Expected widened TotalTrackWidth on {assetPaths[i]}");
                Assert.GreaterOrEqual(def.MaxOffRoadWidth, 37.0f, $"Expected widened MaxOffRoadWidth on {assetPaths[i]}");
            }
        }
#endif

        [Test]
        public void OuterBarrier_SteepAngleAtZeroSpeed_WallSlidesAndReturnsToTrackQuickly()
        {
            var trackGo = Track(new GameObject("BarrierSlideTrack"));
            var carGo = Track(new GameObject("BarrierSlideCar"));
            var track = trackGo.AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();

            var car = carGo.AddComponent<Car>();
            var driver = carGo.AddComponent<ScriptedDriver>();
            driver.Input = new VehicleInput(1f, 0f, -1f); // Full throttle + steer inward left from right barrier

            Vector2 dir = track.GetTrackDirection(0);
            Vector2 right = new Vector2(dir.y, -dir.x);
            float maxHalfOffRoad = track.MaxOffRoadWidth * 0.5f;
            Vector2 stuckAtBarrier = track.GetWaypoint(0) + right * (maxHalfOffRoad - 0.05f);
            float trackHeading = Mathf.Atan2(dir.y, dir.x);
            float steepOutwardHeading = trackHeading - 70f * Mathf.Deg2Rad; // nose jammed sharply into right barrier

            car.Initialize(driver, stuckAtBarrier, steepOutwardHeading, track);

            const float dt = 0.02f;
            float timeToReturn = -1f;
            for (int step = 0; step < 75; step++) // max 1.5s
            {
                car.StepPhysics(dt, track);
                if (car.IsOnTrack)
                {
                    timeToReturn = (step + 1) * dt;
                    break;
                }
            }

            Assert.Greater(timeToReturn, 0f, "Steep-angle barrier pin should recover onto the road within 1.5s.");
            Assert.LessOrEqual(timeToReturn, 1.5f, $"Barrier wall-slide recovery took too long ({timeToReturn:F2}s).");
            Assert.Greater(car.Physics.ForwardSpeed, 12f, "Car should build smooth forward speed while sliding off the barrier.");
        }

        [Test]
        public void GameManager_OffRoadPlayer_NeverAutoTeleports_AndCruisesSmoothlyOffRoadLikeRoadRash()
        {
            var trackGo = Track(new GameObject("RoadRashOffRoadGMTrack"));
            var gmGo = Track(new GameObject("RoadRashOffRoadGM"));
            var playerGo = Track(new GameObject("RoadRashOffRoadPlayerCar"));

            var track = trackGo.AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();

            var car = playerGo.AddComponent<Car>();
            var driver = playerGo.AddComponent<ScriptedPlayerDriver>();
            driver.Input = new VehicleInput(1f, 0f, 0f); // Straight throttle off-road

            Vector2 dir = track.GetTrackDirection(0);
            Vector2 right = new Vector2(dir.y, -dir.x);
            float trackHeading = Mathf.Atan2(dir.y, dir.x);
            Vector2 offRoadPos = track.GetWaypoint(0) + right * (track.MaxOffRoadWidth * 0.5f - 1.5f);
            car.Initialize(driver, offRoadPos, trackHeading - Mathf.PI * 0.5f, track);

            var gm = gmGo.AddComponent<GameManager>();
            SetPrivateField(gm, "track", track);
            SetPrivateField(gm, "playerCar", car);
            SetPrivateField(gm, "aiOpponentCount", 0);
            InvokePrivateMethod(gm, "SpawnOrSyncOpponents");
            gm.TransitionTo(GameFlowState.Racing);

            int recoveryEvents = 0;
            gm.PlayerRecoveredToTrack += recoveredCar =>
            {
                if (recoveredCar == car) recoveryEvents++;
            };

            Assert.IsTrue(car.IsRoadRashArcadeActive, "PlayerDriver car should use Road Rash arcade handling.");
            Assert.IsFalse(car.IsOnTrack, "Car must begin off-road.");

            const float dt = 0.02f;
            for (int step = 0; step < 100; step++) // 2.0s off-road cruising
            {
                gm.StepRaceSimulation(dt);
            }

            Assert.AreEqual(0, recoveryEvents, "Going off-road must NOT auto-teleport the player back to center.");
            Assert.IsFalse(car.IsOnTrack, "Player should be free to keep riding off-road until steering back.");
            Assert.Greater(car.Physics.ForwardSpeed, 28f, "Off-road Road Rash riding should feel fast and smooth like on the road.");

            Vector2 currentTrackDir = track.SampleAtDistance(car.TrackDistance).tangent;
            Assert.Greater(Vector2.Dot(car.Physics.Forward, currentTrackDir), 0.90f,
                "Bike should stay aligned forward along the track instead of spinning sideways.");

            // Steer back onto the road smoothly using A/D (left from right shoulder)
            float steerSign = -Mathf.Sign(car.LateralOffset);
            driver.Input = new VehicleInput(1f, 0f, steerSign);
            float timeToReturn = -1f;
            for (int step = 0; step < 60; step++) // up to 1.2s
            {
                gm.StepRaceSimulation(dt);
                if (car.IsOnTrack)
                {
                    timeToReturn = (step + 1) * dt;
                    break;
                }
            }

            Assert.Greater(timeToReturn, 0f, "Steering inward from off-road must smoothly return to asphalt.");
            Assert.LessOrEqual(timeToReturn, 1.0f, $"Returning to asphalt took too long ({timeToReturn:F2}s).");
            Assert.AreEqual(0, recoveryEvents, "Returning via steering must not fire teleport recovery events.");

            // Manual [F] recovery still works only when explicitly triggered
            car.Initialize(driver, offRoadPos, trackHeading + Mathf.PI * 0.5f, track);
            Assert.IsFalse(car.IsOnTrack);
            Assert.IsTrue(gm.RecoverPlayerToTrack(automatic: false));
            Assert.AreEqual(1, recoveryEvents);
            Assert.IsTrue(car.IsOnTrack);
        }

        [Test]
        public void RoadRashPlayerHandling_SharpCorner_FollowsCurveSmoothly_AndCurvatureMatchesVisualDirection()
        {
            var trackGo = Track(new GameObject("SharpCornerTrack"));
            var carGo = Track(new GameObject("SharpCornerPlayerCar"));

            // Deterministic CW right-turn circuit so every corner is a sharp right-hand bend
            var wps = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 100f),
                new Vector2(100f, 100f),
                new Vector2(100f, 0f)
            };
            var cornerDef = Track(TrackDefinition.CreateRuntime(
                "sharp_corner_test",
                "Sharp Corner Test",
                wps,
                lanes: 4,
                widthPerLane: 4.6f,
                mapTheme: null,
                obstacles: 0));

            var track = trackGo.AddComponent<WaypointTrack>();
            track.ApplyTrackDefinition(cornerDef);

            // Around distance s = 96 (approaching top-left 90-deg right turn), curvature must be positive for CW right bend
            TrackSample cornerSample = track.SampleAtDistance(96f);
            Assert.Greater(cornerSample.curvature, 0.005f,
                "Clockwise right bend must report positive curvature to match visual RoadRashRenderer2D bend & chevrons.");

            var car = carGo.AddComponent<Car>();
            var driver = carGo.AddComponent<ScriptedPlayerDriver>();
            driver.Input = new VehicleInput(1f, 0f, 0f); // Player holds straight gas through a 90-degree corner

            Vector2 dir = track.GetTrackDirection(0);
            float startHeading = Mathf.Atan2(dir.y, dir.x);
            car.Initialize(driver, new Vector2(0f, 40f), startHeading, track);

            const float dt = 0.02f;
            for (int step = 0; step < 200; step++) // 4.0s drives well past the 90-degree corner at s=100
            {
                car.StepPhysics(dt, track);
                if (step >= 25) // After 0.5s heading align
                {
                    if (step >= 60) // After 1.2s acceleration warmup from standstill
                    {
                        Assert.Greater(car.Physics.ForwardSpeed, 22f,
                            $"Bike lost too much speed around sharp corner at step {step}.");
                    }
                    Vector2 expectedTangent = track.SampleAtDistance(car.TrackDistance).tangent;
                    Assert.Greater(Vector2.Dot(car.Physics.Forward, expectedTangent), 0.85f,
                        $"Bike spun away from road tangent at step {step}.");
                }
            }

            Assert.Greater(car.TrackDistance, 115f, "Player should have smoothly cleared the 90-degree corner.");
        }

        [Test]
        public void Car_PhysicsInterpolation_SmoothlyBlendsBetweenFixedSteps()
        {
            var trackGo = Track(new GameObject("InterpTrack"));
            var carGo = Track(new GameObject("InterpCar"));
            var track = trackGo.AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();

            var car = carGo.AddComponent<Car>();
            var driver = carGo.AddComponent<ScriptedDriver>();
            driver.Input = new VehicleInput(1f, 0f, 0.5f);

            Vector2 dir = track.GetTrackDirection(0);
            float heading = Mathf.Atan2(dir.y, dir.x);
            car.Initialize(driver, track.GetWaypoint(0), heading, track);

            for (int i = 0; i < 20; i++)
            {
                car.StepPhysics(0.02f, track);
            }

            float prevS = car.TrackDistance;
            float prevLat = car.LateralOffset;
            car.StepPhysics(0.02f, track);
            float currS = car.TrackDistance;
            float currLat = car.LateralOffset;

            Assert.AreEqual(prevS, car.GetInterpolatedTrackDistance(0f, track), 0.001f);
            Assert.AreEqual(currS, car.GetInterpolatedTrackDistance(1f, track), 0.001f);

            float midS = car.GetInterpolatedTrackDistance(0.5f, track);
            float midLat = car.GetInterpolatedLateralOffset(0.5f);
            Assert.AreEqual(Mathf.Lerp(prevS, currS, 0.5f), midS, 0.001f);
            Assert.AreEqual(Mathf.Lerp(prevLat, currLat, 0.5f), midLat, 0.001f);
        }

        [Test]
        public void VehicleCollisionPhysics_ElasticImpact_ConservesMomentum_RespectsEnergyAndCoulombFriction()
        {
            Vector2 posA = Vector2.zero;
            Vector2 velA = new Vector2(38f, 12f);
            float massA = 260f;
            float radA = 1.3f;

            Vector2 posB = new Vector2(1.2f, 0.5f);
            Vector2 velB = new Vector2(20f, -10f);
            float massB = 180f;
            float radB = 1.3f;

            const float e = 0.68f;
            const float mu = 0.28f;

            VehicleCollisionResult res = VehicleCollisionPhysics.Resolve(
                posA, velA, massA, radA,
                posB, velB, massB, radB,
                e, mu);

            Assert.IsTrue(res.Collided);
            Assert.Greater(res.NormalImpulse, 0f);
            Assert.Greater(Mathf.Abs(res.AngularVelocityDeltaA), 0.1f, "Oblique impact should induce angular impulse.");
            Assert.Greater(Mathf.Abs(res.AngularVelocityDeltaB), 0.1f);

            Vector2 velAAfter = velA + res.VelocityDeltaA;
            Vector2 velBAfter = velB + res.VelocityDeltaB;

            // 1. Exact linear momentum conservation: mA*vA + mB*vB == mA*vA' + mB*vB'
            Vector2 pBefore = massA * velA + massB * velB;
            Vector2 pAfter = massA * velAAfter + massB * velBAfter;
            Assert.AreEqual(pBefore.x, pAfter.x, 0.01f, "Momentum X must be conserved.");
            Assert.AreEqual(pBefore.y, pAfter.y, 0.01f, "Momentum Y must be conserved.");

            // 2. Inverse-mass delta-v ratio: |dvB| / |dvA| == mA / mB
            Assert.AreEqual(massA / massB, res.VelocityDeltaB.magnitude / res.VelocityDeltaA.magnitude, 0.001f,
                "Velocity change magnitude must scale inversely with vehicle mass.");

            // 3. Exact normal restitution law
            float relNormalBefore = Vector2.Dot(velB - velA, res.Normal);
            float relNormalAfter = Vector2.Dot(velBAfter - velAAfter, res.Normal);
            Assert.AreEqual(-e * relNormalBefore, relNormalAfter, 0.001f,
                "Post-collision normal relative velocity must follow Newton's restitution law.");

            // 4. Coulomb friction cone bound: |jt| <= mu * jn
            Assert.LessOrEqual(Mathf.Abs(res.TangentialImpulse), mu * res.NormalImpulse + 0.001f);

            // 5. Energy conservation bound (2D after + Z hop <= 2D before, and inelastic loss < before)
            float keBefore = 0.5f * (massA * velA.sqrMagnitude + massB * velB.sqrMagnitude);
            float ke2DAfter = 0.5f * (massA * velAAfter.sqrMagnitude + massB * velBAfter.sqrMagnitude);
            float keZAfter = 0.5f * (massA * res.VerticalLaunchVelA * res.VerticalLaunchVelA +
                                     massB * res.VerticalLaunchVelB * res.VerticalLaunchVelB);
            Assert.Less(ke2DAfter, keBefore, "Inelastic restitution must dissipate horizontal kinetic energy.");
            Assert.LessOrEqual(ke2DAfter + keZAfter, keBefore + 0.01f,
                "Combined 3D kinetic energy cannot exceed initial kinetic energy.");
        }

        [Test]
        public void Car_HighSpeedVehicleCollision_FlingsStruckBikeSidewaysWithSlidingFrictionAndBallisticHop()
        {
            var trackGo = Track(new GameObject("CollisionPhysTrack"));
            var gmGo = Track(new GameObject("CollisionPhysGM"));
            var playerGo = Track(new GameObject("CollisionPlayerCar"));
            var rivalGo = Track(new GameObject("CollisionRivalCar"));

            var track = trackGo.AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();

            var playerCar = playerGo.AddComponent<Car>();
            var playerDriver = playerGo.AddComponent<ScriptedPlayerDriver>();
            playerDriver.Input = new VehicleInput(1f, 0f, 0f);

            var rivalCar = rivalGo.AddComponent<Car>();
            var rivalDriver = rivalGo.AddComponent<PassiveAiDriver>();
            rivalDriver.Input = new VehicleInput(1f, 0f, 0f);

            Vector2 wp0 = track.GetWaypoint(0);
            Vector2 dir = track.GetTrackDirection(0);
            Vector2 right = new Vector2(dir.y, -dir.x);
            float heading = Mathf.Atan2(dir.y, dir.x);

            playerCar.Initialize(playerDriver, wp0, heading, track);
            playerCar.Physics.SetVelocity(dir * 44f + right * 8f);

            rivalCar.Initialize(rivalDriver, wp0 + dir * 0.6f + right * 1.1f, heading, track);
            rivalCar.Physics.SetVelocity(dir * 20f - right * 6f);
            float rivalStartLat = rivalCar.LateralOffset;

            var gm = gmGo.AddComponent<GameManager>();
            SetPrivateField(gm, "track", track);
            SetPrivateField(gm, "playerCar", playerCar);
            SetPrivateField(gm, "aiOpponentCount", 1);
            InvokePrivateMethod(gm, "SpawnOrSyncOpponents");
            gm.TransitionTo(GameFlowState.Racing);

            int collisionCount = 0;
            gm.RacersCollided += (_, __, ___) => collisionCount++;

            const float dt = 0.02f;
            float peakRivalAir = 0f;
            float peakLatDelta = 0f;

            // Run first tick to resolve collision and trigger knockback + vertical launch
            gm.StepRaceSimulation(dt);
            Assert.GreaterOrEqual(collisionCount, 1, "High-speed overlap should fire RacersCollided.");
            Assert.IsTrue(rivalCar.IsCollisionKnockback, "Struck bike should enter physical knockback skid.");
            Assert.IsTrue(playerCar.IsCollisionKnockback, "Impacting player bike should also feel physical recoil.");

            // Simulate 1.2s (60 ticks) of sliding friction + ballistic bounce under g=9.81
            for (int step = 0; step < 60; step++)
            {
                gm.StepRaceSimulation(dt);
                peakRivalAir = Mathf.Max(peakRivalAir, rivalCar.AirHeight);
                if (step < 30)
                {
                    peakLatDelta = Mathf.Max(peakLatDelta, Mathf.Abs(rivalCar.LateralOffset - rivalStartLat));
                }
            }

            Assert.Greater(peakLatDelta, 3.0f, "Struck bike should be flung physically sideways across lanes.");
            Assert.Greater(peakRivalAir, 0.05f, "High-energy collision should visibly bounce bike into the air.");
            Assert.AreEqual(0f, rivalCar.AirHeight, 0.001f, "Bike should land back onto ground under gravity.");
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
