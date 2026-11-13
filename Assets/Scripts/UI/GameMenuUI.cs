using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>
    /// Modernized uGUI Main Menu and Upgrade Shop (Garage) matching the retro Road Rash palette.
    /// Reads state and invokes controlled commands on GameManager (Single Responsibility).
    /// </summary>
    public class GameMenuUI : MonoBehaviour
    {
        private GameManager _gameManager;

        private Canvas _canvas;
        private GameObject _menuPanel;
        private GameObject _garagePanel;

        // Main Menu elements
        private Text _menuStatsText;
        private readonly Dictionary<DifficultyLevel, Image> _diffBtnImgs = new Dictionary<DifficultyLevel, Image>();
        private readonly Dictionary<int, Image> _lapBtnImgs = new Dictionary<int, Image>();
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

        public void Bind(GameManager gameManager)
        {
            _gameManager = gameManager;
            _confirmResetPending = false;
            _shopFeedbackUntil = 0f;
            EnsureBuilt();
            RefreshMenu();
            RefreshGarage(true);
        }

        private void Update()
        {
            if (_gameManager == null || _canvas == null) return;

            GameFlowState state = _gameManager.CurrentFlowState;
            bool showMenu = state == GameFlowState.MainMenu;
            bool showGarage = state == GameFlowState.UpgradeShop;

            if (_menuPanel.activeSelf != showMenu)
            {
                _menuPanel.SetActive(showMenu);
                if (showMenu)
                {
                    _confirmResetPending = false;
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
            if (_gameManager == null) return;

            CarUpgradeSystem upg = _gameManager.UpgradeSystem;
            int money = upg != null ? upg.Money : 0;
            int won = upg != null ? upg.RacesWon : 0;
            int played = upg != null ? upg.RacesPlayed : 0;
            string best = (upg != null && upg.BestRaceTime > 0f) ? RaceHUD.FormatTime(upg.BestRaceTime) : "--:--.--";
            _menuStatsText.text = $"Tiền: ${money}   |   Thắng: {won}/{played}   |   Kỷ lục: {best}";

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

            RefreshMenuDynamic();
        }

        private void RefreshMenuDynamic()
        {
            if (_menuMuteLabel != null && _gameManager.AudioController != null)
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
            BuildMainMenuPanel();
            BuildGaragePanel();
        }

        private void BuildMainMenuPanel()
        {
            _menuPanel = CreateCenteredPanel(_canvas.transform, "MainMenuPanel", 560f, 470f);

            Text title = RoadRashUiSprites.CreateStyledText(
                _menuPanel.transform, "Title", "ROAD RASH 2D", 38, RoadRashVisualTheme.HudYellow, TextAnchor.MiddleCenter);
            PlaceTop(title.rectTransform, -18f, 500f, 46f);

            Text sub = RoadRashUiSprites.CreateStyledText(
                _menuPanel.transform, "Subtitle", "Retro Highway Edition  |  Phím SPACE: Đá đối thủ", 14, new Color(0.86f, 0.90f, 0.96f), TextAnchor.MiddleCenter);
            PlaceTop(sub.rectTransform, -64f, 520f, 24f);

            _menuStatsText = RoadRashUiSprites.CreateStyledText(
                _menuPanel.transform, "Stats", "", 16, RoadRashVisualTheme.AccentGreen, TextAnchor.MiddleCenter);
            PlaceTop(_menuStatsText.rectTransform, -96f, 520f, 28f);

            // Difficulty row
            Text diffLabel = RoadRashUiSprites.CreateStyledText(_menuPanel.transform, "DiffLabel", "Độ khó AI:", 15, Color.white);
            PlaceTopLeft(diffLabel.rectTransform, 32f, -142f, 130f, 32f);

            AddDifficultyButton(170f, -142f, DifficultyLevel.Easy, "Dễ");
            AddDifficultyButton(284f, -142f, DifficultyLevel.Normal, "Vừa");
            AddDifficultyButton(398f, -142f, DifficultyLevel.Hard, "Khó");

            // Lap count row
            Text lapLabel = RoadRashUiSprites.CreateStyledText(_menuPanel.transform, "LapLabel", "Số vòng đua:", 15, Color.white);
            PlaceTopLeft(lapLabel.rectTransform, 32f, -188f, 130f, 32f);

            for (int i = 0; i < LapChoices.Length; i++)
            {
                int laps = LapChoices[i];
                Button btn = RoadRashUiSprites.CreateBevelButton(_menuPanel.transform, $"LapBtn_{laps}", $"{laps} vòng", RoadRashVisualTheme.MetalMid, 14);
                PlaceTopLeft(btn.GetComponent<RectTransform>(), 170f + i * 86f, -188f, 80f, 32f);
                _lapBtnImgs[laps] = btn.GetComponent<Image>();
                btn.onClick.AddListener(() =>
                {
                    _gameManager?.SetTotalLaps(laps);
                    RefreshMenu();
                });
            }

            // Primary buttons
            Button raceBtn = RoadRashUiSprites.CreateBevelButton(_menuPanel.transform, "RaceBtn", "BẮT ĐẦU ĐUA (RACE)", new Color(0.22f, 0.58f, 0.28f), 19);
            PlaceTop(raceBtn.GetComponent<RectTransform>(), -246f, 430f, 48f);
            raceBtn.onClick.AddListener(() =>
            {
                _confirmResetPending = false;
                _gameManager?.StartRace();
            });

            Button garageBtn = RoadRashUiSprites.CreateBevelButton(_menuPanel.transform, "GarageBtn", "CỬA HÀNG NÂNG CẤP (GARAGE)", new Color(0.22f, 0.42f, 0.72f), 17);
            PlaceTop(garageBtn.GetComponent<RectTransform>(), -308f, 430f, 42f);
            garageBtn.onClick.AddListener(() =>
            {
                _confirmResetPending = false;
                _gameManager?.OpenGarage();
            });

            // Audio + Reset row
            Button muteBtn = RoadRashUiSprites.CreateBevelButton(_menuPanel.transform, "MuteBtn", "Âm thanh: BẬT", RoadRashVisualTheme.MetalMid, 14);
            PlaceTopLeft(muteBtn.GetComponent<RectTransform>(), 65f, -366f, 200f, 34f);
            _menuMuteLabel = muteBtn.GetComponentInChildren<Text>();
            muteBtn.onClick.AddListener(() =>
            {
                _gameManager?.AudioController?.ToggleMute();
                RefreshMenuDynamic();
            });

            Button resetBtn = RoadRashUiSprites.CreateBevelButton(_menuPanel.transform, "ResetBtn", "Xóa dữ liệu lưu", new Color(0.56f, 0.22f, 0.22f), 14);
            PlaceTopLeft(resetBtn.GetComponent<RectTransform>(), 280f, -366f, 215f, 34f);
            _resetBtnLabel = resetBtn.GetComponentInChildren<Text>();
            resetBtn.onClick.AddListener(() =>
            {
                if (_confirmResetPending)
                {
                    _gameManager?.ResetSaveData();
                    _confirmResetPending = false;
                    RefreshMenu();
                }
                else
                {
                    _confirmResetPending = true;
                    RefreshMenuDynamic();
                }
            });

            Text controls = RoadRashUiSprites.CreateStyledText(
                _menuPanel.transform, "ControlsHint",
                "W/↑ Ga  |  S/↓ Phanh  |  A/D Lái  |  SPACE Đá đối thủ  |  M Tắt/Bật tiếng",
                12, new Color(0.80f, 0.84f, 0.90f), TextAnchor.MiddleCenter);
            PlaceTop(controls.rectTransform, -424f, 520f, 24f);
        }

        private void AddDifficultyButton(float x, float y, DifficultyLevel level, string label)
        {
            Button btn = RoadRashUiSprites.CreateBevelButton(_menuPanel.transform, $"Diff_{level}", label, RoadRashVisualTheme.MetalMid, 14);
            PlaceTopLeft(btn.GetComponent<RectTransform>(), x, y, 106f, 32f);
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

            Button backBtn = RoadRashUiSprites.CreateBevelButton(_garagePanel.transform, "BackBtn", "QUAY LẠI MENU", RoadRashVisualTheme.MetalMid, 16);
            PlaceTopLeft(backBtn.GetComponent<RectTransform>(), 72f, -482f, 260f, 40f);
            backBtn.onClick.AddListener(() => _gameManager?.OpenMenu());

            Button raceBtn = RoadRashUiSprites.CreateBevelButton(_garagePanel.transform, "RaceNowBtn", "ĐUA NGAY (RACE)", new Color(0.22f, 0.58f, 0.28f), 16);
            PlaceTopLeft(raceBtn.GetComponent<RectTransform>(), 388f, -482f, 260f, 40f);
            raceBtn.onClick.AddListener(() => _gameManager?.StartRace());

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
