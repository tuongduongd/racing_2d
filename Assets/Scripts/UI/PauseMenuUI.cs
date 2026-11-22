using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>
    /// Stage 4D uGUI + TextMeshPro Pause Overlay (sortingOrder = 30) and Controls Guide Modal.
    /// Preserves TC-44 required buttons (Resume, Restart, Lobby, Quit), adds CRT Scanline toggle
    /// and a 100%-accurate Controls Guide synced with InputHandler / GameManager / RaceAudioController.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseMenuUI : MonoBehaviour
    {
        private readonly struct ControlBindingEntry
        {
            public readonly string KeyLabel;
            public readonly string ActionTitle;
            public readonly string Description;

            public ControlBindingEntry(string keyLabel, string actionTitle, string description)
            {
                KeyLabel = keyLabel;
                ActionTitle = actionTitle;
                Description = description;
            }
        }

        private static readonly ControlBindingEntry[] RealControlBindings =
        {
            new ControlBindingEntry("W / ↑", "Tăng Ga (Throttle)", "Giữ phím để tăng tốc tối đa trên đường đua"),
            new ControlBindingEntry("S / ↓", "Phanh / Giảm Tốc (Brake)", "Giảm tốc khi vào cua gắt hoặc né chướng ngại vật"),
            new ControlBindingEntry("A / D", "Đánh Lái Trái / Phải (Steer)", "Chuyển làn và nghiêng xe (hỗ trợ cả mũi tên ← / →)"),
            new ControlBindingEntry("SPACE", "Đá Đối Thủ (Road Rash Kick)", "Tung cước đạp lệch hướng đối thủ khi áp sát"),
            new ControlBindingEntry("M", "Tắt / Bật Âm Thanh (Mute)", "Chuyển đổi nhanh âm thanh động cơ & hiệu ứng"),
            new ControlBindingEntry("R", "Đua Lại (Quick Restart)", "Khởi động lại ngay vòng đua hiện tại"),
            new ControlBindingEntry("ESC / P", "Tạm Dừng (Pause / Resume)", "Đóng băng trận đấu hoặc tiếp tục cuộc đua")
        };

        private GameManager _gameManager;
        private CrtScanlineController _crtController;

        private Canvas _canvas;
        private GameObject _pauseRoot;
        private GameObject _pauseCard;
        private GameObject _controlsModal;

        private TMP_Text _statusSubtitleText;
        private TMP_Text _telemetrySpeedText;
        private TMP_Text _telemetryBikeText;
        private TMP_Text _telemetryLapText;

        private Button _resumeButton;
        private Button _restartButton;
        private Button _lobbyButton;
        private Button _quitButton;
        private Button _crtToggleButton;
        private Image _crtToggleBtnImg;
        private TMP_Text _crtToggleLabel;
        private Button _controlsGuideButton;
        private Button _closeControlsModalButton;

        public Canvas PauseCanvas => _canvas;
        public GameObject PauseRoot => _pauseRoot;
        public GameObject ControlsModal => _controlsModal;
        public Button ResumeButton => _resumeButton;
        public Button RestartButton => _restartButton;
        public Button LobbyButton => _lobbyButton;
        public Button QuitButton => _quitButton;
        public Button CrtToggleButton => _crtToggleButton;
        public Button ControlsGuideButton => _controlsGuideButton;
        public Button CloseControlsModalButton => _closeControlsModalButton;
        public bool IsShowing => _pauseRoot != null && _pauseRoot.activeSelf;
        public bool IsControlsModalOpen => _controlsModal != null && _controlsModal.activeSelf;

        public void Bind(GameManager gameManager)
        {
            _gameManager = gameManager;
            EnsureBuilt();
            RefreshState();
        }

        private void Awake()
        {
            EnsureBuilt();
        }

        private void Update()
        {
            RefreshState();
        }

        public void OpenControlsModal()
        {
            EnsureBuilt();
            if (_controlsModal != null)
            {
                _controlsModal.SetActive(true);
            }
        }

        public void CloseControlsModal()
        {
            if (_controlsModal != null)
            {
                _controlsModal.SetActive(false);
            }
        }

        public bool ToggleCrtFromPause()
        {
            EnsureBuilt();
            CrtScanlineController crt = ResolveCrtController();
            bool enabledState;
            if (crt != null)
            {
                enabledState = crt.ToggleCrt();
            }
            else
            {
                enabledState = PlayerPrefs.GetInt(CrtScanlineController.PlayerPrefsKey, 0) == 0;
                PlayerPrefs.SetInt(CrtScanlineController.PlayerPrefsKey, enabledState ? 1 : 0);
                PlayerPrefs.Save();
            }

            RefreshCrtButtonVisual(enabledState);
            return enabledState;
        }

        public void RefreshState()
        {
            EnsureBuilt();
            bool shouldShow = _gameManager != null && _gameManager.IsPaused &&
                              (_gameManager.CurrentFlowState == GameFlowState.Racing ||
                               _gameManager.CurrentFlowState == GameFlowState.Countdown);

            if (_pauseRoot.activeSelf != shouldShow)
            {
                _pauseRoot.SetActive(shouldShow);
                if (!shouldShow)
                {
                    CloseControlsModal();
                }
            }

            if (!shouldShow) return;

            RefreshPauseTelemetry();
            RefreshCrtButtonVisual(ReadCurrentCrtState());
        }

        private void RefreshPauseTelemetry()
        {
            if (_gameManager == null) return;

            string skinName = "Xanh SM";
            if (_gameManager.SkinManager != null && _gameManager.SkinManager.SelectedSkin != null)
            {
                skinName = _gameManager.SkinManager.SelectedSkin.DisplayName;
            }

            int currentLap = 1;
            int totalLaps = Mathf.Max(1, _gameManager.TotalLaps);
            if (_gameManager.LapManager != null && _gameManager.PlayerCar != null)
            {
                RacerProgress progress = _gameManager.LapManager.GetProgress(_gameManager.PlayerCar);
                if (progress != null)
                {
                    currentLap = Mathf.Clamp(progress.CompletedLaps + 1, 1, totalLaps);
                }
            }

            int speedKmh = 0;
            if (_gameManager.PlayerCar != null && _gameManager.PlayerCar.Physics != null)
            {
                speedKmh = Mathf.RoundToInt(Mathf.Abs(_gameManager.PlayerCar.Physics.SpeedKmh));
            }

            if (_statusSubtitleText != null)
            {
                _statusSubtitleText.text =
                    $"XE: {skinName.ToUpperInvariant()}   •   VÒNG ĐUA: {currentLap}/{totalLaps}   •   NHẤN [ESC / P] ĐỂ TIẾP TỤC";
            }

            if (_telemetrySpeedText != null) _telemetrySpeedText.text = $"{speedKmh:000} KM/H";
            if (_telemetryBikeText != null) _telemetryBikeText.text = skinName.ToUpperInvariant();
            if (_telemetryLapText != null) _telemetryLapText.text = $"LAP {currentLap}/{totalLaps}";
        }

        private CrtScanlineController ResolveCrtController()
        {
            if (_crtController == null)
            {
                _crtController = FindAnyObjectByType<CrtScanlineController>();
            }
            return _crtController;
        }

        private bool ReadCurrentCrtState()
        {
            CrtScanlineController crt = ResolveCrtController();
            if (crt != null) return crt.IsEnabled;
            return PlayerPrefs.GetInt(CrtScanlineController.PlayerPrefsKey, 0) != 0;
        }

        private void RefreshCrtButtonVisual(bool crtEnabled)
        {
            if (_crtToggleLabel != null)
            {
                _crtToggleLabel.text = crtEnabled ? "CRT SCANLINE: BẬT" : "CRT SCANLINE: TẮT";
                _crtToggleLabel.color = crtEnabled ? LobbyUiBuilder.ElectricCyan : LobbyUiBuilder.TextMuted;
            }
            if (_crtToggleBtnImg != null)
            {
                _crtToggleBtnImg.color = crtEnabled
                    ? new Color(0.08f, 0.24f, 0.28f, 1f)
                    : LobbyUiBuilder.SurfaceHigh;
            }
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;

            RoadRashUiSprites.EnsureEventSystem();

            GameObject canvasObj = new GameObject("PauseMenuCanvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject scrimObj = new GameObject("PauseRoot");
            scrimObj.transform.SetParent(canvasObj.transform, false);
            RectTransform scrimRt = scrimObj.AddComponent<RectTransform>();
            LobbyUiBuilder.StretchFill(scrimRt);
            Image scrimImg = scrimObj.AddComponent<Image>();
            scrimImg.color = new Color(0.02f, 0.03f, 0.06f, 0.86f);
            _pauseRoot = scrimObj;

            BuildPauseCard();
            BuildControlsModal();

            RefreshCrtButtonVisual(ReadCurrentCrtState());
            _pauseRoot.SetActive(false);
        }

        private void BuildPauseCard()
        {
            Image cardBg = LobbyUiBuilder.CreateCyberPanel(
                _pauseRoot.transform, "PauseCard", LobbyUiBuilder.SurfaceLowest, LobbyUiBuilder.SafetyAmber);
            _pauseCard = cardBg.gameObject;

            RectTransform cardRt = cardBg.rectTransform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(640f, 560f);

            // Header strip & status tags
            TMP_Text badge = LobbyUiBuilder.CreateTmpText(
                _pauseCard.transform, "PauseBadge", "● HỆ THỐNG ĐÓNG BĂNG // DISPATCH HOLD",
                10f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LobbyUiBuilder.PlaceTopLeft(badge.rectTransform, 28f, -12f, 380f, 20f);

            TMP_Text timeTag = LobbyUiBuilder.CreateTmpText(
                _pauseCard.transform, "TimeScaleBadge", "TIMESCALE: 0.00x",
                10f, LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            LobbyUiBuilder.PlaceTopRight(timeTag.rectTransform, -28f, -12f, 180f, 20f);

            TMP_Text title = LobbyUiBuilder.CreateTmpText(
                _pauseCard.transform, "PauseTitle", "TẠM DỪNG TRẬN ĐUA // GAME PAUSED",
                25f, LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.Center, FontStyles.Bold);
            title.enableAutoSizing = true;
            title.fontSizeMin = 18f;
            title.fontSizeMax = 26f;
            LobbyUiBuilder.PlaceTop(title.rectTransform, -34f, 584f, 34f);

            _statusSubtitleText = LobbyUiBuilder.CreateTmpText(
                _pauseCard.transform, "StatusSubtitle",
                "XE: XANH SM   •   VÒNG ĐUA: 1/3   •   NHẤN [ESC / P] ĐỂ TIẾP TỤC",
                11.5f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.Center, FontStyles.Bold);
            _statusSubtitleText.enableAutoSizing = true;
            _statusSubtitleText.fontSizeMin = 9f;
            _statusSubtitleText.fontSizeMax = 12f;
            LobbyUiBuilder.PlaceTop(_statusSubtitleText.rectTransform, -70f, 584f, 20f);

            CreateHorizontalDivider(_pauseCard.transform, "DividerTop", 28f, -96f, 584f);

            // 3-Column Live Telemetry Strip
            GameObject teleRow = new GameObject("TelemetryStrip");
            teleRow.transform.SetParent(_pauseCard.transform, false);
            RectTransform teleRt = teleRow.AddComponent<RectTransform>();
            LobbyUiBuilder.PlaceTopLeft(teleRt, 28f, -104f, 584f, 56f);

            HorizontalLayoutGroup teleHlg = teleRow.AddComponent<HorizontalLayoutGroup>();
            teleHlg.spacing = 10f;
            teleHlg.childControlWidth = true;
            teleHlg.childControlHeight = true;
            teleHlg.childForceExpandWidth = true;
            teleHlg.childForceExpandHeight = true;

            _telemetrySpeedText = CreateTelemetryCell(teleRow.transform, "TelemetrySpeed", "VẬN TỐC", "000 KM/H", LobbyUiBuilder.ElectricCyan);
            _telemetryBikeText = CreateTelemetryCell(teleRow.transform, "TelemetryBike", "CHIẾN MÃ", "XANH SM", LobbyUiBuilder.SafetyAmber);
            _telemetryLapText = CreateTelemetryCell(teleRow.transform, "TelemetryLap", "TIẾN ĐỘ", "LAP 1/3", LobbyUiBuilder.SuccessGreen);

            // Utility Row (CRT Toggle + Controls Guide Modal Button)
            GameObject utilRow = new GameObject("UtilityButtonsRow");
            utilRow.transform.SetParent(_pauseCard.transform, false);
            RectTransform utilRt = utilRow.AddComponent<RectTransform>();
            LobbyUiBuilder.PlaceTopLeft(utilRt, 28f, -170f, 584f, 40f);

            HorizontalLayoutGroup utilHlg = utilRow.AddComponent<HorizontalLayoutGroup>();
            utilHlg.spacing = 12f;
            utilHlg.childControlWidth = true;
            utilHlg.childControlHeight = true;
            utilHlg.childForceExpandWidth = true;
            utilHlg.childForceExpandHeight = true;

            _crtToggleButton = LobbyUiBuilder.CreateCyberButton(
                utilRow.transform, "CrtToggleBtn", "CRT SCANLINE: TẮT",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.TextMuted, 12f);
            EnableButtonLabelAutoSize(_crtToggleButton, 9.5f, 12.5f);
            _crtToggleBtnImg = _crtToggleButton.GetComponent<Image>();
            _crtToggleLabel = LobbyUiBuilder.GetPrimaryLabel(_crtToggleButton);
            _crtToggleButton.onClick.AddListener(() => ToggleCrtFromPause());

            _controlsGuideButton = LobbyUiBuilder.CreateCyberButton(
                utilRow.transform, "ControlsGuideBtn", "⌨   HƯỚNG DẪN PHÍM (CONTROLS)",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.ElectricCyan, 12f);
            EnableButtonLabelAutoSize(_controlsGuideButton, 9.5f, 12.5f);
            _controlsGuideButton.onClick.AddListener(OpenControlsModal);

            // Core 4 Action Buttons Stack (TC-44 required)
            GameObject btnStack = new GameObject("ActionButtonsStack");
            btnStack.transform.SetParent(_pauseCard.transform, false);
            RectTransform stackRt = btnStack.AddComponent<RectTransform>();
            LobbyUiBuilder.PlaceTopLeft(stackRt, 28f, -222f, 584f, 268f);

            VerticalLayoutGroup stackVlg = btnStack.AddComponent<VerticalLayoutGroup>();
            stackVlg.spacing = 12f;
            stackVlg.childControlWidth = true;
            stackVlg.childControlHeight = true;
            stackVlg.childForceExpandWidth = true;
            stackVlg.childForceExpandHeight = true;

            _resumeButton = LobbyUiBuilder.CreateCyberButton(
                btnStack.transform, "ResumeBtn", "▶   TIẾP TỤC ĐUA (RESUME) [ESC / P]",
                LobbyUiBuilder.ElectricCyan, LobbyUiBuilder.OnCyan, 15f);
            EnableButtonLabelAutoSize(_resumeButton, 12f, 16f);
            _resumeButton.onClick.AddListener(() =>
            {
                CloseControlsModal();
                if (_gameManager != null) _gameManager.ResumeGame();
            });

            _restartButton = LobbyUiBuilder.CreateCyberButton(
                btnStack.transform, "RestartBtn", "↻   CHƠI LẠI VÒNG NÀY (RESTART) [R]",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.SafetyAmber, 14f);
            EnableButtonLabelAutoSize(_restartButton, 11.5f, 15f);
            _restartButton.onClick.AddListener(() =>
            {
                CloseControlsModal();
                if (_gameManager != null)
                {
                    _gameManager.ResumeGame();
                    _gameManager.StartRace();
                }
            });

            _lobbyButton = LobbyUiBuilder.CreateCyberButton(
                btnStack.transform, "LobbyBtn", "⌂   VỀ SẢNH CHỜ ĐỔI SKIN (LOBBY)",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.TextPrimary, 14f);
            EnableButtonLabelAutoSize(_lobbyButton, 11.5f, 15f);
            _lobbyButton.onClick.AddListener(() =>
            {
                CloseControlsModal();
                if (_gameManager != null)
                {
                    _gameManager.ResumeGame();
                    _gameManager.OpenMenu();
                }
            });

            _quitButton = LobbyUiBuilder.CreateCyberButton(
                btnStack.transform, "QuitGameBtn", "✕   THOÁT GAME (QUIT GAME)",
                new Color(0.54f, 0.14f, 0.16f, 1f), Color.white, 14f);
            EnableButtonLabelAutoSize(_quitButton, 11.5f, 15f);
            _quitButton.onClick.AddListener(() =>
            {
                if (_gameManager != null)
                {
                    _gameManager.QuitGame();
                }
                else
                {
                    Application.Quit();
                }
            });

            // Bottom quick cheat-sheet reminder
            Image footerBar = LobbyUiBuilder.CreateCyberPanel(
                _pauseCard.transform, "CheatSheetFooter", LobbyUiBuilder.SurfaceContainer);
            footerBar.raycastTarget = false;
            LobbyUiBuilder.PlaceTopLeft(footerBar.rectTransform, 28f, -504f, 584f, 40f);

            TMP_Text cheatTxt = LobbyUiBuilder.CreateTmpText(
                footerBar.transform, "CheatSheetText",
                "PHÍM TẮT:  [W / ↑] GA   •   [S / ↓] PHANH   •   [A / D] LÁI   •   [SPACE] ĐÁ   •   [M] ÂM   •   [R] ĐUA LẠI   •   [ESC / P] PAUSE",
                10f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.Center, FontStyles.Bold);
            cheatTxt.enableAutoSizing = true;
            cheatTxt.fontSizeMin = 8f;
            cheatTxt.fontSizeMax = 10.5f;
            LobbyUiBuilder.StretchFill(cheatTxt.rectTransform, 10f, 4f);
        }

        private void BuildControlsModal()
        {
            Image modalBg = LobbyUiBuilder.CreateCyberPanel(
                _pauseRoot.transform, "ControlsModal", LobbyUiBuilder.SurfaceLowest, LobbyUiBuilder.ElectricCyan);
            _controlsModal = modalBg.gameObject;

            RectTransform rt = modalBg.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(640f, 540f);

            TMP_Text badge = LobbyUiBuilder.CreateTmpText(
                _controlsModal.transform, "ControlsBadge", "● MODAL #01 // THIẾT LẬP PHÍM TÁC CHIẾN",
                10f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LobbyUiBuilder.PlaceTopLeft(badge.rectTransform, 24f, -14f, 420f, 18f);

            Button closeXBtn = LobbyUiBuilder.CreateCyberButton(
                _controlsModal.transform, "CloseControlsXBtn", "✕",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.TextPrimary, 14f);
            LobbyUiBuilder.PlaceTopRight(closeXBtn.GetComponent<RectTransform>(), -20f, -14f, 38f, 34f);
            closeXBtn.onClick.AddListener(CloseControlsModal);

            TMP_Text title = LobbyUiBuilder.CreateTmpText(
                _controlsModal.transform, "ControlsTitle", "BẢNG HƯỚNG DẪN ĐIỀU KHIỂN",
                24f, LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.enableAutoSizing = true;
            title.fontSizeMin = 17f;
            title.fontSizeMax = 24f;
            LobbyUiBuilder.PlaceTopLeft(title.rectTransform, 24f, -36f, 540f, 32f);

            CreateHorizontalDivider(_controlsModal.transform, "ControlsDivider", 24f, -74f, 592f);

            GameObject listObj = new GameObject("KeybindingsList");
            listObj.transform.SetParent(_controlsModal.transform, false);
            RectTransform listRt = listObj.AddComponent<RectTransform>();
            LobbyUiBuilder.PlaceTopLeft(listRt, 24f, -86f, 592f, 378f);

            VerticalLayoutGroup vlg = listObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(0, 0, 2, 2);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;

            for (int i = 0; i < RealControlBindings.Length; i++)
            {
                BuildControlBindingRow(listObj.transform, i, RealControlBindings[i]);
            }

            _closeControlsModalButton = LobbyUiBuilder.CreateCyberButton(
                _controlsModal.transform, "ConfirmControlsBtn", "✓   ĐÃ HIỂU — QUAY LẠI TẠM DỪNG",
                LobbyUiBuilder.ElectricCyan, LobbyUiBuilder.OnCyan, 14f);
            EnableButtonLabelAutoSize(_closeControlsModalButton, 11f, 15f);
            RectTransform closeBtnRt = _closeControlsModalButton.GetComponent<RectTransform>();
            closeBtnRt.anchorMin = new Vector2(0.5f, 0f);
            closeBtnRt.anchorMax = new Vector2(0.5f, 0f);
            closeBtnRt.pivot = new Vector2(0.5f, 0f);
            closeBtnRt.anchoredPosition = new Vector2(0f, 18f);
            closeBtnRt.sizeDelta = new Vector2(592f, 44f);
            _closeControlsModalButton.onClick.AddListener(CloseControlsModal);

            _controlsModal.SetActive(false);
        }

        private static void BuildControlBindingRow(Transform parent, int index, ControlBindingEntry entry)
        {
            Image rowBg = LobbyUiBuilder.CreateCyberPanel(
                parent, $"BindingRow_{index}", LobbyUiBuilder.SurfaceContainer);
            rowBg.raycastTarget = false;

            // Left Keycap Pill
            Image keyBg = LobbyUiBuilder.CreateCyberPanel(
                rowBg.transform, "Keycap", LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.ElectricCyan);
            keyBg.raycastTarget = false;
            RectTransform keyRt = keyBg.rectTransform;
            keyRt.anchorMin = new Vector2(0.015f, 0.14f);
            keyRt.anchorMax = new Vector2(0.24f, 0.86f);
            keyRt.offsetMin = Vector2.zero;
            keyRt.offsetMax = Vector2.zero;

            TMP_Text keyTxt = LobbyUiBuilder.CreateTmpText(
                keyBg.transform, "KeyLabel", entry.KeyLabel,
                13f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.Center, FontStyles.Bold);
            keyTxt.enableAutoSizing = true;
            keyTxt.fontSizeMin = 9.5f;
            keyTxt.fontSizeMax = 13.5f;
            LobbyUiBuilder.StretchFill(keyTxt.rectTransform, 6f, 2f);

            // Right Action Title + Description
            TMP_Text actionTxt = LobbyUiBuilder.CreateTmpText(
                rowBg.transform, "ActionTitle", entry.ActionTitle,
                13f, LobbyUiBuilder.TextPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            actionTxt.enableAutoSizing = true;
            actionTxt.fontSizeMin = 10f;
            actionTxt.fontSizeMax = 13.5f;
            RectTransform actRt = actionTxt.rectTransform;
            actRt.anchorMin = new Vector2(0.265f, 0.50f);
            actRt.anchorMax = new Vector2(0.985f, 0.94f);
            actRt.offsetMin = Vector2.zero;
            actRt.offsetMax = Vector2.zero;

            TMP_Text descTxt = LobbyUiBuilder.CreateTmpText(
                rowBg.transform, "ActionDesc", entry.Description,
                10.5f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            descTxt.enableAutoSizing = true;
            descTxt.fontSizeMin = 8.5f;
            descTxt.fontSizeMax = 11f;
            RectTransform descRt = descTxt.rectTransform;
            descRt.anchorMin = new Vector2(0.265f, 0.06f);
            descRt.anchorMax = new Vector2(0.985f, 0.50f);
            descRt.offsetMin = Vector2.zero;
            descRt.offsetMax = Vector2.zero;
        }

        private static TMP_Text CreateTelemetryCell(
            Transform parent, string name, string caption, string initialValue, Color valueColor)
        {
            Image box = LobbyUiBuilder.CreateCyberPanel(parent, name, LobbyUiBuilder.SurfaceContainer);
            box.raycastTarget = false;

            TMP_Text capTxt = LobbyUiBuilder.CreateTmpText(
                box.transform, "Caption", caption,
                9.5f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.Center, FontStyles.Bold);
            RectTransform crt = capTxt.rectTransform;
            crt.anchorMin = new Vector2(0f, 0.58f);
            crt.anchorMax = new Vector2(1f, 0.92f);
            crt.offsetMin = new Vector2(8f, 0f);
            crt.offsetMax = new Vector2(-8f, 0f);

            TMP_Text valTxt = LobbyUiBuilder.CreateTmpText(
                box.transform, "Value", initialValue,
                15f, valueColor, TextAlignmentOptions.Center, FontStyles.Bold);
            valTxt.enableAutoSizing = true;
            valTxt.fontSizeMin = 11f;
            valTxt.fontSizeMax = 16f;
            RectTransform vrt = valTxt.rectTransform;
            vrt.anchorMin = new Vector2(0f, 0.08f);
            vrt.anchorMax = new Vector2(1f, 0.56f);
            vrt.offsetMin = new Vector2(8f, 0f);
            vrt.offsetMax = new Vector2(-8f, 0f);
            return valTxt;
        }

        private static void CreateHorizontalDivider(Transform parent, string name, float x, float yOffset, float width)
        {
            GameObject div = new GameObject(name);
            div.transform.SetParent(parent, false);
            Image img = div.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = LobbyUiBuilder.OutlineVariant;
            LobbyUiBuilder.PlaceTopLeft(img.rectTransform, x, yOffset, width, 2f);
        }

        private static void EnableButtonLabelAutoSize(Button btn, float minSize, float maxSize)
        {
            TMP_Text lbl = LobbyUiBuilder.GetPrimaryLabel(btn);
            if (lbl != null)
            {
                lbl.enableAutoSizing = true;
                lbl.fontSizeMin = minSize;
                lbl.fontSizeMax = maxSize;
            }
        }
    }
}
