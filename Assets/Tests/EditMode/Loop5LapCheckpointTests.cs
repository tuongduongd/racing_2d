using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class Loop5LapCheckpointTests
    {
        private GameObject _trackGo;
        private GameObject _managerGo;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private WaypointTrack _track;
        private LapCheckpointManager _manager;

        [SetUp]
        public void SetUp()
        {
            _trackGo = new GameObject("TestTrack");
            _track = _trackGo.AddComponent<WaypointTrack>();
            _track.BuildDefaultCircuit();

            _managerGo = new GameObject("TestLapManager");
            _manager = _managerGo.AddComponent<LapCheckpointManager>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) Object.DestroyImmediate(go);
            _spawned.Clear();
            Object.DestroyImmediate(_managerGo);
            Object.DestroyImmediate(_trackGo);
        }

        private Car CreateCar(string name, Vector2 position)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var car = go.AddComponent<Car>();
            Vector2 dir = _track.GetTrackDirection(0);
            car.Initialize(null, position, Mathf.Atan2(dir.y, dir.x), _track);
            return car;
        }

        /// <summary>Simulates driving forward straight through a checkpoint gate.</summary>
        private void CrossGate(Car car, int checkpointIndex)
        {
            Checkpoint cp = _manager.Checkpoints[checkpointIndex];
            _manager.ProcessRacerMovement(car, cp.Position - cp.Forward * 2f, cp.Position + cp.Forward * 2f);
        }

        private void DriveFullLap(Car car)
        {
            for (int i = 1; i < _manager.CheckpointCount; i++) CrossGate(car, i);
            CrossGate(car, 0);
        }

        [Test]
        public void Checkpoint_CountsOnlyForwardCrossingInsideGate()
        {
            var cp = new Checkpoint(1, 5, Vector2.zero, Vector2.right, 10f, 0f);

            Assert.IsTrue(cp.IsCrossedForward(new Vector2(-1f, 0f), new Vector2(1f, 0f)), "Forward crossing should count.");
            Assert.IsFalse(cp.IsCrossedForward(new Vector2(1f, 0f), new Vector2(-1f, 0f)), "Reverse crossing must not count.");
            Assert.IsFalse(cp.IsCrossedForward(new Vector2(-1f, 20f), new Vector2(1f, 20f)), "Crossing outside the gate must not count.");
            Assert.IsFalse(cp.IsCrossedForward(new Vector2(-3f, 0f), new Vector2(-1f, 0f)), "Not reaching the gate must not count.");
        }

        [Test]
        public void Lap_IsCountedOnlyWhenAllCheckpointsPassedInOrder()
        {
            Car car = CreateCar("Racer", _track.GetWaypoint(0) - _track.GetTrackDirection(0) * 5f);
            _manager.Initialize(_track, new[] { car }, 3);
            RacerProgress p = _manager.GetProgress(car);

            Assert.GreaterOrEqual(_manager.CheckpointCount, 3);
            Assert.AreEqual(1, p.NextCheckpointIndex, "Start: first expected gate is checkpoint 1.");

            // Crossing the finish line right at the start never counts as a lap.
            CrossGate(car, 0);
            Assert.AreEqual(0, p.CompletedLaps);

            DriveFullLap(car);
            Assert.AreEqual(1, p.CompletedLaps, "A full ordered lap must add exactly one lap.");
            Assert.AreEqual(1, p.NextCheckpointIndex);
            Assert.Greater(p.LastLapTime, -0.001f);
        }

        [Test]
        public void Shortcut_SkippingCheckpointsDoesNotCountLap()
        {
            Car car = CreateCar("Cheater", _track.GetWaypoint(0) - _track.GetTrackDirection(0) * 5f);
            _manager.Initialize(_track, new[] { car }, 3);
            RacerProgress p = _manager.GetProgress(car);

            CrossGate(car, 1);
            // Skip checkpoint 2 (cut across the track), go straight to the rest and the finish line.
            for (int i = 3; i < _manager.CheckpointCount; i++) CrossGate(car, i);
            CrossGate(car, 0);

            Assert.AreEqual(0, p.CompletedLaps, "Cutting the track must not count a lap.");
            Assert.AreEqual(2, p.NextCheckpointIndex, "Racer must still need the skipped checkpoint.");
        }

        [Test]
        public void Ranking_UsesCheckpointsThenDistanceToNextCheckpoint()
        {
            Vector2 start = _track.GetWaypoint(0);
            Vector2 dir = _track.GetTrackDirection(0);

            Car leader = CreateCar("Leader", start + dir * 5f);
            Car closer = CreateCar("Closer", start + dir * 40f);
            Car behind = CreateCar("Behind", start + dir * 20f);
            _manager.Initialize(_track, new[] { behind, closer, leader }, 3);

            // Leader passes checkpoint 1 -> more checkpoints than everyone else.
            CrossGate(leader, 1);
            _manager.ProcessRacerMovement(closer, closer.Physics.Position, closer.Physics.Position);
            _manager.ProcessRacerMovement(behind, behind.Physics.Position, behind.Physics.Position);

            IReadOnlyList<RacerProgress> ranking = _manager.GetRanking();
            Assert.AreEqual(leader, ranking[0].Car, "More checkpoints passed ranks first.");
            Assert.AreEqual(closer, ranking[1].Car, "Same checkpoints: closer to next checkpoint ranks higher.");
            Assert.AreEqual(behind, ranking[2].Car);
            Assert.AreEqual(1, _manager.GetPosition(leader));
        }

        [Test]
        public void Finish_RacerFinishesAfterTotalLapsAndKeepsFinishOrder()
        {
            Vector2 start = _track.GetWaypoint(0) - _track.GetTrackDirection(0) * 5f;
            Car first = CreateCar("First", start);
            Car second = CreateCar("Second", start + Vector2.up);
            _manager.Initialize(_track, new[] { first, second }, 2);

            int finishedEvents = 0;
            _manager.RacerFinished += _ => finishedEvents++;

            DriveFullLap(first);
            DriveFullLap(first);
            DriveFullLap(second);
            DriveFullLap(second);

            RacerProgress p1 = _manager.GetProgress(first);
            RacerProgress p2 = _manager.GetProgress(second);
            Assert.IsTrue(p1.IsFinished);
            Assert.IsTrue(p2.IsFinished);
            Assert.AreEqual(1, p1.FinishPosition);
            Assert.AreEqual(2, p2.FinishPosition);
            Assert.AreEqual(2, finishedEvents);
            Assert.IsTrue(_manager.AreAllRacersFinished());

            // A finished racer cannot gain extra laps.
            DriveFullLap(first);
            Assert.AreEqual(2, p1.CompletedLaps);
            Assert.AreEqual(first, _manager.GetRanking()[0].Car);
        }

        [Test]
        public void WrongWay_MovingOppositeAboveMinSpeedSetsIsWrongWay()
        {
            Vector2 pos = new Vector2(20f, 0f);
            Car car = CreateCar("WrongWayDriver", pos);
            _manager.Initialize(_track, new[] { car }, 3);
            RacerProgress p = _manager.GetProgress(car);

            Vector2 forwardDir = _track.GetTrackDirection(0);
            float reverseHeading = Mathf.Atan2(-forwardDir.y, -forwardDir.x);
            car.ResetToTrackPoint(pos, reverseHeading, _track);
            car.Physics.ApplyVelocityImpulse(car.Physics.Forward * 8f);

            _manager.Tick(1f / 60f);

            Assert.IsTrue(p.IsWrongWay, "Facing opposite to track direction with speed > wrongWayMinSpeed (4f) must set IsWrongWay.");
        }

        [Test]
        public void WrongWay_StationaryOrAtOrBelowMinSpeedDoesNotFlag()
        {
            Vector2 pos = new Vector2(20f, 0f);
            Car car = CreateCar("SlowReverseDriver", pos);
            _manager.Initialize(_track, new[] { car }, 3);
            RacerProgress p = _manager.GetProgress(car);

            Vector2 forwardDir = _track.GetTrackDirection(0);
            float reverseHeading = Mathf.Atan2(-forwardDir.y, -forwardDir.x);

            // Stationary (speed = 0)
            car.ResetToTrackPoint(pos, reverseHeading, _track);
            _manager.Tick(1f / 60f);
            Assert.IsFalse(p.IsWrongWay, "Stationary car facing backward must not be flagged as wrong-way.");

            // Below threshold (speed = 3f < 4f)
            car.Physics.ApplyVelocityImpulse(car.Physics.Forward * 3f);
            _manager.Tick(1f / 60f);
            Assert.IsFalse(p.IsWrongWay, "Car moving opposite below wrongWayMinSpeed must not be flagged as wrong-way.");

            // Exact threshold (speed = 4f == wrongWayMinSpeed)
            car.ResetToTrackPoint(pos, reverseHeading, _track);
            car.Physics.ApplyVelocityImpulse(car.Physics.Forward * 4f);
            _manager.Tick(1f / 60f);
            Assert.IsFalse(p.IsWrongWay, "Car moving opposite at exact wrongWayMinSpeed (4f) must not be flagged as wrong-way.");
        }

        [Test]
        public void WrongWay_TurningBackToForwardDirectionClearsIsWrongWay()
        {
            Vector2 pos = new Vector2(20f, 0f);
            Car car = CreateCar("RecoveringDriver", pos);
            _manager.Initialize(_track, new[] { car }, 3);
            RacerProgress p = _manager.GetProgress(car);

            Vector2 forwardDir = _track.GetTrackDirection(0);
            float forwardHeading = Mathf.Atan2(forwardDir.y, forwardDir.x);
            float reverseHeading = Mathf.Atan2(-forwardDir.y, -forwardDir.x);

            car.ResetToTrackPoint(pos, reverseHeading, _track);
            car.Physics.ApplyVelocityImpulse(car.Physics.Forward * 8f);
            _manager.Tick(1f / 60f);
            Assert.IsTrue(p.IsWrongWay, "Precondition: car should initially be flagged as wrong-way.");

            car.ResetToTrackPoint(pos, forwardHeading, _track);
            car.Physics.ApplyVelocityImpulse(car.Physics.Forward * 8f);
            _manager.Tick(1f / 60f);
            Assert.IsFalse(p.IsWrongWay, "Turning back to forward track direction must clear IsWrongWay.");
        }

        [Test]
        public void WrongWay_FinishedRacerIsNotFlaggedEvenWhenFacingBackwardAtSpeed()
        {
            Car car = CreateCar("FinishedDriver", _track.GetWaypoint(0) - _track.GetTrackDirection(0) * 5f);
            _manager.Initialize(_track, new[] { car }, 1);
            RacerProgress p = _manager.GetProgress(car);

            DriveFullLap(car);
            Assert.IsTrue(p.IsFinished, "Precondition: racer must have finished the race.");

            Vector2 pos = new Vector2(20f, 0f);
            Vector2 forwardDir = _track.GetTrackDirection(0);
            float reverseHeading = Mathf.Atan2(-forwardDir.y, -forwardDir.x);
            car.ResetToTrackPoint(pos, reverseHeading, _track);
            car.Physics.ApplyVelocityImpulse(car.Physics.Forward * 10f);

            _manager.Tick(1f / 60f);

            Assert.IsFalse(p.IsWrongWay, "Finished racer must never be flagged as wrong-way even when facing backward at speed.");
        }
    }
}
