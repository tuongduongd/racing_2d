using System;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Quản lý hệ thống Cảnh sát truy đuổi người chơi trong cuộc đua:
    /// Khởi tạo xe cảnh sát (PoliceCar + PoliceRacerDriver), điều khiển đèn còi xanh-đỏ,
    /// tính khoảng cách áp sát người chơi và kích hoạt cảnh báo truy đuổi trên HUD.
    /// </summary>
    public class PoliceChaseController : MonoBehaviour
    {
        public static readonly Color PoliceBikeBlue = new Color(0.10f, 0.28f, 0.72f);
        public static readonly Color SirenRedColor = new Color(1.00f, 0.16f, 0.16f);
        public static readonly Color SirenBlueColor = new Color(0.16f, 0.58f, 1.00f);

        [Header("Cấu hình Cảnh Sát Truy Đuổi")]
        [SerializeField] private bool enablePoliceChase = true;
        [SerializeField] private float spawnBehindPlayerMeters = 24f;
        [SerializeField] private float warningRangeMeters = 42f;
        [SerializeField] private float policeSpeedBonus = 4.5f;
        [SerializeField] private float sirenFlashFrequency = 9f;

        [Header("Tham chiếu tự động hoặc gán tay")]
        [SerializeField] private Car policeCar;
        [SerializeField] private PoliceRacerDriver policeDriver;

        private Car _playerCar;
        private WaypointTrack _track;
        private float _sirenTimer;
        private float _distanceToPlayer = 999f;
        private bool _isPursuing;

        public event Action<Car> PoliceEngaged;

        public bool EnablePoliceChase
        {
            get => enablePoliceChase;
            set => enablePoliceChase = value;
        }

        public Car PoliceCar => policeCar;
        public PoliceRacerDriver PoliceDriver => policeDriver;
        public bool IsPursuing => enablePoliceChase && _isPursuing && policeCar != null;
        public float DistanceToPlayer => _distanceToPlayer;
        public bool IsPlayerInWarningRange => IsPursuing && _distanceToPlayer <= warningRangeMeters;
        public bool IsSirenRedPhase => Mathf.FloorToInt(_sirenTimer * sirenFlashFrequency) % 2 == 0;
        public Color CurrentSirenColor => IsSirenRedPhase ? SirenRedColor : SirenBlueColor;

        public void ApplyMapSettings(TrackDefinition def)
        {
            if (def == null) return;
            enablePoliceChase = def.EnablePoliceChase;
            policeSpeedBonus = def.PoliceSpeedBonus;
            warningRangeMeters = def.PoliceWarningRangeMeters;
        }

        public Car EnsurePoliceUnit(Car playerCar, WaypointTrack track, VehicleStatsAsset defaultStats = null)
        {
            _playerCar = playerCar;
            _track = track;

            if (!enablePoliceChase) return null;

            if (policeCar == null)
            {
                PoliceRacerDriver existingDriver = FindAnyObjectByType<PoliceRacerDriver>();
                if (existingDriver != null)
                {
                    policeDriver = existingDriver;
                    policeCar = existingDriver.GetComponent<Car>();
                }
            }

            if (policeCar == null)
            {
                GameObject policeObj = new GameObject("PoliceCar_Unit01");
                policeCar = policeObj.AddComponent<Car>();
                policeDriver = policeObj.AddComponent<PoliceRacerDriver>();
            }
            else if (policeDriver == null)
            {
                policeDriver = policeCar.GetComponent<PoliceRacerDriver>();
                if (policeDriver == null) policeDriver = policeCar.gameObject.AddComponent<PoliceRacerDriver>();
            }

            if (defaultStats != null)
            {
                policeCar.SetBaseStatsAsset(defaultStats);
            }

            // Tăng nhẹ tốc độ tối đa và gia tốc cho xe đặc chủng cảnh sát
            VehicleStats policeStats = policeCar.BaseStats;
            policeStats.maxSpeed += policeSpeedBonus;
            policeStats.enginePower += 450f;
            policeStats.grip += 0.8f;
            policeCar.Physics.ConfigureStats(policeStats);

            policeCar.ConfigureIdentity(false, PoliceBikeBlue);
            policeDriver.ConfigurePolice(playerCar, "CẢNH SÁT");
            return policeCar;
        }

        public void ResetPoliceOnGrid(Car playerCar, WaypointTrack track)
        {
            _playerCar = playerCar;
            _track = track;
            _sirenTimer = 0f;

            if (!enablePoliceChase || policeCar == null || track == null || track.WaypointCount < 2)
            {
                _isPursuing = false;
                return;
            }

            policeDriver.ConfigurePolice(playerCar, "CẢNH SÁT");

            float playerDist = playerCar != null ? playerCar.TrackDistance : 0f;
            float spawnDist = track.WrapDistance(playerDist - spawnBehindPlayerMeters);
            TrackSample sample = track.SampleAtDistance(spawnDist);
            float heading = Mathf.Atan2(sample.tangent.y, sample.tangent.x);

            policeCar.Initialize(policeDriver, sample.position, heading, track);
            _isPursuing = true;
            UpdatePursuitMetrics(0f);
            PoliceEngaged?.Invoke(policeCar);
        }

        public void TickPursuit(float dt)
        {
            if (!IsPursuing) return;
            _sirenTimer += dt;
            UpdatePursuitMetrics(dt);
        }

        private void UpdatePursuitMetrics(float dt)
        {
            if (_playerCar == null || policeCar == null || _track == null || _track.TotalLength <= 1f)
            {
                _distanceToPlayer = 999f;
                return;
            }

            float signedDelta = PoliceRacerDriver.SignedTrackDelta(
                policeCar.TrackDistance,
                _playerCar.TrackDistance,
                _track.TotalLength);
            _distanceToPlayer = Mathf.Abs(signedDelta);
        }
    }
}
