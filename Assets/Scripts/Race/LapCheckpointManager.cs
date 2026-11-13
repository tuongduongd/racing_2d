using System;
using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Counts laps, validates checkpoint order (anti-cheat / anti-shortcut),
    /// detects wrong-way driving, and produces a realtime ranking of all racers.
    /// </summary>
    public class LapCheckpointManager : MonoBehaviour
    {
        [Header("Race Rules")]
        [SerializeField] private int totalLaps = 3;
        [SerializeField] private int checkpointCount = 6;
        [SerializeField] private float gateExtraMargin = 2f;

        [Header("Wrong Way Detection")]
        [SerializeField] private float wrongWayDotThreshold = -0.2f;
        [SerializeField] private float wrongWayMinSpeed = 4f;

        private readonly List<Checkpoint> _checkpoints = new List<Checkpoint>();
        private readonly List<RacerProgress> _racers = new List<RacerProgress>();
        private readonly List<RacerProgress> _ranking = new List<RacerProgress>();
        private readonly Dictionary<Car, RacerProgress> _lookup = new Dictionary<Car, RacerProgress>();
        private readonly IComparer<RacerProgress> _rankingComparer = new RacerRankingComparer();

        private WaypointTrack _track;
        private float _raceTime;
        private int _finishedCount;

        public event Action<RacerProgress, Checkpoint> CheckpointPassed;
        public event Action<RacerProgress> LapCompleted;
        public event Action<RacerProgress> RacerFinished;

        public IReadOnlyList<Checkpoint> Checkpoints => _checkpoints;
        public int TotalLaps => totalLaps;
        public int CheckpointCount => _checkpoints.Count;
        public float RaceTime => _raceTime;
        public int RacerCount => _racers.Count;
        public bool IsInitialized => _track != null && _checkpoints.Count > 0;

        public void Initialize(WaypointTrack track, IReadOnlyList<Car> cars, int laps)
        {
            _track = track;
            totalLaps = Mathf.Max(1, laps);
            BuildCheckpoints(track);

            _racers.Clear();
            _lookup.Clear();
            if (cars != null)
            {
                for (int i = 0; i < cars.Count; i++)
                {
                    RegisterRacer(cars[i]);
                }
            }

            ResetRace();
        }

        public void RegisterRacer(Car car)
        {
            if (car == null || _lookup.ContainsKey(car)) return;
            var progress = new RacerProgress(car);
            progress.Reset(FirstExpectedCheckpoint(), _raceTime);
            _racers.Add(progress);
            _lookup.Add(car, progress);
        }

        /// <summary>
        /// Creates evenly spaced checkpoint gates along the track. Checkpoint 0 is the finish line.
        /// </summary>
        public void BuildCheckpoints(WaypointTrack track)
        {
            _checkpoints.Clear();
            if (track == null || track.WaypointCount < 3) return;

            int wpCount = track.WaypointCount;
            int count = Mathf.Clamp(checkpointCount, 3, wpCount);
            float halfWidth = track.MaxOffRoadWidth * 0.5f + gateExtraMargin;

            int lastWp = -1;
            for (int i = 0; i < count; i++)
            {
                int wp = Mathf.RoundToInt(i * wpCount / (float)count) % wpCount;
                if (wp == lastWp) continue;
                lastWp = wp;

                _checkpoints.Add(new Checkpoint(
                    _checkpoints.Count,
                    wp,
                    track.GetWaypoint(wp),
                    track.GetDirectionAtWaypoint(wp),
                    halfWidth,
                    track.GetDistanceAtWaypoint(wp)));
            }
        }

        public void ResetRace()
        {
            _raceTime = 0f;
            _finishedCount = 0;
            int first = FirstExpectedCheckpoint();
            for (int i = 0; i < _racers.Count; i++)
            {
                _racers[i].Reset(first, _raceTime);
            }
            UpdateDistances();
            UpdateRanking();
        }

        public void SetTotalLaps(int laps)
        {
            totalLaps = Mathf.Max(1, laps);
        }

        /// <summary>
        /// Called once per physics step after all cars moved.
        /// </summary>
        public void Tick(float dt)
        {
            if (!IsInitialized) return;
            _raceTime += Mathf.Max(0f, dt);

            for (int i = 0; i < _racers.Count; i++)
            {
                Car car = _racers[i].Car;
                if (car == null) continue;
                ProcessRacerMovement(car, car.PreviousPosition, car.Physics.Position);
            }

            UpdateRanking();
        }

        /// <summary>
        /// Checks whether the movement segment of a car crossed its next expected checkpoint.
        /// Crossing any other checkpoint (skipping / shortcut / reverse) is ignored.
        /// </summary>
        public void ProcessRacerMovement(Car car, Vector2 from, Vector2 to)
        {
            if (!IsInitialized || car == null) return;
            if (!_lookup.TryGetValue(car, out RacerProgress progress)) return;

            if (!progress.IsFinished)
            {
                Checkpoint expected = _checkpoints[progress.NextCheckpointIndex];
                if (expected.IsCrossedForward(from, to))
                {
                    OnCheckpointPassed(progress, expected);
                }
            }

            UpdateDistance(progress);
            UpdateWrongWay(progress);
        }

        public RacerProgress GetProgress(Car car)
        {
            if (car == null) return null;
            _lookup.TryGetValue(car, out RacerProgress progress);
            return progress;
        }

        public IReadOnlyList<RacerProgress> GetRanking()
        {
            UpdateRanking();
            return _ranking;
        }

        /// <summary>1-based race position of a car, or 0 if it is not registered.</summary>
        public int GetPosition(Car car)
        {
            for (int i = 0; i < _ranking.Count; i++)
            {
                if (_ranking[i].Car == car) return i + 1;
            }
            return 0;
        }

        public bool AreAllRacersFinished()
        {
            return _racers.Count > 0 && _finishedCount >= _racers.Count;
        }

        private void OnCheckpointPassed(RacerProgress progress, Checkpoint checkpoint)
        {
            progress.AdvanceCheckpoint(_checkpoints.Count);
            CheckpointPassed?.Invoke(progress, checkpoint);

            if (!checkpoint.IsFinishLine) return;

            progress.CompleteLap(_raceTime);
            LapCompleted?.Invoke(progress);

            if (progress.CompletedLaps >= totalLaps)
            {
                _finishedCount++;
                progress.MarkFinished(_finishedCount, _raceTime);
                RacerFinished?.Invoke(progress);
            }
        }

        /// <summary>
        /// Cars start on the grid behind or on the finish line, so the first gate they must
        /// pass is checkpoint 1; crossing the finish line at the start never counts as a lap.
        /// </summary>
        private int FirstExpectedCheckpoint()
        {
            return _checkpoints.Count > 1 ? 1 : 0;
        }

        private void UpdateDistances()
        {
            for (int i = 0; i < _racers.Count; i++)
            {
                UpdateDistance(_racers[i]);
            }
        }

        private void UpdateDistance(RacerProgress progress)
        {
            if (_track == null || progress.Car == null || _checkpoints.Count == 0) return;
            Checkpoint next = _checkpoints[progress.NextCheckpointIndex];
            float total = _track.TotalLength;
            float d = next.TrackDistance - progress.Car.TrackDistance;
            if (total > 0.01f)
            {
                d %= total;
                if (d < 0f) d += total;
            }
            progress.SetDistanceToNextCheckpoint(d);
        }

        private void UpdateWrongWay(RacerProgress progress)
        {
            Car car = progress.Car;
            if (_track == null || car == null) return;

            Vector2 trackDir = _track.GetTrackDirection(car.CurrentWaypointIndex);
            float dot = Vector2.Dot(car.Physics.Forward, trackDir);
            bool wrongWay = dot < wrongWayDotThreshold && car.Physics.Speed > wrongWayMinSpeed;
            progress.SetWrongWay(wrongWay && !progress.IsFinished);
        }

        private void UpdateRanking()
        {
            _ranking.Clear();
            _ranking.AddRange(_racers);
            _ranking.Sort(_rankingComparer);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < _checkpoints.Count; i++)
            {
                Checkpoint cp = _checkpoints[i];
                Gizmos.color = cp.IsFinishLine ? Color.white : Color.cyan;
                Gizmos.DrawLine(cp.GateStart, cp.GateEnd);
            }
        }
    }
}
