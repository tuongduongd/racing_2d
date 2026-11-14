using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// ScriptableObject defining AI driving difficulty parameters
    /// (speed limits, cornering smoothness, throttle control, and steering noise).
    /// </summary>
    [CreateAssetMenu(fileName = "AIProfile", menuName = "RacingGame/AI Profile")]
    public class AIProfile : ScriptableObject
    {
        [Header("Difficulty")]
        [SerializeField] private DifficultyLevel difficulty = DifficultyLevel.Normal;

        [Header("Speed & Cornering")]
        [Range(0.4f, 1.0f)]
        [SerializeField] private float skillLevel = 0.82f;

        [Range(0.5f, 1.05f)]
        [SerializeField] private float topSpeedFactor = 0.90f;

        [Min(5f)]
        [SerializeField] private float minCornerSpeed = 22f;

        [Range(0.5f, 1.4f)]
        [SerializeField] private float cornerBrakeAggression = 0.92f;

        [Range(0.5f, 1.1f)]
        [SerializeField] private float throttleScale = 0.94f;

        [Header("Steering Variation")]
        [Range(0f, 0.35f)]
        [SerializeField] private float noiseAmplitude = 0.08f;

        [Range(0.1f, 3.0f)]
        [SerializeField] private float noiseFrequency = 0.70f;

        public DifficultyLevel Difficulty => difficulty;
        public float SkillLevel => skillLevel;
        public float TopSpeedFactor => topSpeedFactor;
        public float MinCornerSpeed => minCornerSpeed;
        public float CornerBrakeAggression => cornerBrakeAggression;
        public float ThrottleScale => throttleScale;
        public float NoiseAmplitude => noiseAmplitude;
        public float NoiseFrequency => noiseFrequency;

        public void ConfigureValues(
            DifficultyLevel difficultyLevel,
            float newSkillLevel,
            float newTopSpeedFactor,
            float newMinCornerSpeed,
            float newCornerBrakeAggression,
            float newThrottleScale,
            float newNoiseAmplitude,
            float newNoiseFrequency)
        {
            difficulty = difficultyLevel;
            skillLevel = newSkillLevel;
            topSpeedFactor = newTopSpeedFactor;
            minCornerSpeed = newMinCornerSpeed;
            cornerBrakeAggression = newCornerBrakeAggression;
            throttleScale = newThrottleScale;
            noiseAmplitude = newNoiseAmplitude;
            noiseFrequency = newNoiseFrequency;
            ClampValues();
        }

        /// <summary>
        /// Creates a runtime fallback AIProfile when no ScriptableObject asset is assigned.
        /// </summary>
        public static AIProfile CreateDefault(DifficultyLevel level)
        {
            AIProfile profile = CreateInstance<AIProfile>();
            profile.name = $"AIProfile_{level}_Runtime";

            switch (level)
            {
                case DifficultyLevel.Easy:
                    profile.ConfigureValues(
                        DifficultyLevel.Easy,
                        newSkillLevel: 0.62f,
                        newTopSpeedFactor: 0.78f,
                        newMinCornerSpeed: 16f,
                        newCornerBrakeAggression: 1.15f,
                        newThrottleScale: 0.82f,
                        newNoiseAmplitude: 0.14f,
                        newNoiseFrequency: 0.90f);
                    break;

                case DifficultyLevel.Hard:
                    profile.ConfigureValues(
                        DifficultyLevel.Hard,
                        newSkillLevel: 0.96f,
                        newTopSpeedFactor: 0.99f,
                        newMinCornerSpeed: 28f,
                        newCornerBrakeAggression: 0.75f,
                        newThrottleScale: 1.00f,
                        newNoiseAmplitude: 0.03f,
                        newNoiseFrequency: 0.50f);
                    break;

                default:
                    profile.ConfigureValues(
                        DifficultyLevel.Normal,
                        newSkillLevel: 0.82f,
                        newTopSpeedFactor: 0.90f,
                        newMinCornerSpeed: 22f,
                        newCornerBrakeAggression: 0.92f,
                        newThrottleScale: 0.94f,
                        newNoiseAmplitude: 0.08f,
                        newNoiseFrequency: 0.70f);
                    break;
            }

            return profile;
        }

        private void OnValidate()
        {
            ClampValues();
        }

        private void ClampValues()
        {
            skillLevel = Mathf.Clamp(skillLevel, 0.4f, 1.0f);
            topSpeedFactor = Mathf.Clamp(topSpeedFactor, 0.5f, 1.05f);
            minCornerSpeed = Mathf.Max(5f, minCornerSpeed);
            cornerBrakeAggression = Mathf.Clamp(cornerBrakeAggression, 0.5f, 1.4f);
            throttleScale = Mathf.Clamp(throttleScale, 0.5f, 1.1f);
            noiseAmplitude = Mathf.Clamp(noiseAmplitude, 0f, 0.35f);
            noiseFrequency = Mathf.Clamp(noiseFrequency, 0.1f, 3.0f);
        }
    }
}
