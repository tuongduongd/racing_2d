using System;
using System.Collections.Generic;

namespace RacingGame
{
    /// <summary>
    /// Serializable DTO persisted to JSON for player money, upgrade levels, race records,
    /// unlocked maps, and top high scores.
    /// </summary>
    [Serializable]
    public class PlayerSaveData
    {
        public int money = 200;
        public int engineLevel;
        public int brakesLevel;
        public int gripLevel;
        public int weightLevel;
        public int racesPlayed;
        public int racesWon;
        public float bestRaceTime;
        public List<string> unlockedMapIds = new List<string>();
        public string selectedMapId = string.Empty;
        public List<HighScoreEntry> highScores = new List<HighScoreEntry>();
    }
}
