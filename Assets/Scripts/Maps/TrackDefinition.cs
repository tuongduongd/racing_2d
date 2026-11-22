using System;
using UnityEngine;

namespace RacingGame
{
    public enum TrackFeatureKind
    {
        None = 0,
        Overpass = 1,
        Tunnel = 2,
        Crosswalk = 3
    }

    [Serializable]
    public class TrackFeatureZone
    {
        public TrackFeatureKind kind = TrackFeatureKind.Overpass;
        [Range(0f, 1f)] public float startNormalized = 0.2f;
        [Range(0f, 1f)] public float endNormalized = 0.35f;
        public float elevationBoostMeters = 6.5f;
        public string label = "Cầu vượt";
    }

    /// <summary>
    /// Định nghĩa dữ liệu cho một Map đua xe hoàn chỉnh:
    /// gồm danh sách Waypoints, số làn (2 đến 6), độ rộng từng làn, thông số thử thách
    /// (chướng ngại vật, cảnh sát, hệ số thưởng) và tham chiếu MapTheme.
    /// </summary>
    [CreateAssetMenu(fileName = "TrackDefinition", menuName = "RacingGame/Track Definition")]
    public class TrackDefinition : ScriptableObject
    {
        [Header("Thông Tin Map & Hiển Thị Sảnh Chờ")]
        [SerializeField] private string mapId = "city_morning";
        [SerializeField] private string displayName = "Phố Sáng Ban Mai";
        [TextArea(2, 3)]
        [SerializeField] private string description = "Đường phố 4 làn cổ điển với khúc cua vừa phải.";
        [Range(1, 5)]
        [SerializeField] private int difficultyStars = 2;
        [SerializeField] private Sprite previewBannerSprite;
        [SerializeField] private bool unlockedByDefault = true;

        [Header("Hình Học Đường Đua & Làn Xe")]
        [SerializeField] private Vector2[] waypoints = Array.Empty<Vector2>();
        [Range(2, 6)]
        [SerializeField] private int laneCount = 4;
        [Min(2.4f)]
        [SerializeField] private float laneWidth = 4.0f;
        [Min(3.0f)]
        [SerializeField] private float shoulderExtraWidth = 4.5f;

        [Header("Địa Hình Nhấp Nhô (Elevation)")]
        [SerializeField] private float elevationAmplitude = 3.5f;
        [SerializeField] private float elevationFrequency = 3.0f;
        [SerializeField] private float elevationSecondaryAmp = 1.5f;

        [Header("Độ Khó Chướng Ngại Vật & Cảnh Sát Của Map")]
        [Range(0, 40)]
        [SerializeField] private int obstacleCount = 12;
        [Min(15f)]
        [SerializeField] private float obstacleMinSpacingMeters = 34f;
        [SerializeField] private ObstacleKind[] allowedObstacles =
        {
            ObstacleKind.TrafficCone,
            ObstacleKind.OilBarrel,
            ObstacleKind.RoadBarrier,
            ObstacleKind.OilSlick
        };

        [SerializeField] private bool enablePoliceChase = true;
        [SerializeField] private float policeSpeedBonus = 4.5f;
        [SerializeField] private float policeWarningRangeMeters = 42f;
        [Range(0.75f, 1.35f)]
        [SerializeField] private float aiCornerSpeedMultiplier = 1.0f;
        [Range(1.0f, 3.0f)]
        [SerializeField] private float rewardMultiplier = 1.0f;
        [Range(1, 5)]
        [SerializeField] private int recommendedLaps = 3;

        [Header("Chủ Đề Hình Ảnh & Cảnh Vật (MapTheme)")]
        [SerializeField] private MapTheme theme;

        [Header("Cấu Trúc Đặc Biệt (Cầu Vượt, Đoạn Hầm, Vạch Qua Đường)")]
        [SerializeField] private TrackFeatureZone[] featureZones = Array.Empty<TrackFeatureZone>();

        public string MapId => mapId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public int DifficultyStars => Mathf.Clamp(difficultyStars, 1, 5);
        public Sprite PreviewBannerSprite => previewBannerSprite;
        public bool UnlockedByDefault => unlockedByDefault;

        public Vector2[] Waypoints => waypoints;
        public int LaneCount => Mathf.Clamp(laneCount, 2, 6);
        public float LaneWidth => Mathf.Max(2.4f, laneWidth);
        public float TotalTrackWidth => LaneCount * LaneWidth;
        public float MaxOffRoadWidth => TotalTrackWidth + Mathf.Max(3f, shoulderExtraWidth) * 2f;

        public float ElevationAmplitude => elevationAmplitude;
        public float ElevationFrequency => elevationFrequency;
        public float ElevationSecondaryAmp => elevationSecondaryAmp;

        public int ObstacleCount => Mathf.Clamp(obstacleCount, 0, 60);
        public float ObstacleMinSpacingMeters => Mathf.Max(15f, obstacleMinSpacingMeters);
        public ObstacleKind[] AllowedObstacles => allowedObstacles;

        public bool EnablePoliceChase => enablePoliceChase;
        public float PoliceSpeedBonus => policeSpeedBonus;
        public float PoliceWarningRangeMeters => Mathf.Max(10f, policeWarningRangeMeters);
        public float AiCornerSpeedMultiplier => Mathf.Clamp(aiCornerSpeedMultiplier, 0.75f, 1.35f);
        public float RewardMultiplier => Mathf.Clamp(rewardMultiplier, 1.0f, 3.0f);
        public int RecommendedLaps => Mathf.Clamp(recommendedLaps, 1, 10);
        public MapTheme Theme => theme;
        public TrackFeatureZone[] FeatureZones => featureZones;

        public string GetDifficultyLabel()
        {
            return DifficultyStars switch
            {
                1 => "★☆☆☆☆ DỄ",
                2 => "★★☆☆☆ VỪA",
                3 => "★★★☆☆ KHÁ KHÓ",
                4 => "★★★★☆ KHÓ",
                _ => "★★★★★ CỰC GẮT"
            };
        }

        public static TrackDefinition CreateRuntime(
            string id,
            string title,
            Vector2[] trackWaypoints,
            int lanes,
            float widthPerLane,
            MapTheme mapTheme,
            int stars = 2,
            float rewardMult = 1.0f,
            int obstacles = 12,
            TrackFeatureZone[] zones = null)
        {
            TrackDefinition def = CreateInstance<TrackDefinition>();
            def.mapId = id;
            def.displayName = title;
            def.waypoints = trackWaypoints ?? GetDefaultCityWaypoints();
            def.laneCount = Mathf.Clamp(lanes, 2, 6);
            def.laneWidth = Mathf.Max(2.4f, widthPerLane);
            def.theme = mapTheme;
            def.difficultyStars = Mathf.Clamp(stars, 1, 5);
            def.rewardMultiplier = Mathf.Clamp(rewardMult, 1.0f, 3.0f);
            def.obstacleCount = Mathf.Clamp(obstacles, 0, 60);
            def.featureZones = zones ?? Array.Empty<TrackFeatureZone>();
            def.unlockedByDefault = true;
            return def;
        }

        public static TrackDefinition CreateCityMorningPreset(MapTheme cityTheme)
        {
            TrackDefinition def = CreateRuntime(
                "city_morning",
                "Phố Sáng Ban Mai",
                GetDefaultCityWaypoints(),
                4,
                4.0f,
                cityTheme,
                stars: 2,
                rewardMult: 1.0f,
                obstacles: 10);
            def.description = "Đường phố 4 làn cân bằng, tầm nhìn sáng rõ, phù hợp luyện tập.";
            def.elevationAmplitude = 3.5f;
            def.elevationFrequency = 3.0f;
            def.elevationSecondaryAmp = 1.5f;
            def.obstacleMinSpacingMeters = 38f;
            def.enablePoliceChase = true;
            def.policeSpeedBonus = 3.8f;
            def.policeWarningRangeMeters = 40f;
            def.aiCornerSpeedMultiplier = 0.96f;
            def.recommendedLaps = 3;
            return def;
        }

        public static TrackDefinition CreateCoastalHighwayPreset(MapTheme coastalTheme)
        {
            TrackDefinition def = CreateRuntime(
                "coastal_highway",
                "Đại Lộ Ven Biển",
                GetCoastalWaypoints(),
                6,
                3.8f,
                coastalTheme,
                stars: 3,
                rewardMult: 1.3f,
                obstacles: 14);
            def.description = "Đại lộ rộng 6 làn tốc độ cao ven biển, cua dài mượt và nhiều xe tranh làn.";
            def.elevationAmplitude = 2.6f;
            def.elevationFrequency = 2.0f;
            def.elevationSecondaryAmp = 1.0f;
            def.obstacleMinSpacingMeters = 34f;
            def.allowedObstacles = new[]
            {
                ObstacleKind.TrafficCone,
                ObstacleKind.OilSlick,
                ObstacleKind.RoadBarrier
            };
            def.enablePoliceChase = true;
            def.policeSpeedBonus = 4.8f;
            def.policeWarningRangeMeters = 44f;
            def.aiCornerSpeedMultiplier = 1.05f;
            def.recommendedLaps = 3;
            return def;
        }

        public static TrackDefinition CreateNeonNightExpressPreset(MapTheme neonTheme)
        {
            TrackDefinition def = CreateRuntime(
                "neon_night",
                "Cao Tốc Đêm Neon",
                GetNeonNightWaypoints(),
                5,
                4.6f,
                neonTheme,
                stars: 5,
                rewardMult: 2.0f,
                obstacles: 20);
            def.description = "Cao tốc ban đêm 5 làn nhiều khúc cua chữ S gắt, dày vật cản và cảnh sát bám gắt.";
            def.elevationAmplitude = 4.6f;
            def.elevationFrequency = 4.0f;
            def.elevationSecondaryAmp = 2.0f;
            def.obstacleMinSpacingMeters = 26f;
            def.allowedObstacles = new[]
            {
                ObstacleKind.RoadBarrier,
                ObstacleKind.OilBarrel,
                ObstacleKind.OilSlick,
                ObstacleKind.TrafficCone
            };
            def.enablePoliceChase = true;
            def.policeSpeedBonus = 6.2f;
            def.policeWarningRangeMeters = 48f;
            def.aiCornerSpeedMultiplier = 1.12f;
            def.recommendedLaps = 3;
            return def;
        }

        public static TrackDefinition CreateCityPreset(MapTheme cityTheme)
        {
            return CreateCityDistrictPreset(cityTheme);
        }

        public static TrackDefinition CreateCityDistrictPreset(MapTheme cityDistrictTheme)
        {
            TrackDefinition def = CreateRuntime(
                "city_district",
                "Thành Phố Đêm Neon",
                GetCityDistrictWaypoints(),
                4,
                4.0f,
                cityDistrictTheme,
                stars: 4,
                rewardMult: 1.7f,
                obstacles: 18);
            def.description = "Đô thị ban đêm 4-6 làn với ngã tư góc gắt 90°, cầu vượt cao, đoạn hầm tối, biển neon và xe buýt đỗ.";
            def.elevationAmplitude = 1.6f;
            def.elevationFrequency = 2.0f;
            def.elevationSecondaryAmp = 0.8f;
            def.obstacleMinSpacingMeters = 28f;
            def.allowedObstacles = new[]
            {
                ObstacleKind.ParkedBus,
                ObstacleKind.TrafficCone,
                ObstacleKind.RoadBarrier,
                ObstacleKind.OilBarrel
            };
            def.enablePoliceChase = true;
            def.policeSpeedBonus = 5.4f;
            def.policeWarningRangeMeters = 46f;
            def.aiCornerSpeedMultiplier = 1.04f;
            def.recommendedLaps = 3;
            def.featureZones = new[]
            {
                // Ngã tư 1 (góc gắt phía đông-nam)
                new TrackFeatureZone { kind = TrackFeatureKind.Crosswalk, startNormalized = 0.135f, endNormalized = 0.160f, elevationBoostMeters = 0f, label = "Vạch qua đường Ngã tư 1" },
                // Cầu vượt trung tâm trên đại lộ dọc phía đông
                new TrackFeatureZone { kind = TrackFeatureKind.Overpass, startNormalized = 0.195f, endNormalized = 0.315f, elevationBoostMeters = 7.5f, label = "Cầu Vượt Đô Thị" },
                // Ngã tư 2 (góc gắt phía đông-bắc rẽ sang tây)
                new TrackFeatureZone { kind = TrackFeatureKind.Crosswalk, startNormalized = 0.345f, endNormalized = 0.370f, elevationBoostMeters = 0f, label = "Vạch qua đường Ngã tư 2" },
                // Đoạn hầm ngầm xuyên trung tâm phía tây
                new TrackFeatureZone { kind = TrackFeatureKind.Tunnel, startNormalized = 0.725f, endNormalized = 0.840f, elevationBoostMeters = 0f, label = "Hầm Chui Thành Phố" },
                // Ngã tư 3 (góc gắt cuối vòng về đường chính)
                new TrackFeatureZone { kind = TrackFeatureKind.Crosswalk, startNormalized = 0.870f, endNormalized = 0.895f, elevationBoostMeters = 0f, label = "Vạch qua đường Ngã tư 3" }
            };
            return def;
        }

        public static Vector2[] GetCityDistrictWaypoints()
        {
            // Mạch đường phố 26 waypoint (~1836m) dạng ô bàn cờ đô thị có các ngã tư góc gắt rõ rệt,
            // đại lộ thẳng dài phía đông cho Cầu vượt, cụm phố bắc-trung tâm và trục thẳng phía tây xuyên Hầm chui.
            return new[]
            {
                new Vector2(   0f,   0f),  // 0: Vạch xuất phát
                new Vector2( 115f,   0f),  // 1: Đại lộ Nam
                new Vector2( 235f,   0f),  // 2: Tiệm cận ngã tư 1
                new Vector2( 270f,  14f),  // 3: Ngã tư góc gắt 1 (rẽ lên Bắc)
                new Vector2( 285f,  50f),  // 4: Vào đại lộ Đông
                new Vector2( 285f, 155f),  // 5: Đoạn Cầu vượt
                new Vector2( 285f, 265f),  // 6: Đỉnh Cầu vượt
                new Vector2( 285f, 355f),  // 7: Tiệm cận ngã tư 2
                new Vector2( 270f, 388f),  // 8: Ngã tư góc gắt 2 (rẽ sang Tây)
                new Vector2( 235f, 402f),  // 9: Ra đại lộ Bắc
                new Vector2( 145f, 402f),  // 10: Đại lộ Bắc (đoạn Đông)
                new Vector2(  75f, 402f),  // 11: Rẽ vào khu phố trung tâm
                new Vector2(  62f, 468f),  // 12: Khúc bẻ góc khu phố Bắc
                new Vector2(  -8f, 480f),  // 13: Phố Bắc trung tâm
                new Vector2( -75f, 468f),  // 14: Khúc bẻ góc Tây-Bắc
                new Vector2( -88f, 402f),  // 15: Nhập lại đại lộ Bắc
                new Vector2(-175f, 402f),  // 16: Tiệm cận ngã tư Tây-Bắc
                new Vector2(-208f, 386f),  // 17: Góc rẽ xuống Nam
                new Vector2(-222f, 350f),  // 18: Vào đại lộ Tây
                new Vector2(-222f, 250f),  // 19: Cửa Hầm chui
                new Vector2(-222f, 145f),  // 20: Giữa Hầm chui
                new Vector2(-222f,  50f),  // 21: Ra khỏi Hầm chui
                new Vector2(-208f,  14f),  // 22: Ngã tư góc gắt 3 (rẽ về đường chính)
                new Vector2(-175f,   0f),  // 23: Vào đại lộ Nam-Tây
                new Vector2(-105f,   0f),  // 24: Đường về đích
                new Vector2( -50f,   0f),  // 25: Đoạn thẳng cuối vòng
            };
        }

        public static TrackDefinition CreateValleyDefaultPreset(MapTheme valleyTheme)
        {
            TrackDefinition def = CreateCityMorningPreset(valleyTheme);
            def.mapId = "valley_default";
            def.displayName = "Thung Lũng Cổ Điển";
            def.description = "Đường đua 22 waypoint gốc rộng 16m (4 làn x 4m).";
            return def;
        }

        public static Vector2[] GetDefaultValleyWaypoints()
        {
            return new[]
            {
                new Vector2(  0f,   0f),
                new Vector2( 90f,   0f),
                new Vector2(180f,   0f),
                new Vector2(260f,  15f),
                new Vector2(320f,  60f),
                new Vector2(345f, 125f),
                new Vector2(325f, 195f),
                new Vector2(270f, 240f),
                new Vector2(200f, 255f),
                new Vector2(145f, 220f),
                new Vector2(115f, 165f),
                new Vector2( 70f, 145f),
                new Vector2( 20f, 175f),
                new Vector2(-25f, 235f),
                new Vector2(-95f, 265f),
                new Vector2(-175f, 245f),
                new Vector2(-225f, 185f),
                new Vector2(-235f, 110f),
                new Vector2(-200f,  45f),
                new Vector2(-140f,   8f),
                new Vector2( -75f,  -4f),
                new Vector2( -35f,   0f),
            };
        }

        public static Vector2[] GetDefaultCityWaypoints()
        {
            return GetDefaultValleyWaypoints();
        }

        public static Vector2[] GetCoastalWaypoints()
        {
            return new[]
            {
                new Vector2(  0f,   0f),
                new Vector2(120f,   0f),
                new Vector2(250f,  10f),
                new Vector2(360f,  55f),
                new Vector2(420f, 140f),
                new Vector2(410f, 240f),
                new Vector2(340f, 315f),
                new Vector2(230f, 345f),
                new Vector2(115f, 330f),
                new Vector2(  0f, 295f),
                new Vector2(-120f, 315f),
                new Vector2(-235f, 285f),
                new Vector2(-315f, 205f),
                new Vector2(-335f, 110f),
                new Vector2(-290f,  35f),
                new Vector2(-195f,  -5f),
                new Vector2( -95f,  -4f),
                new Vector2( -40f,   0f),
            };
        }

        public static Vector2[] GetNeonNightWaypoints()
        {
            return new[]
            {
                new Vector2(  0f,   0f),
                new Vector2(105f,   0f),
                new Vector2(215f,  18f),
                new Vector2(300f,  78f),
                new Vector2(335f, 165f),
                new Vector2(280f, 240f),
                new Vector2(195f, 235f),
                new Vector2(145f, 160f),
                new Vector2( 85f, 125f),
                new Vector2( 25f, 170f),
                new Vector2(  0f, 255f),
                new Vector2(-70f, 310f),
                new Vector2(-165f, 290f),
                new Vector2(-225f, 215f),
                new Vector2(-180f, 135f),
                new Vector2(-235f,  75f),
                new Vector2(-195f,  10f),
                new Vector2(-105f, -12f),
                new Vector2( -45f,  -2f),
            };
        }
    }
}
