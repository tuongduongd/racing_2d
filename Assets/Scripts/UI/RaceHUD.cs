using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Draws the race HUD: speed, lap, position, race/lap times, live ranking board,
    /// wrong-way warning and the final results panel.
    /// Only reads data (no game logic) — Single Responsibility.
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        [SerializeField] private float lapMessageDuration = 2.5f;

        private Car _player;
        private LapCheckpointManager _lapManager;
        private WaypointTrack _track;

        private string _flashMessage;
        private float _flashUntil;

        private GUIStyle _bigStyle;
        private GUIStyle _infoStyle;
        private GUIStyle _warnStyle;
        private GUIStyle _centerStyle;
        private GUIStyle _rowStyle;
        private GUIStyle _warnCenterStyle;
        private GUIStyle _infoCenterStyle;
        public void Bind(Car player, LapCheckpointManager lapManager, WaypointTrack track)
        {
            Unsubscribe();
            _player = player;
            _lapManager = lapManager;
            _track = track;
            if (_lapManager != null)
            {
                _lapManager.LapCompleted += HandleLapCompleted;
                _lapManager.RacerFinished += HandleRacerFinished;
            }
            ClearMessage();
        }

        public void ClearMessage()
        {
            _flashMessage = null;
            _flashUntil = 0f;
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
        }

        private void HandleLapCompleted(RacerProgress progress)
        {
            if (progress.Car != _player || progress.IsFinished) return;
            ShowMessage($"VÒNG {progress.CompletedLaps}/{_lapManager.TotalLaps}  -  {FormatTime(progress.LastLapTime)}");
        }

        private void HandleRacerFinished(RacerProgress progress)
        {
            if (progress.Car != _player) return;
            ShowMessage($"VỀ ĐÍCH! HẠNG {progress.FinishPosition}");
        }

        private void ShowMessage(string msg)
        {
            _flashMessage = msg;
            _flashUntil = Time.time + lapMessageDuration;
        }

        private void OnGUI()
        {
            if (_player == null) return;
            EnsureStyles();

            RacerProgress playerProgress = _lapManager != null ? _lapManager.GetProgress(_player) : null;

            DrawMainPanel(playerProgress);
            DrawRankingBoard();
            DrawCenterMessages(playerProgress);

            if (playerProgress != null && playerProgress.IsFinished)
            {
                DrawResultsPanel();
            }
        }

        private void DrawMainPanel(RacerProgress progress)
        {
            GUI.Box(new Rect(14, 14, 320, 168), GUIContent.none);

            int kmh = Mathf.RoundToInt(Mathf.Abs(_player.Physics.SpeedKmh));
            GUI.Label(new Rect(26, 18, 300, 40), $"{kmh} km/h", _bigStyle);

            if (_lapManager != null && progress != null)
            {
                int totalLaps = _lapManager.TotalLaps;
                int currentLap = Mathf.Min(progress.CompletedLaps + 1, totalLaps);
                int pos = _lapManager.GetPosition(_player);

                GUI.Label(new Rect(26, 58, 300, 24),
                    $"Vòng: {currentLap}/{totalLaps}     Hạng: {pos}/{_lapManager.RacerCount}", _infoStyle);
                GUI.Label(new Rect(26, 80, 300, 24),
                    $"Thời gian: {FormatTime(_lapManager.RaceTime)}", _infoStyle);
                string best = progress.HasBestLap ? FormatTime(progress.BestLapTime) : "--:--.--";
                GUI.Label(new Rect(26, 102, 300, 24),
                    $"Checkpoint kế: {progress.NextCheckpointIndex}   Best lap: {best}", _infoStyle);
            }

            string trackState = _player.IsOnTrack ? "ON TRACK" : "OFF-ROAD (SLOW)";
            GUI.Label(new Rect(26, 126, 300, 24), trackState, _player.IsOnTrack ? _infoStyle : _warnStyle);

            GUI.Label(new Rect(26, 152, 310, 22), "W/↑ Ga | S/↓ Phanh | A/D Lái | R Đua lại", _infoStyle);
        }

        private void DrawRankingBoard()
        {
            if (_lapManager == null) return;
            IReadOnlyList<RacerProgress> ranking = _lapManager.GetRanking();
            float rowH = 22f;
            float height = 30f + ranking.Count * rowH;
            GUI.Box(new Rect(14, 192, 320, height), GUIContent.none);
            GUI.Label(new Rect(26, 196, 300, 24), "BẢNG XẾP HẠNG", _infoStyle);

            for (int i = 0; i < ranking.Count; i++)
            {
                RacerProgress r = ranking[i];
                bool isPlayer = r.Car == _player;
                _rowStyle.normal.textColor = isPlayer ? Color.yellow : (r.Car != null ? r.Car.BikeColor : Color.white);

                string status = r.IsFinished
                    ? FormatTime(r.FinishTime)
                    : $"Vòng {Mathf.Min(r.CompletedLaps + 1, _lapManager.TotalLaps)}/{_lapManager.TotalLaps}";
                GUI.Label(new Rect(26, 220 + i * rowH, 300, rowH),
                    $"{i + 1}. {r.RacerName,-10} {status}", _rowStyle);
            }
        }

        private void DrawCenterMessages(RacerProgress progress)
        {
            float w = Screen.width;
            if (progress != null && progress.IsWrongWay)
            {
                GUI.Label(new Rect(0, Screen.height * 0.30f, w, 50), "NGƯỢC CHIỀU!", _warnCenterStyle);
            }

            if (!string.IsNullOrEmpty(_flashMessage) && Time.time < _flashUntil)
            {
                GUI.Label(new Rect(0, Screen.height * 0.20f, w, 50), _flashMessage, _centerStyle);
            }
        }

        private void DrawResultsPanel()
        {
            IReadOnlyList<RacerProgress> ranking = _lapManager.GetRanking();
            float pw = 440f;
            float ph = 90f + ranking.Count * 26f;
            Rect panel = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);
            GUI.Box(panel, GUIContent.none);
            GUI.Box(panel, GUIContent.none);

            GUI.Label(new Rect(panel.x, panel.y + 8, pw, 36), "KẾT QUẢ CUỘC ĐUA", _centerStyle);

            for (int i = 0; i < ranking.Count; i++)
            {
                RacerProgress r = ranking[i];
                _rowStyle.normal.textColor = r.Car == _player ? Color.yellow : Color.white;
                string time = r.IsFinished ? FormatTime(r.FinishTime) : "đang chạy...";
                string best = r.HasBestLap ? FormatTime(r.BestLapTime) : "--";
                GUI.Label(new Rect(panel.x + 24, panel.y + 50 + i * 26, pw - 48, 26),
                    $"{i + 1}. {r.RacerName,-10}  {time,-12}  best {best}", _rowStyle);
            }

            GUI.Label(new Rect(panel.x, panel.yMax - 32, pw, 28), "Nhấn R để đua lại", _infoCenterStyle);
        }

        private void EnsureStyles()
        {
            if (_bigStyle != null) return;

            _bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            _bigStyle.normal.textColor = Color.white;

            _infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _infoStyle.normal.textColor = new Color(0.92f, 0.94f, 0.98f);

            _warnStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _warnStyle.normal.textColor = new Color(1f, 0.35f, 0.25f);

            _centerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _centerStyle.normal.textColor = new Color(1f, 0.92f, 0.3f);

            _rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _rowStyle.normal.textColor = Color.white;

            _warnCenterStyle = new GUIStyle(_centerStyle);
            _warnCenterStyle.normal.textColor = new Color(1f, 0.25f, 0.2f);

            _infoCenterStyle = new GUIStyle(_infoStyle) { alignment = TextAnchor.MiddleCenter };
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
