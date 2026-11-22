using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>
    /// 4-corner Cyber Arcade in-game HUD (uGUI + TextMeshPro):
    /// - Top-left: live standings & quick-action bar (Pause [Esc], Retry [R], Audio [M])
    /// - Top-center: rank shield & tactical flash banner (GO!, lap split, POW! kick, obstacle hit)
    /// - Top-right: radar track minimap & live racer dots
    /// - Bottom-left: tachometer dial + red needle, LCD km/h, 12-LED RPM strip, virtual gear (G1-G6), road/police status
    /// - Bottom-right: lap counter, race timer, and best lap
    /// - Center (hidden unless triggered): 3..2..1 Countdown, Wrong-way alert, and Post-Race Results panel.
    /// Strict Observer unsubscribe in OnDisable/OnDestroy and strictly read-only View semantics.
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        private const int RpmSegmentCount = 12;
        private const float MinimapSizePx = 152f;

        [SerializeField] private float lapMessageDuration = 2.4f;

        private GameManager _gameManager;
        private Car _player;
        private LapCheckpointManager _lapManager;
        private WaypointTrack _track;
        private TrackObstacleSystem _boundObstacleSystem;
        private bool _isBound;
        private bool _isSubscribed;

        private Canvas _canvas;
        private GameObject _hudRoot;

        // Top-Center Rank & Flash Banner
        private TMP_Text _rankText;
        private Image _rankBadgeBg;
        private TMP_Text _rankStatusText;
        private GameObject _flashBannerPanel;
        private Image _flashBannerAccentStrip;
        private TMP_Text _flashBannerText;
        private float _flashUntil;
        private float _flashStartTime;

        // Bottom-Left Cockpit Cluster
        private RectTransform _needleRect;
        private TMP_Text _lcdSpeedText;
        private TMP_Text _lcdGearText;
        private Image _trackStateBadgeBg;
        private TMP_Text _trackStateText;
        private readonly List<Image> _rpmSegments = new List<Image>(RpmSegmentCount);
        private float _needleAngleSmoothed;

        // Bottom-Right Timer & Lap
        private TMP_Text _lapBottomText;
        private TMP_Text _timerBottomText;
        private TMP_Text _bestLapBottomText;

        // Top-Left Leaderboard & Buttons
        private readonly List<TMP_Text> _rankingRows = new List<TMP_Text>();
        private RectTransform _rankingBoxRect;
        private TMP_Text _racerCountText;
        private TMP_Text _muteBtnLabel;

        // Top-Right Minimap
        private RawImage _minimapImg;
        private RectTransform _minimapRect;
        private TMP_Text _minimapTrackLenText;
        private Vector2 _mapMin;
        private float _mapMaxDim = 1f;
        private readonly List<Image> _mapDots = new List<Image>();
        private readonly List<Car> _fallbackCars = new List<Car>();

        // Center Overlays
        private GameObject _countdownPanel;
        private TMP_Text _countdownText;
        private TMP_Text _countdownSubText;
        private GameObject _wrongWayPanel;
        private TMP_Text _wrongWayText;

        // Post-Race Results Panel
        private GameObject _resultsPanel;
        private readonly List<TMP_Text> _resultRows = new List<TMP_Text>();
        private TMP_Text _prizeText;

        private int _lastCountdownWhole = -1;
        private float _countdownPopTimer;

        public Canvas HudCanvas => _canvas;
        public GameObject HudRoot => _hudRoot;
        public bool IsSubscribed => _isSubscribed;
        public string CurrentRankText => _rankText != null ? _rankText.text : string.Empty;
        public string CurrentSpeedText => _lcdSpeedText != null ? _lcdSpeedText.text : string.Empty;
        public string CurrentGearText => _lcdGearText != null ? _lcdGearText.text : string.Empty;
        public string CurrentTrackStateText => _trackStateText != null ? _trackStateText.text : string.Empty;
        public string CurrentLapText => _lapBottomText != null ? _lapBottomText.text : string.Empty;
        public string CurrentTimerText => _timerBottomText != null ? _timerBottomText.text : string.Empty;
        public string CurrentFlashText => _flashBannerText != null ? _flashBannerText.text : string.Empty;
        public bool IsFlashVisible => _flashBannerPanel != null && _flashBannerPanel.activeSelf;
        public bool IsCountdownVisible => _countdownPanel != null && _countdownPanel.activeSelf;
        public bool IsWrongWayVisible => _wrongWayPanel != null && _wrongWayPanel.activeSelf;
        public int LeaderboardRowCount => _rankingRows.Count;
        public int MinimapDotCount => _mapDots.Count;

        public void Bind(
            GameManager gameManager,
            Car player,
            LapCheckpointManager lapManager,
            WaypointTrack track,
            TrackObstacleSystem obstacleSystemOverride = null)
        {
            Unsubscribe();
            _gameManager = gameManager;
            _player = player;
            _lapManager = lapManager;
            _track = track;
            _boundObstacleSystem = obstacleSystemOverride != null
                ? obstacleSystemOverride
                : (_gameManager != null
                    ? (_gameManager.ObstacleSystem != null
                        ? _gameManager.ObstacleSystem
                        : _gameManager.GetComponent<TrackObstacleSystem>())
                    : null);
            _isBound = true;

            EnsureBuilt();
            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            RebuildMinimapTexture();
            ClearMessage();
        }

        public void ClearMessage()
        {
            _flashUntil = 0f;
            if (_flashBannerPanel != null)
            {
                _flashBannerPanel.SetActive(false);
            }
        }

        public void RefreshHudImmediate(GameFlowState? stateOverride = null)
        {
            EnsureBuilt();
            if (_player != null)
            {
                UpdateDashboardCluster();
            }
            if (_lapManager != null)
            {
                UpdateTopCenterRank();
                UpdateBottomRightStatus();
            }
            UpdateLeaderboardAndButtons();
            UpdateMinimapDots();

            GameFlowState state = stateOverride ?? (_gameManager != null ? _gameManager.CurrentFlowState : GameFlowState.Racing);
            UpdateStateOverlays(state);
        }

        private void OnEnable()
        {
            if (_isBound && !_isSubscribed)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            Unsubscribe();
            if (!_isBound) return;

            bool attachedAny = false;

            if (_lapManager != null)
            {
                _lapManager.LapCompleted += HandleLapCompleted;
                _lapManager.RacerFinished += HandleRacerFinished;
                attachedAny = true;
            }

            if (_gameManager != null)
            {
                _gameManager.RacerKicked += HandleRacerKicked;
                attachedAny = true;
            }

            if (_boundObstacleSystem == null && _gameManager != null && _gameManager.ObstacleSystem != null)
            {
                _boundObstacleSystem = _gameManager.ObstacleSystem;
            }

            if (_boundObstacleSystem != null)
            {
                _boundObstacleSystem.ObstacleHit += HandleObstacleHit;
                attachedAny = true;
            }

            _isSubscribed = attachedAny;
        }

        private void Unsubscribe()
        {
            if (_lapManager != null)
            {
                _lapManager.LapCompleted -= HandleLapCompleted;
                _lapManager.RacerFinished -= HandleRacerFinished;
            }

            if (_gameManager != null)
            {
                _gameManager.RacerKicked -= HandleRacerKicked;
                if (_gameManager.ObstacleSystem != null)
                {
                    _gameManager.ObstacleSystem.ObstacleHit -= HandleObstacleHit;
                }
            }

            if (_boundObstacleSystem != null)
            {
                _boundObstacleSystem.ObstacleHit -= HandleObstacleHit;
            }

            _isSubscribed = false;
        }

        private void HandleObstacleHit(Car car, TrackObstacle obstacle)
        {
            if (this == null || !isActiveAndEnabled) return;
            if (car != _player || obstacle == null) return;
            ShowFlash("⚠ VA CHẠM CHƯỚNG NGẠI VẬT!", LobbyUiBuilder.DangerRed, 1.0f);
        }

        private void HandleLapCompleted(RacerProgress progress)
        {
            if (this == null || !isActiveAndEnabled) return;
            if (progress == null || progress.Car != _player || progress.IsFinished || _lapManager == null) return;
            ShowFlash(
                $"VÒNG {progress.CompletedLaps}/{_lapManager.TotalLaps}  -  {FormatTime(progress.LastLapTime)}",
                LobbyUiBuilder.SafetyAmber);
        }

        private void HandleRacerFinished(RacerProgress progress)
        {
            if (this == null || !isActiveAndEnabled) return;
            if (progress == null || progress.Car != _player) return;
            ShowFlash($"VỀ ĐÍCH! HẠNG {progress.FinishPosition}", LobbyUiBuilder.SuccessGreen);
        }

        private void HandleRacerKicked(Car attacker, Car target, Vector2 hitPos)
        {
            if (this == null || !isActiveAndEnabled) return;
            if (attacker == _player)
            {
                string targetName = target != null ? target.RacerName.ToUpperInvariant() : "ĐỐI THỦ";
                ShowFlash($"POW! ĐÁ TRÚNG {targetName}!", LobbyUiBuilder.SafetyAmber, 1.1f);
            }
            else if (target == _player)
            {
                ShowFlash("⚠ BỊ ĐỐI THỦ ĐÁ!", LobbyUiBuilder.DangerRed, 1.0f);
            }
        }

        private void ShowFlash(string msg, Color color, float duration = -1f)
        {
            if (_flashBannerPanel == null || _flashBannerText == null) return;
            _flashBannerText.text = msg;
            _flashBannerText.color = color;
            if (_flashBannerAccentStrip != null)
            {
                _flashBannerAccentStrip.color = color;
            }
            _flashBannerPanel.SetActive(true);
            _flashStartTime = Time.unscaledTime;
            _flashUntil = Time.unscaledTime + (duration > 0f ? duration : lapMessageDuration);
        }

        private void Update()
        {
            if (_hudRoot == null || _gameManager == null || _player == null) return;

            GameFlowState state = _gameManager.CurrentFlowState;
            bool visible = state != GameFlowState.MainMenu && state != GameFlowState.UpgradeShop;
            if (_hudRoot.activeSelf != visible)
            {
                _hudRoot.SetActive(visible);
            }
            if (!visible) return;

            UpdateDashboardCluster();
            UpdateTopCenterRank();
            UpdateBottomRightStatus();
            UpdateLeaderboardAndButtons();
            UpdateMinimapDots();
            UpdateStateOverlays(state);
        }

        private void UpdateDashboardCluster()
        {
            if (_player == null || _player.Physics == null) return;

            float absSpeed = Mathf.Abs(_player.Physics.ForwardSpeed);
            int kmh = Mathf.RoundToInt(Mathf.Abs(_player.Physics.SpeedKmh));
            float speedNorm = Mathf.Clamp01(absSpeed / Mathf.Max(1f, _player.Physics.MaxSpeed));

            if (_lcdSpeedText != null)
            {
                _lcdSpeedText.text = kmh.ToString("000");
            }

            if (_lcdGearText != null)
            {
                int gear = Mathf.Clamp(1 + Mathf.FloorToInt(speedNorm * 5.9f), 1, 6);
                _lcdGearText.text = $"G{gear}";
            }

            if (_trackStateText != null)
            {
                bool policeNear = _gameManager != null && _gameManager.PoliceController != null &&
                                  _gameManager.PoliceController.IsPlayerInWarningRange;
                if (!_player.IsOnTrack)
                {
                    _trackStateText.text = "OFF-ROAD!";
                    _trackStateText.color = LobbyUiBuilder.DangerRed;
                    if (_trackStateBadgeBg != null) _trackStateBadgeBg.color = new Color(0.32f, 0.06f, 0.08f, 0.96f);
                }
                else if (policeNear)
                {
                    _trackStateText.text = "CẢNH SÁT ĐUỔI!";
                    _trackStateText.color = _gameManager.PoliceController.CurrentSirenColor;
                    if (_trackStateBadgeBg != null) _trackStateBadgeBg.color = new Color(0.28f, 0.08f, 0.14f, 0.96f);
                }
                else
                {
                    _trackStateText.text = "ON ROAD";
                    _trackStateText.color = LobbyUiBuilder.SuccessGreen;
                    if (_trackStateBadgeBg != null) _trackStateBadgeBg.color = LobbyUiBuilder.SurfaceContainer;
                }
            }

            float rpmNorm = Mathf.Clamp01((speedNorm * 0.85f) + (_player.LastInput.throttle * 0.15f));
            if (_needleRect != null)
            {
                // Dial ticks run from +130 deg (zero) clockwise to -130 deg (redline)
                float targetAngle = Mathf.Lerp(130f, -130f, rpmNorm);
                float dt = Time.deltaTime;
                _needleAngleSmoothed = dt > 0f
                    ? Mathf.Lerp(_needleAngleSmoothed, targetAngle, 1f - Mathf.Exp(-14f * dt))
                    : targetAngle;
                _needleRect.localRotation = Quaternion.Euler(0f, 0f, _needleAngleSmoothed);
            }

            if (_rpmSegments.Count > 0)
            {
                int litCount = Mathf.RoundToInt(rpmNorm * _rpmSegments.Count);
                for (int i = 0; i < _rpmSegments.Count; i++)
                {
                    if (i < litCount)
                    {
                        _rpmSegments[i].color = i >= 10
                            ? LobbyUiBuilder.DangerRed
                            : (i >= 7 ? LobbyUiBuilder.SafetyAmber : LobbyUiBuilder.ElectricCyan);
                    }
                    else
                    {
                        _rpmSegments[i].color = LobbyUiBuilder.SurfaceHigh;
                    }
                }
            }
        }

        private void UpdateTopCenterRank()
        {
            if (_rankText == null || _lapManager == null) return;
            int pos = _lapManager.GetPosition(_player);
            _rankText.text = FormatOrdinal(pos);
            _rankText.color = pos == 1 ? LobbyUiBuilder.ElectricCyan : LobbyUiBuilder.SafetyAmber;

            if (_rankStatusText != null && _rankBadgeBg != null)
            {
                if (pos == 1)
                {
                    _rankStatusText.text = "DẪN ĐẦU";
                    _rankBadgeBg.color = LobbyUiBuilder.SafetyAmber;
                    _rankStatusText.color = LobbyUiBuilder.OnAmber;
                }
                else
                {
                    _rankStatusText.text = pos > 0 ? $"HẠNG #{pos}" : "ĐANG ĐUA";
                    _rankBadgeBg.color = LobbyUiBuilder.SurfaceHigh;
                    _rankStatusText.color = LobbyUiBuilder.TextPrimary;
                }
            }
        }

        private void UpdateBottomRightStatus()
        {
            if (_lapManager == null) return;
            RacerProgress progress = _lapManager.GetProgress(_player);
            if (_lapBottomText != null && progress != null)
            {
                int curLap = Mathf.Min(progress.CompletedLaps + 1, _lapManager.TotalLaps);
                _lapBottomText.text = $"LAP {curLap}/{_lapManager.TotalLaps}";
            }
            if (_timerBottomText != null)
            {
                _timerBottomText.text = FormatTime(_lapManager.RaceTime);
            }
            if (_bestLapBottomText != null)
            {
                _bestLapBottomText.text = progress != null && progress.HasBestLap
                    ? FormatTime(progress.BestLapTime)
                    : "--:--.--";
            }
        }

        private void UpdateLeaderboardAndButtons()
        {
            if (_muteBtnLabel != null && _gameManager != null && _gameManager.AudioController != null)
            {
                _muteBtnLabel.text = _gameManager.AudioController.IsMuted ? "Âm: TẮT [M]" : "Âm: BẬT [M]";
            }

            if (_lapManager == null) return;
            IReadOnlyList<RacerProgress> ranking = _lapManager.GetRanking();
            EnsureRankingRows(ranking.Count);

            if (_racerCountText != null)
            {
                _racerCountText.text = $"{ranking.Count} TAY ĐUA";
            }

            for (int i = 0; i < _rankingRows.Count; i++)
            {
                bool active = i < ranking.Count;
                _rankingRows[i].gameObject.SetActive(active);
                if (!active) continue;

                RacerProgress r = ranking[i];
                bool isPlayer = r.Car == _player;
                _rankingRows[i].color = isPlayer
                    ? LobbyUiBuilder.SafetyAmber
                    : (r.Car != null ? Color.Lerp(r.Car.BikeColor, Color.white, 0.35f) : LobbyUiBuilder.TextPrimary);
                string st = r.IsFinished
                    ? FormatTime(r.FinishTime)
                    : $"L{Mathf.Min(r.CompletedLaps + 1, _lapManager.TotalLaps)}/{_lapManager.TotalLaps}";
                _rankingRows[i].text = $"{i + 1}. {r.RacerName,-9} {st}";
            }

            if (_rankingBoxRect != null)
            {
                _rankingBoxRect.sizeDelta = new Vector2(296f, 40f + ranking.Count * 23f);
            }
        }

        private void UpdateMinimapDots()
        {
            if (_minimapRect == null) return;

            IReadOnlyList<Car> cars = ResolveMinimapCars();
            while (_mapDots.Count < cars.Count)
            {
                GameObject dotObj = new GameObject($"MapDot_{_mapDots.Count}");
                dotObj.transform.SetParent(_minimapRect, false);
                Image img = dotObj.AddComponent<Image>();
                img.sprite = RoadRashUiSprites.GetDotSprite();
                img.raycastTarget = false;
                _mapDots.Add(img);
            }

            float pad = MinimapSizePx * 0.12f;
            float usable = MinimapSizePx - pad * 2f;

            for (int i = 0; i < _mapDots.Count; i++)
            {
                bool active = i < cars.Count && cars[i] != null;
                _mapDots[i].gameObject.SetActive(active);
                if (!active) continue;

                Car car = cars[i];
                Vector2 norm = (car.Physics.Position - _mapMin) / Mathf.Max(1f, _mapMaxDim);
                Vector2 px = new Vector2(pad + norm.x * usable, pad + norm.y * usable);

                RectTransform rt = _mapDots[i].rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = px;

                bool isPlayer = car == _player;
                float size = isPlayer ? 11f : 8f;
                rt.sizeDelta = new Vector2(size, size);
                _mapDots[i].color = isPlayer
                    ? (car.IsOnTrack ? LobbyUiBuilder.ElectricCyan : LobbyUiBuilder.DangerRed)
                    : car.BikeColor;
                if (isPlayer) rt.SetAsLastSibling();
            }
        }

        private IReadOnlyList<Car> ResolveMinimapCars()
        {
            if (_gameManager != null && _gameManager.AllCars != null && _gameManager.AllCars.Count > 0)
            {
                return _gameManager.AllCars;
            }

            _fallbackCars.Clear();
            if (_lapManager != null)
            {
                IReadOnlyList<RacerProgress> ranking = _lapManager.GetRanking();
                for (int i = 0; i < ranking.Count; i++)
                {
                    if (ranking[i].Car != null) _fallbackCars.Add(ranking[i].Car);
                }
            }
            if (_fallbackCars.Count == 0 && _player != null)
            {
                _fallbackCars.Add(_player);
            }
            return _fallbackCars;
        }

        private void UpdateStateOverlays(GameFlowState state)
        {
            RacerProgress progress = _lapManager != null ? _lapManager.GetProgress(_player) : null;

            // Center Countdown
            bool isCountdown = state == GameFlowState.Countdown;
            if (_countdownPanel != null && _countdownPanel.activeSelf != isCountdown)
            {
                _countdownPanel.SetActive(isCountdown);
            }
            if (isCountdown && _countdownText != null && _countdownSubText != null)
            {
                float rem = _gameManager != null ? _gameManager.CountdownRemaining : 3f;
                int whole = Mathf.Max(1, Mathf.CeilToInt(rem));
                if (whole != _lastCountdownWhole)
                {
                    _lastCountdownWhole = whole;
                    _countdownPopTimer = 0.08f;
                }
                _countdownPopTimer += Time.deltaTime;
                float s = Mathf.Max(0.45f, EvalOutBack(Mathf.Clamp01(_countdownPopTimer / 0.25f)));
                _countdownText.text = whole.ToString();
                _countdownText.rectTransform.localScale = Vector3.one * s;
                _countdownSubText.text = "CHUẨN BỊ XUẤT PHÁT!  [SPACE: ĐÁ ĐỐI THỦ]";
            }
            else
            {
                _lastCountdownWhole = -1;
            }

            // Center Wrong-way alert (only shown when triggered during Racing)
            bool wrongWay = state == GameFlowState.Racing && progress != null && progress.IsWrongWay;
            if (_wrongWayPanel != null)
            {
                if (_wrongWayPanel.activeSelf != wrongWay)
                {
                    _wrongWayPanel.SetActive(wrongWay);
                }
                if (wrongWay)
                {
                    float pulse = 1f + Mathf.Sin(Time.unscaledTime * 12f) * 0.05f;
                    _wrongWayPanel.transform.localScale = Vector3.one * pulse;
                }
            }

            // Top-center Flash Banner (GO!, Lap split, POW!, Obstacle hit)
            if (_flashBannerPanel != null && _flashBannerText != null)
            {
                if (state == GameFlowState.Racing && Time.unscaledTime < _flashUntil)
                {
                    _flashBannerPanel.SetActive(true);
                    float t = Mathf.Clamp01((Time.unscaledTime - _flashStartTime) / 0.2f);
                    float s = Mathf.Max(0.6f, EvalOutBack(t));
                    _flashBannerText.rectTransform.localScale = Vector3.one * s;
                }
                else if (state == GameFlowState.Racing && _lapManager != null && _lapManager.RaceTime > 0.001f && _lapManager.RaceTime < 1.1f)
                {
                    _flashBannerPanel.SetActive(true);
                    _flashBannerText.text = "GO! XUẤT PHÁT!";
                    _flashBannerText.color = LobbyUiBuilder.SuccessGreen;
                    if (_flashBannerAccentStrip != null) _flashBannerAccentStrip.color = LobbyUiBuilder.SuccessGreen;
                    _flashBannerText.rectTransform.localScale = Vector3.one * 1.08f;
                }
                else
                {
                    _flashBannerPanel.SetActive(false);
                }
            }

            // Center Post-Race Results Panel
            if (_resultsPanel != null)
            {
                bool showResults = state == GameFlowState.Finished;
                if (_resultsPanel.activeSelf != showResults)
                {
                    _resultsPanel.SetActive(showResults);
                    if (showResults) RefreshResultsPanel();
                }
            }
        }

        private void RefreshResultsPanel()
        {
            if (_lapManager == null) return;
            IReadOnlyList<RacerProgress> ranking = _lapManager.GetRanking();
            EnsureResultRows(ranking.Count);

            for (int i = 0; i < _resultRows.Count; i++)
            {
                bool active = i < ranking.Count;
                _resultRows[i].gameObject.SetActive(active);
                if (!active) continue;

                RacerProgress r = ranking[i];
                _resultRows[i].color = r.Car == _player ? LobbyUiBuilder.SafetyAmber : LobbyUiBuilder.TextPrimary;
                string finishTime = r.IsFinished ? FormatTime(r.FinishTime) : "đang chạy...";
                string best = r.HasBestLap ? FormatTime(r.BestLapTime) : "--:--.--";
                _resultRows[i].text = $"{i + 1}. {r.RacerName,-10}  {finishTime,-11}  Best {best}";
            }

            if (_prizeText != null && _gameManager != null)
            {
                int totalMoney = _gameManager.UpgradeSystem != null ? _gameManager.UpgradeSystem.Money : 0;
                _prizeText.text = $"Thưởng hạng {_gameManager.LastFinishPosition}: +${_gameManager.LastPrizeEarned}   |   Tổng tiền: ${totalMoney}";
            }
        }

        public void RebuildMinimapTexture()
        {
            if (_minimapImg == null || _track == null) return;
            Texture2D tex = RoadRashUiSprites.BuildTrackMinimapTexture(_track, 160, out _mapMin, out _mapMaxDim);
            _minimapImg.texture = tex;
            if (_minimapTrackLenText != null)
            {
                _minimapTrackLenText.text = $"{Mathf.RoundToInt(_track.TotalLength)}M";
            }
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;

            _canvas = RoadRashUiSprites.CreateOverlayCanvas("RaceHUDCanvas", 10);
            _hudRoot = new GameObject("HudRoot");
            _hudRoot.transform.SetParent(_canvas.transform, false);
            RectTransform rootRt = _hudRoot.AddComponent<RectTransform>();
            LobbyUiBuilder.StretchFill(rootRt);

            BuildTopLeftLeaderboard();
            BuildTopCenterRank();
            BuildTopRightMinimap();
            BuildBottomLeftCluster();
            BuildBottomRightReadout();
            BuildCenterOverlays();
            BuildResultsPanel();
        }

        private void BuildTopLeftLeaderboard()
        {
            // Quick Action Bar above Leaderboard
            GameObject bar = new GameObject("QuickButtons");
            bar.transform.SetParent(_hudRoot.transform, false);
            RectTransform barRt = bar.AddComponent<RectTransform>();
            LobbyUiBuilder.PlaceTopLeft(barRt, 16f, -14f, 296f, 30f);

            HorizontalLayoutGroup hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            Button menuBtn = LobbyUiBuilder.CreateCyberButton(
                bar.transform, "MenuBtn", "Pause [Esc]",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.TextPrimary, 11f);
            EnableButtonLabelAutoSize(menuBtn, 9f, 11.5f);
            menuBtn.onClick.AddListener(() => _gameManager?.TogglePause());

            Button restartBtn = LobbyUiBuilder.CreateCyberButton(
                bar.transform, "RestartBtn", "Đua lại [R]",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.TextPrimary, 11f);
            EnableButtonLabelAutoSize(restartBtn, 9f, 11.5f);
            restartBtn.onClick.AddListener(() => _gameManager?.StartRace());

            Button muteBtn = LobbyUiBuilder.CreateCyberButton(
                bar.transform, "MuteBtn", "Âm: BẬT [M]",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.SafetyAmber, 11f);
            EnableButtonLabelAutoSize(muteBtn, 9f, 11.5f);
            _muteBtnLabel = LobbyUiBuilder.GetPrimaryLabel(muteBtn);
            muteBtn.onClick.AddListener(() => _gameManager?.AudioController?.ToggleMute());

            // Live Standings Box
            Image bg = LobbyUiBuilder.CreateCyberPanel(
                _hudRoot.transform, "LeaderboardPanel", LobbyUiBuilder.SurfaceLowest, LobbyUiBuilder.ElectricCyan);
            _rankingBoxRect = bg.rectTransform;
            LobbyUiBuilder.PlaceTopLeft(_rankingBoxRect, 16f, -50f, 296f, 160f);

            TMP_Text header = LobbyUiBuilder.CreateTmpText(
                bg.transform, "Header", "● BẢNG XẾP HẠNG // LIVE",
                10.5f, LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            header.enableAutoSizing = true;
            header.fontSizeMin = 9f;
            header.fontSizeMax = 11f;
            LobbyUiBuilder.PlaceTopLeft(header.rectTransform, 12f, -8f, 190f, 20f);

            _racerCountText = LobbyUiBuilder.CreateTmpText(
                bg.transform, "RacerCountBadge", "0 TAY ĐUA",
                9.5f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            _racerCountText.enableAutoSizing = true;
            _racerCountText.fontSizeMin = 8.5f;
            _racerCountText.fontSizeMax = 10.5f;
            LobbyUiBuilder.PlaceTopRight(_racerCountText.rectTransform, -12f, -8f, 80f, 20f);
        }

        private void EnsureRankingRows(int count)
        {
            if (_rankingBoxRect == null) return;
            while (_rankingRows.Count < count)
            {
                int idx = _rankingRows.Count;
                TMP_Text row = LobbyUiBuilder.CreateTmpText(
                    _rankingBoxRect, $"RankRow_{idx}", "",
                    12.5f, LobbyUiBuilder.TextPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                row.enableAutoSizing = true;
                row.fontSizeMin = 10f;
                row.fontSizeMax = 13f;
                LobbyUiBuilder.PlaceTopLeft(row.rectTransform, 12f, -34f - idx * 23f, 272f, 21f);
                _rankingRows.Add(row);
            }
        }

        private void BuildTopCenterRank()
        {
            Image shieldBg = LobbyUiBuilder.CreateCyberPanel(
                _hudRoot.transform, "RankShieldPanel", LobbyUiBuilder.SurfaceLowest, LobbyUiBuilder.ElectricCyan);
            shieldBg.raycastTarget = false;
            LobbyUiBuilder.PlaceTop(shieldBg.rectTransform, -14f, 264f, 62f);

            TMP_Text rankCaption = LobbyUiBuilder.CreateTmpText(
                shieldBg.transform, "RankCaption", "THỨ HẠNG\nPOSITION",
                9.5f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.MidlineRight, FontStyles.Bold, null, wrap: true);
            LobbyUiBuilder.PlaceTopLeft(rankCaption.rectTransform, 12f, -10f, 70f, 42f);

            _rankText = LobbyUiBuilder.CreateTmpText(
                shieldBg.transform, "RankText", "1st",
                34f, LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.Center, FontStyles.Bold);
            _rankText.enableAutoSizing = true;
            _rankText.fontSizeMin = 22f;
            _rankText.fontSizeMax = 36f;
            LobbyUiBuilder.PlaceTopLeft(_rankText.rectTransform, 86f, -6f, 92f, 50f);

            _rankBadgeBg = LobbyUiBuilder.CreateCyberPanel(
                shieldBg.transform, "RankStatusPill", LobbyUiBuilder.SafetyAmber);
            _rankBadgeBg.raycastTarget = false;
            LobbyUiBuilder.PlaceTopRight(_rankBadgeBg.rectTransform, -12f, -17f, 74f, 28f);

            _rankStatusText = LobbyUiBuilder.CreateTmpText(
                _rankBadgeBg.transform, "RankStatus", "DẪN ĐẦU",
                10f, LobbyUiBuilder.OnAmber, TextAlignmentOptions.Center, FontStyles.Bold);
            _rankStatusText.enableAutoSizing = true;
            _rankStatusText.fontSizeMin = 8f;
            _rankStatusText.fontSizeMax = 10.5f;
            LobbyUiBuilder.StretchFill(_rankStatusText.rectTransform, 4f, 2f);

            // Top-center Tactical Flash Banner (kept high above the center road corridor)
            Image flashBg = LobbyUiBuilder.CreateCyberPanel(
                _hudRoot.transform, "FlashBannerPanel", LobbyUiBuilder.SurfaceLowest, LobbyUiBuilder.SafetyAmber);
            flashBg.raycastTarget = false;
            LobbyUiBuilder.PlaceTop(flashBg.rectTransform, -82f, 480f, 42f);

            Transform stripTr = flashBg.transform.Find("TopAccentStrip");
            if (stripTr != null)
            {
                _flashBannerAccentStrip = stripTr.GetComponent<Image>();
            }

            _flashBannerText = LobbyUiBuilder.CreateTmpText(
                flashBg.transform, "FlashBanner", "",
                20f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.Center, FontStyles.Bold);
            _flashBannerText.enableAutoSizing = true;
            _flashBannerText.fontSizeMin = 12f;
            _flashBannerText.fontSizeMax = 22f;
            LobbyUiBuilder.StretchFill(_flashBannerText.rectTransform, 14f, 4f);

            _flashBannerPanel = flashBg.gameObject;
            _flashBannerPanel.SetActive(false);
        }

        private void BuildTopRightMinimap()
        {
            Image mapFrame = LobbyUiBuilder.CreateCyberPanel(
                _hudRoot.transform, "MinimapPanel", LobbyUiBuilder.SurfaceLowest, LobbyUiBuilder.ElectricCyan);
            mapFrame.raycastTarget = false;
            LobbyUiBuilder.PlaceTopRight(mapFrame.rectTransform, -16f, -14f, 184f, 202f);

            TMP_Text title = LobbyUiBuilder.CreateTmpText(
                mapFrame.transform, "RadarHeader", "RADAR // TRACK",
                10f, LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LobbyUiBuilder.PlaceTopLeft(title.rectTransform, 12f, -6f, 118f, 18f);

            TMP_Text liveTag = LobbyUiBuilder.CreateTmpText(
                mapFrame.transform, "RadarLiveTag", "LIVE",
                9.5f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            LobbyUiBuilder.PlaceTopRight(liveTag.rectTransform, -12f, -6f, 44f, 18f);

            GameObject mapObj = new GameObject("Minimap");
            mapObj.transform.SetParent(mapFrame.transform, false);
            _minimapImg = mapObj.AddComponent<RawImage>();
            _minimapImg.raycastTarget = false;
            _minimapRect = _minimapImg.rectTransform;
            LobbyUiBuilder.PlaceTopLeft(_minimapRect, 16f, -26f, MinimapSizePx, MinimapSizePx);

            TMP_Text footerLbl = LobbyUiBuilder.CreateTmpText(
                mapFrame.transform, "TrackLenLabel", "CỰ LY VÒNG",
                9f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LobbyUiBuilder.PlaceTopLeft(footerLbl.rectTransform, 12f, -180f, 84f, 16f);

            _minimapTrackLenText = LobbyUiBuilder.CreateTmpText(
                mapFrame.transform, "TrackLenValue", "--M",
                9.5f, LobbyUiBuilder.TextPrimary, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            LobbyUiBuilder.PlaceTopRight(_minimapTrackLenText.rectTransform, -12f, -180f, 76f, 16f);
        }

        private void BuildBottomLeftCluster()
        {
            Image dashBg = LobbyUiBuilder.CreateCyberPanel(
                _hudRoot.transform, "DashboardCluster", LobbyUiBuilder.SurfaceLowest, LobbyUiBuilder.ElectricCyan);
            dashBg.raycastTarget = false;

            RectTransform dashRt = dashBg.rectTransform;
            dashRt.anchorMin = Vector2.zero;
            dashRt.anchorMax = Vector2.zero;
            dashRt.pivot = Vector2.zero;
            dashRt.anchoredPosition = new Vector2(16f, 14f);
            dashRt.sizeDelta = new Vector2(348f, 144f);

            // Analog RPM Dial + Rotating Red Needle on left side
            GameObject dialObj = new GameObject("TachDial");
            dialObj.transform.SetParent(dashBg.transform, false);
            Image dialImg = dialObj.AddComponent<Image>();
            dialImg.sprite = RoadRashUiSprites.GetGaugeDialSprite();
            dialImg.raycastTarget = false;
            RectTransform dialRt = dialImg.rectTransform;
            dialRt.anchorMin = Vector2.zero;
            dialRt.anchorMax = Vector2.zero;
            dialRt.pivot = new Vector2(0.5f, 0.5f);
            dialRt.anchoredPosition = new Vector2(66f, 72f);
            dialRt.sizeDelta = new Vector2(108f, 108f);

            GameObject needleObj = new GameObject("Needle");
            needleObj.transform.SetParent(dialObj.transform, false);
            Image needleImg = needleObj.AddComponent<Image>();
            needleImg.sprite = RoadRashUiSprites.GetNeedleSprite();
            needleImg.raycastTarget = false;
            _needleRect = needleImg.rectTransform;
            _needleRect.anchorMin = new Vector2(0.5f, 0.5f);
            _needleRect.anchorMax = new Vector2(0.5f, 0.5f);
            _needleRect.pivot = new Vector2(0.5f, 0.14f);
            _needleRect.anchoredPosition = Vector2.zero;
            _needleRect.sizeDelta = new Vector2(9f, 44f);

            // Track / Police State Pill
            _trackStateBadgeBg = LobbyUiBuilder.CreateCyberPanel(
                dashBg.transform, "TrackStatePill", LobbyUiBuilder.SurfaceContainer);
            _trackStateBadgeBg.raycastTarget = false;
            LobbyUiBuilder.PlaceTopLeft(_trackStateBadgeBg.rectTransform, 132f, -10f, 140f, 24f);

            _trackStateText = LobbyUiBuilder.CreateTmpText(
                _trackStateBadgeBg.transform, "TrackState", "ON ROAD",
                11f, LobbyUiBuilder.SuccessGreen, TextAlignmentOptions.Center, FontStyles.Bold);
            _trackStateText.enableAutoSizing = true;
            _trackStateText.fontSizeMin = 8.5f;
            _trackStateText.fontSizeMax = 11.5f;
            LobbyUiBuilder.StretchFill(_trackStateText.rectTransform, 6f, 2f);

            // Virtual Gear Capsule (G1-G6)
            Image gearPill = LobbyUiBuilder.CreateCyberPanel(
                dashBg.transform, "GearCapsule", LobbyUiBuilder.SurfaceHigh);
            gearPill.raycastTarget = false;
            LobbyUiBuilder.PlaceTopRight(gearPill.rectTransform, -12f, -10f, 56f, 24f);

            _lcdGearText = LobbyUiBuilder.CreateTmpText(
                gearPill.transform, "LcdGear", "G1",
                14f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.Center, FontStyles.Bold);
            LobbyUiBuilder.StretchFill(_lcdGearText.rectTransform, 4f, 1f);

            // Digital LCD Speed Box
            Image lcdBox = LobbyUiBuilder.CreateCyberPanel(
                dashBg.transform, "LcdSpeedBox", LobbyUiBuilder.SurfaceContainer);
            lcdBox.raycastTarget = false;
            LobbyUiBuilder.PlaceTopLeft(lcdBox.rectTransform, 132f, -38f, 204f, 56f);

            _lcdSpeedText = LobbyUiBuilder.CreateTmpText(
                lcdBox.transform, "LcdSpeed", "000",
                38f, LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            _lcdSpeedText.enableAutoSizing = true;
            _lcdSpeedText.fontSizeMin = 24f;
            _lcdSpeedText.fontSizeMax = 38f;
            LobbyUiBuilder.PlaceTopLeft(_lcdSpeedText.rectTransform, 10f, -4f, 122f, 48f);

            TMP_Text kmhUnit = LobbyUiBuilder.CreateTmpText(
                lcdBox.transform, "SpeedUnit", "KM/H",
                12f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LobbyUiBuilder.PlaceTopLeft(kmhUnit.rectTransform, 138f, -8f, 58f, 20f);

            TMP_Text speedCaption = LobbyUiBuilder.CreateTmpText(
                lcdBox.transform, "SpeedCaption", "SPEED // LCD",
                8.5f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LobbyUiBuilder.PlaceTopLeft(speedCaption.rectTransform, 138f, -30f, 58f, 16f);

            // 12-Segment LED RPM Strip
            GameObject rpmStrip = new GameObject("RpmLedStrip");
            rpmStrip.transform.SetParent(dashBg.transform, false);
            RectTransform rpmRt = rpmStrip.AddComponent<RectTransform>();
            LobbyUiBuilder.PlaceTopLeft(rpmRt, 132f, -98f, 204f, 14f);

            HorizontalLayoutGroup rpmHlg = rpmStrip.AddComponent<HorizontalLayoutGroup>();
            rpmHlg.spacing = 3f;
            rpmHlg.childControlWidth = true;
            rpmHlg.childControlHeight = true;
            rpmHlg.childForceExpandWidth = true;
            rpmHlg.childForceExpandHeight = true;

            _rpmSegments.Clear();
            for (int i = 0; i < RpmSegmentCount; i++)
            {
                GameObject seg = new GameObject($"RpmSeg_{i}");
                seg.transform.SetParent(rpmStrip.transform, false);
                Image segImg = seg.AddComponent<Image>();
                segImg.raycastTarget = false;
                segImg.color = LobbyUiBuilder.SurfaceHigh;
                _rpmSegments.Add(segImg);
            }

            TMP_Text hintStrip = LobbyUiBuilder.CreateTmpText(
                dashBg.transform, "DashHint", "W/S/A/D: LÁI  •  [SPACE]: ĐẠP ĐỐI THỦ",
                9.5f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            hintStrip.enableAutoSizing = true;
            hintStrip.fontSizeMin = 8f;
            hintStrip.fontSizeMax = 10f;
            LobbyUiBuilder.PlaceTopLeft(hintStrip.rectTransform, 132f, -118f, 204f, 18f);
        }

        private void BuildBottomRightReadout()
        {
            Image brPanel = LobbyUiBuilder.CreateCyberPanel(
                _hudRoot.transform, "BottomRightReadout", LobbyUiBuilder.SurfaceLowest, LobbyUiBuilder.SafetyAmber);
            brPanel.raycastTarget = false;

            RectTransform rt = brPanel.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-16f, 14f);
            rt.sizeDelta = new Vector2(256f, 128f);

            TMP_Text lapCaption = LobbyUiBuilder.CreateTmpText(
                brPanel.transform, "LapCaption", "TIẾN ĐỘ CHẶNG\nCURRENT LAP",
                9f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, null, wrap: true);
            LobbyUiBuilder.PlaceTopLeft(lapCaption.rectTransform, 14f, -10f, 100f, 28f);

            _lapBottomText = LobbyUiBuilder.CreateTmpText(
                brPanel.transform, "LapText", "LAP 1/3",
                22f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            _lapBottomText.enableAutoSizing = true;
            _lapBottomText.fontSizeMin = 15f;
            _lapBottomText.fontSizeMax = 23f;
            LobbyUiBuilder.PlaceTopRight(_lapBottomText.rectTransform, -14f, -8f, 128f, 32f);

            Image timerBox = LobbyUiBuilder.CreateCyberPanel(
                brPanel.transform, "TimerBox", LobbyUiBuilder.SurfaceContainer);
            timerBox.raycastTarget = false;
            LobbyUiBuilder.PlaceTopLeft(timerBox.rectTransform, 12f, -44f, 232f, 54f);

            TMP_Text timerCaption = LobbyUiBuilder.CreateTmpText(
                timerBox.transform, "TimerCaption", "THỜI GIAN ĐUA // RACE TIME",
                8.5f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LobbyUiBuilder.PlaceTopLeft(timerCaption.rectTransform, 10f, -4f, 212f, 14f);

            _timerBottomText = LobbyUiBuilder.CreateTmpText(
                timerBox.transform, "TimerText", "00:00.00",
                27f, LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            _timerBottomText.enableAutoSizing = true;
            _timerBottomText.fontSizeMin = 18f;
            _timerBottomText.fontSizeMax = 28f;
            LobbyUiBuilder.PlaceTopLeft(_timerBottomText.rectTransform, 10f, -18f, 212f, 32f);

            TMP_Text bestLbl = LobbyUiBuilder.CreateTmpText(
                brPanel.transform, "BestLapLabel", "BEST LAP",
                9.5f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LobbyUiBuilder.PlaceTopLeft(bestLbl.rectTransform, 14f, -104f, 90f, 18f);

            _bestLapBottomText = LobbyUiBuilder.CreateTmpText(
                brPanel.transform, "BestLapValue", "--:--.--",
                11f, LobbyUiBuilder.TextPrimary, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            LobbyUiBuilder.PlaceTopRight(_bestLapBottomText.rectTransform, -14f, -104f, 130f, 18f);
        }

        private void BuildCenterOverlays()
        {
            // Countdown overlay (strictly hidden when not in Countdown state)
            _countdownPanel = new GameObject("CountdownPanel");
            _countdownPanel.transform.SetParent(_hudRoot.transform, false);
            RectTransform cdRt = _countdownPanel.AddComponent<RectTransform>();
            cdRt.anchorMin = new Vector2(0.5f, 0.56f);
            cdRt.anchorMax = new Vector2(0.5f, 0.56f);
            cdRt.pivot = new Vector2(0.5f, 0.5f);
            cdRt.sizeDelta = new Vector2(540f, 150f);

            _countdownText = LobbyUiBuilder.CreateTmpText(
                _countdownPanel.transform, "CountdownNumber", "3",
                78f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.Center, FontStyles.Bold);
            _countdownText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _countdownText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _countdownText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _countdownText.rectTransform.anchoredPosition = new Vector2(0f, 24f);
            _countdownText.rectTransform.sizeDelta = new Vector2(240f, 96f);

            _countdownSubText = LobbyUiBuilder.CreateTmpText(
                _countdownPanel.transform, "CountdownSub", "CHUẨN BỊ XUẤT PHÁT!  [SPACE: ĐÁ ĐỐI THỦ]",
                15f, LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.Center, FontStyles.Bold);
            _countdownSubText.enableAutoSizing = true;
            _countdownSubText.fontSizeMin = 11f;
            _countdownSubText.fontSizeMax = 16f;
            _countdownSubText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _countdownSubText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _countdownSubText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _countdownSubText.rectTransform.anchoredPosition = new Vector2(0f, -48f);
            _countdownSubText.rectTransform.sizeDelta = new Vector2(520f, 32f);
            _countdownPanel.SetActive(false);

            // Wrong-way overlay (strictly hidden unless IsWrongWay during Racing)
            Image wrongBg = LobbyUiBuilder.CreateCyberPanel(
                _hudRoot.transform, "WrongWayPanel", new Color(0.36f, 0.04f, 0.06f, 0.94f), LobbyUiBuilder.DangerRed);
            wrongBg.raycastTarget = false;
            RectTransform wrt = wrongBg.rectTransform;
            wrt.anchorMin = new Vector2(0.5f, 0.66f);
            wrt.anchorMax = new Vector2(0.5f, 0.66f);
            wrt.pivot = new Vector2(0.5f, 0.5f);
            wrt.sizeDelta = new Vector2(430f, 48f);

            _wrongWayText = LobbyUiBuilder.CreateTmpText(
                wrongBg.transform, "WrongWay", "⚠ NGƯỢC CHIỀU! // WRONG WAY",
                20f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.Center, FontStyles.Bold);
            _wrongWayText.enableAutoSizing = true;
            _wrongWayText.fontSizeMin = 13f;
            _wrongWayText.fontSizeMax = 21f;
            LobbyUiBuilder.StretchFill(_wrongWayText.rectTransform, 12f, 4f);

            _wrongWayPanel = wrongBg.gameObject;
            _wrongWayPanel.SetActive(false);
        }

        private void BuildResultsPanel()
        {
            Image bg = LobbyUiBuilder.CreateCyberPanel(
                _hudRoot.transform, "ResultsPanel", LobbyUiBuilder.SurfaceLowest, LobbyUiBuilder.ElectricCyan);
            _resultsPanel = bg.gameObject;

            RectTransform rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(560f, 360f);

            TMP_Text title = LobbyUiBuilder.CreateTmpText(
                _resultsPanel.transform, "ResultsTitle", "KẾT QUẢ CUỘC ĐUA",
                26f, LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.Center, FontStyles.Bold);
            LobbyUiBuilder.PlaceTop(title.rectTransform, -18f, 500f, 38f);

            _prizeText = LobbyUiBuilder.CreateTmpText(
                _resultsPanel.transform, "PrizeText", "",
                17f, LobbyUiBuilder.SuccessGreen, TextAlignmentOptions.Center, FontStyles.Bold);
            _prizeText.enableAutoSizing = true;
            _prizeText.fontSizeMin = 12f;
            _prizeText.fontSizeMax = 18f;
            SetAnchoredCenterBottom(_prizeText.rectTransform, 0f, 74f, 510f, 30f);

            Button raceBtn = LobbyUiBuilder.CreateCyberButton(
                _resultsPanel.transform, "RetryBtn", "Đua lại [R]",
                LobbyUiBuilder.ElectricCyan, LobbyUiBuilder.OnCyan, 14f);
            SetAnchoredCenterBottom(raceBtn.GetComponent<RectTransform>(), -168f, 18f, 154f, 40f);
            raceBtn.onClick.AddListener(() => _gameManager?.StartRace());

            Button garageBtn = LobbyUiBuilder.CreateCyberButton(
                _resultsPanel.transform, "GarageBtn", "Nâng cấp xe",
                LobbyUiBuilder.SafetyAmber, LobbyUiBuilder.OnAmber, 14f);
            SetAnchoredCenterBottom(garageBtn.GetComponent<RectTransform>(), 0f, 18f, 154f, 40f);
            garageBtn.onClick.AddListener(() => _gameManager?.OpenGarage());

            Button menuBtn = LobbyUiBuilder.CreateCyberButton(
                _resultsPanel.transform, "ResultMenuBtn", "Về Menu [Esc]",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.TextPrimary, 14f);
            SetAnchoredCenterBottom(menuBtn.GetComponent<RectTransform>(), 168f, 18f, 154f, 40f);
            menuBtn.onClick.AddListener(() => _gameManager?.OpenMenu());

            _resultsPanel.SetActive(false);
        }

        private void EnsureResultRows(int count)
        {
            if (_resultsPanel == null) return;
            while (_resultRows.Count < count)
            {
                int idx = _resultRows.Count;
                TMP_Text row = LobbyUiBuilder.CreateTmpText(
                    _resultsPanel.transform, $"ResRow_{idx}", "",
                    15f, LobbyUiBuilder.TextPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                row.enableAutoSizing = true;
                row.fontSizeMin = 11f;
                row.fontSizeMax = 16f;
                row.rectTransform.anchorMin = new Vector2(0.5f, 1f);
                row.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                row.rectTransform.pivot = new Vector2(0.5f, 1f);
                row.rectTransform.anchoredPosition = new Vector2(0f, -72f - idx * 28f);
                row.rectTransform.sizeDelta = new Vector2(500f, 26f);
                _resultRows.Add(row);
            }
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

        private static void SetAnchoredCenterBottom(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static float EvalOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private static string FormatOrdinal(int pos)
        {
            if (pos <= 0) return "--";
            int rem100 = pos % 100;
            if (rem100 >= 11 && rem100 <= 13) return $"{pos}th";
            return (pos % 10) switch
            {
                1 => $"{pos}st",
                2 => $"{pos}nd",
                3 => $"{pos}rd",
                _ => $"{pos}th"
            };
        }

        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int minutes = (int)(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return $"{minutes:00}:{rest:00.00}";
        }
    }
}
