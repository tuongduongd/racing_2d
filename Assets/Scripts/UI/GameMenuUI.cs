using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>
    /// Giao diện Sảnh Chờ (Main Menu & Lobby) và Cửa hàng nâng cấp (Garage).
    /// Bao gồm:
    /// - Banner / Logo của game ở đầu sảnh chờ (hỗ trợ gắn Sprite/Texture tùy chỉnh).
    /// - Hệ thống chọn 4 Skin thương hiệu: Xanh SM, Shopee Food, Grab, Ahamove (kèm Preview trực tiếp).
    /// - Nút "PLAY" bắt đầu vào trận (hỗ trợ cả chuyển từ LobbyScene sang SampleScene hoặc bắt đầu ngay).
    /// - Nút biểu tượng "X" ở góc phải sảnh chờ để thoát game (Application.Quit).
    /// </summary>
    public class GameMenuUI : MonoBehaviour
    {
        [Header("Banner / Logo Sảnh Chờ (Kéo thả Sprite hoặc Texture vào đây)")]
        [SerializeField] private Sprite customBannerSprite;
        [SerializeField] private Texture2D customBannerTexture;
        [SerializeField] private string bannerTitle = "ROAD RASH 2D: ĐẠI CHIẾN SHIPPER";
        [SerializeField] private string bannerSubtitle = "XANH SM  •  SHOPEE FOOD  •  GRAB  •  AHAMOVE";

        [Header("Thiết lập Scene Khởi Động (Lobby)")]
        [SerializeField] private bool isStandaloneLobbyScene;
        [SerializeField] private string gameplaySceneName = "SampleScene";

        private GameManager _gameManager;
        private SkinManager _skinManager;
        private MapManager _mapManager;
        private CarUpgradeSystem _standaloneUpgradeSystem;

        private Canvas _canvas;
        private GameObject _menuPanel;
        private GameObject _garagePanel;
        private Button _outerQuitButtonX;
        private Image _bannerImage;
        private Sprite _generatedBannerSprite;

        // Main Menu & Lobby elements
        private Text _menuStatsText;
        private Text _selectedSkinStatusText;
        private Text _mapNameText;
        private Text _mapMetaText;
        private Text _mapDescText;
        private Image _mapAccentStrip;
        private Button _mapActionButton;
        private Image _mapActionBtnImg;
        private Text _mapActionBtnLabel;
        private RectTransform _mapTabsContainer;
        private readonly List<MapTabWidgets> _mapTabs = new List<MapTabWidgets>();
        private int _browsedMapIndex;
        private string _mapFeedbackMsg;
        private Color _mapFeedbackColor;
        private float _mapFeedbackUntil;
        private readonly Dictionary<DifficultyLevel, Image> _diffBtnImgs = new Dictionary<DifficultyLevel, Image>();
        private readonly Dictionary<int, Image> _lapBtnImgs = new Dictionary<int, Image>();
        private readonly List<SkinCardWidgets> _skinCards = new List<SkinCardWidgets>();
        private Text _menuMuteLabel;
        private Text _resetBtnLabel;
        private bool _confirmResetPending;

        // Garage elements
        private Text _garageMoneyText;
        private Text _garageStatsText;
        private Text _garageFeedbackText;
        private float _shopFeedbackUntil;

        private Image _speedBarFill;
        private Image _powerBarFill;
        private Image _brakeBarFill;
        private Image _gripBarFill;
        private float _dispSpeedBar;
        private float _dispPowerBar;
        private float _dispBrakeBar;
        private float _dispGripBar;

        private readonly List<UpgradeCardWidgets> _upgradeCards = new List<UpgradeCardWidgets>();

        private class MapTabWidgets
        {
            public string MapId;
            public Button Button;
            public Image Bg;
            public Text Label;
        }

        private class SkinCardWidgets
        {
            public DriverBrandSkin Brand;
            public Image CardBg;
            public Image HeaderStrip;
            public Image PreviewIcon;
            public Text NameText;
            public Text TaglineText;
            public Text SelectBtnLabel;
            public Image SelectBtnImg;
        }

        private class UpgradeCardWidgets
        {
            public UpgradeType Type;
            public Text TitleText;
            public Text EffectText;
            public Text ButtonLabel;
            public Image ButtonImg;
            public readonly List<Image> LevelPips = new List<Image>();
        }

        private static readonly UpgradeType[] AllUpgradeTypes =
        {
            UpgradeType.Engine,
            UpgradeType.Brakes,
            UpgradeType.Grip,
            UpgradeType.WeightReduction
        };

        private static readonly int[] LapChoices = { 1, 2, 3, 5 };

        public Canvas MenuCanvas => _canvas;
        public Button OuterQuitButton => _outerQuitButtonX;
        public int SkinCardCount => _skinCards.Count;
        public int MapCount => ResolveMapManager() != null ? _mapManager.MapCount : 0;
        public TrackDefinition SelectedMap => ResolveMapManager() != null ? _mapManager.SelectedMap : null;
        public int SelectedMapIndex => ResolveMapManager() != null ? _mapManager.SelectedIndex : 0;
        public TrackDefinition BrowsedMap => ResolveMapManager() != null ? (_mapManager.GetMapAt(_browsedMapIndex) ?? _mapManager.SelectedMap) : null;
        public int BrowsedMapIndex => _browsedMapIndex;
        public int MapTabCount => _mapTabs.Count;
        public Button MapActionButton => _mapActionButton;
        public string MapActionButtonLabel => _mapActionBtnLabel != null ? _mapActionBtnLabel.text : string.Empty;
        public string MapNameDisplayText => _mapNameText != null ? _mapNameText.text : string.Empty;
        public bool IsBrowsedMapLocked
        {
            get
            {
                MapManager mm = ResolveMapManager();
                TrackDefinition browsed = BrowsedMap;
                return mm != null && browsed != null && !mm.IsMapUnlocked(browsed.MapId);
            }
        }

        public void BindStandaloneSaveSystem(CarUpgradeSystem saveSystem)
        {
            _standaloneUpgradeSystem = saveSystem;
            if (_mapManager != null && saveSystem != null)
            {
                _mapManager.BindSaveSystem(saveSystem);
                _browsedMapIndex = _mapManager.SelectedIndex;
                RefreshMenu();
            }
        }

        private CarUpgradeSystem ResolveUpgradeSystem()
        {
            if (_gameManager != null && _gameManager.UpgradeSystem != null)
            {
                return _gameManager.UpgradeSystem;
            }

            if (_standaloneUpgradeSystem == null)
            {
                if (isStandaloneLobbyScene || Application.isPlaying)
                {
                    _standaloneUpgradeSystem = new CarUpgradeSystem();
                    _standaloneUpgradeSystem.Load();
                }
                else
                {
                    _standaloneUpgradeSystem = new CarUpgradeSystem(null);
                }
            }

            return _standaloneUpgradeSystem;
        }

        private MapManager ResolveMapManager()
        {
            if (_mapManager == null)
            {
                _mapManager = _gameManager != null ? _gameManager.MapManager : FindAnyObjectByType<MapManager>();
                if (_mapManager == null && _gameManager != null)
                {
                    _mapManager = _gameManager.GetComponent<MapManager>();
                    if (_mapManager == null) _mapManager = _gameManager.gameObject.AddComponent<MapManager>();
                }
            }
            if (_mapManager != null)
            {
                CarUpgradeSystem upg = ResolveUpgradeSystem();
                if (upg != null && _mapManager.SaveSystem != upg)
                {
                    _mapManager.BindSaveSystem(upg);
                }
                else
                {
                    _mapManager.EnsureInitialized();
                }
            }
            return _mapManager;
        }

        private void Start()
        {
            // Hỗ trợ chạy độc lập trong LobbyScene (khi không có GameManager trong scene)
            if (_gameManager == null)
            {
                _gameManager = FindAnyObjectByType<GameManager>();
                if (_gameManager == null)
                {
                    InitializeStandaloneLobby();
                }
            }
        }

        public void InitializeStandaloneLobby()
        {
            _skinManager = FindAnyObjectByType<SkinManager>();
            if (_skinManager == null)
            {
                _skinManager = gameObject.AddComponent<SkinManager>();
            }
            _skinManager.EnsureInitialized();
            Car previewCar = FindAnyObjectByType<Car>();
            if (previewCar != null)
            {
                _skinManager.ApplyToCar(previewCar);
            }

            if (ResolveMapManager() == null)
            {
                _mapManager = gameObject.AddComponent<MapManager>();
                CarUpgradeSystem upg = ResolveUpgradeSystem();
                if (upg != null) _mapManager.BindSaveSystem(upg);
                else _mapManager.EnsureInitialized();
            }
            _browsedMapIndex = _mapManager != null ? _mapManager.SelectedIndex : 0;
            WaypointTrack track = FindAnyObjectByType<WaypointTrack>();
            EnvironmentDecorator env = FindAnyObjectByType<EnvironmentDecorator>();
            if (track != null)
            {
                _mapManager.ApplySelectedMapToTrack(track, env);
            }

            EnsureBuilt();
            RefreshMenu();
        }

        public void Bind(GameManager gameManager)
        {
            _gameManager = gameManager;
            _skinManager = gameManager != null ? gameManager.SkinManager : FindAnyObjectByType<SkinManager>();
            if (_skinManager == null && gameManager != null)
            {
                _skinManager = gameManager.GetComponent<SkinManager>();
                if (_skinManager == null) _skinManager = gameManager.gameObject.AddComponent<SkinManager>();
            }
            _skinManager?.EnsureInitialized();
            ResolveMapManager();
            _browsedMapIndex = _mapManager != null ? _mapManager.SelectedIndex : 0;

            _confirmResetPending = false;
            _shopFeedbackUntil = 0f;
            _mapFeedbackMsg = null;
            EnsureBuilt();
            RefreshMenu();
            RefreshGarage(true);
        }

        public void SetCustomBanner(Sprite bannerSprite, Texture2D bannerTexture = null)
        {
            customBannerSprite = bannerSprite;
            customBannerTexture = bannerTexture;
            if (_bannerImage != null)
            {
                _bannerImage.sprite = ResolveBannerSprite();
            }
        }

        public bool SelectSkinFromLobby(DriverBrandSkin brand)
        {
            bool changed;
            if (_gameManager != null)
            {
                changed = _gameManager.SelectPlayerSkin(brand);
            }
            else
            {
                if (_skinManager == null)
                {
                    _skinManager = GetComponent<SkinManager>();
                    if (_skinManager == null) _skinManager = gameObject.AddComponent<SkinManager>();
                }
                Car previewCar = FindAnyObjectByType<Car>();
                changed = _skinManager.SelectSkin(brand, previewCar);
            }

            RefreshSkinCards();
            return changed;
        }

        /// <summary>
        /// Chuyển đổi Map đang xem/chọn ở sảnh chờ; nếu map đã mở khóa thì chọn và cập nhật nền đường đua xem trước trực tiếp.
        /// </summary>
        public TrackDefinition StepMapFromLobby(int delta)
        {
            MapManager mm = ResolveMapManager();
            if (mm == null || mm.MapCount == 0) return null;

            _mapFeedbackMsg = null;
            int count = mm.MapCount;
            int current = Mathf.Clamp(_browsedMapIndex, 0, count - 1);
            int nextIdx = ((current + delta) % count + count) % count;
            return FocusOrSelectMapIndex(nextIdx);
        }

        public bool SelectMapFromLobby(string mapId)
        {
            MapManager mm = ResolveMapManager();
            if (mm == null) return false;

            _mapFeedbackMsg = null;
            int idx = mm.GetMapIndex(mapId);
            if (idx >= 0)
            {
                _browsedMapIndex = idx;
            }

            bool changed;
            if (_gameManager != null)
            {
                changed = _gameManager.SelectPlayerMap(mapId);
                _mapManager = _gameManager.MapManager;
            }
            else
            {
                WaypointTrack track = FindAnyObjectByType<WaypointTrack>();
                EnvironmentDecorator env = FindAnyObjectByType<EnvironmentDecorator>();
                changed = mm.SelectMap(mapId, track, env);
            }

            RefreshMapCarousel();
            return changed;
        }

        public bool TryUnlockMapFromLobby(string mapId)
        {
            MapManager mm = ResolveMapManager();
            if (mm == null) return false;

            TrackDefinition target = mm.GetMap(mapId);
            if (target == null) return false;

            if (mm.IsMapUnlocked(target.MapId))
            {
                return SelectMapFromLobby(target.MapId);
            }

            int cost = mm.GetMapUnlockCost(target.MapId);
            bool unlocked;
            if (_gameManager != null)
            {
                unlocked = _gameManager.TryUnlockPlayerMap(target.MapId);
                _mapManager = _gameManager.MapManager;
            }
            else
            {
                unlocked = mm.TryPurchaseMapUnlock(target.MapId);
                if (unlocked)
                {
                    WaypointTrack track = FindAnyObjectByType<WaypointTrack>();
                    EnvironmentDecorator env = FindAnyObjectByType<EnvironmentDecorator>();
                    mm.SelectMap(target.MapId, track, env);
                }
            }

            int idx = mm.GetMapIndex(target.MapId);
            if (idx >= 0) _browsedMapIndex = idx;

            if (unlocked)
            {
                SetMapFeedback($"Đã mở khóa và chọn {target.DisplayName}!", RoadRashVisualTheme.AccentGreen);
                RefreshMenu();
            }
            else
            {
                SetMapFeedback($"Không đủ tiền! Cần ${cost} để mở khóa {target.DisplayName}.", RoadRashVisualTheme.DangerRed);
                RefreshMapCarousel();
            }

            return unlocked;
        }

        private TrackDefinition FocusOrSelectMapIndex(int index)
        {
            MapManager mm = ResolveMapManager();
            if (mm == null || mm.MapCount == 0) return null;

            int safeIdx = ((index % mm.MapCount) + mm.MapCount) % mm.MapCount;
            _browsedMapIndex = safeIdx;
            TrackDefinition candidate = mm.GetMapAt(safeIdx);
            if (candidate == null) return null;

            if (mm.IsMapUnlocked(candidate.MapId))
            {
                if (_gameManager != null)
                {
                    _gameManager.SelectPlayerMap(candidate.MapId);
                    _mapManager = _gameManager.MapManager;
                }
                else
                {
                    WaypointTrack track = FindAnyObjectByType<WaypointTrack>();
                    EnvironmentDecorator env = FindAnyObjectByType<EnvironmentDecorator>();
                    mm.SelectMap(candidate.MapId, track, env);
                }
            }

            RefreshMapCarousel();
            return candidate;
        }

        private void OnMapActionButtonClicked()
        {
            TrackDefinition browsed = BrowsedMap;
            if (browsed == null) return;

            MapManager mm = ResolveMapManager();
            if (mm == null) return;

            if (mm.IsMapUnlocked(browsed.MapId))
            {
                SelectMapFromLobby(browsed.MapId);
            }
            else
            {
                TryUnlockMapFromLobby(browsed.MapId);
            }
        }

        private void OnPlayButtonClicked()
        {
            if (IsBrowsedMapLocked)
            {
                TrackDefinition browsed = BrowsedMap;
                int cost = ResolveMapManager() != null && browsed != null
                    ? _mapManager.GetMapUnlockCost(browsed.MapId)
                    : 0;
                SetMapFeedback($"Map đang khóa! Bấm MỞ KHÓA (${cost}) hoặc chọn map đã mở.", RoadRashVisualTheme.DangerRed);
                RefreshMapCarousel();
                return;
            }

            TriggerPlay();
        }

        private void SetMapFeedback(string msg, Color color)
        {
            _mapFeedbackMsg = msg;
            _mapFeedbackColor = color;
            _mapFeedbackUntil = Time.unscaledTime + 2.5f;
        }

        public void TriggerPlay()
        {
            _confirmResetPending = false;
            if (_gameManager != null && !isStandaloneLobbyScene)
            {
                _gameManager.StartRace();
                return;
            }

            SkinManager.AutoStartRaceOnGameplayLoad = true;
            if (!string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                SceneManager.LoadScene(gameplaySceneName);
            }
        }

        public void TriggerQuit()
        {
            if (_gameManager != null)
            {
                _gameManager.QuitGame();
                return;
            }

            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void Update()
        {
            if (_canvas == null) return;

            if (_gameManager == null)
            {
                // Chế độ LobbyScene độc lập: luôn hiển thị sảnh chờ và nút X
                if (!_menuPanel.activeSelf) _menuPanel.SetActive(true);
                if (_outerQuitButtonX != null && !_outerQuitButtonX.gameObject.activeSelf)
                {
                    _outerQuitButtonX.gameObject.SetActive(true);
                }
                if (!string.IsNullOrEmpty(_mapFeedbackMsg) && Time.unscaledTime > _mapFeedbackUntil)
                {
                    _mapFeedbackMsg = null;
                    RefreshMapCarousel();
                }
                return;
            }

            GameFlowState state = _gameManager.CurrentFlowState;
            bool showMenu = state == GameFlowState.MainMenu;
            bool showGarage = state == GameFlowState.UpgradeShop;

            if (_outerQuitButtonX != null && _outerQuitButtonX.gameObject.activeSelf != showMenu)
            {
                _outerQuitButtonX.gameObject.SetActive(showMenu);
            }

            if (_menuPanel.activeSelf != showMenu)
            {
                _menuPanel.SetActive(showMenu);
                if (showMenu)
                {
                    _confirmResetPending = false;
                    _mapFeedbackMsg = null;
                    if (_mapManager != null)
                    {
                        _browsedMapIndex = _mapManager.SelectedIndex;
                    }
                    RefreshMenu();
                }
            }

            if (_garagePanel.activeSelf != showGarage)
            {
                _garagePanel.SetActive(showGarage);
                if (showGarage) RefreshGarage(true);
            }

            if (showMenu)
            {
                if (!string.IsNullOrEmpty(_mapFeedbackMsg) && Time.unscaledTime > _mapFeedbackUntil)
                {
                    _mapFeedbackMsg = null;
                    RefreshMapCarousel();
                }
                RefreshMenuDynamic();
            }
            else if (showGarage)
            {
                AnimateGarageBars(Time.unscaledDeltaTime);
                if (_garageFeedbackText != null && _garageFeedbackText.gameObject.activeSelf && Time.unscaledTime > _shopFeedbackUntil)
                {
                    _garageFeedbackText.gameObject.SetActive(false);
                }
            }
        }

        private void RefreshMenu()
        {
            if (_menuStatsText != null)
            {
                CarUpgradeSystem upg = ResolveUpgradeSystem();
                int money = upg != null ? upg.Money : 200;
                int won = upg != null ? upg.RacesWon : 0;
                int played = upg != null ? upg.RacesPlayed : 0;
                string best = (upg != null && upg.BestRaceTime > 0f) ? RaceHUD.FormatTime(upg.BestRaceTime) : "--:--.--";
                _menuStatsText.text = $"Tiền: ${money}   |   Thắng: {won}/{played}   |   Kỷ lục: {best}";
            }

            if (_gameManager != null)
            {
                foreach (var kv in _diffBtnImgs)
                {
                    bool sel = _gameManager.Difficulty == kv.Key;
                    kv.Value.color = sel ? RoadRashVisualTheme.HudYellow : RoadRashVisualTheme.MetalMid;
                }

                foreach (var kv in _lapBtnImgs)
                {
                    bool sel = _gameManager.TotalLaps == kv.Key;
                    kv.Value.color = sel ? RoadRashVisualTheme.HudYellow : RoadRashVisualTheme.MetalMid;
                }
            }

            RefreshSkinCards();
            RefreshMapCarousel();
            RefreshMenuDynamic();
        }

        private void RefreshMapCarousel()
        {
            MapManager mm = ResolveMapManager();
            if (mm == null || mm.MapCount == 0) return;

            EnsureMapTabsSynced(mm);

            _browsedMapIndex = Mathf.Clamp(_browsedMapIndex, 0, mm.MapCount - 1);
            TrackDefinition displayMap = mm.GetMapAt(_browsedMapIndex) ?? mm.SelectedMap;
            if (displayMap == null) return;

            bool isUnlocked = mm.IsMapUnlocked(displayMap.MapId);
            bool isSelected = mm.SelectedMap != null && mm.SelectedMap.MapId == displayMap.MapId;
            int idx = _browsedMapIndex + 1;
            int total = mm.MapCount;

            if (_mapAccentStrip != null && displayMap.Theme != null)
            {
                _mapAccentStrip.color = isUnlocked
                    ? displayMap.Theme.CenterYellowLine
                    : new Color(0.55f, 0.25f, 0.25f);
            }

            if (_mapNameText != null)
            {
                string prefix = isUnlocked ? "ĐƯỜNG ĐUA" : "[ĐÃ KHÓA] ĐƯỜNG ĐUA";
                _mapNameText.text = $"{prefix} ({idx}/{total}): {displayMap.DisplayName.ToUpperInvariant()}";
                _mapNameText.color = isUnlocked
                    ? RoadRashVisualTheme.HudYellow
                    : new Color(0.95f, 0.54f, 0.45f);
            }

            if (_mapMetaText != null)
            {
                string stars = new string('★', displayMap.DifficultyStars) + new string('☆', Mathf.Max(0, 5 - displayMap.DifficultyStars));
                float best = mm.GetBestTime(displayMap.MapId);
                string bestStr = best > 0f ? RaceHUD.FormatTime(best) : "--:--.--";
                string policeTag = displayMap.EnablePoliceChase ? "CS: BẬT" : "CS: TẮT";
                string lockTag = isUnlocked
                    ? (isSelected ? "Đang chọn" : "Đã mở")
                    : $"Khóa (${mm.GetMapUnlockCost(displayMap.MapId)})";
                _mapMetaText.text =
                    $"{displayMap.LaneCount} Làn ({displayMap.TotalTrackWidth:0}m)  |  {stars}  |  x{displayMap.RewardMultiplier:0.0}  |  {policeTag}  |  {lockTag}  |  {bestStr}";
            }

            if (_mapDescText != null)
            {
                if (!string.IsNullOrEmpty(_mapFeedbackMsg))
                {
                    _mapDescText.text = _mapFeedbackMsg;
                    _mapDescText.color = _mapFeedbackColor;
                }
                else
                {
                    _mapDescText.text = displayMap.Description;
                    _mapDescText.color = new Color(0.76f, 0.80f, 0.86f);
                }
            }

            if (_mapActionButton != null && _mapActionBtnImg != null && _mapActionBtnLabel != null)
            {
                if (!isUnlocked)
                {
                    int cost = mm.GetMapUnlockCost(displayMap.MapId);
                    CarUpgradeSystem upg = ResolveUpgradeSystem();
                    bool canAfford = upg != null && upg.Money >= cost;
                    _mapActionBtnLabel.text = $"MỞ KHÓA (${cost})";
                    _mapActionBtnImg.color = canAfford
                        ? new Color(0.20f, 0.62f, 0.30f)
                        : new Color(0.48f, 0.24f, 0.22f);
                    _mapActionBtnLabel.color = Color.white;
                }
                else if (isSelected)
                {
                    _mapActionBtnLabel.text = "✓ ĐANG CHỌN";
                    _mapActionBtnImg.color = RoadRashVisualTheme.HudYellow;
                    _mapActionBtnLabel.color = new Color(0.10f, 0.10f, 0.12f);
                }
                else
                {
                    _mapActionBtnLabel.text = "CHỌN MAP";
                    _mapActionBtnImg.color = new Color(0.20f, 0.44f, 0.74f);
                    _mapActionBtnLabel.color = Color.white;
                }
            }

            for (int i = 0; i < _mapTabs.Count; i++)
            {
                MapTabWidgets tab = _mapTabs[i];
                TrackDefinition tabDef = mm.GetMapAt(i);
                if (tabDef == null) continue;

                bool tabUnlocked = mm.IsMapUnlocked(tabDef.MapId);
                bool tabSelected = mm.SelectedMap != null && mm.SelectedMap.MapId == tabDef.MapId;
                bool tabBrowsed = i == _browsedMapIndex;

                string shortName = tabDef.DisplayName;
                if (shortName.Length > 15) shortName = shortName.Substring(0, 14) + "…";

                if (!tabUnlocked)
                {
                    tab.Label.text = $"[KHÓA] {shortName}";
                    tab.Bg.color = tabBrowsed ? new Color(0.46f, 0.24f, 0.24f) : new Color(0.20f, 0.16f, 0.18f);
                    tab.Label.color = new Color(0.92f, 0.72f, 0.72f);
                }
                else if (tabSelected)
                {
                    tab.Label.text = $"▶ {shortName}";
                    tab.Bg.color = RoadRashVisualTheme.HudYellow;
                    tab.Label.color = new Color(0.10f, 0.10f, 0.12f);
                }
                else
                {
                    tab.Label.text = shortName;
                    tab.Bg.color = tabBrowsed ? new Color(0.26f, 0.36f, 0.50f) : RoadRashVisualTheme.MetalMid;
                    tab.Label.color = Color.white;
                }
            }
        }

        private void EnsureMapTabsSynced(MapManager mm)
        {
            if (_mapTabsContainer == null || mm == null) return;

            bool needsRebuild = _mapTabs.Count != mm.MapCount;
            if (!needsRebuild)
            {
                for (int i = 0; i < _mapTabs.Count; i++)
                {
                    TrackDefinition def = mm.GetMapAt(i);
                    if (def == null || _mapTabs[i].MapId != def.MapId)
                    {
                        needsRebuild = true;
                        break;
                    }
                }
            }

            if (!needsRebuild) return;

            for (int i = _mapTabsContainer.childCount - 1; i >= 0; i--)
            {
                GameObject child = _mapTabsContainer.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            _mapTabs.Clear();

            int count = mm.MapCount;
            if (count <= 0) return;

            const float totalWidth = 688f;
            const float gap = 6f;
            float tabWidth = (totalWidth - gap * (count - 1)) / count;

            for (int i = 0; i < count; i++)
            {
                TrackDefinition def = mm.GetMapAt(i);
                if (def == null) continue;

                int capturedIdx = i;
                Button tabBtn = RoadRashUiSprites.CreateBevelButton(
                    _mapTabsContainer, $"MapTab_{i}", def.DisplayName, RoadRashVisualTheme.MetalMid, 11);
                PlaceTopLeft(tabBtn.GetComponent<RectTransform>(), i * (tabWidth + gap), 0f, tabWidth, 24f);
                tabBtn.onClick.AddListener(() => FocusOrSelectMapIndex(capturedIdx));

                _mapTabs.Add(new MapTabWidgets
                {
                    MapId = def.MapId,
                    Button = tabBtn,
                    Bg = tabBtn.GetComponent<Image>(),
                    Label = tabBtn.GetComponentInChildren<Text>()
                });
            }
        }

        private void RefreshSkinCards()
        {
            if (_skinManager == null)
            {
                _skinManager = _gameManager != null ? _gameManager.SkinManager : FindAnyObjectByType<SkinManager>();
            }
            if (_skinManager == null) return;

            DriverBrandSkin activeBrand = _skinManager.SelectedBrand;
            BikeSkinData activeData = _skinManager.SelectedSkin;

            if (_selectedSkinStatusText != null && activeData != null)
            {
                _selectedSkinStatusText.text = $"CHỌN SKIN ĐỒNG PHỤC XE (Đang chọn: {activeData.DisplayName.ToUpperInvariant()})";
            }

            for (int i = 0; i < _skinCards.Count; i++)
            {
                SkinCardWidgets card = _skinCards[i];
                BikeSkinData data = _skinManager.GetSkin(card.Brand);
                bool isSelected = card.Brand == activeBrand;

                if (data != null)
                {
                    card.NameText.text = data.DisplayName;
                    card.TaglineText.text = data.Tagline;
                    card.HeaderStrip.color = data.PrimaryColor;

                    Sprite preview = data.CustomLogoSprite != null
                        ? data.CustomLogoSprite
                        : (data.GetOverrideSprite() != null
                            ? data.GetOverrideSprite()
                            : RoadRashBikeSprites.GetOrCreateSet(data).Upright0);
                    card.PreviewIcon.sprite = preview;
                }

                card.CardBg.color = isSelected
                    ? new Color(0.22f, 0.27f, 0.35f, 0.98f)
                    : new Color(0.12f, 0.14f, 0.18f, 0.94f);
                card.SelectBtnLabel.text = isSelected ? "ĐANG CHỌN" : "CHỌN SKIN";
                card.SelectBtnImg.color = isSelected
                    ? RoadRashVisualTheme.HudYellow
                    : (data != null ? Color.Lerp(data.PrimaryColor, Color.black, 0.25f) : RoadRashVisualTheme.MetalMid);
                card.SelectBtnLabel.color = isSelected ? new Color(0.10f, 0.10f, 0.12f) : Color.white;
            }
        }

        private void RefreshMenuDynamic()
        {
            if (_menuMuteLabel != null && _gameManager != null && _gameManager.AudioController != null)
            {
                _menuMuteLabel.text = _gameManager.AudioController.IsMuted ? "Âm thanh: TẮT" : "Âm thanh: BẬT";
            }
            if (_resetBtnLabel != null)
            {
                _resetBtnLabel.text = _confirmResetPending ? "Bấm lần nữa ĐỂ XÓA!" : "Xóa dữ liệu lưu";
            }
        }

        private void RefreshGarage(bool snapBars)
        {
            CarUpgradeSystem upg = _gameManager != null ? _gameManager.UpgradeSystem : null;
            if (upg == null) return;

            _garageMoneyText.text = $"Tiền hiện có: ${upg.Money}";

            VehiclePhysics phys = _gameManager.PlayerCar != null ? _gameManager.PlayerCar.Physics : null;
            if (phys != null)
            {
                int maxKmh = Mathf.RoundToInt(phys.MaxSpeed * 3.6f);
                _garageStatsText.text =
                    $"Max {maxKmh} km/h  |  Power {phys.EnginePower:F0}  |  Phanh {phys.BrakePower:F0}  |  Bám {phys.Grip:F1}  |  {phys.Mass:F0} kg";

                if (snapBars)
                {
                    _dispSpeedBar = Mathf.InverseLerp(180f, 340f, maxKmh);
                    _dispPowerBar = Mathf.InverseLerp(5000f, 11500f, phys.EnginePower);
                    _dispBrakeBar = Mathf.InverseLerp(7500f, 15500f, phys.BrakePower);
                    _dispGripBar = Mathf.InverseLerp(7f, 16f, phys.Grip);
                    ApplyBarFills();
                }
            }

            for (int i = 0; i < _upgradeCards.Count; i++)
            {
                UpgradeCardWidgets card = _upgradeCards[i];
                int level = upg.GetLevel(card.Type);
                bool isMax = upg.IsMaxLevel(card.Type);
                int cost = upg.GetUpgradeCost(card.Type);

                card.TitleText.text = $"{upg.GetUpgradeDisplayName(card.Type)}   Lv {level}/{CarUpgradeSystem.MaxUpgradeLevel}";
                card.EffectText.text = upg.GetUpgradeEffectText(card.Type);
                card.ButtonLabel.text = isMax ? "ĐẠT TỐI ĐA" : $"Nâng cấp (${cost})";
                card.ButtonImg.color = isMax
                    ? RoadRashVisualTheme.MetalDark
                    : (upg.Money >= cost ? new Color(0.24f, 0.58f, 0.28f) : new Color(0.45f, 0.26f, 0.24f));

                for (int p = 0; p < card.LevelPips.Count; p++)
                {
                    card.LevelPips[p].color = p < level ? RoadRashVisualTheme.HudYellow : new Color(0.18f, 0.20f, 0.24f);
                }
            }
        }

        private void AnimateGarageBars(float dt)
        {
            VehiclePhysics phys = _gameManager.PlayerCar != null ? _gameManager.PlayerCar.Physics : null;
            if (phys == null) return;

            int maxKmh = Mathf.RoundToInt(phys.MaxSpeed * 3.6f);
            float tSpeed = Mathf.InverseLerp(180f, 340f, maxKmh);
            float tPower = Mathf.InverseLerp(5000f, 11500f, phys.EnginePower);
            float tBrake = Mathf.InverseLerp(7500f, 15500f, phys.BrakePower);
            float tGrip = Mathf.InverseLerp(7f, 16f, phys.Grip);

            float lerp = 1f - Mathf.Exp(-10f * dt);
            _dispSpeedBar = Mathf.Lerp(_dispSpeedBar, tSpeed, lerp);
            _dispPowerBar = Mathf.Lerp(_dispPowerBar, tPower, lerp);
            _dispBrakeBar = Mathf.Lerp(_dispBrakeBar, tBrake, lerp);
            _dispGripBar = Mathf.Lerp(_dispGripBar, tGrip, lerp);
            ApplyBarFills();
        }

        private void ApplyBarFills()
        {
            if (_speedBarFill != null) _speedBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_dispSpeedBar), 1f);
            if (_powerBarFill != null) _powerBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_dispPowerBar), 1f);
            if (_brakeBarFill != null) _brakeBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_dispBrakeBar), 1f);
            if (_gripBarFill != null) _gripBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_dispGripBar), 1f);
        }

        private void SetShopFeedback(string msg, Color color)
        {
            if (_garageFeedbackText == null) return;
            _garageFeedbackText.text = msg;
            _garageFeedbackText.color = color;
            _garageFeedbackText.gameObject.SetActive(true);
            _shopFeedbackUntil = Time.unscaledTime + 2.2f;
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;

            _canvas = RoadRashUiSprites.CreateOverlayCanvas("GameMenuCanvas", 20);
            BuildOuterQuitButton();
            BuildMainMenuPanel();
            BuildGaragePanel();
        }

        /// <summary>
        /// Nút biểu tượng "X" ở góc phải ngoài sảnh chờ để thoát game (Application.Quit).
        /// </summary>
        private void BuildOuterQuitButton()
        {
            _outerQuitButtonX = RoadRashUiSprites.CreateBevelButton(
                _canvas.transform,
                "QuitButtonX",
                "X",
                new Color(0.82f, 0.18f, 0.18f),
                22);

            RectTransform rt = _outerQuitButtonX.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-22f, -18f);
            rt.sizeDelta = new Vector2(48f, 48f);

            _outerQuitButtonX.onClick.AddListener(TriggerQuit);
        }

        private void BuildMainMenuPanel()
        {
            _menuPanel = CreateCenteredPanel(_canvas.transform, "MainMenuPanel", 760f, 696f);

            // 1. Khung Banner / Logo ở đầu Sảnh Chờ
            GameObject bannerObj = new GameObject("LobbyBanner");
            bannerObj.transform.SetParent(_menuPanel.transform, false);
            _bannerImage = bannerObj.AddComponent<Image>();
            _bannerImage.sprite = ResolveBannerSprite();
            _bannerImage.type = Image.Type.Simple;
            _bannerImage.preserveAspect = false;
            PlaceTop(_bannerImage.rectTransform, -14f, 724f, 92f);

            Text title = RoadRashUiSprites.CreateStyledText(
                bannerObj.transform, "Title", bannerTitle, 30, RoadRashVisualTheme.HudYellow, TextAnchor.MiddleCenter);
            PlaceTop(title.rectTransform, -12f, 700f, 40f);

            Text sub = RoadRashUiSprites.CreateStyledText(
                bannerObj.transform, "Subtitle", bannerSubtitle, 13, new Color(0.95f, 0.97f, 1.00f), TextAnchor.MiddleCenter);
            PlaceTop(sub.rectTransform, -54f, 700f, 24f);

            // Nút "X" phụ gắn ngay góc phải trên của Sảnh Chờ để thao tác nhanh
            Button panelCloseX = RoadRashUiSprites.CreateBevelButton(
                _menuPanel.transform, "LobbyCloseXBtn", "X", new Color(0.78f, 0.18f, 0.18f), 16);
            PlaceTopLeft(panelCloseX.GetComponent<RectTransform>(), 710f, -16f, 34f, 34f);
            panelCloseX.onClick.AddListener(TriggerQuit);

            _menuStatsText = RoadRashUiSprites.CreateStyledText(
                _menuPanel.transform, "Stats", "", 15, RoadRashVisualTheme.AccentGreen, TextAnchor.MiddleCenter);
            PlaceTop(_menuStatsText.rectTransform, -112f, 700f, 24f);

            // 2. Khu vực Chọn 4 Skin: Xanh SM, Shopee Food, Grab, Ahamove
            _selectedSkinStatusText = RoadRashUiSprites.CreateStyledText(
                _menuPanel.transform,
                "SkinSectionTitle",
                "CHỌN SKIN ĐỒNG PHỤC XE (XANH SM / SHOPEE FOOD / GRAB / AHAMOVE)",
                14,
                RoadRashVisualTheme.HudYellow,
                TextAnchor.MiddleCenter);
            PlaceTop(_selectedSkinStatusText.rectTransform, -140f, 700f, 22f);

            _skinCards.Clear();
            DriverBrandSkin[] brands =
            {
                DriverBrandSkin.XanhSM,
                DriverBrandSkin.ShopeeFood,
                DriverBrandSkin.Grab,
                DriverBrandSkin.Ahamove
            };

            const float firstCardX = 22f;
            const float cardWidth = 170f;
            const float cardSpacing = 180f;
            const float cardY = -166f;
            const float cardHeight = 166f;

            for (int i = 0; i < brands.Length; i++)
            {
                DriverBrandSkin brand = brands[i];
                float x = firstCardX + i * cardSpacing;
                BuildSkinCard(brand, x, cardY, cardWidth, cardHeight);
            }

            // 2b. Map Carousel + Lock/Unlock + Map Tabs (Chọn Map đường đua + Live Preview nền phía sau)
            GameObject mapBox = new GameObject("MapSelectorBox");
            mapBox.transform.SetParent(_menuPanel.transform, false);
            Image mapBoxBg = mapBox.AddComponent<Image>();
            mapBoxBg.sprite = RoadRashUiSprites.GetPanelSprite();
            mapBoxBg.type = Image.Type.Sliced;
            mapBoxBg.color = new Color(0.10f, 0.12f, 0.17f, 0.94f);
            PlaceTopLeft(mapBoxBg.rectTransform, 26f, -340f, 708f, 108f);

            GameObject stripObj = new GameObject("MapAccentStrip");
            stripObj.transform.SetParent(mapBox.transform, false);
            _mapAccentStrip = stripObj.AddComponent<Image>();
            _mapAccentStrip.raycastTarget = false;
            _mapAccentStrip.color = RoadRashVisualTheme.HudYellow;
            PlaceTopLeft(_mapAccentStrip.rectTransform, 4f, -4f, 700f, 5f);

            Button prevMapBtn = RoadRashUiSprites.CreateBevelButton(mapBox.transform, "PrevMapBtn", "◀", RoadRashVisualTheme.MetalMid, 18);
            PlaceTopLeft(prevMapBtn.GetComponent<RectTransform>(), 10f, -14f, 40f, 54f);
            prevMapBtn.onClick.AddListener(() => StepMapFromLobby(-1));

            Button nextMapBtn = RoadRashUiSprites.CreateBevelButton(mapBox.transform, "NextMapBtn", "▶", RoadRashVisualTheme.MetalMid, 18);
            PlaceTopLeft(nextMapBtn.GetComponent<RectTransform>(), 658f, -14f, 40f, 54f);
            nextMapBtn.onClick.AddListener(() => StepMapFromLobby(1));

            _mapNameText = RoadRashUiSprites.CreateStyledText(mapBox.transform, "MapNameText", "ĐƯỜNG ĐUA (1/4): PHỐ SÁNG BAN MAI", 14, RoadRashVisualTheme.HudYellow, TextAnchor.MiddleLeft, FontStyle.Bold);
            PlaceTopLeft(_mapNameText.rectTransform, 58f, -11f, 446f, 22f);

            _mapMetaText = RoadRashUiSprites.CreateStyledText(mapBox.transform, "MapMetaText", "4 Làn (16m)  |  ★★☆☆☆  |  x1.0  |  --:--.--", 11, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal);
            PlaceTopLeft(_mapMetaText.rectTransform, 58f, -33f, 446f, 18f);

            _mapDescText = RoadRashUiSprites.CreateStyledText(mapBox.transform, "MapDescText", "Đường phố 4 làn cổ điển với khúc cua vừa phải.", 11, new Color(0.76f, 0.80f, 0.86f), TextAnchor.MiddleLeft, FontStyle.Italic);
            PlaceTopLeft(_mapDescText.rectTransform, 58f, -51f, 446f, 18f);

            _mapActionButton = RoadRashUiSprites.CreateBevelButton(mapBox.transform, "MapActionBtn", "✓ ĐANG CHỌN", RoadRashVisualTheme.HudYellow, 12);
            PlaceTopLeft(_mapActionButton.GetComponent<RectTransform>(), 510f, -15f, 140f, 52f);
            _mapActionBtnImg = _mapActionButton.GetComponent<Image>();
            _mapActionBtnLabel = _mapActionButton.GetComponentInChildren<Text>();
            _mapActionButton.onClick.AddListener(OnMapActionButtonClicked);

            GameObject tabsObj = new GameObject("MapTabsRow");
            tabsObj.transform.SetParent(mapBox.transform, false);
            _mapTabsContainer = tabsObj.AddComponent<RectTransform>();
            PlaceTopLeft(_mapTabsContainer, 10f, -75f, 688f, 26f);
            _mapTabs.Clear();

            // 3. Hàng chọn Độ khó AI & Số vòng đua
            Text diffLabel = RoadRashUiSprites.CreateStyledText(_menuPanel.transform, "DiffLabel", "Độ khó AI:", 14, Color.white);
            PlaceTopLeft(diffLabel.rectTransform, 26f, -458f, 95f, 30f);

            AddDifficultyButton(118f, -458f, DifficultyLevel.Easy, "Dễ");
            AddDifficultyButton(206f, -458f, DifficultyLevel.Normal, "Vừa");
            AddDifficultyButton(294f, -458f, DifficultyLevel.Hard, "Khó");

            Text lapLabel = RoadRashUiSprites.CreateStyledText(_menuPanel.transform, "LapLabel", "Số vòng:", 14, Color.white);
            PlaceTopLeft(lapLabel.rectTransform, 398f, -458f, 76f, 30f);

            for (int i = 0; i < LapChoices.Length; i++)
            {
                int laps = LapChoices[i];
                Button btn = RoadRashUiSprites.CreateBevelButton(_menuPanel.transform, $"LapBtn_{laps}", $"{laps}V", RoadRashVisualTheme.MetalMid, 13);
                PlaceTopLeft(btn.GetComponent<RectTransform>(), 474f + i * 66f, -458f, 60f, 30f);
                _lapBtnImgs[laps] = btn.GetComponent<Image>();
                btn.onClick.AddListener(() =>
                {
                    _gameManager?.SetTotalLaps(laps);
                    RefreshMenu();
                });
            }

            // 4. Nút PLAY chính & Nút mở GARAGE
            Button playBtn = RoadRashUiSprites.CreateBevelButton(
                _menuPanel.transform,
                "PlayBtn",
                "▶  PLAY (BẮT ĐẦU ĐUA)",
                new Color(0.18f, 0.64f, 0.28f),
                21);
            PlaceTopLeft(playBtn.GetComponent<RectTransform>(), 26f, -502f, 445f, 50f);
            playBtn.onClick.AddListener(OnPlayButtonClicked);

            Button garageBtn = RoadRashUiSprites.CreateBevelButton(
                _menuPanel.transform,
                "GarageBtn",
                "NÂNG CẤP XE (GARAGE)",
                new Color(0.20f, 0.42f, 0.74f),
                15);
            PlaceTopLeft(garageBtn.GetComponent<RectTransform>(), 485f, -502f, 249f, 50f);
            garageBtn.onClick.AddListener(() =>
            {
                _confirmResetPending = false;
                _gameManager?.OpenGarage();
            });

            // 5. Hàng Âm thanh + Xóa dữ liệu + Thoát Game
            Button muteBtn = RoadRashUiSprites.CreateBevelButton(_menuPanel.transform, "MuteBtn", "Âm thanh: BẬT", RoadRashVisualTheme.MetalMid, 13);
            PlaceTopLeft(muteBtn.GetComponent<RectTransform>(), 26f, -564f, 220f, 34f);
            _menuMuteLabel = muteBtn.GetComponentInChildren<Text>();
            muteBtn.onClick.AddListener(() =>
            {
                _gameManager?.AudioController?.ToggleMute();
                RefreshMenuDynamic();
            });

            Button resetBtn = RoadRashUiSprites.CreateBevelButton(_menuPanel.transform, "ResetBtn", "Xóa dữ liệu lưu", new Color(0.52f, 0.24f, 0.22f), 13);
            PlaceTopLeft(resetBtn.GetComponent<RectTransform>(), 260f, -564f, 230f, 34f);
            _resetBtnLabel = resetBtn.GetComponentInChildren<Text>();
            resetBtn.onClick.AddListener(() =>
            {
                if (_confirmResetPending)
                {
                    if (_gameManager != null)
                    {
                        _gameManager.ResetSaveData();
                    }
                    else
                    {
                        ResolveUpgradeSystem()?.ResetProgress();
                        ResolveMapManager()?.ResetProgress();
                    }
                    _browsedMapIndex = _mapManager != null ? _mapManager.SelectedIndex : 0;
                    _confirmResetPending = false;
                    _mapFeedbackMsg = null;
                    RefreshMenu();
                }
                else
                {
                    _confirmResetPending = true;
                    RefreshMenuDynamic();
                }
            });

            Button quitLobbyBtn = RoadRashUiSprites.CreateBevelButton(_menuPanel.transform, "QuitLobbyBtn", "✕ THOÁT GAME", new Color(0.72f, 0.18f, 0.18f), 13);
            PlaceTopLeft(quitLobbyBtn.GetComponent<RectTransform>(), 504f, -564f, 230f, 34f);
            quitLobbyBtn.onClick.AddListener(TriggerQuit);

            Text controls = RoadRashUiSprites.CreateStyledText(
                _menuPanel.transform, "ControlsHint",
                "W/↑ Ga  |  S/↓ Phanh  |  A/D Lái  |  SPACE Đá đối thủ  |  M Tắt/Bật tiếng",
                12, new Color(0.80f, 0.84f, 0.90f), TextAnchor.MiddleCenter);
            PlaceTop(controls.rectTransform, -612f, 700f, 22f);

            Text skinHint = RoadRashUiSprites.CreateStyledText(
                _menuPanel.transform, "SkinEditorHint",
                "Mẹo Editor: Gắn Material / Texture / Map Theme trong Assets/ScriptableObjects/",
                11, new Color(0.68f, 0.78f, 0.90f), TextAnchor.MiddleCenter);
            PlaceTop(skinHint.rectTransform, -636f, 700f, 20f);
        }

        private void BuildSkinCard(DriverBrandSkin brand, float x, float y, float w, float h)
        {
            GameObject cardObj = new GameObject($"SkinCard_{brand}");
            cardObj.transform.SetParent(_menuPanel.transform, false);
            Image cardBg = cardObj.AddComponent<Image>();
            cardBg.sprite = RoadRashUiSprites.GetPanelSprite();
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.12f, 0.14f, 0.18f, 0.94f);
            PlaceTopLeft(cardBg.rectTransform, x, y, w, h);

            GameObject stripObj = new GameObject("BrandColorStrip");
            stripObj.transform.SetParent(cardObj.transform, false);
            Image stripImg = stripObj.AddComponent<Image>();
            stripImg.raycastTarget = false;
            PlaceTopLeft(stripImg.rectTransform, 4f, -4f, w - 8f, 8f);

            GameObject iconObj = new GameObject("BikePreviewIcon");
            iconObj.transform.SetParent(cardObj.transform, false);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            PlaceTop(iconImg.rectTransform, -16f, 64f, 64f);

            Text nameTxt = RoadRashUiSprites.CreateStyledText(
                cardObj.transform, "BrandName", brand.ToString(), 15, Color.white, TextAnchor.MiddleCenter);
            PlaceTop(nameTxt.rectTransform, -84f, w - 10f, 22f);

            Text tagTxt = RoadRashUiSprites.CreateStyledText(
                cardObj.transform, "BrandTagline", "", 11, new Color(0.80f, 0.86f, 0.92f), TextAnchor.MiddleCenter, FontStyle.Normal);
            PlaceTop(tagTxt.rectTransform, -106f, w - 10f, 18f);

            Button selectBtn = RoadRashUiSprites.CreateBevelButton(
                cardObj.transform, $"SelectSkinBtn_{brand}", "CHỌN SKIN", RoadRashVisualTheme.MetalMid, 12);
            PlaceTop(selectBtn.GetComponent<RectTransform>(), -130f, w - 18f, 28f);
            selectBtn.onClick.AddListener(() => SelectSkinFromLobby(brand));

            _skinCards.Add(new SkinCardWidgets
            {
                Brand = brand,
                CardBg = cardBg,
                HeaderStrip = stripImg,
                PreviewIcon = iconImg,
                NameText = nameTxt,
                TaglineText = tagTxt,
                SelectBtnLabel = selectBtn.GetComponentInChildren<Text>(),
                SelectBtnImg = selectBtn.GetComponent<Image>()
            });
        }

        private Sprite ResolveBannerSprite()
        {
            if (customBannerSprite != null) return customBannerSprite;

            if (customBannerTexture != null)
            {
                return Sprite.Create(
                    customBannerTexture,
                    new Rect(0f, 0f, customBannerTexture.width, customBannerTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }

            if (_generatedBannerSprite != null) return _generatedBannerSprite;

            // Sinh Banner mặc định mang 4 dải màu thương hiệu (Xanh SM, Shopee Food, Grab, Ahamove)
            const int bw = 256;
            const int bh = 48;
            Texture2D tex = new Texture2D(bw, bh, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] brandColors =
            {
                new Color(0.00f, 0.72f, 0.71f), // Xanh SM
                new Color(0.93f, 0.30f, 0.18f), // Shopee Food
                new Color(0.00f, 0.69f, 0.31f), // Grab
                new Color(0.96f, 0.50f, 0.13f)  // Ahamove
            };

            for (int y = 0; y < bh; y++)
            {
                float v = y / (float)(bh - 1);
                for (int x = 0; x < bw; x++)
                {
                    float u = x / (float)(bw - 1);
                    int stripe = Mathf.Clamp(Mathf.FloorToInt(u * 4f), 0, 3);
                    Color baseDark = Color.Lerp(new Color(0.08f, 0.10f, 0.16f), new Color(0.16f, 0.20f, 0.28f), v);
                    Color accent = brandColors[stripe];

                    bool topOrBottomBorder = y < 4 || y >= bh - 4;
                    Color c = topOrBottomBorder
                        ? accent
                        : Color.Lerp(baseDark, accent, 0.22f);
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            _generatedBannerSprite = Sprite.Create(tex, new Rect(0, 0, bw, bh), new Vector2(0.5f, 0.5f), 100f);
            return _generatedBannerSprite;
        }

        private void AddDifficultyButton(float x, float y, DifficultyLevel level, string label)
        {
            Button btn = RoadRashUiSprites.CreateBevelButton(_menuPanel.transform, $"Diff_{level}", label, RoadRashVisualTheme.MetalMid, 13);
            PlaceTopLeft(btn.GetComponent<RectTransform>(), x, y, 82f, 30f);
            _diffBtnImgs[level] = btn.GetComponent<Image>();
            btn.onClick.AddListener(() =>
            {
                _gameManager?.SetDifficulty(level);
                RefreshMenu();
            });
        }

        private void BuildGaragePanel()
        {
            _garagePanel = CreateCenteredPanel(_canvas.transform, "GaragePanel", 720f, 540f);

            Text title = RoadRashUiSprites.CreateStyledText(
                _garagePanel.transform, "GarageTitle", "CỬA HÀNG NÂNG CẤP XE (GARAGE)", 26, RoadRashVisualTheme.HudYellow, TextAnchor.MiddleCenter);
            PlaceTop(title.rectTransform, -14f, 680f, 38f);

            _garageMoneyText = RoadRashUiSprites.CreateStyledText(
                _garagePanel.transform, "GarageMoney", "Tiền hiện có: $0", 18, RoadRashVisualTheme.AccentGreen, TextAnchor.MiddleCenter);
            PlaceTop(_garageMoneyText.rectTransform, -54f, 680f, 26f);

            _garageStatsText = RoadRashUiSprites.CreateStyledText(
                _garagePanel.transform, "GarageStats", "", 13, new Color(0.88f, 0.91f, 0.96f), TextAnchor.MiddleCenter);
            PlaceTop(_garageStatsText.rectTransform, -82f, 680f, 22f);

            // Animated stat bars
            _speedBarFill = CreateStatBar("MAX SPD", 40f, -112f, new Color(0.95f, 0.72f, 0.18f));
            _powerBarFill = CreateStatBar("POWER", 205f, -112f, new Color(0.92f, 0.35f, 0.24f));
            _brakeBarFill = CreateStatBar("BRAKE", 370f, -112f, new Color(0.28f, 0.76f, 0.94f));
            _gripBarFill = CreateStatBar("GRIP", 535f, -112f, RoadRashVisualTheme.AccentGreen);

            // 4 upgrade cards
            for (int i = 0; i < AllUpgradeTypes.Length; i++)
            {
                UpgradeType type = AllUpgradeTypes[i];
                float cardY = -148f - i * 74f;

                GameObject cardObj = new GameObject($"Card_{type}");
                cardObj.transform.SetParent(_garagePanel.transform, false);
                Image cardBg = cardObj.AddComponent<Image>();
                cardBg.sprite = RoadRashUiSprites.GetPanelSprite();
                cardBg.type = Image.Type.Sliced;
                cardBg.color = new Color(0.14f, 0.17f, 0.22f, 0.95f);
                PlaceTop(cardBg.rectTransform, cardY, 664f, 66f);

                Text cardTitle = RoadRashUiSprites.CreateStyledText(cardObj.transform, "CardTitle", "", 16, Color.white);
                PlaceTopLeft(cardTitle.rectTransform, 16f, -6f, 280f, 24f);

                Text cardEffect = RoadRashUiSprites.CreateStyledText(cardObj.transform, "CardEffect", "", 13, new Color(0.80f, 0.86f, 0.94f));
                PlaceTopLeft(cardEffect.rectTransform, 16f, -34f, 430f, 22f);

                UpgradeCardWidgets widgets = new UpgradeCardWidgets
                {
                    Type = type,
                    TitleText = cardTitle,
                    EffectText = cardEffect
                };

                // Level pips
                for (int p = 0; p < CarUpgradeSystem.MaxUpgradeLevel; p++)
                {
                    GameObject pip = new GameObject($"Pip_{p}");
                    pip.transform.SetParent(cardObj.transform, false);
                    Image pipImg = pip.AddComponent<Image>();
                    pipImg.raycastTarget = false;
                    PlaceTopLeft(pipImg.rectTransform, 310f + p * 26f, -11f, 20f, 14f);
                    widgets.LevelPips.Add(pipImg);
                }

                Button buyBtn = RoadRashUiSprites.CreateBevelButton(cardObj.transform, "BuyBtn", "Nâng cấp", new Color(0.24f, 0.58f, 0.28f), 14);
                PlaceTopLeft(buyBtn.GetComponent<RectTransform>(), 488f, -13f, 160f, 40f);
                widgets.ButtonImg = buyBtn.GetComponent<Image>();
                widgets.ButtonLabel = buyBtn.GetComponentInChildren<Text>();
                buyBtn.onClick.AddListener(() => OnBuyUpgradeClicked(type));

                _upgradeCards.Add(widgets);
            }

            _garageFeedbackText = RoadRashUiSprites.CreateStyledText(
                _garagePanel.transform, "Feedback", "", 15, RoadRashVisualTheme.HudYellow, TextAnchor.MiddleCenter);
            PlaceTop(_garageFeedbackText.rectTransform, -450f, 660f, 26f);
            _garageFeedbackText.gameObject.SetActive(false);

            Button backBtn = RoadRashUiSprites.CreateBevelButton(_garagePanel.transform, "BackBtn", "QUAY LẠI SẢNH CHỜ", RoadRashVisualTheme.MetalMid, 16);
            PlaceTopLeft(backBtn.GetComponent<RectTransform>(), 72f, -482f, 260f, 40f);
            backBtn.onClick.AddListener(() => _gameManager?.OpenMenu());

            Button raceBtn = RoadRashUiSprites.CreateBevelButton(_garagePanel.transform, "RaceNowBtn", "PLAY (ĐUA NGAY)", new Color(0.22f, 0.58f, 0.28f), 16);
            PlaceTopLeft(raceBtn.GetComponent<RectTransform>(), 388f, -482f, 260f, 40f);
            raceBtn.onClick.AddListener(TriggerPlay);

            _garagePanel.SetActive(false);
        }

        private void OnBuyUpgradeClicked(UpgradeType type)
        {
            if (_gameManager == null || _gameManager.UpgradeSystem == null) return;
            CarUpgradeSystem upg = _gameManager.UpgradeSystem;
            if (upg.IsMaxLevel(type))
            {
                SetShopFeedback("Đã đạt cấp tối đa!", RoadRashVisualTheme.HudYellow);
                return;
            }

            int cost = upg.GetUpgradeCost(type);
            if (_gameManager.TryBuyUpgrade(type))
            {
                SetShopFeedback($"Đã nâng cấp {upg.GetUpgradeDisplayName(type)} lên Lv {upg.GetLevel(type)}!", RoadRashVisualTheme.AccentGreen);
                RefreshGarage(false);
            }
            else
            {
                SetShopFeedback($"Không đủ tiền! Cần ${cost}.", RoadRashVisualTheme.DangerRed);
            }
        }

        private Image CreateStatBar(string label, float x, float y, Color fillColor)
        {
            Text lbl = RoadRashUiSprites.CreateStyledText(_garagePanel.transform, $"Lbl_{label}", label, 11, new Color(0.82f, 0.86f, 0.92f));
            PlaceTopLeft(lbl.rectTransform, x, y, 70f, 18f);

            GameObject track = new GameObject($"Bar_{label}");
            track.transform.SetParent(_garagePanel.transform, false);
            Image trackImg = track.AddComponent<Image>();
            trackImg.color = new Color(0.14f, 0.16f, 0.20f, 1f);
            trackImg.raycastTarget = false;
            PlaceTopLeft(trackImg.rectTransform, x + 64f, y + -3f, 82f, 12f);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(track.transform, false);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.raycastTarget = false;
            RectTransform frt = fillImg.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(0.5f, 1f);
            frt.offsetMin = new Vector2(1f, 1f);
            frt.offsetMax = new Vector2(-1f, -1f);
            return fillImg;
        }

        private static GameObject CreateCenteredPanel(Transform parent, string name, float w, float h)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Image img = obj.AddComponent<Image>();
            img.sprite = RoadRashUiSprites.GetPanelSprite();
            img.type = Image.Type.Sliced;
            img.color = RoadRashVisualTheme.PanelBackdrop;

            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            return obj;
        }

        private static void PlaceTop(RectTransform rt, float yOffset, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yOffset);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void PlaceTopLeft(RectTransform rt, float x, float yOffset, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, yOffset);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
