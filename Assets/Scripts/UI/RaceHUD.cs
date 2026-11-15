using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>
    /// Retro PS1 Road Rash uGUI HUD:
    /// Top-center rank badge, bottom-left metallic speedometer/tachometer cluster with rotating needle & LCD,
    /// bottom-right yellow lap/timer readout, top-right minimap, top-left leaderboard & quick controls,
    /// animated center popups (Countdown, Lap, Wrong-way, Kick hit), and Post-Race Results panel.
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        [SerializeField] private float lapMessageDuration = 2.4f;

        private GameManager _gameManager;
        private Car _player;
        private LapCheckpointManager _lapManager;
        private WaypointTrack _track;

        private Canvas _canvas;
        private GameObject _hudRoot;

        // Top-Center Rank
        private Text _rankText;

        // Bottom-Left Metallic Cluster
        private RectTransform _needleRect;
        private Text _lcdSpeedText;
        private Text _lcdGearText;
        private Text _trackStateText;
        private float _needleAngleSmoothed;

        // Bottom-Right Timer & Lap
        private Text _lapBottomText;
        private Text _timerBottomText;

        // Top-Left Leaderboard & Buttons
        private readonly List<Text> _rankingRows = new List<Text>();
        private RectTransform _rankingBoxRect;
        private Text _muteBtnLabel;

        // Top-Right Minimap
        private RawImage _minimapImg;
        private RectTransform _minimapRect;
        private Vector2 _mapMin;
        private float _mapMaxDim = 1f;
        private readonly List<Image> _mapDots = new List<Image>();
        private const float MinimapSizePx = 152f;

        // Center Overlays
        private Text _countdownText;
        private Text _countdownSubText;
        private Text _wrongWayText;
        private Text _flashBannerText;
        private float _flashUntil;
        private float _flashStartTime;

        // Results Panel
        private GameObject _resultsPanel;
        private readonly List<Text> _resultRows = new List<Text>();
        private Text _prizeText;

        private int _lastCountdownWhole = -1;
        private float _countdownPopTimer;

        public void Bind(GameManager gameManager, Car player, LapCheckpointManager lapManager, WaypointTrack track)
        {
            Unsubscribe();
            _gameManager = gameManager;
            _player = player;
            _lapManager = lapManager;
            _track = track;

            if (_lapManager != null)
            {
                _lapManager.LapCompleted += HandleLapCompleted;
                _lapManager.RacerFinished += HandleRacerFinished;
            }
            if (_gameManager != null)
            {
                _gameManager.RacerKicked += HandleRacerKicked;
                if (_gameManager.ObstacleSystem != null)
                {
                    _gameManager.ObstacleSystem.ObstacleHit += HandleObstacleHit;
                }
            }

            EnsureBuilt();
            RebuildMinimapTexture();
            ClearMessage();
        }

        public void ClearMessage()
        {
            _flashUntil = 0f;
            if (_flashBannerText != null) _flashBannerText.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Unsubscribe();
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
        }

        private void HandleObstacleHit(Car car, TrackObstacle obstacle)
        {
            if (car != _player || obstacle == null) return;
            ShowFlash("⚠ VA CHẠM CHƯỚNG NGẠI VẬT!", RoadRashVisualTheme.DangerRed, 1.0f);
        }

        private void HandleLapCompleted(RacerProgress progress)
        {
            if (progress.Car != _player || progress.IsFinished) return;
            ShowFlash($"VÒNG {progress.CompletedLaps}/{_lapManager.TotalLaps}  -  {FormatTime(progress.LastLapTime)}", RoadRashVisualTheme.HudYellow);
        }

        private void HandleRacerFinished(RacerProgress progress)
        {
            if (progress.Car != _player) return;
            ShowFlash($"VỀ ĐÍCH! HẠNG {progress.FinishPosition}", RoadRashVisualTheme.AccentGreen);
        }

        private void HandleRacerKicked(Car attacker, Car target, Vector2 hitPos)
        {
            if (attacker == _player)
            {
                string targetName = target != null ? target.RacerName.ToUpperInvariant() : "ĐỐI THỦ";
                ShowFlash($"POW! ĐÁ TRÚNG {targetName}!", RoadRashVisualTheme.HudYellow, 1.1f);
            }
            else if (target == _player)
            {
                ShowFlash("BỊ ĐÁ!", RoadRashVisualTheme.DangerRed, 1.0f);
            }
        }

        private void ShowFlash(string msg, Color color, float duration = -1f)
        {
            if (_flashBannerText == null) return;
            _flashBannerText.text = msg;
            _flashBannerText.color = color;
            _flashBannerText.gameObject.SetActive(true);
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
                    _trackStateText.color = RoadRashVisualTheme.DangerRed;
                }
                else if (policeNear)
                {
                    _trackStateText.text = "CẢNH SÁT ĐUỔI!";
                    _trackStateText.color = _gameManager.PoliceController.CurrentSirenColor;
                }
                else
                {
                    _trackStateText.text = "ON ROAD";
                    _trackStateText.color = RoadRashVisualTheme.AccentGreen;
                }
            }

            if (_needleRect != null)
            {
                // Dial ticks run from +130 deg (zero) clockwise to -130 deg (redline)
                float rpmNorm = Mathf.Clamp01((speedNorm * 0.85f) + (_player.LastInput.throttle * 0.15f));
                float targetAngle = Mathf.Lerp(130f, -130f, rpmNorm);
                _needleAngleSmoothed = Mathf.Lerp(_needleAngleSmoothed, targetAngle, 1f - Mathf.Exp(-14f * Time.deltaTime));
                _needleRect.localRotation = Quaternion.Euler(0f, 0f, _needleAngleSmoothed);
            }
        }

        private void UpdateTopCenterRank()
        {
            if (_rankText == null || _lapManager == null) return;
            int pos = _lapManager.GetPosition(_player);
            _rankText.text = FormatOrdinal(pos);
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
        }

        private void UpdateLeaderboardAndButtons()
        {
            if (_muteBtnLabel != null && _gameManager.AudioController != null)
            {
                _muteBtnLabel.text = _gameManager.AudioController.IsMuted ? "Âm: TẮT [M]" : "Âm: BẬT [M]";
            }

            if (_lapManager == null) return;
            IReadOnlyList<RacerProgress> ranking = _lapManager.GetRanking();
            EnsureRankingRows(ranking.Count);

            for (int i = 0; i < _rankingRows.Count; i++)
            {
                bool active = i < ranking.Count;
                _rankingRows[i].gameObject.SetActive(active);
                if (!active) continue;

                RacerProgress r = ranking[i];
                bool isPlayer = r.Car == _player;
                _rankingRows[i].color = isPlayer
                    ? RoadRashVisualTheme.HudYellow
                    : (r.Car != null ? Color.Lerp(r.Car.BikeColor, Color.white, 0.35f) : Color.white);
                string st = r.IsFinished
                    ? FormatTime(r.FinishTime)
                    : $"L{Mathf.Min(r.CompletedLaps + 1, _lapManager.TotalLaps)}/{_lapManager.TotalLaps}";
                _rankingRows[i].text = $"{i + 1}. {r.RacerName,-8} {st}";
            }

            if (_rankingBoxRect != null)
            {
                _rankingBoxRect.sizeDelta = new Vector2(236f, 34f + ranking.Count * 22f);
            }
        }

        private void UpdateMinimapDots()
        {
            if (_minimapRect == null || _gameManager == null) return;
            IReadOnlyList<Car> cars = _gameManager.AllCars;
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
                Vector2 norm = (car.Physics.Position - _mapMin) / _mapMaxDim;
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
                    ? (car.IsOnTrack ? RoadRashVisualTheme.HudYellow : RoadRashVisualTheme.DangerRed)
                    : car.BikeColor;
                if (isPlayer) rt.SetAsLastSibling();
            }
        }

        private void UpdateStateOverlays(GameFlowState state)
        {
            RacerProgress progress = _lapManager != null ? _lapManager.GetProgress(_player) : null;

            // Countdown
            bool isCountdown = state == GameFlowState.Countdown;
            _countdownText.gameObject.SetActive(isCountdown);
            _countdownSubText.gameObject.SetActive(isCountdown);
            if (isCountdown)
            {
                int whole = Mathf.Max(1, Mathf.CeilToInt(_gameManager.CountdownRemaining));
                if (whole != _lastCountdownWhole)
                {
                    _lastCountdownWhole = whole;
                    _countdownPopTimer = 0f;
                }
                _countdownPopTimer += Time.deltaTime;
                float s = EvalOutBack(Mathf.Clamp01(_countdownPopTimer / 0.25f));
                _countdownText.text = whole.ToString();
                _countdownText.rectTransform.localScale = Vector3.one * s;
                _countdownSubText.text = "CHUẨN BỊ XUẤT PHÁT!  [SPACE: ĐÁ ĐỐI THỦ]";
            }
            else
            {
                _lastCountdownWhole = -1;
            }

            // Wrong way
            bool wrongWay = state == GameFlowState.Racing && progress != null && progress.IsWrongWay;
            _wrongWayText.gameObject.SetActive(wrongWay);
            if (wrongWay)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 12f) * 0.08f;
                _wrongWayText.rectTransform.localScale = Vector3.one * pulse;
            }

            // Flash banner or GO!
            if (_flashBannerText != null)
            {
                if (state == GameFlowState.Racing && Time.unscaledTime < _flashUntil)
                {
                    _flashBannerText.gameObject.SetActive(true);
                    float t = Mathf.Clamp01((Time.unscaledTime - _flashStartTime) / 0.2f);
                    _flashBannerText.rectTransform.localScale = Vector3.one * EvalOutBack(t);
                }
                else if (state == GameFlowState.Racing && _lapManager != null && _lapManager.RaceTime < 1.1f)
                {
                    _flashBannerText.gameObject.SetActive(true);
                    _flashBannerText.text = "GO!";
                    _flashBannerText.color = RoadRashVisualTheme.AccentGreen;
                    _flashBannerText.rectTransform.localScale = Vector3.one * 1.3f;
                }
                else
                {
                    _flashBannerText.gameObject.SetActive(false);
                }
            }

            // Results panel
            bool showResults = state == GameFlowState.Finished;
            if (_resultsPanel.activeSelf != showResults)
            {
                _resultsPanel.SetActive(showResults);
                if (showResults) RefreshResultsPanel();
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
                _resultRows[i].color = r.Car == _player ? RoadRashVisualTheme.HudYellow : Color.white;
                string finishTime = r.IsFinished ? FormatTime(r.FinishTime) : "đang chạy...";
                string best = r.HasBestLap ? FormatTime(r.BestLapTime) : "--:--.--";
                _resultRows[i].text = $"{i + 1}. {r.RacerName,-10}  {finishTime,-11}  Best {best}";
            }

            int totalMoney = _gameManager.UpgradeSystem != null ? _gameManager.UpgradeSystem.Money : 0;
            _prizeText.text = $"Thưởng hạng {_gameManager.LastFinishPosition}: +${_gameManager.LastPrizeEarned}   |   Tổng tiền: ${totalMoney}";
        }

        private void RebuildMinimapTexture()
        {
            if (_minimapImg == null || _track == null) return;
            Texture2D tex = RoadRashUiSprites.BuildTrackMinimapTexture(_track, 160, out _mapMin, out _mapMaxDim);
            _minimapImg.texture = tex;
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;

            _canvas = RoadRashUiSprites.CreateOverlayCanvas("RaceHUDCanvas", 10);
            _hudRoot = new GameObject("HudRoot");
            _hudRoot.transform.SetParent(_canvas.transform, false);
            RectTransform rootRt = _hudRoot.AddComponent<RectTransform>();
            RoadRashUiSprites.StretchFill(rootRt);

            BuildTopCenterRank();
            BuildBottomLeftCluster();
            BuildBottomRightReadout();
            BuildTopLeftLeaderboard();
            BuildTopRightMinimap();
            BuildCenterOverlays();
            BuildResultsPanel();
        }

        private void BuildTopCenterRank()
        {
            _rankText = RoadRashUiSprites.CreateStyledText(
                _hudRoot.transform, "RankText", "1st", 46, RoadRashVisualTheme.HudYellow, TextAnchor.UpperCenter);
            RectTransform rt = _rankText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -14f);
            rt.sizeDelta = new Vector2(220f, 64f);
        }

        private void BuildBottomLeftCluster()
        {
            GameObject dashObj = new GameObject("DashboardCluster");
            dashObj.transform.SetParent(_hudRoot.transform, false);
            Image dashBg = dashObj.AddComponent<Image>();
            dashBg.sprite = RoadRashUiSprites.GetDashboardSprite();
            dashBg.raycastTarget = false;

            RectTransform dashRt = dashBg.rectTransform;
            dashRt.anchorMin = Vector2.zero;
            dashRt.anchorMax = Vector2.zero;
            dashRt.pivot = Vector2.zero;
            dashRt.anchoredPosition = new Vector2(16f, 14f);
            dashRt.sizeDelta = new Vector2(312f, 125f);

            // Analog dial on left
            GameObject dialObj = new GameObject("TachDial");
            dialObj.transform.SetParent(dashObj.transform, false);
            Image dialImg = dialObj.AddComponent<Image>();
            dialImg.sprite = RoadRashUiSprites.GetGaugeDialSprite();
            dialImg.raycastTarget = false;
            RectTransform dialRt = dialImg.rectTransform;
            dialRt.anchorMin = Vector2.zero;
            dialRt.anchorMax = Vector2.zero;
            dialRt.pivot = new Vector2(0.5f, 0.5f);
            dialRt.anchoredPosition = new Vector2(62f, 62f);
            dialRt.sizeDelta = new Vector2(106f, 106f);

            // Red needle
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

            // Top label strip
            Text hintStrip = RoadRashUiSprites.CreateStyledText(
                dashObj.transform, "DashHint", "W/S/A/D  |  SPACE: ĐÁ", 10, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, false);
            hintStrip.rectTransform.anchorMin = Vector2.zero;
            hintStrip.rectTransform.anchorMax = Vector2.zero;
            hintStrip.rectTransform.pivot = Vector2.zero;
            hintStrip.rectTransform.anchoredPosition = new Vector2(142f, 83f);
            hintStrip.rectTransform.sizeDelta = new Vector2(152f, 14f);

            // LCD Speed readout
            _lcdSpeedText = RoadRashUiSprites.CreateStyledText(
                dashObj.transform, "LcdSpeed", "000", 38, RoadRashVisualTheme.LcdInk, TextAnchor.MiddleRight, FontStyle.Bold, false);
            _lcdSpeedText.rectTransform.anchorMin = Vector2.zero;
            _lcdSpeedText.rectTransform.anchorMax = Vector2.zero;
            _lcdSpeedText.rectTransform.pivot = Vector2.zero;
            _lcdSpeedText.rectTransform.anchoredPosition = new Vector2(174f, 26f);
            _lcdSpeedText.rectTransform.sizeDelta = new Vector2(114f, 46f);

            // LCD Gear
            _lcdGearText = RoadRashUiSprites.CreateStyledText(
                dashObj.transform, "LcdGear", "G1", 20, RoadRashVisualTheme.LcdInk, TextAnchor.MiddleLeft, FontStyle.Bold, false);
            _lcdGearText.rectTransform.anchorMin = Vector2.zero;
            _lcdGearText.rectTransform.anchorMax = Vector2.zero;
            _lcdGearText.rectTransform.pivot = Vector2.zero;
            _lcdGearText.rectTransform.anchoredPosition = new Vector2(148f, 34f);
            _lcdGearText.rectTransform.sizeDelta = new Vector2(40f, 28f);

            // Track state badge right above dash
            _trackStateText = RoadRashUiSprites.CreateStyledText(
                dashObj.transform, "TrackState", "ON ROAD", 12, RoadRashVisualTheme.AccentGreen, TextAnchor.MiddleLeft);
            _trackStateText.rectTransform.anchorMin = Vector2.zero;
            _trackStateText.rectTransform.anchorMax = Vector2.zero;
            _trackStateText.rectTransform.pivot = Vector2.zero;
            _trackStateText.rectTransform.anchoredPosition = new Vector2(148f, 64f);
            _trackStateText.rectTransform.sizeDelta = new Vector2(120f, 18f);
        }

        private void BuildBottomRightReadout()
        {
            GameObject box = new GameObject("BottomRightReadout");
            box.transform.SetParent(_hudRoot.transform, false);
            RectTransform rt = box.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-24f, 20f);
            rt.sizeDelta = new Vector2(240f, 76f);

            GameObject triObj = new GameObject("RedTri");
            triObj.transform.SetParent(box.transform, false);
            Image triImg = triObj.AddComponent<Image>();
            triImg.sprite = RoadRashUiSprites.GetTriangleSprite();
            triImg.color = RoadRashVisualTheme.DangerRed;
            triImg.raycastTarget = false;
            triImg.rectTransform.anchorMin = new Vector2(0f, 0f);
            triImg.rectTransform.anchorMax = new Vector2(0f, 0f);
            triImg.rectTransform.pivot = new Vector2(0f, 0f);
            triImg.rectTransform.anchoredPosition = new Vector2(24f, 8f);
            triImg.rectTransform.sizeDelta = new Vector2(20f, 20f);

            _lapBottomText = RoadRashUiSprites.CreateStyledText(
                box.transform, "LapText", "LAP 1/3", 22, RoadRashVisualTheme.HudYellow, TextAnchor.MiddleRight);
            _lapBottomText.rectTransform.anchorMin = new Vector2(1f, 0f);
            _lapBottomText.rectTransform.anchorMax = new Vector2(1f, 0f);
            _lapBottomText.rectTransform.pivot = new Vector2(1f, 0f);
            _lapBottomText.rectTransform.anchoredPosition = new Vector2(0f, 38f);
            _lapBottomText.rectTransform.sizeDelta = new Vector2(210f, 30f);

            _timerBottomText = RoadRashUiSprites.CreateStyledText(
                box.transform, "TimerText", "00:00.00", 30, RoadRashVisualTheme.HudYellow, TextAnchor.MiddleRight);
            _timerBottomText.rectTransform.anchorMin = new Vector2(1f, 0f);
            _timerBottomText.rectTransform.anchorMax = new Vector2(1f, 0f);
            _timerBottomText.rectTransform.pivot = new Vector2(1f, 0f);
            _timerBottomText.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            _timerBottomText.rectTransform.sizeDelta = new Vector2(180f, 34f);
        }

        private void BuildTopLeftLeaderboard()
        {
            GameObject box = new GameObject("LeaderboardPanel");
            box.transform.SetParent(_hudRoot.transform, false);
            Image bg = box.AddComponent<Image>();
            bg.sprite = RoadRashUiSprites.GetPanelSprite();
            bg.type = Image.Type.Sliced;
            bg.color = RoadRashVisualTheme.PanelBackdrop;

            _rankingBoxRect = bg.rectTransform;
            _rankingBoxRect.anchorMin = new Vector2(0f, 1f);
            _rankingBoxRect.anchorMax = new Vector2(0f, 1f);
            _rankingBoxRect.pivot = new Vector2(0f, 1f);
            _rankingBoxRect.anchoredPosition = new Vector2(16f, -16f);
            _rankingBoxRect.sizeDelta = new Vector2(236f, 150f);

            Text header = RoadRashUiSprites.CreateStyledText(
                box.transform, "Header", "BẢNG XẾP HẠNG", 13, RoadRashVisualTheme.HudYellow, TextAnchor.MiddleLeft);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.anchoredPosition = new Vector2(10f, -6f);
            header.rectTransform.sizeDelta = new Vector2(-20f, 22f);

            // Quick action row right below leaderboard
            GameObject bar = new GameObject("QuickButtons");
            bar.transform.SetParent(_hudRoot.transform, false);
            RectTransform barRt = bar.AddComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 1f);
            barRt.anchorMax = new Vector2(0f, 1f);
            barRt.pivot = new Vector2(0f, 1f);
            barRt.anchoredPosition = new Vector2(16f, -184f);
            barRt.sizeDelta = new Vector2(300f, 30f);

            Button menuBtn = RoadRashUiSprites.CreateBevelButton(bar.transform, "MenuBtn", "Pause [Esc]", RoadRashVisualTheme.MetalMid, 12);
            SetFixedRect(menuBtn.GetComponent<RectTransform>(), 0f, 0f, 92f, 28f);
            menuBtn.onClick.AddListener(() => _gameManager?.TogglePause());

            Button restartBtn = RoadRashUiSprites.CreateBevelButton(bar.transform, "RestartBtn", "Đua lại [R]", RoadRashVisualTheme.MetalMid, 12);
            SetFixedRect(restartBtn.GetComponent<RectTransform>(), 98f, 0f, 92f, 28f);
            restartBtn.onClick.AddListener(() => _gameManager?.StartRace());

            Button muteBtn = RoadRashUiSprites.CreateBevelButton(bar.transform, "MuteBtn", "Âm: BẬT [M]", RoadRashVisualTheme.MetalMid, 12);
            SetFixedRect(muteBtn.GetComponent<RectTransform>(), 196f, 0f, 102f, 28f);
            _muteBtnLabel = muteBtn.GetComponentInChildren<Text>();
            muteBtn.onClick.AddListener(() => _gameManager?.AudioController?.ToggleMute());
        }

        private void EnsureRankingRows(int count)
        {
            while (_rankingRows.Count < count)
            {
                int idx = _rankingRows.Count;
                Text row = RoadRashUiSprites.CreateStyledText(
                    _rankingBoxRect, $"RankRow_{idx}", "", 13, Color.white, TextAnchor.MiddleLeft);
                row.rectTransform.anchorMin = new Vector2(0f, 1f);
                row.rectTransform.anchorMax = new Vector2(1f, 1f);
                row.rectTransform.pivot = new Vector2(0.5f, 1f);
                row.rectTransform.anchoredPosition = new Vector2(10f, -28f - idx * 22f);
                row.rectTransform.sizeDelta = new Vector2(-20f, 20f);
                _rankingRows.Add(row);
            }
        }

        private void BuildTopRightMinimap()
        {
            GameObject mapObj = new GameObject("Minimap");
            mapObj.transform.SetParent(_hudRoot.transform, false);
            _minimapImg = mapObj.AddComponent<RawImage>();
            _minimapImg.raycastTarget = false;

            _minimapRect = _minimapImg.rectTransform;
            _minimapRect.anchorMin = new Vector2(1f, 1f);
            _minimapRect.anchorMax = new Vector2(1f, 1f);
            _minimapRect.pivot = new Vector2(1f, 1f);
            _minimapRect.anchoredPosition = new Vector2(-16f, -16f);
            _minimapRect.sizeDelta = new Vector2(MinimapSizePx, MinimapSizePx);
        }

        private void BuildCenterOverlays()
        {
            _countdownText = RoadRashUiSprites.CreateStyledText(
                _hudRoot.transform, "CountdownNumber", "3", 84, RoadRashVisualTheme.HudYellow, TextAnchor.MiddleCenter);
            _countdownText.rectTransform.anchorMin = new Vector2(0.5f, 0.62f);
            _countdownText.rectTransform.anchorMax = new Vector2(0.5f, 0.62f);
            _countdownText.rectTransform.sizeDelta = new Vector2(240f, 100f);

            _countdownSubText = RoadRashUiSprites.CreateStyledText(
                _hudRoot.transform, "CountdownSub", "", 20, Color.white, TextAnchor.MiddleCenter);
            _countdownSubText.rectTransform.anchorMin = new Vector2(0.5f, 0.50f);
            _countdownSubText.rectTransform.anchorMax = new Vector2(0.5f, 0.50f);
            _countdownSubText.rectTransform.sizeDelta = new Vector2(640f, 36f);

            _wrongWayText = RoadRashUiSprites.CreateStyledText(
                _hudRoot.transform, "WrongWay", "NGƯỢC CHIỀU!", 36, RoadRashVisualTheme.DangerRed, TextAnchor.MiddleCenter);
            _wrongWayText.rectTransform.anchorMin = new Vector2(0.5f, 0.68f);
            _wrongWayText.rectTransform.anchorMax = new Vector2(0.5f, 0.68f);
            _wrongWayText.rectTransform.sizeDelta = new Vector2(460f, 52f);
            _wrongWayText.gameObject.SetActive(false);

            _flashBannerText = RoadRashUiSprites.CreateStyledText(
                _hudRoot.transform, "FlashBanner", "", 32, RoadRashVisualTheme.HudYellow, TextAnchor.MiddleCenter);
            _flashBannerText.rectTransform.anchorMin = new Vector2(0.5f, 0.77f);
            _flashBannerText.rectTransform.anchorMax = new Vector2(0.5f, 0.77f);
            _flashBannerText.rectTransform.sizeDelta = new Vector2(680f, 50f);
            _flashBannerText.gameObject.SetActive(false);
        }

        private void BuildResultsPanel()
        {
            _resultsPanel = new GameObject("ResultsPanel");
            _resultsPanel.transform.SetParent(_hudRoot.transform, false);
            Image bg = _resultsPanel.AddComponent<Image>();
            bg.sprite = RoadRashUiSprites.GetPanelSprite();
            bg.type = Image.Type.Sliced;
            bg.color = RoadRashVisualTheme.PanelBackdrop;

            RectTransform rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(540f, 350f);

            Text title = RoadRashUiSprites.CreateStyledText(
                _resultsPanel.transform, "ResultsTitle", "KẾT QUẢ CUỘC ĐUA", 28, RoadRashVisualTheme.HudYellow, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            title.rectTransform.sizeDelta = new Vector2(480f, 40f);

            _prizeText = RoadRashUiSprites.CreateStyledText(
                _resultsPanel.transform, "PrizeText", "", 18, RoadRashVisualTheme.AccentGreen, TextAnchor.MiddleCenter);
            _prizeText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _prizeText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _prizeText.rectTransform.pivot = new Vector2(0.5f, 0f);
            _prizeText.rectTransform.anchoredPosition = new Vector2(0f, 72f);
            _prizeText.rectTransform.sizeDelta = new Vector2(500f, 30f);

            Button raceBtn = RoadRashUiSprites.CreateBevelButton(_resultsPanel.transform, "RetryBtn", "Đua lại [R]", new Color(0.24f, 0.58f, 0.28f), 15);
            SetAnchoredCenterBottom(raceBtn.GetComponent<RectTransform>(), -158f, 18f, 146f, 38f);
            raceBtn.onClick.AddListener(() => _gameManager?.StartRace());

            Button garageBtn = RoadRashUiSprites.CreateBevelButton(_resultsPanel.transform, "GarageBtn", "Nâng cấp xe", new Color(0.24f, 0.44f, 0.72f), 15);
            SetAnchoredCenterBottom(garageBtn.GetComponent<RectTransform>(), 0f, 18f, 146f, 38f);
            garageBtn.onClick.AddListener(() => _gameManager?.OpenGarage());

            Button menuBtn = RoadRashUiSprites.CreateBevelButton(_resultsPanel.transform, "ResultMenuBtn", "Về Menu [Esc]", RoadRashVisualTheme.MetalMid, 15);
            SetAnchoredCenterBottom(menuBtn.GetComponent<RectTransform>(), 158f, 18f, 146f, 38f);
            menuBtn.onClick.AddListener(() => _gameManager?.OpenMenu());

            _resultsPanel.SetActive(false);
        }

        private void EnsureResultRows(int count)
        {
            while (_resultRows.Count < count)
            {
                int idx = _resultRows.Count;
                Text row = RoadRashUiSprites.CreateStyledText(
                    _resultsPanel.transform, $"ResRow_{idx}", "", 16, Color.white, TextAnchor.MiddleLeft);
                row.rectTransform.anchorMin = new Vector2(0.5f, 1f);
                row.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                row.rectTransform.pivot = new Vector2(0.5f, 1f);
                row.rectTransform.anchoredPosition = new Vector2(0f, -68f - idx * 28f);
                row.rectTransform.sizeDelta = new Vector2(480f, 26f);
                _resultRows.Add(row);
            }
        }

        private static void SetFixedRect(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
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
