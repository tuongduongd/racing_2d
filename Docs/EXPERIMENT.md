# Experiment: Benchmark thời gian 1 vòng của AIRacerDriver theo mức Skill

## Điều kiện thí nghiệm
- **Đường đua:** `WaypointTrack.BuildDefaultCircuit()` (22 waypoints, chiều dài `1529.88 m`, `6` checkpoint).
- **Thông số xe:** `VehicleStats.Default` (`maxSpeed = 62.0 m/s`, `enginePower = 6200`, `mass = 220`).
- **Bước mô phỏng:** `dt = 0.0200 s` (`50 Hz`), cấu hình `AIRacerDriver.Configure(..., seed: 2026)`.
- **Bài test thực thi:** `AIRacerDeterminismAndBenchmarkTests.AIRacerDriver_Benchmark_SingleLapTimeAcrossSkills` (EditMode).

## Kết quả đo 1 vòng

| Skill | Seed | Thời gian 1 vòng (s) | Số bước (50 Hz) | Tốc độ TB (m/s) | Tốc độ TB (km/h) | Tốc độ đỉnh (m/s) | Số bước ngoài đường |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| 0.4 | 2026 | 39.02 | 1951 | 39.21 | 141.15 | 52.67 | 0 |
| 0.7 | 2026 | 33.68 | 1684 | 45.42 | 163.52 | 56.93 | 0 |
| 1.0 | 2026 | 30.34 | 1517 | 50.42 | 181.53 | 61.15 | 0 |

## Nhận xét
- AI skill `0.7` nhanh hơn skill `0.4` là **5.34 s** (`13.7%`).
- AI skill `1.0` nhanh hơn skill `0.7` là **3.34 s** (`9.9%`).
- Toàn bộ mô phỏng sử dụng seed cố định và đồng hồ mô phỏng nội bộ (`_simulationTime`), đảm bảo chạy lại cho kết quả giống hệt nhau.
