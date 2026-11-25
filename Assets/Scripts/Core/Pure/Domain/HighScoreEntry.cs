using System;

namespace RacingGame
{
    /// <summary>
    /// Persistent high-score leaderboard entry recorded upon completing a race.
    /// </summary>
    [Serializable]
    public class HighScoreEntry
    {
        public string mapId = string.Empty;
        public string racerName = "Player";
        public float finishTime;
        public int totalLaps = 3;
        public string difficulty = "Normal";
        public string recordedAtUtc = string.Empty;
    }
}
