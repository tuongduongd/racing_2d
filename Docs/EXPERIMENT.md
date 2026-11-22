# Experiment: Benchmark AI 3 vòng theo từng Map & mức Skill

## Điều kiện thí nghiệm chung
- **Thông số xe:** `VehicleStats.Default` (`maxSpeed = 48.0 m/s`, `enginePower = 4800`, `mass = 220`).
- **Số vòng đua mỗi lượt test:** `3` vòng liên tiếp, `dt = 0.0200 s` (`50 Hz`), `seed = 2026`.
- **Tiêu chí đạt:** `STUCK = 0`, qua toàn bộ checkpoint đúng thứ tự (`1 -> ... -> N-1 -> 0` lặp 3 vòng), hoàn thành đủ 3 vòng.
- **Bài test thực thi:** `MapAIBenchmark3LapTests` (EditMode).

## Map: Valley (Thung Lũng Cổ Điển)
- **Đường đua:** `22` waypoints, chiều dài 1 vòng `1529.88 m`, `6` checkpoint/vòng (tổng `18` checkpoint / 3 vòng).

| Skill | Vòng 1 (s) | Vòng 2 (s) | Vòng 3 (s) | Thời gian 1 vòng TB (s) | Vòng nhanh nhất (s) | Tổng 3 vòng (s) | Tốc độ TB (m/s) | Tốc độ đỉnh (m/s) | STUCK | Checkpoint đúng thứ tự |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| 0.4 | 46.82 | 44.26 | 44.24 | 45.10 | 44.24 | 135.32 | 33.92 | 40.80 | 0 | OK (18/18) |
| 0.7 | 40.96 | 39.40 | 39.43 | 39.93 | 39.40 | 119.80 | 38.31 | 44.09 | 0 | OK (18/18) |
| 1.0 | 35.96 | 34.72 | 34.71 | 35.13 | 34.71 | 105.40 | 43.55 | 47.36 | 0 | OK (18/18) |

- **Nhận xét (Valley):** Skill `0.7` nhanh hơn `0.4` trung bình **5.17 s/vòng**; Skill `1.0` nhanh hơn `0.7` trung bình **4.80 s/vòng**.

## Map: City (Thành Phố Đêm Neon)
- **Đường đua:** `26` waypoints, chiều dài 1 vòng `1836.04 m`, `6` checkpoint/vòng (tổng `18` checkpoint / 3 vòng).

| Skill | Vòng 1 (s) | Vòng 2 (s) | Vòng 3 (s) | Thời gian 1 vòng TB (s) | Vòng nhanh nhất (s) | Tổng 3 vòng (s) | Tốc độ TB (m/s) | Tốc độ đỉnh (m/s) | STUCK | Checkpoint đúng thứ tự |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| 0.4 | 56.04 | 53.39 | 53.38 | 54.27 | 53.38 | 162.82 | 33.83 | 40.80 | 0 | OK (18/18) |
| 0.7 | 49.42 | 47.65 | 47.76 | 48.28 | 47.65 | 144.84 | 38.03 | 44.09 | 0 | OK (18/18) |
| 1.0 | 45.54 | 44.36 | 44.35 | 44.75 | 44.35 | 134.26 | 41.03 | 47.36 | 0 | OK (18/18) |

- **Nhận xét (City):** Skill `0.7` nhanh hơn `0.4` trung bình **5.99 s/vòng**; Skill `1.0` nhanh hơn `0.7` trung bình **3.53 s/vòng**.
