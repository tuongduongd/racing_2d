using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Centralized Art Bible palette for the PS1 Road Rash-inspired valley highway look.
    /// Every renderer, particle effect, and HUD panel pulls colors from here for visual cohesion.
    /// </summary>
    public static class RoadRashVisualTheme
    {
        // Sky & Atmosphere
        public static readonly Color SkyTop = new Color(0.11f, 0.27f, 0.74f);
        public static readonly Color SkyMid = new Color(0.28f, 0.50f, 0.86f);
        public static readonly Color SkyHorizon = new Color(0.62f, 0.77f, 0.92f);
        public static readonly Color SunGlow = new Color(1.00f, 0.94f, 0.78f);

        // Distant Mountains & Valley Hills
        public static readonly Color FarRidgeTop = new Color(0.42f, 0.36f, 0.30f);
        public static readonly Color FarRidgeBase = new Color(0.30f, 0.27f, 0.24f);
        public static readonly Color MidHillLight = new Color(0.45f, 0.50f, 0.23f);
        public static readonly Color MidHillDark = new Color(0.32f, 0.38f, 0.18f);

        // Roadside Embankments & Terrain
        public static readonly Color TerrainEarthLight = new Color(0.50f, 0.41f, 0.27f);
        public static readonly Color TerrainEarthDark = new Color(0.44f, 0.36f, 0.23f);
        public static readonly Color TerrainGrassLight = new Color(0.38f, 0.45f, 0.21f);
        public static readonly Color TerrainGrassDark = new Color(0.32f, 0.39f, 0.17f);
        public static readonly Color ShoulderLight = new Color(0.52f, 0.46f, 0.37f);
        public static readonly Color ShoulderDark = new Color(0.46f, 0.40f, 0.32f);

        // Highway Asphalt & Markings
        public static readonly Color AsphaltLight = new Color(0.23f, 0.24f, 0.27f);
        public static readonly Color AsphaltDark = new Color(0.20f, 0.21f, 0.24f);
        public static readonly Color CenterYellowLine = new Color(0.92f, 0.72f, 0.16f);
        public static readonly Color LaneWhiteLine = new Color(0.88f, 0.90f, 0.92f);
        public static readonly Color StartCheckDark = new Color(0.10f, 0.11f, 0.13f);
        public static readonly Color StartCheckLight = new Color(0.95f, 0.95f, 0.95f);

        // Rider & Effects (HDR values trigger subtle URP Bloom)
        public static readonly Color PlayerJacketBlue = new Color(0.16f, 0.44f, 0.92f);
        public static readonly Color TailLightIdle = new Color(0.85f, 0.12f, 0.10f, 0.55f);
        public static readonly Color TailLightBrakeHdr = new Color(2.6f, 0.32f, 0.22f, 1.0f);
        public static readonly Color SparkHdr = new Color(2.8f, 1.55f, 0.35f, 1.0f);
        public static readonly Color DustTan = new Color(0.66f, 0.56f, 0.41f, 0.75f);
        public static readonly Color SpeedStreak = new Color(0.85f, 0.92f, 1.0f, 0.45f);

        // Retro Road Rash HUD Palette
        public static readonly Color HudYellow = new Color(0.98f, 0.85f, 0.16f);
        public static readonly Color HudShadow = new Color(0.05f, 0.06f, 0.09f, 0.92f);
        public static readonly Color MetalLight = new Color(0.68f, 0.71f, 0.75f);
        public static readonly Color MetalMid = new Color(0.46f, 0.49f, 0.53f);
        public static readonly Color MetalDark = new Color(0.20f, 0.22f, 0.25f);
        public static readonly Color LcdBackground = new Color(0.60f, 0.65f, 0.53f);
        public static readonly Color LcdInk = new Color(0.08f, 0.12f, 0.08f);
        public static readonly Color PanelBackdrop = new Color(0.08f, 0.10f, 0.14f, 0.86f);
        public static readonly Color AccentGreen = new Color(0.30f, 0.88f, 0.42f);
        public static readonly Color DangerRed = new Color(0.94f, 0.22f, 0.18f);
    }
}
