using System;

namespace RacingGame
{
    [Serializable]
    public struct TrackPointDto
    {
        public float x;
        public float y;
    }

    [Serializable]
    public class TrackFeatureZoneDto
    {
        public string kind = "Overpass";
        public float startNormalized = 0.2f;
        public float endNormalized = 0.35f;
        public float elevationBoostMeters = 6.5f;
        public string label = string.Empty;
    }

    /// <summary>
    /// Pure C# JSON schema DTO for external track map files in StreamingAssets/Maps/*.json.
    /// Validates structural domain invariants and throws InvalidTrackConfigurationException on invalid data.
    /// </summary>
    [Serializable]
    public class TrackJsonDto
    {
        public string mapId = string.Empty;
        public string displayName = string.Empty;
        public string description = string.Empty;
        public int difficultyStars = 2;
        public bool unlockedByDefault = true;

        public int laneCount = 4;
        public float laneWidth = 6.0f;
        public float shoulderExtraWidth = 6.5f;

        public float elevationAmplitude = 3.5f;
        public float elevationFrequency = 3.0f;
        public float elevationSecondaryAmp = 1.5f;

        public int obstacleCount = 12;
        public float obstacleMinSpacingMeters = 34f;
        public string[] allowedObstacles = { "TrafficCone", "OilBarrel", "RoadBarrier", "OilSlick" };

        public bool enablePoliceChase = true;
        public float policeSpeedBonus = 4.5f;
        public float policeWarningRangeMeters = 42f;
        public float aiCornerSpeedMultiplier = 1.0f;
        public float rewardMultiplier = 1.0f;
        public int recommendedLaps = 3;

        public TrackPointDto[] waypoints = Array.Empty<TrackPointDto>();
        public TrackFeatureZoneDto[] featureZones = Array.Empty<TrackFeatureZoneDto>();

        public void ValidateOrThrow(string sourceIdentifier = null)
        {
            string id = string.IsNullOrWhiteSpace(sourceIdentifier) ? mapId : sourceIdentifier;
            if (string.IsNullOrWhiteSpace(mapId))
            {
                throw new InvalidTrackConfigurationException(id, "Track JSON 'mapId' must not be empty.");
            }
            if (waypoints == null || waypoints.Length < 3)
            {
                throw new InvalidTrackConfigurationException(id, $"Track '{mapId}' must have at least 3 waypoints.");
            }
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (float.IsNaN(waypoints[i].x) || float.IsInfinity(waypoints[i].x) ||
                    float.IsNaN(waypoints[i].y) || float.IsInfinity(waypoints[i].y))
                {
                    throw new InvalidTrackConfigurationException(id, $"Track '{mapId}' contains non-finite waypoint coordinates at index {i}.");
                }
            }
            if (laneCount < 2 || laneCount > 6)
            {
                throw new InvalidTrackConfigurationException(id, $"Track '{mapId}' laneCount={laneCount} must be in [2, 6].");
            }
            if (laneWidth < 2.4f || float.IsNaN(laneWidth))
            {
                throw new InvalidTrackConfigurationException(id, $"Track '{mapId}' laneWidth={laneWidth} must be >= 2.4m.");
            }
            if (rewardMultiplier < 1.0f || rewardMultiplier > 3.0f)
            {
                throw new InvalidTrackConfigurationException(id, $"Track '{mapId}' rewardMultiplier={rewardMultiplier} must be in [1.0, 3.0].");
            }
        }
    }
}
