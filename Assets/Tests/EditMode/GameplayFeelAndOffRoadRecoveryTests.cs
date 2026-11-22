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
                Assert.GreaterOrEqual(def.LaneWidth, 4.4f, $"Expected widened LaneWidth on {assetPaths[i]}");
                Assert.GreaterOrEqual(def.TotalTrackWidth, 18.4f, $"Expected widened TotalTrackWidth on {assetPaths[i]}");
            }
        }
#endif
    }
}
