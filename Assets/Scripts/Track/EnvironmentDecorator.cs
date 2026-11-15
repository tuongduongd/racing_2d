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

        public bool EnableExtraProps => enableExtraProps;
        public float SlotIntervalMeters => Mathf.Max(4f, slotIntervalMeters);
        public IReadOnlyList<RoadsideDecoration> Decorations => _decorations;

        public void SetEnableExtraProps(bool enabled)
        {
            enableExtraProps = enabled;
        }

        public void RebuildEnvironment(WaypointTrack track, int? customSeed = null)
        {
            _decorations.Clear();
            if (track == null || track.TotalLength <= 1f) return;

            int activeSeed = customSeed ?? environmentSeed;
            var rng = new System.Random(activeSeed);
            float totalLength = track.TotalLength;
            _cachedTrackLength = totalLength;

            float step = Mathf.Max(4f, slotIntervalMeters / Mathf.Clamp(propDensity, 0.25f, 2f));
            int totalSlots = Mathf.Max(1, Mathf.FloorToInt(totalLength / step));

            for (int slot = 0; slot < totalSlots; slot++)
            {
                float s = slot * step;
                TrackSample sample = track.SampleAtDistance(s);
                int zone = (slot / 8) % 3; // 0: Khu đô thị, 1: Đại lộ ven biển/cây cọ, 2: Ngoại ô rừng cây

                // Bên trái đường
                RoadsidePropKind leftKind = ChooseZoneProp(zone, true, slot, sample.curvature, rng);
                _decorations.Add(new RoadsideDecoration
                {
                    TrackDistance = s,
                    SideSign = -1f,
                    ExtraShoulderOffset = GetLateralPadding(leftKind),
                    ScaleMultiplier = GetPropScale(leftKind),
                    Kind = leftKind,
                    CustomOverrideSprite = PickCustomSprite(slot, rng)
                });

                // Bên phải đường
                if ((slot % 2) == 0 || Mathf.Abs(sample.curvature) > 0.012f)
                {
                    RoadsidePropKind rightKind = ChooseZoneProp(zone, false, slot, sample.curvature, rng);
                    _decorations.Add(new RoadsideDecoration
                    {
                        TrackDistance = s,
                        SideSign = 1f,
                        ExtraShoulderOffset = GetLateralPadding(rightKind),
                        ScaleMultiplier = GetPropScale(rightKind),
                        Kind = rightKind,
                        CustomOverrideSprite = PickCustomSprite(slot + 17, rng)
                    });
                }
            }
        }

        public void EnsureBuiltForTrack(WaypointTrack track)
        {
            if (track == null) return;
            if (_decorations.Count == 0 || Mathf.Abs(_cachedTrackLength - track.TotalLength) > 0.5f)
            {
                RebuildEnvironment(track);
            }
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
