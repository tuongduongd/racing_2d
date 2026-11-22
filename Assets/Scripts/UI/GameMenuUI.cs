using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>
    /// Giao diện Sảnh Chờ (Main Menu & Lobby), Chọn Skin (4 hãng), Chọn Map Carousel,
    /// và Cửa hàng nâng cấp (Garage).
    /// Tuân thủ View != Logic: mọi nghiệp vụ tiền/mở khóa/lưu dữ liệu ủy thác cho Manager.
    /// </summary>
    public class GameMenuUI : MonoBehaviour
    {
        [Header("Banner / Logo & Hình Nền Sảnh Chờ")]
        [SerializeField] private Sprite customBannerSprite;
        [SerializeField] private Texture2D customBannerTexture;
        [SerializeField] private Sprite lobbyBackgroundSprite;
        [SerializeField] private TMP_FontAsset customTmpFont;
        [SerializeField] private string bannerTitle = "ROAD RASH";
        [SerializeField] private string bannerSubtitle = "CHIẾN THẦN GIAO HÀNG";

        [Header("Thiết lập Scene Khởi Động (Lobby)")]
        [SerializeField] private bool isStandaloneLobbyScene;
        [SerializeField] private string gameplaySceneName = "SampleScene";

        private GameManager _gameManager;
        private SkinManager _skinManager;
        private MapManager _mapManager;
        private CarUpgradeSystem _standaloneUpgradeSystem;
        private CrtScanlineController _crtController;

        private Canvas _canvas;
        private GameObject _menuPanel;
        private GameObject _skinSelectorPanel;
        private GameObject _garagePanel;
        private Button _outerQuitButtonX;
        private Image _bannerImage;
        private Image _lobbyBackgroundImg;
        private Sprite _generatedBannerSprite;

        // Lobby Header & Command Column
        private TMP_Text _menuStatsText;
        private TMP_Text _lobbyFeedbackText;
        private float _lobbyFeedbackUntil;
        private TMP_Text _menuMuteLabel;
        private TMP_Text _crtBtnLabel;
        private Image _crtBtnImg;
        private TMP_Text _resetBtnLabel;
        private bool _confirmResetPending;

        private int _selectedLaps = 3;
        private DifficultyLevel _selectedDifficulty = DifficultyLevel.Normal;
        private readonly Dictionary<DifficultyLevel, Image> _diffBtnImgs = new Dictionary<DifficultyLevel, Image>();
        private readonly Dictionary<DifficultyLevel, TMP_Text> _diffBtnLabels = new Dictionary<DifficultyLevel, TMP_Text>();
        private readonly Dictionary<int, Image> _lapBtnImgs = new Dictionary<int, Image>();
        private readonly Dictionary<int, TMP_Text> _lapBtnLabels = new Dictionary<int, TMP_Text>();

        // Lobby Right Top: Active Bike Showcase
        private TMP_Text _selectedSkinStatusText;
        private TMP_Text _activeBikeTaglineText;
        private Image _activeBikeAccentBar;
        private Image _activeBikePreviewImage;

        // Lobby Right Bottom: Map Carousel
        private Image _mapBannerImage;
        private Image _mapAccentStrip;
        private TMP_Text _mapStatusBadgeText;
        private Image _mapStatusBadgeBg;
        private TMP_Text _mapNameText;
        private TMP_Text _mapMetaText;
        private TMP_Text _mapDescText;
        private Button _mapActionButton;
        private Image _mapActionBtnImg;
        private TMP_Text _mapActionBtnLabel;
        private RectTransform _mapTabsContainer;
        private readonly List<MapTabWidgets> _mapTabs = new List<MapTabWidgets>();
        private int _browsedMapIndex;
        private string _mapFeedbackMsg;
        private Color _mapFeedbackColor;
        private float _mapFeedbackUntil;

        // Dedicated Skin Selector Panel (4 Brands)
        private readonly List<SkinCardWidgets> _skinCards = new List<SkinCardWidgets>();
        private Image _skinModalPreviewImage;
        private Image _skinModalAccentBar;
        private TMP_Text _skinModalBrandTitle;
        private TMP_Text _skinModalBrandTagline;
        private TMP_Text _skinModalWalletText;

        // Garage elements (Stage 4B preserves existing Garage functionality)
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

        private static readonly DriverBrandSkin[] AllBrandOrder =
        {
            DriverBrandSkin.XanhSM,
            DriverBrandSkin.ShopeeFood,
            DriverBrandSkin.Grab,
            DriverBrandSkin.Ahamove
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
        public int SelectedLaps => _gameManager != null ? _gameManager.TotalLaps : _selectedLaps;
        public DifficultyLevel SelectedDifficulty => _gameManager != null ? _gameManager.Difficulty : _selectedDifficulty;
        public bool IsSkinSelectorOpen => _skinSelectorPanel != null && _skinSelectorPanel.activeSelf;

        public bool IsBrowsedMapLocked
        {
            get
            {
                MapManager mm = ResolveMapManager();
                TrackDefinition browsed = BrowsedMap;
                return mm != null && browsed != null && !mm.IsMapUnlocked(browsed.MapId);
            }
        }

        private void OnDestroy()
        {
            if (_generatedBannerSprite != null)
            {
                if (_generatedBannerSprite.texture != null)
                {
                    DestroyUnityObject(_generatedBannerSprite.texture);
                }
                DestroyUnityObject(_generatedBannerSprite);
                _generatedBannerSprite = null;
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

        private CrtScanlineController ResolveCrtController()
        {
            if (_crtController == null)
            {
                _crtController = FindAnyObjectByType<CrtScanlineController>();
            }
            return _crtController;
        }

        private void Start()
        {
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

            _selectedLaps = GameManager.LoadSavedTotalLaps(3);
            _selectedDifficulty = GameManager.LoadSavedDifficulty(DifficultyLevel.Normal);

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

            if (_gameManager != null)
            {
                _selectedLaps = _gameManager.TotalLaps;
                _selectedDifficulty = _gameManager.Difficulty;
            }

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

        public void OpenSkinSelector()
        {
            EnsureBuilt();
            if (_skinSelectorPanel != null)
            {
                _skinSelectorPanel.SetActive(true);
                RefreshSkinCards();
            }
        }

        public void CloseSkinSelector()
        {
            if (_skinSelectorPanel != null)
            {
                _skinSelectorPanel.SetActive(false);
                RefreshSkinCards();
            }
        }

        public void SelectLapsFromLobby(int laps)
        {
            _selectedLaps = Mathf.Clamp(laps, 1, 10);
            if (_gameManager != null)
            {
                _gameManager.SetTotalLaps(_selectedLaps);
            }
            else
            {
                GameManager.SaveTotalLapsPreference(_selectedLaps);
            }
            RefreshMenu();
        }

        public void SelectDifficultyFromLobby(DifficultyLevel level)
        {
            _selectedDifficulty = level;
            if (_gameManager != null)
            {
                _gameManager.SetDifficulty(level);
            }
            else
            {
                GameManager.SaveDifficultyPreference(level);
            }
            RefreshMenu();
        }

        public bool ToggleCrtFromLobby()
        {
            CrtScanlineController crt = ResolveCrtController();
            bool result = crt != null && crt.ToggleCrt();
            RefreshMenuDynamic();
            return result;
        }

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
                SetMapFeedback($"Đã mở khóa và chọn {target.DisplayName}!", LobbyUiBuilder.SuccessGreen);
                RefreshMenu();
            }
            else
            {
                SetMapFeedback($"Không đủ tiền! Cần ${cost} để mở khóa {target.DisplayName}.", LobbyUiBuilder.DangerRed);
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
                SetMapFeedback($"Map đang khóa! Bấm MỞ KHÓA (${cost}) hoặc chọn map đã mở.", LobbyUiBuilder.DangerRed);
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

        private void SetLobbyFeedback(string msg, Color color)
        {
            if (_lobbyFeedbackText == null) return;
            _lobbyFeedbackText.text = msg;
            _lobbyFeedbackText.color = color;
            _lobbyFeedbackText.gameObject.SetActive(true);
            _lobbyFeedbackUntil = Time.unscaledTime + 2.5f;
        }

        public void TriggerPlay()
        {
            _confirmResetPending = false;
            if (_gameManager == null)
            {
                GameManager.SaveTotalLapsPreference(_selectedLaps);
                GameManager.SaveDifficultyPreference(_selectedDifficulty);
            }

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

            if (_skinSelectorPanel != null && _skinSelectorPanel.activeSelf &&
                Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseSkinSelector();
                return;
            }

            if (_lobbyFeedbackText != null && _lobbyFeedbackText.gameObject.activeSelf &&
                Time.unscaledTime > _lobbyFeedbackUntil)
            {
                _lobbyFeedbackText.gameObject.SetActive(false);
            }

            if (_gameManager == null)
            {
                if (_menuPanel != null && !_menuPanel.activeSelf) _menuPanel.SetActive(true);
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
                if (!showMenu && _skinSelectorPanel != null && _skinSelectorPanel.activeSelf)
                {
                    _skinSelectorPanel.SetActive(false);
                }
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
            CarUpgradeSystem upg = ResolveUpgradeSystem();
            int money = upg != null ? upg.Money : 200;
            int won = upg != null ? upg.RacesWon : 0;
            int played = upg != null ? upg.RacesPlayed : 0;
            string best = (upg != null && upg.BestRaceTime > 0f) ? RaceHUD.FormatTime(upg.BestRaceTime) : "--:--.--";

            if (_menuStatsText != null)
            {
                _menuStatsText.text = $"{money:N0} XU   •   THẮNG {won}/{played}   •   KỶ LỤC {best}";
            }
            if (_skinModalWalletText != null)
            {
                _skinModalWalletText.text = $"{money:N0} XU";
            }

            DifficultyLevel activeDiff = SelectedDifficulty;
            foreach (KeyValuePair<DifficultyLevel, Image> kv in _diffBtnImgs)
            {
                bool sel = activeDiff == kv.Key;
                kv.Value.color = sel ? LobbyUiBuilder.SafetyAmber : LobbyUiBuilder.SurfaceHigh;
                if (_diffBtnLabels.TryGetValue(kv.Key, out TMP_Text lbl) && lbl != null)
                {
                    lbl.color = sel ? LobbyUiBuilder.OnAmber : LobbyUiBuilder.TextPrimary;
                }
            }

            int activeLaps = SelectedLaps;
            foreach (KeyValuePair<int, Image> kv in _lapBtnImgs)
            {
                bool sel = activeLaps == kv.Key;
                kv.Value.color = sel ? LobbyUiBuilder.ElectricCyan : LobbyUiBuilder.SurfaceHigh;
                if (_lapBtnLabels.TryGetValue(kv.Key, out TMP_Text lbl) && lbl != null)
                {
                    lbl.color = sel ? LobbyUiBuilder.OnCyan : LobbyUiBuilder.TextPrimary;
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

            if (_mapBannerImage != null)
            {
                _mapBannerImage.sprite = displayMap.PreviewBannerSprite;
                _mapBannerImage.color = displayMap.PreviewBannerSprite != null
                    ? (isUnlocked ? Color.white : new Color(0.55f, 0.42f, 0.42f, 1f))
                    : new Color(0.08f, 0.10f, 0.14f, 1f);
            }

            if (_mapAccentStrip != null)
            {
                _mapAccentStrip.color = isUnlocked
                    ? (displayMap.Theme != null ? displayMap.Theme.CenterYellowLine : LobbyUiBuilder.ElectricCyan)
                    : LobbyUiBuilder.DangerRed;
            }

            if (_mapStatusBadgeText != null && _mapStatusBadgeBg != null)
            {
                if (!isUnlocked)
                {
                    _mapStatusBadgeText.text = $"KHÓA • ${mm.GetMapUnlockCost(displayMap.MapId)}";
                    _mapStatusBadgeBg.color = LobbyUiBuilder.DangerRed;
                    _mapStatusBadgeText.color = Color.white;
                }
                else if (isSelected)
                {
                    _mapStatusBadgeText.text = "ĐÃ CHỌN";
                    _mapStatusBadgeBg.color = LobbyUiBuilder.ElectricCyan;
                    _mapStatusBadgeText.color = LobbyUiBuilder.OnCyan;
                }
                else
                {
                    _mapStatusBadgeText.text = "ĐÃ MỞ KHÓA";
                    _mapStatusBadgeBg.color = LobbyUiBuilder.SuccessGreen;
                    _mapStatusBadgeText.color = LobbyUiBuilder.OnCyan;
                }
            }

            if (_mapNameText != null)
            {
                string prefix = isUnlocked ? $"MAP {idx:00}/{total:00}" : $"[KHÓA] MAP {idx:00}/{total:00}";
                _mapNameText.text = $"{prefix}: {displayMap.DisplayName.ToUpperInvariant()}";
                _mapNameText.color = isUnlocked ? Color.white : new Color(1f, 0.68f, 0.62f, 1f);
            }

            if (_mapMetaText != null)
            {
                string stars = new string('★', displayMap.DifficultyStars) + new string('☆', Mathf.Max(0, 5 - displayMap.DifficultyStars));
                float best = mm.GetBestTime(displayMap.MapId);
                string bestStr = best > 0f ? RaceHUD.FormatTime(best) : "--:--.--";
                string policeTag = displayMap.EnablePoliceChase ? "CS: BẬT" : "CS: TẮT";
                _mapMetaText.text =
                    $"{displayMap.LaneCount} LÀN ({displayMap.TotalTrackWidth:0}m)  •  {stars}  •  x{displayMap.RewardMultiplier:0.0}  •  {policeTag}  •  KỶ LỤC {bestStr}";
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
                    _mapDescText.color = LobbyUiBuilder.TextMuted;
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
                    _mapActionBtnImg.color = canAfford ? LobbyUiBuilder.SuccessGreen : LobbyUiBuilder.DangerRed;
                    _mapActionBtnLabel.color = canAfford ? LobbyUiBuilder.OnCyan : Color.white;
                }
                else if (isSelected)
                {
                    _mapActionBtnLabel.text = "✓ ĐANG CHỌN";
                    _mapActionBtnImg.color = LobbyUiBuilder.ElectricCyan;
                    _mapActionBtnLabel.color = LobbyUiBuilder.OnCyan;
                }
                else
                {
                    _mapActionBtnLabel.text = "CHỌN MAP";
                    _mapActionBtnImg.color = LobbyUiBuilder.SafetyAmber;
                    _mapActionBtnLabel.color = LobbyUiBuilder.OnAmber;
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
                    tab.Bg.color = tabBrowsed ? new Color(0.46f, 0.20f, 0.20f) : new Color(0.18f, 0.14f, 0.16f);
                    tab.Label.color = new Color(0.95f, 0.72f, 0.70f);
                }
                else if (tabSelected)
                {
                    tab.Label.text = $"▶ {shortName}";
                    tab.Bg.color = LobbyUiBuilder.ElectricCyan;
                    tab.Label.color = LobbyUiBuilder.OnCyan;
                }
                else
                {
                    tab.Label.text = shortName;
                    tab.Bg.color = tabBrowsed ? LobbyUiBuilder.OutlineVariant : LobbyUiBuilder.SurfaceHigh;
                    tab.Label.color = LobbyUiBuilder.TextPrimary;
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
                DestroyUnityObject(_mapTabsContainer.GetChild(i).gameObject);
            }
            _mapTabs.Clear();

            int count = mm.MapCount;
            if (count <= 0) return;

            const float totalWidth = 612f;
            const float gap = 6f;
            float tabWidth = (totalWidth - gap * (count - 1)) / count;

            for (int i = 0; i < count; i++)
            {
                TrackDefinition def = mm.GetMapAt(i);
                if (def == null) continue;

                MapTabWidgets tab = LobbyUiBuilder.CreateMapTab(
                    _mapTabsContainer,
                    i,
                    def.MapId,
                    def.DisplayName,
                    i * (tabWidth + gap),
                    tabWidth,
                    28f,
                    idx => FocusOrSelectMapIndex(idx),
                    customTmpFont);
                _mapTabs.Add(tab);
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

            if (activeData != null)
            {
                Sprite activePreview = ResolveSkinPreviewSprite(activeData);

                if (_selectedSkinStatusText != null)
                {
                    _selectedSkinStatusText.text = activeData.DisplayName.ToUpperInvariant();
                    _selectedSkinStatusText.color = Color.Lerp(activeData.PrimaryColor, Color.white, 0.35f);
                }
                if (_activeBikeTaglineText != null)
                {
                    _activeBikeTaglineText.text = activeData.Tagline.ToUpperInvariant();
                }
                if (_activeBikeAccentBar != null)
                {
                    _activeBikeAccentBar.color = activeData.PrimaryColor;
                }
                if (_activeBikePreviewImage != null)
                {
                    _activeBikePreviewImage.sprite = activePreview;
                    _activeBikePreviewImage.color = activePreview != null ? Color.white : LobbyUiBuilder.SurfaceLowest;
                }

                if (_skinModalPreviewImage != null)
                {
                    _skinModalPreviewImage.sprite = activePreview;
                }
                if (_skinModalAccentBar != null)
                {
                    _skinModalAccentBar.color = activeData.PrimaryColor;
                }
                if (_skinModalBrandTitle != null)
                {
                    _skinModalBrandTitle.text = activeData.DisplayName.ToUpperInvariant();
                    _skinModalBrandTitle.color = Color.Lerp(activeData.PrimaryColor, Color.white, 0.35f);
                }
                if (_skinModalBrandTagline != null)
                {
                    _skinModalBrandTagline.text = activeData.Tagline;
                }
            }

            for (int i = 0; i < _skinCards.Count; i++)
            {
                SkinCardWidgets card = _skinCards[i];
                BikeSkinData data = _skinManager.GetSkin(card.Brand);
                bool isSelected = card.Brand == activeBrand;

                if (data != null)
                {
                    card.NameText.text = data.DisplayName.ToUpperInvariant();
                    card.TaglineText.text = data.Tagline;
                    card.HeaderStrip.color = data.PrimaryColor;
                    card.PreviewIcon.sprite = ResolveSkinPreviewSprite(data);
                }

                card.CardBg.color = isSelected
                    ? new Color(0.11f, 0.21f, 0.26f, 0.98f)
                    : LobbyUiBuilder.SurfaceContainer;
                card.SelectBtnLabel.text = isSelected ? "✓ ĐANG CHỌN" : "CHỌN SKIN";
                card.SelectBtnImg.color = isSelected
                    ? LobbyUiBuilder.ElectricCyan
                    : (data != null ? Color.Lerp(data.PrimaryColor, LobbyUiBuilder.SurfaceHigh, 0.45f) : LobbyUiBuilder.SurfaceHigh);
                card.SelectBtnLabel.color = isSelected ? LobbyUiBuilder.OnCyan : Color.white;
            }
        }

        private static Sprite ResolveSkinPreviewSprite(BikeSkinData data)
        {
            if (data == null) return null;
            if (data.CustomLogoSprite != null) return data.CustomLogoSprite;
            Sprite ov = data.GetOverrideSprite();
            if (ov != null) return ov;
            return BikePaletteSwapper.GetOrCreateSet(data).Upright0;
        }

        private void RefreshMenuDynamic()
        {
            if (_menuMuteLabel != null && _gameManager != null && _gameManager.AudioController != null)
            {
                _menuMuteLabel.text = _gameManager.AudioController.IsMuted ? "ÂM: TẮT" : "ÂM: BẬT";
            }

            if (_crtBtnLabel != null)
            {
                CrtScanlineController crt = ResolveCrtController();
                bool crtOn = crt != null && crt.IsEnabled;
                _crtBtnLabel.text = crtOn ? "CRT FX: BẬT" : "CRT FX: TẮT";
                if (_crtBtnImg != null)
                {
                    _crtBtnImg.color = crtOn ? new Color(0.08f, 0.24f, 0.28f, 1f) : LobbyUiBuilder.SurfaceHigh;
                }
            }

            if (_resetBtnLabel != null)
            {
                _resetBtnLabel.text = _confirmResetPending ? "BẤM LẦN 2 ĐỂ XÓA!" : "XÓA DỮ LIỆU";
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
            BuildMainMenuPanel();
            BuildSkinSelectorPanel();
            BuildGaragePanel();
            BuildOuterQuitButton();
        }

        private void BuildOuterQuitButton()
        {
            _outerQuitButtonX = LobbyUiBuilder.CreateCyberButton(
                _canvas.transform,
                "QuitButtonX",
                "[X] THOÁT",
                LobbyUiBuilder.DangerRed,
                Color.white,
                12f,
                null,
                customTmpFont);

            RectTransform rt = _outerQuitButtonX.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -10f);
            rt.sizeDelta = new Vector2(96f, 36f);

            _outerQuitButtonX.onClick.AddListener(TriggerQuit);
        }

        private void BuildMainMenuPanel()
        {
            _menuPanel = new GameObject("MainMenuPanel");
            _menuPanel.transform.SetParent(_canvas.transform, false);
            RectTransform rootRt = _menuPanel.AddComponent<RectTransform>();
            LobbyUiBuilder.StretchFill(rootRt);

            // 1. Full-screen Lobby Background + Cyber Gradient Scrim
            GameObject bgObj = new GameObject("LobbyBackground");
            bgObj.transform.SetParent(_menuPanel.transform, false);
            _lobbyBackgroundImg = bgObj.AddComponent<Image>();
            _lobbyBackgroundImg.sprite = lobbyBackgroundSprite;
            _lobbyBackgroundImg.color = lobbyBackgroundSprite != null
                ? Color.white
                : new Color(0.06f, 0.08f, 0.11f, 0.85f);
            _lobbyBackgroundImg.preserveAspect = false;
            _lobbyBackgroundImg.raycastTarget = false;
            LobbyUiBuilder.StretchFill(_lobbyBackgroundImg.rectTransform);

            GameObject scrimObj = new GameObject("LobbyScrim");
            scrimObj.transform.SetParent(_menuPanel.transform, false);
            Image scrimImg = scrimObj.AddComponent<Image>();
            scrimImg.sprite = LobbyUiBuilder.GetSoftDarkScrimSprite();
            scrimImg.color = new Color(1f, 1f, 1f, 0.90f);
            scrimImg.raycastTarget = false;
            LobbyUiBuilder.StretchFill(scrimImg.rectTransform);

            // 2. Top Header Bar (56px)
            Image headerBar = LobbyUiBuilder.CreateCyberPanel(
                _menuPanel.transform, "LobbyHeaderBar", LobbyUiBuilder.SurfaceLowest);
            LobbyUiBuilder.PlaceTop(headerBar.rectTransform, 0f, 1280f, 56f);
            headerBar.rectTransform.anchorMin = new Vector2(0f, 1f);
            headerBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            headerBar.rectTransform.sizeDelta = new Vector2(0f, 56f);

            GameObject bannerObj = new GameObject("LobbyBanner");
            bannerObj.transform.SetParent(headerBar.transform, false);
            _bannerImage = bannerObj.AddComponent<Image>();
            _bannerImage.sprite = ResolveBannerSprite();
            _bannerImage.preserveAspect = true;
            _bannerImage.raycastTarget = false;
            LobbyUiBuilder.PlaceTopLeft(_bannerImage.rectTransform, 24f, -10f, 120f, 36f);

            TMP_Text logoTitle = LobbyUiBuilder.CreateTmpText(
                headerBar.transform, "HeaderLogoTitle", bannerTitle, 20f, LobbyUiBuilder.ElectricCyan,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(logoTitle.rectTransform, 154f, -8f, 180f, 22f);

            TMP_Text logoSub = LobbyUiBuilder.CreateTmpText(
                headerBar.transform, "HeaderLogoSub", "ARCADE MOTORBIKE 2D", 10f, LobbyUiBuilder.SafetyAmber,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(logoSub.rectTransform, 154f, -30f, 180f, 16f);

            Image walletPill = LobbyUiBuilder.CreateCyberPanel(headerBar.transform, "WalletPill", LobbyUiBuilder.SurfaceHigh);
            LobbyUiBuilder.PlaceTopRight(walletPill.rectTransform, -356f, -10f, 390f, 36f);
            _menuStatsText = LobbyUiBuilder.CreateTmpText(
                walletPill.transform, "Stats", "200 XU", 12f, LobbyUiBuilder.SafetyAmber,
                TextAlignmentOptions.Center, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.StretchFill(_menuStatsText.rectTransform, 10f, 2f);

            Button muteBtn = LobbyUiBuilder.CreateCyberButton(
                headerBar.transform, "MuteBtn", "ÂM: BẬT",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.TextPrimary, 11f, null, customTmpFont);
            LobbyUiBuilder.PlaceTopRight(muteBtn.GetComponent<RectTransform>(), -248f, -10f, 100f, 36f);
            _menuMuteLabel = LobbyUiBuilder.GetPrimaryLabel(muteBtn);
            muteBtn.onClick.AddListener(() =>
            {
                _gameManager?.AudioController?.ToggleMute();
                RefreshMenuDynamic();
            });

            Button crtBtn = LobbyUiBuilder.CreateCyberButton(
                headerBar.transform, "CrtToggleBtn", "CRT FX: BẬT",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.ElectricCyan, 11f, null, customTmpFont);
            LobbyUiBuilder.PlaceTopRight(crtBtn.GetComponent<RectTransform>(), -128f, -10f, 112f, 36f);
            _crtBtnImg = crtBtn.GetComponent<Image>();
            _crtBtnLabel = LobbyUiBuilder.GetPrimaryLabel(crtBtn);
            crtBtn.onClick.AddListener(() => ToggleCrtFromLobby());

            // 3. Left Command Column (x = 32, y = -74, w = 556)
            TMP_Text tagBadge = LobbyUiBuilder.CreateTmpText(
                _menuPanel.transform, "EditionTag", "OFFLINE ARCADE EDITION // V2.0", 11f, LobbyUiBuilder.SafetyAmber,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(tagBadge.rectTransform, 32f, -74f, 540f, 20f);

            TMP_Text heroTitle = LobbyUiBuilder.CreateTmpText(
                _menuPanel.transform, "HeroTitle", bannerTitle, 54f, LobbyUiBuilder.ElectricCyan,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(heroTitle.rectTransform, 30f, -94f, 550f, 58f);

            TMP_Text heroSub = LobbyUiBuilder.CreateTmpText(
                _menuPanel.transform, "HeroSubtitle", bannerSubtitle, 28f, LobbyUiBuilder.SafetyAmber,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(heroSub.rectTransform, 32f, -150f, 550f, 34f);

            TMP_Text heroDesc = LobbyUiBuilder.CreateTmpText(
                _menuPanel.transform, "HeroDesc",
                "Đua xe tay ga đường phố bất quy tắc. Đạp xe đối thủ, né chướng ngại vật và bứt tốc qua 4 cung đường!",
                13f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.TopLeft, FontStyles.Normal, customTmpFont, wrap: true);
            LobbyUiBuilder.PlaceTopLeft(heroDesc.rectTransform, 32f, -190f, 548f, 40f);

            Button playBtn = LobbyUiBuilder.CreateCyberButton(
                _menuPanel.transform,
                "PlayBtn",
                "▶  CHƠI NGAY (PLAY)",
                LobbyUiBuilder.ElectricCyan,
                LobbyUiBuilder.OnCyan,
                22f,
                "XUẤT PHÁT ĐẤU TRƯỜNG TỐC ĐỘ ĐƯỜNG PHỐ",
                customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(playBtn.GetComponent<RectTransform>(), 32f, -242f, 556f, 70f);
            playBtn.onClick.AddListener(OnPlayButtonClicked);

            const float actionBtnY = -324f;
            const float actionBtnW = 178f;
            const float actionBtnGap = 11f;

            Button skinModalBtn = LobbyUiBuilder.CreateCyberButton(
                _menuPanel.transform, "OpenSkinsBtn", "CHỌN SKIN (4 HÃNG)",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.TextPrimary, 13f,
                "Xanh SM • Shopee • Grab • Ahamove", customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(skinModalBtn.GetComponent<RectTransform>(), 32f, actionBtnY, actionBtnW, 62f);
            skinModalBtn.onClick.AddListener(OpenSkinSelector);

            Button garageBtn = LobbyUiBuilder.CreateCyberButton(
                _menuPanel.transform, "GarageBtn", "GARAGE NÂNG CẤP",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.SafetyAmber, 13f,
                "Động cơ • Phanh • Bám • Trọng lượng", customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(garageBtn.GetComponent<RectTransform>(), 32f + (actionBtnW + actionBtnGap), actionBtnY, actionBtnW, 62f);
            garageBtn.onClick.AddListener(() =>
            {
                _confirmResetPending = false;
                if (_gameManager != null)
                {
                    _gameManager.OpenGarage();
                }
                else
                {
                    SetLobbyFeedback("Hãy bấm CHƠI NGAY để mở Garage trong trận đua!", LobbyUiBuilder.SafetyAmber);
                }
            });

            Button resetBtn = LobbyUiBuilder.CreateCyberButton(
                _menuPanel.transform, "ResetBtn", "XÓA DỮ LIỆU",
                new Color(0.28f, 0.14f, 0.14f, 1f), LobbyUiBuilder.TextPrimary, 13f,
                "Đặt lại tiền & map đã mở", customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(resetBtn.GetComponent<RectTransform>(), 32f + 2f * (actionBtnW + actionBtnGap), actionBtnY, actionBtnW, 62f);
            _resetBtnLabel = LobbyUiBuilder.GetPrimaryLabel(resetBtn);
            resetBtn.onClick.AddListener(OnResetSaveClicked);

            // Race Settings Box (Difficulty + Laps)
            Image settingsPanel = LobbyUiBuilder.CreateCyberPanel(
                _menuPanel.transform, "RaceSettingsBox", LobbyUiBuilder.SurfaceLow, LobbyUiBuilder.OutlineVariant);
            LobbyUiBuilder.PlaceTopLeft(settingsPanel.rectTransform, 32f, -398f, 556f, 224f);

            TMP_Text settingsTitle = LobbyUiBuilder.CreateTmpText(
                settingsPanel.transform, "SettingsHeader", "CẤU HÌNH TRẬN ĐẤU (RACE SETTINGS)", 12f,
                LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(settingsTitle.rectTransform, 16f, -12f, 360f, 22f);

            TMP_Text localBadge = LobbyUiBuilder.CreateTmpText(
                settingsPanel.transform, "LocalBadge", "ARCADE LOCAL", 10f,
                LobbyUiBuilder.TextMuted, TextAlignmentOptions.MidlineRight, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopRight(localBadge.rectTransform, -16f, -14f, 140f, 18f);

            TMP_Text diffHeader = LobbyUiBuilder.CreateTmpText(
                settingsPanel.transform, "DiffLabel", "ĐỘ KHÓ AI ĐỐI THỦ", 11f,
                LobbyUiBuilder.TextMuted, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(diffHeader.rectTransform, 16f, -44f, 260f, 20f);

            _diffBtnImgs.Clear();
            _diffBtnLabels.Clear();
            AddDifficultyButton(settingsPanel.transform, 16f, -68f, 168f, 40f, DifficultyLevel.Easy, "DỄ (EASY)");
            AddDifficultyButton(settingsPanel.transform, 194f, -68f, 168f, 40f, DifficultyLevel.Normal, "VỪA (NORMAL)");
            AddDifficultyButton(settingsPanel.transform, 372f, -68f, 168f, 40f, DifficultyLevel.Hard, "KHÓ (HARD)");

            TMP_Text lapHeader = LobbyUiBuilder.CreateTmpText(
                settingsPanel.transform, "LapLabel", "SỐ VÒNG ĐUA (LAPS)", 11f,
                LobbyUiBuilder.TextMuted, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(lapHeader.rectTransform, 16f, -122f, 260f, 20f);

            _lapBtnImgs.Clear();
            _lapBtnLabels.Clear();
            for (int i = 0; i < LapChoices.Length; i++)
            {
                int laps = LapChoices[i];
                Button btn = LobbyUiBuilder.CreateCyberButton(
                    settingsPanel.transform, $"LapBtn_{laps}", $"{laps} VÒNG",
                    LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.TextPrimary, 13f, null, customTmpFont);
                LobbyUiBuilder.PlaceTopLeft(btn.GetComponent<RectTransform>(), 16f + i * 133f, -146f, 125f, 40f);
                _lapBtnImgs[laps] = btn.GetComponent<Image>();
                _lapBtnLabels[laps] = LobbyUiBuilder.GetPrimaryLabel(btn);
                btn.onClick.AddListener(() => SelectLapsFromLobby(laps));
            }

            _lobbyFeedbackText = LobbyUiBuilder.CreateTmpText(
                settingsPanel.transform, "LobbyFeedback", "", 12f, LobbyUiBuilder.SafetyAmber,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(_lobbyFeedbackText.rectTransform, 16f, -194f, 524f, 22f);
            _lobbyFeedbackText.gameObject.SetActive(false);

            // 4. Right Column: Active Equipped Bike Showcase (top) + Map Carousel (bottom)
            BuildRightActiveBikeCard();
            BuildRightMapCarouselCard();

            // 5. Bottom Keyboard Hints Strip (32px)
            Image bottomBar = LobbyUiBuilder.CreateCyberPanel(
                _menuPanel.transform, "BottomControlsBar", LobbyUiBuilder.SurfaceLowest);
            LobbyUiBuilder.PlaceBottomStretch(bottomBar.rectTransform, 32f);

            TMP_Text controlsHint = LobbyUiBuilder.CreateTmpText(
                bottomBar.transform, "ControlsHint",
                "THAO TÁC:   [W / ↑] TĂNG GA   •   [S / ↓] PHANH   •   [A / D] LÁI XE   •   [SPACE] ĐẠP ĐỐI THỦ   •   [M] ÂM THANH   •   [ESC] TẠM DỪNG",
                11f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.Center, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.StretchFill(controlsHint.rectTransform, 16f, 2f);
        }

        private void BuildRightActiveBikeCard()
        {
            Image card = LobbyUiBuilder.CreateCyberPanel(
                _menuPanel.transform, "ActiveBikeCard", LobbyUiBuilder.SurfaceContainer, LobbyUiBuilder.ElectricCyan);
            LobbyUiBuilder.PlaceTopLeft(card.rectTransform, 612f, -72f, 636f, 266f);

            TMP_Text badge = LobbyUiBuilder.CreateTmpText(
                card.transform, "EquippedHeader", "● XẾ CHIẾN ĐANG TRANG BỊ", 11f, LobbyUiBuilder.ElectricCyan,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(badge.rectTransform, 16f, -12f, 260f, 20f);

            Button swapSkinBtn = LobbyUiBuilder.CreateCyberButton(
                card.transform, "QuickChangeSkinBtn", "ĐỔI SKIN ⇄",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.TextPrimary, 12f, null, customTmpFont);
            LobbyUiBuilder.PlaceTopRight(swapSkinBtn.GetComponent<RectTransform>(), -16f, -10f, 130f, 30f);
            swapSkinBtn.onClick.AddListener(OpenSkinSelector);

            _selectedSkinStatusText = LobbyUiBuilder.CreateTmpText(
                card.transform, "ActiveSkinName", "XANH SM", 24f, LobbyUiBuilder.ElectricCyan,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(_selectedSkinStatusText.rectTransform, 16f, -38f, 440f, 30f);

            _activeBikeTaglineText = LobbyUiBuilder.CreateTmpText(
                card.transform, "ActiveSkinTagline", "THUẦN ĐIỆN XANH CYAN", 12f, LobbyUiBuilder.TextMuted,
                TextAlignmentOptions.MidlineLeft, FontStyles.Normal, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(_activeBikeTaglineText.rectTransform, 16f, -68f, 440f, 20f);

            Image previewFrame = LobbyUiBuilder.CreateCyberPanel(card.transform, "ActivePreviewStage", LobbyUiBuilder.SurfaceLowest);
            LobbyUiBuilder.PlaceTopLeft(previewFrame.rectTransform, 16f, -94f, 604f, 158f);

            GameObject previewImgObj = new GameObject("ActiveBikePreviewImg");
            previewImgObj.transform.SetParent(previewFrame.transform, false);
            _activeBikePreviewImage = previewImgObj.AddComponent<Image>();
            _activeBikePreviewImage.preserveAspect = true;
            _activeBikePreviewImage.raycastTarget = false;
            LobbyUiBuilder.StretchFill(_activeBikePreviewImage.rectTransform, 3f, 3f);

            GameObject accentBarObj = new GameObject("ActiveBikeAccent");
            accentBarObj.transform.SetParent(previewFrame.transform, false);
            _activeBikeAccentBar = accentBarObj.AddComponent<Image>();
            _activeBikeAccentBar.raycastTarget = false;
            _activeBikeAccentBar.color = LobbyUiBuilder.ElectricCyan;
            LobbyUiBuilder.PlaceBottomStretch(_activeBikeAccentBar.rectTransform, 4f);
        }

        private void BuildRightMapCarouselCard()
        {
            Image mapBox = LobbyUiBuilder.CreateCyberPanel(
                _menuPanel.transform, "MapSelectorBox", LobbyUiBuilder.SurfaceContainer);
            LobbyUiBuilder.PlaceTopLeft(mapBox.rectTransform, 612f, -350f, 636f, 272f);

            GameObject stripObj = new GameObject("MapAccentStrip");
            stripObj.transform.SetParent(mapBox.transform, false);
            _mapAccentStrip = stripObj.AddComponent<Image>();
            _mapAccentStrip.raycastTarget = false;
            _mapAccentStrip.color = LobbyUiBuilder.ElectricCyan;
            LobbyUiBuilder.PlaceTopLeft(_mapAccentStrip.rectTransform, 2f, -2f, 632f, 4f);

            TMP_Text header = LobbyUiBuilder.CreateTmpText(
                mapBox.transform, "MapBoxHeader", "BẢN ĐỒ THI ĐẤU (TRACK SELECT)", 11f, LobbyUiBuilder.SafetyAmber,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(header.rectTransform, 16f, -12f, 300f, 20f);

            _mapStatusBadgeBg = LobbyUiBuilder.CreateCyberPanel(mapBox.transform, "MapStatusBadge", LobbyUiBuilder.ElectricCyan);
            LobbyUiBuilder.PlaceTopRight(_mapStatusBadgeBg.rectTransform, -16f, -10f, 136f, 22f);
            _mapStatusBadgeText = LobbyUiBuilder.CreateTmpText(
                _mapStatusBadgeBg.transform, "BadgeTxt", "ĐÃ CHỌN", 10f, LobbyUiBuilder.OnCyan,
                TextAlignmentOptions.Center, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.StretchFill(_mapStatusBadgeText.rectTransform, 4f, 1f);

            // Banner stage with dark scrim for map title/meta
            Image bannerStage = LobbyUiBuilder.CreateCyberPanel(mapBox.transform, "MapBannerStage", LobbyUiBuilder.SurfaceLowest);
            LobbyUiBuilder.PlaceTopLeft(bannerStage.rectTransform, 12f, -38f, 612f, 146f);

            GameObject bannerImgObj = new GameObject("MapBannerImg");
            bannerImgObj.transform.SetParent(bannerStage.transform, false);
            _mapBannerImage = bannerImgObj.AddComponent<Image>();
            _mapBannerImage.preserveAspect = false;
            _mapBannerImage.raycastTarget = false;
            LobbyUiBuilder.StretchFill(_mapBannerImage.rectTransform, 2f, 2f);

            GameObject bannerScrimObj = new GameObject("MapBannerScrim");
            bannerScrimObj.transform.SetParent(bannerStage.transform, false);
            Image bannerScrim = bannerScrimObj.AddComponent<Image>();
            bannerScrim.sprite = LobbyUiBuilder.GetSoftDarkScrimSprite();
            bannerScrim.color = new Color(1f, 1f, 1f, 0.88f);
            bannerScrim.raycastTarget = false;
            LobbyUiBuilder.StretchFill(bannerScrim.rectTransform, 2f, 2f);

            Button prevMapBtn = LobbyUiBuilder.CreateCyberButton(
                bannerStage.transform, "PrevMapBtn", "◀", LobbyUiBuilder.SurfaceLowest, Color.white, 18f, null, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(prevMapBtn.GetComponent<RectTransform>(), 8f, -44f, 38f, 58f);
            prevMapBtn.onClick.AddListener(() => StepMapFromLobby(-1));

            Button nextMapBtn = LobbyUiBuilder.CreateCyberButton(
                bannerStage.transform, "NextMapBtn", "▶", LobbyUiBuilder.SurfaceLowest, Color.white, 18f, null, customTmpFont);
            LobbyUiBuilder.PlaceTopRight(nextMapBtn.GetComponent<RectTransform>(), -8f, -44f, 38f, 58f);
            nextMapBtn.onClick.AddListener(() => StepMapFromLobby(1));

            _mapNameText = LobbyUiBuilder.CreateTmpText(
                bannerStage.transform, "MapNameText", "MAP 01/04: PHỐ SÁNG BAN MAI", 20f, Color.white,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(_mapNameText.rectTransform, 54f, -18f, 504f, 28f);

            _mapMetaText = LobbyUiBuilder.CreateTmpText(
                bannerStage.transform, "MapMetaText", "4 LÀN (16m)  •  ★★☆☆☆  •  x1.0  •  --:--.--", 11f, LobbyUiBuilder.SafetyAmber,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(_mapMetaText.rectTransform, 54f, -48f, 504f, 20f);

            _mapDescText = LobbyUiBuilder.CreateTmpText(
                bannerStage.transform, "MapDescText", "Đường phố 4 làn cổ điển với khúc cua vừa phải.", 12f, LobbyUiBuilder.TextMuted,
                TextAlignmentOptions.TopLeft, FontStyles.Normal, customTmpFont, wrap: true);
            LobbyUiBuilder.PlaceTopLeft(_mapDescText.rectTransform, 54f, -72f, 376f, 60f);

            _mapActionButton = LobbyUiBuilder.CreateCyberButton(
                bannerStage.transform, "MapActionBtn", "✓ ĐANG CHỌN",
                LobbyUiBuilder.ElectricCyan, LobbyUiBuilder.OnCyan, 13f, null, customTmpFont);
            LobbyUiBuilder.PlaceTopRight(_mapActionButton.GetComponent<RectTransform>(), -54f, -90f, 136f, 44f);
            _mapActionBtnImg = _mapActionButton.GetComponent<Image>();
            _mapActionBtnLabel = LobbyUiBuilder.GetPrimaryLabel(_mapActionButton);
            _mapActionButton.onClick.AddListener(OnMapActionButtonClicked);

            // Map quick tabs row
            GameObject tabsObj = new GameObject("MapTabsRow");
            tabsObj.transform.SetParent(mapBox.transform, false);
            _mapTabsContainer = tabsObj.AddComponent<RectTransform>();
            LobbyUiBuilder.PlaceTopLeft(_mapTabsContainer, 12f, -196f, 612f, 30f);
            _mapTabs.Clear();

            TMP_Text tabsHint = LobbyUiBuilder.CreateTmpText(
                mapBox.transform, "TabsHint", "Chọn nhanh 4 cung đường đua Việt Nam • Bấm ◀ ▶ để xem trước nền đường",
                10f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(tabsHint.rectTransform, 14f, -236f, 608f, 22f);
        }

        private void BuildSkinSelectorPanel()
        {
            _skinSelectorPanel = new GameObject("SkinSelectorPanel");
            _skinSelectorPanel.transform.SetParent(_canvas.transform, false);
            RectTransform rootRt = _skinSelectorPanel.AddComponent<RectTransform>();
            LobbyUiBuilder.StretchFill(rootRt);

            Image modalBackdrop = _skinSelectorPanel.AddComponent<Image>();
            modalBackdrop.color = new Color(0.03f, 0.04f, 0.06f, 0.96f);

            // Header bar
            Image headerBar = LobbyUiBuilder.CreateCyberPanel(
                _skinSelectorPanel.transform, "SkinModalHeader", LobbyUiBuilder.SurfaceLowest, LobbyUiBuilder.ElectricCyan);
            LobbyUiBuilder.PlaceTop(headerBar.rectTransform, 0f, 1280f, 60f);
            headerBar.rectTransform.anchorMin = new Vector2(0f, 1f);
            headerBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            headerBar.rectTransform.sizeDelta = new Vector2(0f, 60f);

            TMP_Text headerTitle = LobbyUiBuilder.CreateTmpText(
                headerBar.transform, "SkinModalTitle", "CHỌN CHIẾN MÃ & TRANG PHỤC SHIPPER", 22f, Color.white,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(headerTitle.rectTransform, 32f, -12f, 680f, 36f);

            Image walletPill = LobbyUiBuilder.CreateCyberPanel(headerBar.transform, "SkinWalletPill", LobbyUiBuilder.SurfaceHigh);
            LobbyUiBuilder.PlaceTopRight(walletPill.rectTransform, -196f, -12f, 160f, 36f);
            _skinModalWalletText = LobbyUiBuilder.CreateTmpText(
                walletPill.transform, "WalletTxt", "0 XU", 13f, LobbyUiBuilder.SafetyAmber,
                TextAlignmentOptions.Center, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.StretchFill(_skinModalWalletText.rectTransform, 6f, 2f);

            Button backBtn = LobbyUiBuilder.CreateCyberButton(
                headerBar.transform, "SkinBackBtn", "[ESC] QUAY LẠI",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.TextPrimary, 13f, null, customTmpFont);
            LobbyUiBuilder.PlaceTopRight(backBtn.GetComponent<RectTransform>(), -24f, -12f, 156f, 36f);
            backBtn.onClick.AddListener(CloseSkinSelector);

            // Left Featured Bike Stage (x=32, y=-80, w=500, h=600)
            Image stagePanel = LobbyUiBuilder.CreateCyberPanel(
                _skinSelectorPanel.transform, "SkinFeaturedStage", LobbyUiBuilder.SurfaceLow);
            LobbyUiBuilder.PlaceTopLeft(stagePanel.rectTransform, 32f, -80f, 500f, 600f);

            _skinModalBrandTitle = LobbyUiBuilder.CreateTmpText(
                stagePanel.transform, "StageBrandTitle", "XANH SM", 30f, LobbyUiBuilder.ElectricCyan,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(_skinModalBrandTitle.rectTransform, 24f, -20f, 452f, 38f);

            _skinModalBrandTagline = LobbyUiBuilder.CreateTmpText(
                stagePanel.transform, "StageBrandTagline", "Thuần điện Xanh Cyan", 14f, LobbyUiBuilder.TextMuted,
                TextAlignmentOptions.MidlineLeft, FontStyles.Normal, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(_skinModalBrandTagline.rectTransform, 24f, -60f, 452f, 24f);

            Image previewBox = LobbyUiBuilder.CreateCyberPanel(stagePanel.transform, "StagePreviewBox", LobbyUiBuilder.SurfaceLowest);
            LobbyUiBuilder.PlaceTopLeft(previewBox.rectTransform, 24f, -98f, 452f, 360f);

            GameObject stageImgObj = new GameObject("StagePreviewImg");
            stageImgObj.transform.SetParent(previewBox.transform, false);
            _skinModalPreviewImage = stageImgObj.AddComponent<Image>();
            _skinModalPreviewImage.preserveAspect = true;
            _skinModalPreviewImage.raycastTarget = false;
            LobbyUiBuilder.StretchFill(_skinModalPreviewImage.rectTransform, 4f, 4f);

            GameObject stageAccent = new GameObject("StageAccent");
            stageAccent.transform.SetParent(previewBox.transform, false);
            _skinModalAccentBar = stageAccent.AddComponent<Image>();
            _skinModalAccentBar.raycastTarget = false;
            _skinModalAccentBar.color = LobbyUiBuilder.ElectricCyan;
            LobbyUiBuilder.PlaceBottomStretch(_skinModalAccentBar.rectTransform, 5f);

            TMP_Text stageHint = LobbyUiBuilder.CreateTmpText(
                stagePanel.transform, "StageHint",
                "Đổi màu xe & đồng phục trực tiếp trên đường đua • Tự động lưu lựa chọn",
                12f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.Center, FontStyles.Normal, customTmpFont, wrap: true);
            LobbyUiBuilder.PlaceTopLeft(stageHint.rectTransform, 24f, -474f, 452f, 40f);

            Button confirmBtn = LobbyUiBuilder.CreateCyberButton(
                stagePanel.transform, "ConfirmSkinBtn", "✓ XÁC NHẬN & QUAY LẠI",
                LobbyUiBuilder.ElectricCyan, LobbyUiBuilder.OnCyan, 16f, null, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(confirmBtn.GetComponent<RectTransform>(), 24f, -526f, 452f, 52f);
            confirmBtn.onClick.AddListener(CloseSkinSelector);

            // Right Fleet List (4 Brand Cards: x=556, y=-80, w=692)
            _skinCards.Clear();
            const float cardW = 692f;
            const float cardH = 138f;
            const float cardGap = 16f;

            for (int i = 0; i < AllBrandOrder.Length; i++)
            {
                DriverBrandSkin brand = AllBrandOrder[i];
                float cardY = -80f - i * (cardH + cardGap);
                SkinCardWidgets card = LobbyUiBuilder.CreateSkinCard(
                    _skinSelectorPanel.transform,
                    brand,
                    556f,
                    cardY,
                    cardW,
                    cardH,
                    b => SelectSkinFromLobby(b),
                    customTmpFont);
                _skinCards.Add(card);
            }

            _skinSelectorPanel.SetActive(false);
        }

        private void OnResetSaveClicked()
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
                SetLobbyFeedback("Đã đặt lại toàn bộ dữ liệu lưu!", LobbyUiBuilder.SuccessGreen);
                RefreshMenu();
            }
            else
            {
                _confirmResetPending = true;
                RefreshMenuDynamic();
            }
        }

        private void AddDifficultyButton(Transform parent, float x, float y, float w, float h, DifficultyLevel level, string label)
        {
            Button btn = LobbyUiBuilder.CreateCyberButton(
                parent, $"Diff_{level}", label,
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.TextPrimary, 12f, null, customTmpFont);
            LobbyUiBuilder.PlaceTopLeft(btn.GetComponent<RectTransform>(), x, y, w, h);
            _diffBtnImgs[level] = btn.GetComponent<Image>();
            _diffBtnLabels[level] = LobbyUiBuilder.GetPrimaryLabel(btn);
            btn.onClick.AddListener(() => SelectDifficultyFromLobby(level));
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

            const int bw = 128;
            const int bh = 32;
            Texture2D tex = new Texture2D(bw, bh, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] brandColors =
            {
                new Color(0.00f, 0.72f, 0.71f),
                new Color(0.93f, 0.30f, 0.18f),
                new Color(0.00f, 0.69f, 0.31f),
                new Color(0.96f, 0.50f, 0.13f)
            };

            for (int y = 0; y < bh; y++)
            {
                float v = y / (float)(bh - 1);
                for (int x = 0; x < bw; x++)
                {
                    float u = x / (float)(bw - 1);
                    int stripe = Mathf.Clamp(Mathf.FloorToInt(u * 4f), 0, 3);
                    Color baseDark = Color.Lerp(new Color(0.04f, 0.06f, 0.09f), new Color(0.10f, 0.13f, 0.18f), v);
                    Color accent = brandColors[stripe];
                    bool border = y < 3 || y >= bh - 3;
                    tex.SetPixel(x, y, border ? accent : Color.Lerp(baseDark, accent, 0.28f));
                }
            }

            tex.Apply();
            _generatedBannerSprite = Sprite.Create(tex, new Rect(0, 0, bw, bh), new Vector2(0.5f, 0.5f), 100f);
            return _generatedBannerSprite;
        }

        private void BuildGaragePanel()
        {
            _garagePanel = CreateCenteredPanel(_canvas.transform, "GaragePanel", 720f, 540f);

            Text title = RoadRashUiSprites.CreateStyledText(
                _garagePanel.transform, "GarageTitle", "CỬA HÀNG NÂNG CẤP XE (GARAGE)", 26, RoadRashVisualTheme.HudYellow, TextAnchor.MiddleCenter);
            LobbyUiBuilder.PlaceTop(title.rectTransform, -14f, 680f, 38f);

            _garageMoneyText = RoadRashUiSprites.CreateStyledText(
                _garagePanel.transform, "GarageMoney", "Tiền hiện có: $0", 18, RoadRashVisualTheme.AccentGreen, TextAnchor.MiddleCenter);
            LobbyUiBuilder.PlaceTop(_garageMoneyText.rectTransform, -54f, 680f, 26f);

            _garageStatsText = RoadRashUiSprites.CreateStyledText(
                _garagePanel.transform, "GarageStats", "", 13, new Color(0.88f, 0.91f, 0.96f), TextAnchor.MiddleCenter);
            LobbyUiBuilder.PlaceTop(_garageStatsText.rectTransform, -82f, 680f, 22f);

            _speedBarFill = CreateStatBar("MAX SPD", 40f, -112f, new Color(0.95f, 0.72f, 0.18f));
            _powerBarFill = CreateStatBar("POWER", 205f, -112f, new Color(0.92f, 0.35f, 0.24f));
            _brakeBarFill = CreateStatBar("BRAKE", 370f, -112f, new Color(0.28f, 0.76f, 0.94f));
            _gripBarFill = CreateStatBar("GRIP", 535f, -112f, RoadRashVisualTheme.AccentGreen);

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
                LobbyUiBuilder.PlaceTop(cardBg.rectTransform, cardY, 664f, 66f);

                Text cardTitle = RoadRashUiSprites.CreateStyledText(cardObj.transform, "CardTitle", "", 16, Color.white);
                LobbyUiBuilder.PlaceTopLeft(cardTitle.rectTransform, 16f, -6f, 280f, 24f);

                Text cardEffect = RoadRashUiSprites.CreateStyledText(cardObj.transform, "CardEffect", "", 13, new Color(0.80f, 0.86f, 0.94f));
                LobbyUiBuilder.PlaceTopLeft(cardEffect.rectTransform, 16f, -34f, 430f, 22f);

                UpgradeCardWidgets widgets = new UpgradeCardWidgets
                {
                    Type = type,
                    TitleText = cardTitle,
                    EffectText = cardEffect
                };

                for (int p = 0; p < CarUpgradeSystem.MaxUpgradeLevel; p++)
                {
                    GameObject pip = new GameObject($"Pip_{p}");
                    pip.transform.SetParent(cardObj.transform, false);
                    Image pipImg = pip.AddComponent<Image>();
                    pipImg.raycastTarget = false;
                    LobbyUiBuilder.PlaceTopLeft(pipImg.rectTransform, 310f + p * 26f, -11f, 20f, 14f);
                    widgets.LevelPips.Add(pipImg);
                }

                Button buyBtn = RoadRashUiSprites.CreateBevelButton(cardObj.transform, "BuyBtn", "Nâng cấp", new Color(0.24f, 0.58f, 0.28f), 14);
                LobbyUiBuilder.PlaceTopLeft(buyBtn.GetComponent<RectTransform>(), 488f, -13f, 160f, 40f);
                widgets.ButtonImg = buyBtn.GetComponent<Image>();
                widgets.ButtonLabel = buyBtn.GetComponentInChildren<Text>();
                buyBtn.onClick.AddListener(() => OnBuyUpgradeClicked(type));

                _upgradeCards.Add(widgets);
            }

            _garageFeedbackText = RoadRashUiSprites.CreateStyledText(
                _garagePanel.transform, "Feedback", "", 15, RoadRashVisualTheme.HudYellow, TextAnchor.MiddleCenter);
            LobbyUiBuilder.PlaceTop(_garageFeedbackText.rectTransform, -450f, 660f, 26f);
            _garageFeedbackText.gameObject.SetActive(false);

            Button backBtn = RoadRashUiSprites.CreateBevelButton(_garagePanel.transform, "BackBtn", "QUAY LẠI SẢNH CHỜ", RoadRashVisualTheme.MetalMid, 16);
            LobbyUiBuilder.PlaceTopLeft(backBtn.GetComponent<RectTransform>(), 72f, -482f, 260f, 40f);
            backBtn.onClick.AddListener(() => _gameManager?.OpenMenu());

            Button raceBtn = RoadRashUiSprites.CreateBevelButton(_garagePanel.transform, "RaceNowBtn", "PLAY (ĐUA NGAY)", new Color(0.22f, 0.58f, 0.28f), 16);
            LobbyUiBuilder.PlaceTopLeft(raceBtn.GetComponent<RectTransform>(), 388f, -482f, 260f, 40f);
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
            LobbyUiBuilder.PlaceTopLeft(lbl.rectTransform, x, y, 70f, 18f);

            GameObject track = new GameObject($"Bar_{label}");
            track.transform.SetParent(_garagePanel.transform, false);
            Image trackImg = track.AddComponent<Image>();
            trackImg.color = new Color(0.14f, 0.16f, 0.20f, 1f);
            trackImg.raycastTarget = false;
            LobbyUiBuilder.PlaceTopLeft(trackImg.rectTransform, x + 64f, y - 3f, 82f, 12f);

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

        private static void DestroyUnityObject(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
    }
}
