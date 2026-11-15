using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RacingGame
{
    [DisallowMultipleComponent]
    public class PauseMenuUI : MonoBehaviour
    {
        private GameManager _gameManager;
        private Canvas _canvas;
        private GameObject _pauseRoot;
        private Text _statusSubtitleText;
        private Button _resumeButton;
        private Button _restartButton;
        private Button _lobbyButton;
        private Button _quitButton;
        private Font _font;

        public GameObject PauseRoot => _pauseRoot;
        public Button ResumeButton => _resumeButton;
        public Button RestartButton => _restartButton;
        public Button LobbyButton => _lobbyButton;
        public Button QuitButton => _quitButton;
        public bool IsShowing => _pauseRoot != null && _pauseRoot.activeSelf;

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

        public void RefreshState()
        {
            EnsureBuilt();
            bool shouldShow = _gameManager != null && _gameManager.IsPaused &&
                              (_gameManager.CurrentFlowState == GameFlowState.Racing ||
                               _gameManager.CurrentFlowState == GameFlowState.Countdown);

            if (_pauseRoot.activeSelf != shouldShow)
            {
                _pauseRoot.SetActive(shouldShow);
            }

            if (shouldShow && _statusSubtitleText != null && _gameManager != null)
            {
                string skinName = "Xanh SM";
                if (_gameManager.SkinManager != null && _gameManager.SkinManager.SelectedSkin != null)
                {
                    skinName = _gameManager.SkinManager.SelectedSkin.DisplayName;
                }

                int currentLap = 1;
                int totalLaps = _gameManager.TotalLaps;
                if (_gameManager.LapManager != null && _gameManager.PlayerCar != null)
                {
                    RacerProgress progress = _gameManager.LapManager.GetProgress(_gameManager.PlayerCar);
                    if (progress != null)
                    {
                        currentLap = Mathf.Clamp(progress.CompletedLaps + 1, 1, totalLaps);
                    }
                }

                _statusSubtitleText.text = $"XE: {skinName.ToUpperInvariant()}   •   VÒNG ĐUA: {currentLap}/{totalLaps}   •   NHẤN [ESC / P] ĐỂ TIẾP TỤC";
            }
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            EnsureEventSystem();

            GameObject canvasObj = new GameObject("PauseMenuCanvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            _pauseRoot = CreatePanel("PauseRoot", canvasObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.03f, 0.06f, 0.82f));

            // Center Pause Card
            GameObject card = CreateStyledCard("PauseCard", _pauseRoot.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(680f, 600f),
                new Color(0.07f, 0.10f, 0.16f, 0.98f), new Color(1f, 0.72f, 0.16f, 0.90f));

            // Top Hazard Accent Stripe
            CreatePanel("TopStripe", card.transform,
                new Vector2(0.04f, 0.96f), new Vector2(0.96f, 0.975f), Vector2.zero, Vector2.zero,
                new Color(1f, 0.65f, 0.12f, 0.95f));

            CreateText("PauseBadge", card.transform, "⏸ GAME PAUSED", 18, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(1f, 0.78f, 0.25f), new Vector2(0.5f, 0.90f), new Vector2(560f, 30f));

            Text title = CreateText("PauseTitle", card.transform, "TẠM DỪNG TRẬN ĐUA", 40, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.98f, 0.99f, 1f), new Vector2(0.5f, 0.81f), new Vector2(600f, 54f));
            Shadow titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            titleShadow.effectDistance = new Vector2(2f, -2f);

            _statusSubtitleText = CreateText("StatusSubtitle", card.transform,
                "XE: XANH SM   •   VÒNG ĐUA: 1/3   •   NHẤN [ESC / P] ĐỂ TIẾP TỤC", 15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.68f, 0.82f, 0.96f), new Vector2(0.5f, 0.72f), new Vector2(620f, 30f));

            CreatePanel("Divider", card.transform,
                new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.663f), Vector2.zero, Vector2.zero,
                new Color(1f, 1f, 1f, 0.14f));

            _resumeButton = CreateButton("ResumeBtn", card.transform, "▶   TIẾP TỤC ĐUA (RESUME)",
                new Vector2(0.5f, 0.54f), new Vector2(500f, 68f),
                new Color(0.14f, 0.68f, 0.34f, 1f), () =>
                {
                    if (_gameManager != null) _gameManager.ResumeGame();
                });

            _restartButton = CreateButton("RestartBtn", card.transform, "↻   CHƠI LẠI VÒNG NÀY (RESTART)",
                new Vector2(0.5f, 0.40f), new Vector2(500f, 64f),
                new Color(0.18f, 0.46f, 0.84f, 1f), () =>
                {
                    if (_gameManager != null)
                    {
                        _gameManager.ResumeGame();
                        _gameManager.StartRace();
                    }
                });

            _lobbyButton = CreateButton("LobbyBtn", card.transform, "⌂   VỀ SẢNH CHỜ ĐỔI SKIN (LOBBY)",
                new Vector2(0.5f, 0.26f), new Vector2(500f, 64f),
                new Color(0.32f, 0.36f, 0.46f, 1f), () =>
                {
                    if (_gameManager != null)
                    {
                        _gameManager.ResumeGame();
                        _gameManager.OpenMenu();
                    }
                });

            _quitButton = CreateButton("QuitGameBtn", card.transform, "✕   THOÁT GAME (QUIT GAME)",
                new Vector2(0.5f, 0.12f), new Vector2(500f, 64f),
                new Color(0.82f, 0.18f, 0.22f, 1f), () =>
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

            _pauseRoot.SetActive(false);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            Image img = go.AddComponent<Image>();
            img.sprite = RoadRashUiSprites.GetPanelSprite();
            img.type = Image.Type.Simple;
            img.color = color;
            return go;
        }

        private static GameObject CreateStyledCard(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 sizeDelta, Color bgColor, Color borderColor)
        {
            GameObject go = CreatePanel(name, parent, anchorMin, anchorMax, anchoredPos, sizeDelta, bgColor);
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(6f, -8f);
            return go;
        }

        private Text CreateText(string name, Transform parent, string content, int fontSize, FontStyle style,
            TextAnchor align, Color color, Vector2 anchorPos, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorPos;
            rt.anchorMax = anchorPos;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;

            Text txt = go.AddComponent<Text>();
            txt.font = _font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = align;
            txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private Button CreateButton(string name, Transform parent, string label, Vector2 anchorPos, Vector2 size,
            Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = CreatePanel(name, parent, anchorPos, anchorPos, Vector2.zero, size, bgColor);
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.25f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(3f, -4f);

            Button btn = go.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            Text labelTxt = CreateText("Label", go.transform, label, 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                Color.white, new Vector2(0.5f, 0.5f), size - new Vector2(16f, 8f));
            Shadow txtShadow = labelTxt.gameObject.AddComponent<Shadow>();
            txtShadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            txtShadow.effectDistance = new Vector2(1.5f, -1.5f);

            return btn;
        }
    }
}
