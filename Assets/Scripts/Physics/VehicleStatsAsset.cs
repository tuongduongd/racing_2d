using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// ScriptableObject wrapper around VehicleStats so designers can author
    /// reusable baseline vehicle templates as project assets.
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleStats", menuName = "RacingGame/Vehicle Stats")]
    public class VehicleStatsAsset : ScriptableObject
    {
        [SerializeField] private VehicleStats stats = VehicleStats.Default;

        public VehicleStats Stats => stats.Sanitize();

        public void SetStats(in VehicleStats newStats)
        {
            stats = newStats.Sanitize();
        }

        private void OnValidate()
        {
            stats = stats.Sanitize();
        }
    }
}
