using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    public struct RoadsideDecoration
    {
        public float TrackDistance;
        public float SideSign; // -1 = left roadside, +1 = right roadside
        public float ExtraShoulderOffset;
        public float ScaleMultiplier;
        public RoadsidePropKind Kind;
        public Sprite CustomOverrideSprite;
    }

    /// <summary>
    /// Sinh cảnh vật và prop trang trí xung quanh đường đua (đèn đường, biển quảng cáo,
    /// cây cọ, cây xanh, cọc tiêu, cột điện) theo từng phân khu sinh động.
    /// Cho phép cấu hình mật độ và gắn thêm Sprite tùy chỉnh trên Unity Inspector.
    /// </summary>
    public class EnvironmentDecorator : MonoBehaviour
    {
        [Header("Cấu hình Sinh Cảnh Vật (Environment)")]
        [SerializeField] private bool enableExtraProps = true;
        [Min(4f)]
        [SerializeField] private float slotIntervalMeters = 9f;
        [Range(0.2f, 2.0f)]
        [SerializeField] private float propDensity = 1.0f;
        [SerializeField] private int environmentSeed = 1995;

        [Header("Tài nguyên Prop Tùy Chỉnh (Kéo thả thêm Sprite cảnh vật nếu muốn)")]
        [SerializeField] private Sprite[] customRoadsideSprites;

        private readonly List<RoadsideDecoration> _decorations = new List<RoadsideDecoration>();
        private float _cachedTrackLength = -1f;
        private MapTheme _cachedTheme;

        public bool EnableExtraProps => enableExtraProps;
        public float SlotIntervalMeters => Mathf.Max(4f, slotIntervalMeters);
        public MapTheme ActiveTheme => _cachedTheme;
        public IReadOnlyList<RoadsideDecoration> Decorations => _decorations;

        public void SetEnableExtraProps(bool enabled)
        {
            enableExtraProps = enabled;
        }

        public void RebuildEnvironment(WaypointTrack track, int? customSeed = null)
        {
            RebuildEnvironment(track, track != null ? track.ActiveTheme : null, customSeed);
        }

        public void RebuildEnvironment(WaypointTrack track, MapTheme theme, int? customSeed = null)
        {
            _decorations.Clear();
            _cachedTheme = theme;
            if (track == null || track.TotalLength <= 1f) return;

            int activeSeed = customSeed ?? environmentSeed;
            var rng = new System.Random(activeSeed);
            float totalLength = track.TotalLength;
            _cachedTrackLength = totalLength;

            float baseInterval = theme != null ? theme.SlotIntervalMeters : SlotIntervalMeters;
            float baseStep = Mathf.Max(4f, baseInterval / Mathf.Clamp(propDensity, 0.2f, 2f));

            MapZonePropConfig[] themeZones = theme != null ? theme.Zones : null;
            bool hasThemeZones = themeZones != null && themeZones.Length > 0;
            int zoneCount = hasThemeZones ? themeZones.Length : 3;

            float s = 0f;
            for (int slot = 0; s < totalLength; slot++)
            {
                int zone = GetZoneIndexAtDistance(s, totalLength, zoneCount);
                MapZonePropConfig zoneCfg = hasThemeZones ? themeZones[zone] : null;
                float rawDensity = zoneCfg != null &&
                                   !float.IsNaN(zoneCfg.propDensityMultiplier) &&
                                   !float.IsInfinity(zoneCfg.propDensityMultiplier)
                    ? zoneCfg.propDensityMultiplier
                    : 1.0f;
                float zoneDensity = Mathf.Clamp(rawDensity, 0.3f, 2.0f);
                float zoneStep = Mathf.Max(2f, baseStep / zoneDensity);

                bool hasFeature = track.TryGetFeatureAtDistance(s, out TrackFeatureKind featureKind, out _);
                if (!hasFeature || featureKind != TrackFeatureKind.Tunnel)
                {
                    TrackSample sample = track.SampleAtDistance(s);
                    bool isCrosswalkApproach = hasFeature && featureKind == TrackFeatureKind.Crosswalk;
                    if (!isCrosswalkApproach && track.TryGetFeatureAtDistance(s + 18f, out TrackFeatureKind aheadKind, out _))
                    {
                        isCrosswalkApproach = aheadKind == TrackFeatureKind.Crosswalk;
                    }

                    // Bên trái đường
                    RoadsidePropKind leftKind = isCrosswalkApproach
                        ? ((slot % 2) == 0 ? RoadsidePropKind.TrafficLight : RoadsidePropKind.NeonSign)
                        : ChoosePropForSide(zoneCfg, zone, true, slot, sample.curvature, rng);
                    Sprite customLeft = (theme != null ? theme.PickOverrideSpriteForKind(leftKind, rng) : null)
                        ?? PickThemeCustomSprite(zoneCfg, true, rng)
                        ?? PickCustomSprite(slot, rng);
                    _decorations.Add(new RoadsideDecoration
                    {
                        TrackDistance = s,
                        SideSign = -1f,
                        ExtraShoulderOffset = GetLateralPadding(leftKind),
                        ScaleMultiplier = GetPropScale(leftKind),
                        Kind = leftKind,
                        CustomOverrideSprite = customLeft
                    });

                    // Bên phải đường
                    if ((slot % 2) == 0 || isCrosswalkApproach || Mathf.Abs(sample.curvature) > 0.012f)
                    {
                        RoadsidePropKind rightKind = isCrosswalkApproach
                            ? RoadsidePropKind.TrafficLight
                            : ChoosePropForSide(zoneCfg, zone, false, slot, sample.curvature, rng);
                        Sprite customRight = (theme != null ? theme.PickOverrideSpriteForKind(rightKind, rng) : null)
                            ?? PickThemeCustomSprite(zoneCfg, false, rng)
                            ?? PickCustomSprite(slot + 17, rng);
                        _decorations.Add(new RoadsideDecoration
                        {
                            TrackDistance = s,
                            SideSign = 1f,
                            ExtraShoulderOffset = GetLateralPadding(rightKind),
                            ScaleMultiplier = GetPropScale(rightKind),
                            Kind = rightKind,
                            CustomOverrideSprite = customRight
                        });
                    }
                }

                s += zoneStep;
            }
        }

        /// <summary>
        /// Maps a distance along the track proportionally to a zone index in [0, zoneCount - 1].
        /// </summary>
        public static int GetZoneIndexAtDistance(float distance, float totalLength, int zoneCount)
        {
            if (zoneCount <= 1 || totalLength <= 0.001f) return 0;
            float normalized = Mathf.Clamp01(distance / totalLength);
            return Mathf.Min(zoneCount - 1, Mathf.FloorToInt(normalized * zoneCount));
        }

        public void EnsureBuiltForTrack(WaypointTrack track)
        {
            if (track == null) return;
            MapTheme targetTheme = track.ActiveTheme;
            if (_decorations.Count == 0 ||
                Mathf.Abs(_cachedTrackLength - track.TotalLength) > 0.5f ||
                _cachedTheme != targetTheme)
            {
                RebuildEnvironment(track, targetTheme);
            }
        }

        private static RoadsidePropKind ChoosePropForSide(
            MapZonePropConfig zoneCfg,
            int zoneIndex,
            bool isLeft,
            int slotIndex,
            float curvature,
            System.Random rng)
        {
            // Khúc cua gắt ưu tiên đặt biển mũi tên cảnh báo ở mép ngoài cua
            if (Mathf.Abs(curvature) > 0.012f && (slotIndex % 2) == 0)
            {
                bool rightTurn = curvature > 0f;
                if ((rightTurn && isLeft) || (!rightTurn && !isLeft))
                {
                    return rightTurn ? RoadsidePropKind.ChevronRight : RoadsidePropKind.ChevronLeft;
                }
            }

            if (zoneCfg != null)
            {
                RoadsidePropKind[] pool = isLeft ? zoneCfg.leftProps : zoneCfg.rightProps;
                if (pool != null && pool.Length > 0)
                {
                    return pool[slotIndex % pool.Length];
                }
            }

            int fallbackZone = ((zoneIndex % 3) + 3) % 3;
            return ChooseZoneProp(fallbackZone, isLeft, slotIndex, curvature, rng);
        }

        private static Sprite PickThemeCustomSprite(MapZonePropConfig zoneCfg, bool isLeft, System.Random rng)
        {
            if (zoneCfg == null) return null;
            Sprite[] pool = isLeft ? zoneCfg.customLeftSprites : zoneCfg.customRightSprites;
            if (pool == null || pool.Length == 0) return null;
            return pool[rng.Next(0, pool.Length)];
        }

        public static RoadsidePropKind ChooseZoneProp(int zone, bool isLeft, int slotIndex, float curvature, System.Random rng)
        {
            // Khúc cua gắt ưu tiên đặt biển mũi tên cảnh báo ở mép ngoài cua
            if (Mathf.Abs(curvature) > 0.012f && (slotIndex % 2) == 0)
            {
                bool rightTurn = curvature > 0f;
                if ((rightTurn && isLeft) || (!rightTurn && !isLeft))
                {
                    return rightTurn ? RoadsidePropKind.ChevronRight : RoadsidePropKind.ChevronLeft;
                }
            }

            switch (zone)
            {
                case 0: // Khu phố đô thị: Đèn cao áp, Biển quảng cáo, Hàng rào
                    if ((slotIndex % 5) == 0) return RoadsidePropKind.Billboard;
                    if ((slotIndex % 2) == 0) return RoadsidePropKind.StreetLamp;
                    return RoadsidePropKind.FencePost;

                case 1: // Đại lộ cây cọ: Cây cọ, Cột mốc, Biển quảng cáo
                    if ((slotIndex % 6) == 0) return RoadsidePropKind.Billboard;
                    if ((slotIndex % 2) == 0) return RoadsidePropKind.PalmTree;
                    return RoadsidePropKind.MilePost;

                default: // Ngoại ô: Cột điện, Cây cổ thụ, Hàng rào gỗ
                    if (isLeft && (slotIndex % 2) == 0) return RoadsidePropKind.UtilityPole;
                    if (!isLeft && (slotIndex % 3) == 0) return RoadsidePropKind.Tree;
                    return isLeft ? RoadsidePropKind.FencePost : RoadsidePropKind.MilePost;
            }
        }

        public static float GetLateralPadding(RoadsidePropKind kind)
        {
            return kind switch
            {
                RoadsidePropKind.FencePost => 0.85f,
                RoadsidePropKind.MilePost => 0.90f,
                RoadsidePropKind.ChevronLeft => 0.65f,
                RoadsidePropKind.ChevronRight => 0.65f,
                RoadsidePropKind.StreetLamp => 1.15f,
                RoadsidePropKind.UtilityPole => 1.45f,
                RoadsidePropKind.PalmTree => 1.50f,
                RoadsidePropKind.Tree => 1.55f,
                RoadsidePropKind.Billboard => 1.85f,
                RoadsidePropKind.TrafficLight => 1.05f,
                RoadsidePropKind.NeonSign => 1.55f,
                RoadsidePropKind.SkyscraperTower => 2.85f,
                RoadsidePropKind.ParkedBus => 0.70f,
                RoadsidePropKind.CityBuilding => 2.30f,
                RoadsidePropKind.HighwaySign => 1.80f,
                RoadsidePropKind.BusStop => 1.65f,
                RoadsidePropKind.FireHydrant => 0.85f,
                RoadsidePropKind.VendingMachine => 1.25f,
                _ => 1.20f
            };
        }

        public static float GetPropScale(RoadsidePropKind kind)
        {
            return kind switch
            {
                RoadsidePropKind.FencePost => 1.15f,
                RoadsidePropKind.MilePost => 1.05f,
                RoadsidePropKind.ChevronLeft => 1.30f,
                RoadsidePropKind.ChevronRight => 1.30f,
                RoadsidePropKind.StreetLamp => 1.50f,
                RoadsidePropKind.UtilityPole => 1.45f,
                RoadsidePropKind.PalmTree => 1.65f,
                RoadsidePropKind.Tree => 1.60f,
                RoadsidePropKind.Billboard => 1.75f,
                RoadsidePropKind.TrafficLight => 1.55f,
                RoadsidePropKind.NeonSign => 1.70f,
                RoadsidePropKind.SkyscraperTower => 2.75f,
                RoadsidePropKind.ParkedBus => 1.45f,
                RoadsidePropKind.CityBuilding => 2.15f,
                RoadsidePropKind.HighwaySign => 1.70f,
                RoadsidePropKind.BusStop => 1.55f,
                RoadsidePropKind.FireHydrant => 1.05f,
                RoadsidePropKind.VendingMachine => 1.35f,
                _ => 1.25f
            };
        }

        private Sprite PickCustomSprite(int slotIndex, System.Random rng)
        {
            if (customRoadsideSprites == null || customRoadsideSprites.Length == 0) return null;
            if ((slotIndex % 4) != 0) return null;
            Sprite candidate = customRoadsideSprites[rng.Next(0, customRoadsideSprites.Length)];
            return candidate;
        }
    }
}
