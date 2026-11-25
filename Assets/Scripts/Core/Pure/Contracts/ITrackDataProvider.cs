using System.Collections.Generic;

namespace RacingGame
{
    /// <summary>
    /// External track repository abstraction (DIP) for loading data-driven circuit definitions from JSON files.
    /// </summary>
    public interface ITrackDataProvider
    {
        IReadOnlyList<TrackJsonDto> LoadTracks(string directoryPath);
        TrackJsonDto LoadTrackFile(string filePath);
    }
}
