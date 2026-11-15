using System;
using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Quản lý danh sách 4 Skin thương hiệu (Xanh SM, Shopee Food, Grab, Ahamove),
    /// lưu lựa chọn của người chơi tại sảnh chờ và áp dụng lên xe khi vào trận.
    /// </summary>
    public class SkinManager : MonoBehaviour
    {
        public const string SelectedSkinPrefKey = "RacingGame_SelectedSkin_v1";

        private static readonly DriverBrandSkin[] AllBrandOrder =
        {
            DriverBrandSkin.XanhSM,
            DriverBrandSkin.ShopeeFood,
            DriverBrandSkin.Grab,
            DriverBrandSkin.Ahamove
        };

        /// <summary>
        /// Cờ chuyển tiếp khi bấm nút PLAY từ Scene khởi động (LobbyScene) sang Scene đua (SampleScene).
        /// </summary>
        public static bool AutoStartRaceOnGameplayLoad { get; set; }

        [Header("Danh sách Data Skin (Gắn 4 ScriptableObject BikeSkinData tại đây)")]
        [SerializeField] private BikeSkinData[] skinAssets = new BikeSkinData[4];

        [Header("Skin mặc định ban đầu")]
        [SerializeField] private DriverBrandSkin selectedBrand = DriverBrandSkin.XanhSM;

        private readonly List<BikeSkinData> _resolvedSkins = new List<BikeSkinData>(4);
        private readonly List<BikeSkinData> _runtimeFallbacks = new List<BikeSkinData>(4);
        private bool _initialized;

        public event Action<BikeSkinData> SkinChanged;

        public IReadOnlyList<BikeSkinData> Skins
        {
            get
            {
                EnsureInitialized();
                return _resolvedSkins;
            }
        }

        public int SkinCount
        {
            get
            {
                EnsureInitialized();
                return _resolvedSkins.Count;
            }
        }

        public DriverBrandSkin SelectedBrand
        {
            get
            {
                EnsureInitialized();
                return selectedBrand;
            }
        }

        public BikeSkinData SelectedSkin
        {
            get
            {
                EnsureInitialized();
                return GetSkin(selectedBrand);
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _runtimeFallbacks.Count; i++)
            {
                if (_runtimeFallbacks[i] != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(_runtimeFallbacks[i]);
                    }
                    else
                    {
                        DestroyImmediate(_runtimeFallbacks[i]);
                    }
                }
            }
            _runtimeFallbacks.Clear();
        }

        public void EnsureInitialized()
        {
            if (_initialized && _resolvedSkins.Count == AllBrandOrder.Length) return;

            _resolvedSkins.Clear();
            for (int i = 0; i < AllBrandOrder.Length; i++)
            {
                DriverBrandSkin brand = AllBrandOrder[i];
                BikeSkinData assigned = FindAssignedSkin(brand);
                if (assigned == null)
                {
                    assigned = BikeSkinData.CreateDefault(brand);
                    _runtimeFallbacks.Add(assigned);
                }
                _resolvedSkins.Add(assigned);
            }

            if (PlayerPrefs.HasKey(SelectedSkinPrefKey))
            {
                int saved = PlayerPrefs.GetInt(SelectedSkinPrefKey, (int)selectedBrand);
                if (Enum.IsDefined(typeof(DriverBrandSkin), saved))
                {
                    selectedBrand = (DriverBrandSkin)saved;
                }
            }

            _initialized = true;
        }

        public void SetSkinAssets(BikeSkinData[] assets)
        {
            skinAssets = assets;
            _initialized = false;
            EnsureInitialized();
        }

        public BikeSkinData GetSkin(DriverBrandSkin brand)
        {
            EnsureInitialized();
            for (int i = 0; i < _resolvedSkins.Count; i++)
            {
                if (_resolvedSkins[i] != null && _resolvedSkins[i].SkinId == brand)
                {
                    return _resolvedSkins[i];
                }
            }
            return _resolvedSkins.Count > 0 ? _resolvedSkins[0] : null;
        }

        public BikeSkinData GetSkinAt(int index)
        {
            EnsureInitialized();
            if (index < 0 || index >= _resolvedSkins.Count) return null;
            return _resolvedSkins[index];
        }

        public bool SelectSkin(DriverBrandSkin brand, Car targetCar = null)
        {
            EnsureInitialized();
            BikeSkinData skin = GetSkin(brand);
            if (skin == null) return false;

            selectedBrand = brand;
            PlayerPrefs.SetInt(SelectedSkinPrefKey, (int)brand);
            PlayerPrefs.Save();

            if (targetCar != null)
            {
                ApplyToCar(targetCar);
            }

            SkinChanged?.Invoke(skin);
            return true;
        }

        public bool SelectSkinByIndex(int index, Car targetCar = null)
        {
            BikeSkinData skin = GetSkinAt(index);
            if (skin == null) return false;
            return SelectSkin(skin.SkinId, targetCar);
        }

        public void ApplyToCar(Car car)
        {
            if (car == null) return;
            EnsureInitialized();
            BikeSkinData skin = SelectedSkin;
            if (skin != null)
            {
                car.ApplySkin(skin);
            }
        }

        private BikeSkinData FindAssignedSkin(DriverBrandSkin brand)
        {
            if (skinAssets == null) return null;
            for (int i = 0; i < skinAssets.Length; i++)
            {
                if (skinAssets[i] != null && skinAssets[i].SkinId == brand)
                {
                    return skinAssets[i];
                }
            }
            return null;
        }
    }
}
