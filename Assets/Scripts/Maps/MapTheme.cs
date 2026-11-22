using System;
using UnityEngine;

namespace RacingGame
{
    public enum HorizonSilhouetteStyle
    {
        ValleyMountains = 0,
        CoastalHills = 1,
        NeonCitySkyline = 2
    }

    [Serializable]
    public class MapZonePropConfig
    {
        public string zoneName = "Khu vực";
        [Range(0.3f, 2.0f)]
        public float propDensityMultiplier = 1.0f;
        public RoadsidePropKind[] leftProps =
        {
            RoadsidePropKind.StreetLamp,
            RoadsidePropKind.FencePost,
            RoadsidePropKind.Billboard
        };
        public RoadsidePropKind[] rightProps =
        {
            RoadsidePropKind.Tree,
            RoadsidePropKind.MilePost,
            RoadsidePropKind.Billboard
        };

        [Header("Sprite Prop Tùy Chỉnh (Kéo thả từ Project nếu muốn)")]
        public Sprite[] customLeftSprites;
        public Sprite[] customRightSprites;
    }

    [Serializable]
    public struct PropSpriteOverride
    {
        public RoadsidePropKind kind;
        public Sprite sprite;
    }

    /// <summary>
    /// Chủ đề hình ảnh và cảnh vật của một Map đua xe:
    /// gồm bảng màu bầu trời/địa hình/mặt đường, kiểu đường chân trời và danh sách prop theo vùng.
    /// </summary>
    [CreateAssetMenu(fileName = "MapTheme", menuName = "RacingGame/Map Theme")]
    public class MapTheme : ScriptableObject
    {
        [Header("Định danh Theme")]
        [SerializeField] private string themeId = "city_morning";
        [SerializeField] private string themeName = "Phố Sáng Ban Mai";

        [Header("Bầu Trời & Mặt Trời / Sao Đêm")]
        [SerializeField] private Color skyTop = RoadRashVisualTheme.SkyTop;
        [SerializeField] private Color skyMid = RoadRashVisualTheme.SkyMid;
        [SerializeField] private Color skyHorizon = RoadRashVisualTheme.SkyHorizon;
        [SerializeField] private Color sunGlow = RoadRashVisualTheme.SunGlow;
        [SerializeField] private bool showSun = true;
        [SerializeField] private float sunScale = 1.0f;
        [SerializeField] private bool showStars;
        [SerializeField] private HorizonSilhouetteStyle horizonStyle = HorizonSilhouetteStyle.ValleyMountains;

        [Header("Núi Xa & Đồi Cận Cảnh")]
        [SerializeField] private Color farRidgeTop = RoadRashVisualTheme.FarRidgeTop;
        [SerializeField] private Color farRidgeBase = RoadRashVisualTheme.FarRidgeBase;
        [SerializeField] private Color midHillLight = RoadRashVisualTheme.MidHillLight;
        [SerializeField] private Color midHillDark = RoadRashVisualTheme.MidHillDark;

        [Header("Lề Đường & Địa Hình Hai Bên")]
        [SerializeField] private Color terrainEarthLight = RoadRashVisualTheme.TerrainEarthLight;
        [SerializeField] private Color terrainEarthDark = RoadRashVisualTheme.TerrainEarthDark;
        [SerializeField] private Color terrainGrassLight = RoadRashVisualTheme.TerrainGrassLight;
        [SerializeField] private Color terrainGrassDark = RoadRashVisualTheme.TerrainGrassDark;
        [SerializeField] private Color shoulderLight = RoadRashVisualTheme.ShoulderLight;
        [SerializeField] private Color shoulderDark = RoadRashVisualTheme.ShoulderDark;

        [Header("Mặt Đường & Vạch Kẻ Làn")]
        [SerializeField] private Color asphaltLight = RoadRashVisualTheme.AsphaltLight;
        [SerializeField] private Color asphaltDark = RoadRashVisualTheme.AsphaltDark;
        [SerializeField] private Color centerYellowLine = RoadRashVisualTheme.CenterYellowLine;
        [SerializeField] private Color laneWhiteLine = RoadRashVisualTheme.LaneWhiteLine;
        [SerializeField] private Color startCheckDark = RoadRashVisualTheme.StartCheckDark;
        [SerializeField] private Color startCheckLight = RoadRashVisualTheme.StartCheckLight;
        [Range(0.25f, 1.0f)]
        [SerializeField] private float fogDensity = 0.85f;

        [Header("Cảnh Vật Theo Vùng (Environment Props)")]
        [Min(4f)]
        [SerializeField] private float slotIntervalMeters = 9f;
        [SerializeField] private MapZonePropConfig[] zones = Array.Empty<MapZonePropConfig>();

        [Header("Sprite Override Theo Đúng Loại Prop")]
        [SerializeField] private PropSpriteOverride[] propSpriteOverrides = Array.Empty<PropSpriteOverride>();

        public string ThemeId => themeId;
        public string ThemeName => themeName;
        public Color SkyTop => skyTop;
        public Color SkyMid => skyMid;
        public Color SkyHorizon => skyHorizon;
        public Color SunGlow => sunGlow;
        public bool ShowSun => showSun;
        public float SunScale => Mathf.Max(0.2f, sunScale);
        public bool ShowStars => showStars;
        public HorizonSilhouetteStyle HorizonStyle => horizonStyle;
        public Color FarRidgeTop => farRidgeTop;
        public Color FarRidgeBase => farRidgeBase;
        public Color MidHillLight => midHillLight;
        public Color MidHillDark => midHillDark;
        public Color TerrainEarthLight => terrainEarthLight;
        public Color TerrainEarthDark => terrainEarthDark;
        public Color TerrainGrassLight => terrainGrassLight;
        public Color TerrainGrassDark => terrainGrassDark;
        public Color ShoulderLight => shoulderLight;
        public Color ShoulderDark => shoulderDark;
        public Color AsphaltLight => asphaltLight;
        public Color AsphaltDark => asphaltDark;
        public Color CenterYellowLine => centerYellowLine;
        public Color LaneWhiteLine => laneWhiteLine;
        public Color StartCheckDark => startCheckDark;
        public Color StartCheckLight => startCheckLight;
        public float FogDensity => Mathf.Clamp(fogDensity, 0.25f, 1.0f);
        public float SlotIntervalMeters => Mathf.Max(4f, slotIntervalMeters);
        public MapZonePropConfig[] Zones => zones;
        public PropSpriteOverride[] PropSpriteOverrides => propSpriteOverrides;

        public Sprite PickOverrideSpriteForKind(RoadsidePropKind kind, System.Random rng)
        {
            if (propSpriteOverrides == null || propSpriteOverrides.Length == 0)
            {
                return null;
            }

            int matchCount = 0;
            for (int i = 0; i < propSpriteOverrides.Length; i++)
            {
                if (propSpriteOverrides[i].kind == kind && propSpriteOverrides[i].sprite != null)
                {
                    matchCount++;
                }
            }

            if (matchCount == 0) return null;

            int pickIndex = matchCount == 1 || rng == null ? 0 : rng.Next(0, matchCount);
            int seen = 0;
            for (int i = 0; i < propSpriteOverrides.Length; i++)
            {
                if (propSpriteOverrides[i].kind == kind && propSpriteOverrides[i].sprite != null)
                {
                    if (seen == pickIndex)
                    {
                        return propSpriteOverrides[i].sprite;
                    }
                    seen++;
                }
            }

            return null;
        }

        public void SetPropSpriteOverrides(PropSpriteOverride[] overrides)
        {
            propSpriteOverrides = overrides ?? Array.Empty<PropSpriteOverride>();
        }

        /// <summary>
        /// Tạo bản chuẩn thung lũng ban đầu tương đương 100% bảng màu RoadRashVisualTheme.
        /// </summary>
        public static MapTheme CreateValleyPreset()
        {
            MapTheme theme = CreateCityMorningPreset();
            theme.themeId = "valley";
            theme.themeName = "MapTheme_Valley";
            theme.fogDensity = 0.86f;
            theme.startCheckDark = RoadRashVisualTheme.StartCheckDark;
            theme.startCheckLight = RoadRashVisualTheme.StartCheckLight;
            return theme;
        }

        public static MapTheme CreateCityMorningPreset()
        {
            MapTheme theme = CreateInstance<MapTheme>();
            theme.themeId = "city_morning";
            theme.themeName = "Phố Sáng Ban Mai";
            theme.skyTop = RoadRashVisualTheme.SkyTop;
            theme.skyMid = RoadRashVisualTheme.SkyMid;
            theme.skyHorizon = RoadRashVisualTheme.SkyHorizon;
            theme.sunGlow = RoadRashVisualTheme.SunGlow;
            theme.showSun = true;
            theme.sunScale = 1.0f;
            theme.showStars = false;
            theme.horizonStyle = HorizonSilhouetteStyle.ValleyMountains;
            theme.farRidgeTop = RoadRashVisualTheme.FarRidgeTop;
            theme.farRidgeBase = RoadRashVisualTheme.FarRidgeBase;
            theme.midHillLight = RoadRashVisualTheme.MidHillLight;
            theme.midHillDark = RoadRashVisualTheme.MidHillDark;
            theme.terrainEarthLight = RoadRashVisualTheme.TerrainEarthLight;
            theme.terrainEarthDark = RoadRashVisualTheme.TerrainEarthDark;
            theme.terrainGrassLight = RoadRashVisualTheme.TerrainGrassLight;
            theme.terrainGrassDark = RoadRashVisualTheme.TerrainGrassDark;
            theme.shoulderLight = RoadRashVisualTheme.ShoulderLight;
            theme.shoulderDark = RoadRashVisualTheme.ShoulderDark;
            theme.asphaltLight = RoadRashVisualTheme.AsphaltLight;
            theme.asphaltDark = RoadRashVisualTheme.AsphaltDark;
            theme.centerYellowLine = RoadRashVisualTheme.CenterYellowLine;
            theme.laneWhiteLine = RoadRashVisualTheme.LaneWhiteLine;
            theme.fogDensity = 0.84f;
            theme.slotIntervalMeters = 9f;
            theme.zones = new[]
            {
                new MapZonePropConfig
                {
                    zoneName = "Khu Phố Đô Thị",
                    propDensityMultiplier = 1.0f,
                    leftProps = new[] { RoadsidePropKind.StreetLamp, RoadsidePropKind.FencePost, RoadsidePropKind.Billboard },
                    rightProps = new[] { RoadsidePropKind.Tree, RoadsidePropKind.MilePost, RoadsidePropKind.Billboard }
                },
                new MapZonePropConfig
                {
                    zoneName = "Công Viên Ven Đường",
                    propDensityMultiplier = 1.0f,
                    leftProps = new[] { RoadsidePropKind.UtilityPole, RoadsidePropKind.Tree, RoadsidePropKind.FencePost },
                    rightProps = new[] { RoadsidePropKind.Tree, RoadsidePropKind.StreetLamp, RoadsidePropKind.MilePost }
                }
            };
            return theme;
        }

        public static MapTheme CreateCoastalHighwayPreset()
        {
            MapTheme theme = CreateInstance<MapTheme>();
            theme.themeId = "coastal_highway";
            theme.themeName = "Đại Lộ Ven Biển";
            theme.skyTop = new Color(0.05f, 0.36f, 0.84f);
            theme.skyMid = new Color(0.22f, 0.64f, 0.94f);
            theme.skyHorizon = new Color(0.76f, 0.90f, 0.98f);
            theme.sunGlow = new Color(1.00f, 0.92f, 0.56f);
            theme.showSun = true;
            theme.sunScale = 1.35f;
            theme.showStars = false;
            theme.horizonStyle = HorizonSilhouetteStyle.CoastalHills;
            theme.farRidgeTop = new Color(0.20f, 0.52f, 0.74f);
            theme.farRidgeBase = new Color(0.14f, 0.42f, 0.65f);
            theme.midHillLight = new Color(0.74f, 0.66f, 0.44f);
            theme.midHillDark = new Color(0.58f, 0.50f, 0.32f);
            theme.terrainEarthLight = new Color(0.84f, 0.75f, 0.52f);
            theme.terrainEarthDark = new Color(0.76f, 0.67f, 0.45f);
            theme.terrainGrassLight = new Color(0.76f, 0.70f, 0.46f);
            theme.terrainGrassDark = new Color(0.68f, 0.62f, 0.39f);
            theme.shoulderLight = new Color(0.78f, 0.72f, 0.60f);
            theme.shoulderDark = new Color(0.70f, 0.64f, 0.52f);
            theme.asphaltLight = new Color(0.28f, 0.30f, 0.34f);
            theme.asphaltDark = new Color(0.24f, 0.26f, 0.30f);
            theme.centerYellowLine = new Color(0.98f, 0.78f, 0.14f);
            theme.laneWhiteLine = new Color(0.95f, 0.97f, 1.00f);
            theme.fogDensity = 0.72f;
            theme.slotIntervalMeters = 9.5f;
            theme.zones = new[]
            {
                new MapZonePropConfig
                {
                    zoneName = "Bờ Biển Hàng Dừa",
                    propDensityMultiplier = 1.05f,
                    leftProps = new[] { RoadsidePropKind.PalmTree, RoadsidePropKind.Billboard, RoadsidePropKind.MilePost },
                    rightProps = new[] { RoadsidePropKind.PalmTree, RoadsidePropKind.StreetLamp, RoadsidePropKind.MilePost }
                },
                new MapZonePropConfig
                {
                    zoneName = "Cầu Cảng & Resort",
                    propDensityMultiplier = 1.0f,
                    leftProps = new[] { RoadsidePropKind.StreetLamp, RoadsidePropKind.PalmTree, RoadsidePropKind.FencePost },
                    rightProps = new[] { RoadsidePropKind.Billboard, RoadsidePropKind.PalmTree, RoadsidePropKind.MilePost }
                }
            };
            return theme;
        }

        public static MapTheme CreateNeonNightExpressPreset()
        {
            MapTheme theme = CreateInstance<MapTheme>();
            theme.themeId = "neon_night";
            theme.themeName = "Cao Tốc Đêm Neon";
            theme.skyTop = new Color(0.03f, 0.04f, 0.14f);
            theme.skyMid = new Color(0.08f, 0.09f, 0.26f);
            theme.skyHorizon = new Color(0.22f, 0.14f, 0.38f);
            theme.sunGlow = new Color(0.88f, 0.36f, 0.96f);
            theme.showSun = false;
            theme.sunScale = 0.9f;
            theme.showStars = true;
            theme.horizonStyle = HorizonSilhouetteStyle.NeonCitySkyline;
            theme.farRidgeTop = new Color(0.14f, 0.14f, 0.30f);
            theme.farRidgeBase = new Color(0.07f, 0.07f, 0.18f);
            theme.midHillLight = new Color(0.18f, 0.12f, 0.32f);
            theme.midHillDark = new Color(0.09f, 0.08f, 0.20f);
            theme.terrainEarthLight = new Color(0.14f, 0.13f, 0.20f);
            theme.terrainEarthDark = new Color(0.10f, 0.09f, 0.16f);
            theme.terrainGrassLight = new Color(0.11f, 0.16f, 0.22f);
            theme.terrainGrassDark = new Color(0.08f, 0.12f, 0.18f);
            theme.shoulderLight = new Color(0.24f, 0.24f, 0.30f);
            theme.shoulderDark = new Color(0.19f, 0.19f, 0.25f);
            theme.asphaltLight = new Color(0.14f, 0.15f, 0.19f);
            theme.asphaltDark = new Color(0.11f, 0.12f, 0.16f);
            theme.centerYellowLine = new Color(0.20f, 0.94f, 0.98f);
            theme.laneWhiteLine = new Color(0.92f, 0.96f, 1.00f);
            theme.fogDensity = 0.68f;
            theme.slotIntervalMeters = 8.0f;
            theme.zones = new[]
            {
                new MapZonePropConfig
                {
                    zoneName = "Đại Lộ Đèn Cao Áp",
                    propDensityMultiplier = 1.15f,
                    leftProps = new[] { RoadsidePropKind.StreetLamp, RoadsidePropKind.Billboard, RoadsidePropKind.StreetLamp },
                    rightProps = new[] { RoadsidePropKind.StreetLamp, RoadsidePropKind.UtilityPole, RoadsidePropKind.Billboard }
                },
                new MapZonePropConfig
                {
                    zoneName = "Cầu Vượt Đô Thị Đêm",
                    propDensityMultiplier = 1.10f,
                    leftProps = new[] { RoadsidePropKind.StreetLamp, RoadsidePropKind.FencePost, RoadsidePropKind.Billboard },
                    rightProps = new[] { RoadsidePropKind.StreetLamp, RoadsidePropKind.MilePost, RoadsidePropKind.FencePost }
                }
            };
            return theme;
        }

        /// <summary>
        /// Tạo chủ đề Thành phố ban đêm Neon kiểu Road Rash PS1 cho Map Thành phố.
        /// </summary>
        public static MapTheme CreateCityPreset()
        {
            return CreateCityDistrictPreset();
        }

        /// <summary>
        /// Tạo chủ đề Thành phố ban đêm Neon kiểu Road Rash PS1 cho Map Thành phố.
        /// </summary>
        public static MapTheme CreateCityDistrictPreset()
        {
            MapTheme theme = CreateInstance<MapTheme>();
            theme.themeId = "city_district";
            theme.themeName = "MapTheme_City";
            theme.skyTop = new Color(0.04f, 0.03f, 0.14f);
            theme.skyMid = new Color(0.11f, 0.08f, 0.28f);
            theme.skyHorizon = new Color(0.30f, 0.16f, 0.44f);
            theme.sunGlow = new Color(1.00f, 0.34f, 0.78f);
            theme.showSun = false;
            theme.sunScale = 1.0f;
            theme.showStars = true;
            theme.horizonStyle = HorizonSilhouetteStyle.NeonCitySkyline;
            theme.farRidgeTop = new Color(0.16f, 0.17f, 0.34f);
            theme.farRidgeBase = new Color(0.07f, 0.07f, 0.16f);
            theme.midHillLight = new Color(0.22f, 0.14f, 0.38f);
            theme.midHillDark = new Color(0.10f, 0.09f, 0.20f);
            theme.terrainEarthLight = new Color(0.16f, 0.16f, 0.22f);
            theme.terrainEarthDark = new Color(0.12f, 0.12f, 0.18f);
            theme.terrainGrassLight = new Color(0.18f, 0.20f, 0.26f);
            theme.terrainGrassDark = new Color(0.14f, 0.16f, 0.22f);
            theme.shoulderLight = new Color(0.34f, 0.35f, 0.42f);
            theme.shoulderDark = new Color(0.27f, 0.28f, 0.34f);
            theme.asphaltLight = new Color(0.13f, 0.14f, 0.18f);
            theme.asphaltDark = new Color(0.10f, 0.11f, 0.15f);
            theme.centerYellowLine = new Color(0.98f, 0.80f, 0.16f);
            theme.laneWhiteLine = new Color(0.92f, 0.96f, 1.00f);
            theme.startCheckDark = RoadRashVisualTheme.StartCheckDark;
            theme.startCheckLight = RoadRashVisualTheme.StartCheckLight;
            theme.fogDensity = 0.72f;
            theme.slotIntervalMeters = 8.0f;
            theme.zones = new[]
            {
                new MapZonePropConfig
                {
                    zoneName = "Khu Cao Ốc Trung Tâm",
                    propDensityMultiplier = 1.15f,
                    leftProps = new[] { RoadsidePropKind.SkyscraperTower, RoadsidePropKind.CityBuilding, RoadsidePropKind.StreetLamp, RoadsidePropKind.NeonSign },
                    rightProps = new[] { RoadsidePropKind.SkyscraperTower, RoadsidePropKind.CityBuilding, RoadsidePropKind.TrafficLight, RoadsidePropKind.NeonSign }
                },
                new MapZonePropConfig
                {
                    zoneName = "Ngã Tư Phố Đêm Neon",
                    propDensityMultiplier = 1.15f,
                    leftProps = new[] { RoadsidePropKind.TrafficLight, RoadsidePropKind.NeonSign, RoadsidePropKind.VendingMachine, RoadsidePropKind.FireHydrant },
                    rightProps = new[] { RoadsidePropKind.NeonSign, RoadsidePropKind.BusStop, RoadsidePropKind.Billboard, RoadsidePropKind.StreetLamp }
                },
                new MapZonePropConfig
                {
                    zoneName = "Đại Lộ Cửa Hàng & Cao Tầng",
                    propDensityMultiplier = 1.10f,
                    leftProps = new[] { RoadsidePropKind.CityBuilding, RoadsidePropKind.VendingMachine, RoadsidePropKind.StreetLamp, RoadsidePropKind.SkyscraperTower },
                    rightProps = new[] { RoadsidePropKind.CityBuilding, RoadsidePropKind.BusStop, RoadsidePropKind.NeonSign, RoadsidePropKind.FireHydrant }
                },
                new MapZonePropConfig
                {
                    zoneName = "Trục Cao Tốc & Cầu Vượt Đô Thị",
                    propDensityMultiplier = 1.05f,
                    leftProps = new[] { RoadsidePropKind.HighwaySign, RoadsidePropKind.StreetLamp, RoadsidePropKind.SkyscraperTower, RoadsidePropKind.Billboard },
                    rightProps = new[] { RoadsidePropKind.HighwaySign, RoadsidePropKind.StreetLamp, RoadsidePropKind.CityBuilding, RoadsidePropKind.NeonSign }
                }
            };
            return theme;
        }
    }
}
