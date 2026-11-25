using System;
using System.Collections.Generic;

namespace RacingGame
{
    /// <summary>
    /// Serializable state snapshot of an individual vehicle during an active race.
    /// </summary>
    [Serializable]
    public class RacerMidRaceSnapshot
    {
        public string racerName = "Racer";
        public bool isPlayer;
        public bool isPolice;
        public float posX;
        public float posY;
        public float velX;
        public float velY;
        public float headingRad;
        public int completedLaps;
        public int nextCheckpointIndex = 1;
        public int totalCheckpointsPassed;
        public bool isFinished;
        public int finishPosition;
        public float finishTime;
        public float lastLapTime;
        public float bestLapTime;
    }

    /// <summary>
    /// Serializable full mid-race state persisted so a race can resume cleanly after closing and reopening the app.
    /// </summary>
    [Serializable]
    public class MidRaceSaveData
    {
        public string mapId = "city_morning";
        public int totalLaps = 3;
        public int difficulty = (int)DifficultyLevel.Normal;
        public float raceTime;
        public int finishedCount;
        public string savedAtUtc = string.Empty;
        public List<RacerMidRaceSnapshot> racers = new List<RacerMidRaceSnapshot>();
    }
}
