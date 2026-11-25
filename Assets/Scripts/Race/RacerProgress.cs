using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Race progress of a single Car (laps, checkpoints, lap times, finish state).
    /// Read-only to the outside world; only LapCheckpointManager may modify it
    /// through internal methods (Encapsulation).
    /// </summary>
    public class RacerProgress
    {
        public Car Car { get; }
        public string RacerName => Car != null ? Car.RacerName : "?";

        public int CompletedLaps { get; private set; }
        public int NextCheckpointIndex { get; private set; }
        public int TotalCheckpointsPassed { get; private set; }
        public float DistanceToNextCheckpoint { get; private set; }

        public bool IsFinished { get; private set; }
        public int FinishPosition { get; private set; }
        public float FinishTime { get; private set; }

        public float LastLapTime { get; private set; }
        public float BestLapTime { get; private set; }
        public bool HasBestLap => BestLapTime > 0f;

        public bool IsWrongWay { get; private set; }

        private float _lapStartTime;

        internal RacerProgress(Car car)
        {
            Car = car;
        }

        internal void Reset(int firstExpectedCheckpoint, float raceTime)
        {
            CompletedLaps = 0;
            NextCheckpointIndex = firstExpectedCheckpoint;
            TotalCheckpointsPassed = 0;
            DistanceToNextCheckpoint = 0f;
            IsFinished = false;
            FinishPosition = 0;
            FinishTime = 0f;
            LastLapTime = 0f;
            BestLapTime = 0f;
            IsWrongWay = false;
            _lapStartTime = raceTime;
        }

        internal void AdvanceCheckpoint(int checkpointCount)
        {
            TotalCheckpointsPassed++;
            NextCheckpointIndex = (NextCheckpointIndex + 1) % Mathf.Max(1, checkpointCount);
        }

        internal void CompleteLap(float raceTime)
        {
            CompletedLaps++;
            LastLapTime = raceTime - _lapStartTime;
            if (!HasBestLap || LastLapTime < BestLapTime)
            {
                BestLapTime = LastLapTime;
            }
            _lapStartTime = raceTime;
        }

        internal void MarkFinished(int position, float raceTime)
        {
            IsFinished = true;
            FinishPosition = position;
            FinishTime = raceTime;
        }

        internal void SetDistanceToNextCheckpoint(float distance)
        {
            DistanceToNextCheckpoint = Mathf.Max(0f, distance);
        }

        internal float LapStartTime => _lapStartTime;

        internal void SetWrongWay(bool wrongWay)
        {
            IsWrongWay = wrongWay;
        }

        internal void RestoreFromSnapshot(RacerMidRaceSnapshot snap, float raceTime)
        {
            if (snap == null) return;
            CompletedLaps = CoreMath.Max(0, snap.completedLaps);
            NextCheckpointIndex = CoreMath.Max(0, snap.nextCheckpointIndex);
            TotalCheckpointsPassed = CoreMath.Max(0, snap.totalCheckpointsPassed);
            IsFinished = snap.isFinished;
            FinishPosition = CoreMath.Max(0, snap.finishPosition);
            FinishTime = CoreMath.Max(0f, snap.finishTime);
            LastLapTime = CoreMath.Max(0f, snap.lastLapTime);
            BestLapTime = CoreMath.Max(0f, snap.bestLapTime);
            IsWrongWay = false;
            _lapStartTime = CoreMath.Max(0f, raceTime);
        }
    }

    /// <summary>
    /// Ranking rule: finished racers first (by finish order), then by
    /// (laps + checkpoints passed) descending, then by distance to next checkpoint ascending.
    /// Implements IComparer so the ranking strategy can be swapped (Polymorphism).
    /// </summary>
    public class RacerRankingComparer : IComparer<RacerProgress>
    {
        public int Compare(RacerProgress a, RacerProgress b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            if (a.IsFinished && b.IsFinished) return a.FinishPosition.CompareTo(b.FinishPosition);
            if (a.IsFinished) return -1;
            if (b.IsFinished) return 1;

            int byCheckpoints = b.TotalCheckpointsPassed.CompareTo(a.TotalCheckpointsPassed);
            if (byCheckpoints != 0) return byCheckpoints;

            return a.DistanceToNextCheckpoint.CompareTo(b.DistanceToNextCheckpoint);
        }
    }
}
