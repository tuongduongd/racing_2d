using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class Phase1And2Tests
    {
        private class StubDriver : IVehicleDriver
        {
            public VehicleInput NextInput;
            public VehicleInput DecideInput(Car car, WaypointTrack track) => NextInput;
        }

        [Test]
        public void VehiclePhysics_ThrottleAcceleratesAndDragSlowsWhenCoasting()
        {
            var physics = new VehiclePhysics();
            physics.Reset(Vector2.zero, 0f); // facing +X

            physics.ApplyThrottle(1f);
            physics.ApplyBrake(0f);
            physics.ApplySteering(0f);

            for (int i = 0; i < 60; i++)
            {
                physics.Update(1f / 60f);
            }

            float speedAfterAccel = physics.ForwardSpeed;
            Assert.Greater(speedAfterAccel, 15f, "Vehicle should accelerate significantly after 1s of full throttle.");
            Assert.Greater(physics.position.x, 5f, "Vehicle should move forward along +X.");

            // Coasting (no throttle, no brake) should slow down via drag & rolling resistance
            physics.ApplyThrottle(0f);
            for (int i = 0; i < 120; i++)
            {
                physics.Update(1f / 60f);
            }

            Assert.Less(physics.ForwardSpeed, speedAfterAccel, "Drag should slow the vehicle down when coasting.");
        }

        [Test]
        public void VehiclePhysics_BrakeDeceleratesAndSteeringTurnsWhenMoving()
        {
            var physics = new VehiclePhysics();
            physics.Reset(Vector2.zero, 0f);

            // Steering at zero speed should not spin the vehicle in place
            physics.ApplySteering(1f);
            physics.Update(0.1f);
            Assert.AreEqual(0f, physics.heading, 0.001f);

            // Accelerate then steer right (+1 decreases heading)
            physics.ApplySteering(0f);
            physics.ApplyThrottle(1f);
            for (int i = 0; i < 60; i++) physics.Update(1f / 60f);

            float speedBeforeBrake = physics.ForwardSpeed;
            physics.ApplyThrottle(0f);
            physics.ApplyBrake(1f);
            physics.ApplySteering(1f);
            for (int i = 0; i < 20; i++) physics.Update(1f / 60f);

            Assert.Less(physics.ForwardSpeed, speedBeforeBrake, "Braking should reduce forward speed.");
            Assert.Less(physics.heading, 0f, "Steering right (+1) while moving should turn heading clockwise.");
        }

        [Test]
        public void WaypointTrack_ClosedLoopQueriesAndIsOnTrack()
        {
            var go = new GameObject("TestTrack");
            try
            {
                var track = go.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                Assert.GreaterOrEqual(track.WaypointCount, 15);
                Assert.LessOrEqual(track.WaypointCount, 30);

                // Wrap-around check
                Vector2 wrapped = track.GetNextWaypoint(track.WaypointCount - 1);
                Assert.AreEqual(track.waypoints[0], wrapped);

                Vector2 dir = track.GetTrackDirection(0);
                Assert.AreEqual(1f, dir.magnitude, 0.001f);

                // On centerline -> true; far outside trackWidth/2 -> false
                Assert.IsTrue(track.IsOnTrack(track.waypoints[0]));
                Assert.IsFalse(track.IsOnTrack(track.waypoints[0] + new Vector2(0f, track.trackWidth * 0.5f + 3f)));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Car_OffRoadAppliesSpeedPenalty()
        {
            var trackGo = new GameObject("TestTrack");
            var carGo = new GameObject("TestCar");
            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                var car = carGo.AddComponent<Car>();
                var driver = new StubDriver { NextInput = new VehicleInput(1f, 0f, 0f) };

                // Place car off-road (lateral offset > trackWidth / 2)
                Vector2 offRoadPos = track.waypoints[0] + new Vector2(0f, track.trackWidth * 0.5f + 4f);
                car.Initialize(driver, offRoadPos, 0f, track);

                for (int i = 0; i < 180; i++)
                {
                    car.StepPhysics(1f / 60f, track);
                }

                Assert.IsFalse(car.isOnTrack, "Car should be detected as off-road.");
                Assert.LessOrEqual(car.physics.ForwardSpeed, car.physics.maxSpeed * 0.6f,
                    "Off-road surface should cap/slow top speed.");
            }
            finally
            {
                Object.DestroyImmediate(carGo);
                Object.DestroyImmediate(trackGo);
            }
        }
    }
}
