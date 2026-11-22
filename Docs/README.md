# Hướng Dẫn Cài Đặt & Chạy Dự Án — Road Rash 2D (Unity)

Tài liệu hướng dẫn thiết lập môi trường, cấu hình scene, chạy game trong Unity Editor và thực thi bộ kiểm thử tự động (EditMode Tests) cho dự án **Road Rash 2D: Đại Chiến Shipper**.

---

## 1. Yêu cầu môi trường
- **Unity Editor:** `6000.6.9f1` (Unity 6 — xem [ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt))
- **Hệ điều hành:** Windows 10/11 (khuyến nghị) hoặc macOS/Linux có hỗ trợ Unity 6
- **Công cụ bổ sung:** .NET / C# SDK đi kèm Unity 6, Git
- **Các package chính** (đã khai báo trong [manifest.json](../Packages/manifest.json)):
  - `com.unity.inputsystem` (`1.20.1`)
  - `com.unity.render-pipelines.universal` (`17.6.0` — URP 2D/Post-Processing)
  - `com.unity.ugui` (`2.6.0`)
  - `com.unity.test-framework` (`1.8.0`)

---

## 2. Cấu trúc thư mục dự án

```text
newbie/
├── Assets/
│   ├── Scenes/
│   │   ├── LobbyScene.unity          # Scene Sảnh chờ khởi động (Index 0)
│   │   └── SampleScene.unity         # Scene Đua xe chính (Index 1)
│   ├── ScriptableObjects/
│   │   ├── DefaultVehicleStats.asset # Thông số xe gốc (VehicleStatsAsset)
│   │   ├── AI/                       # AIProfile_Easy / Normal / Hard
│   │   ├── Maps/                     # TrackDefinition & MapTheme cho các map
│   │   └── Skins/                    # 4 BikeSkinData (XanhSM, ShopeeFood, Grab, Ahamove)
│   ├── Scripts/
│   │   ├── AI/                       # AIProfile, AIRacerDriver, PoliceChaseController, PoliceRacerDriver
│   │   ├── Audio/                    # RaceAudioController (âm thanh tổng hợp procedural)
│   │   ├── Core/                     # Driver, GameManager, GameState (5 state OOP)
│   │   ├── Editor/                   # RoadRashSceneSetup (tự động tạo asset & scene)
│   │   ├── Input/                    # InputHandler (Unity Input System)
│   │   ├── Maps/                     # MapManager, MapTheme, TrackDefinition
│   │   ├── Physics/                  # VehiclePhysics, VehicleStats, VehicleStatsAsset
│   │   ├── Race/                     # Checkpoint, LapCheckpointManager, RacerProgress
│   │   ├── Rendering/                # RoadRashRenderer2D, RaceVfxController, Sprite generators
│   │   ├── Skins/                    # BikeSkinData, SkinManager
│   │   ├── Track/                    # WaypointTrack, EnvironmentDecorator, TrackObstacleSystem
│   │   ├── UI/                       # GameMenuUI, PauseMenuUI, RaceHUD, RoadRashUiSprites
│   │   ├── Upgrades/                 # CarUpgradeSystem, UpgradeType
│   │   └── Vehicle/                  # Car, PlayerDriver
│   └── Tests/
│       └── EditMode/                 # 14 file unit test EditMode (NUnit)
├── Docs/                             # Tài liệu kiến trúc, README, Test Cases, Bug Log, Experiment
├── Packages/manifest.json
└── ProjectSettings/
```

---

## 3. Hướng dẫn mở & chạy game trong Unity Editor

### Bước 1: Mở dự án
1. Mở **Unity Hub**, chọn **Add -> Add project from disk** và trỏ tới thư mục gốc của repository (`newbie/`).
2. Chọn đúng phiên bản Editor **Unity `6000.6.9f1`** và mở dự án.
3. Chờ Unity hoàn tất import asset và biên dịch script (`RacingGame`, `RacingGame.Editor`, `RacingGame.Tests`).

### Bước 2: Khởi tạo / Đồng bộ Scene và ScriptableObject (nếu cần)
Dự án đã có sẵn scene và ScriptableObject trong `Assets/Scenes/` và `Assets/ScriptableObjects/`. Nếu muốn tái tạo hoặc kiểm tra cấu hình chuẩn từ menu Editor, chọn một trong các lệnh trong thanh menu Unity:
- `RacingGame > Setup All Scenes & Skin Assets`: Tạo/kiểm tra toàn bộ ScriptableObject mặc định, cấu hình cả `LobbyScene` và `SampleScene`, đồng thời đăng ký thứ tự Build Settings (`LobbyScene` index `0`, `SampleScene` index `1`).
- `RacingGame > Setup Startup Lobby Scene`: Chỉ dựng lại `Assets/Scenes/LobbyScene.unity`.
- `RacingGame > Setup Road Rash 2D Scene`: Chỉ dựng lại `Assets/Scenes/SampleScene.unity`.
- `RacingGame > Create Default Data Assets`: Chỉ tạo các `ScriptableObject` mặc định (`DefaultVehicleStats`, `AIProfile_*`, `Skin_*`, `Map_*`, `Theme_*`).

### Bước 3: Chạy game
1. **Chạy từ Sảnh chờ khởi động (`LobbyScene`):**
   - Mở `Assets/Scenes/LobbyScene.unity` trong cửa sổ **Project**.
   - Nhấn nút **Play (▶)** trên thanh công cụ Unity Editor.
   - Tại sảnh chờ, chọn **Skin** (`Xanh SM`, `Shopee Food`, `Grab`, `Ahamove`), chọn **Đường đua (Map)**, độ khó AI, số vòng đua, rồi bấm **PLAY (BẮT ĐẦU ĐUA)** để nạp sang `SampleScene` và bắt đầu đếm ngược `3..2..1..GO`.
2. **Hoặc chạy trực tiếp trong `SampleScene`:**
   - Mở `Assets/Scenes/SampleScene.unity` và nhấn **Play (▶)**.
   - Tại đây có thể mở **NÂNG CẤP XE (GARAGE)**, điều chỉnh độ khó (`Dễ`, `Vừa`, `Khó`), chọn số vòng (`1V`, `2V`, `3V`, `5V`), chọn Skin/Map hoặc bấm **PLAY** để vào cuộc đua ngay.

---

## 4. Điều khiển trong game

| Phím / Nút bấm | Chức năng | Ghi chú |
| :--- | :--- | :--- |
| `W` hoặc `↑` | Tăng ga | Áp dụng lực kéo động cơ (`ApplyThrottle`) |
| `S` hoặc `↓` | Phanh / Lùi | Giảm tốc bằng phanh hoặc lùi nhẹ khi dừng (`ApplyBrake`) |
| `A` / `←` và `D` / `→` | Lái trái / phải | Có làm mượt góc lái (`steerResponseSpeed`) |
| `Space` | Đá đối thủ (Kick) | Tự chọn hướng đối thủ gần nhất hoặc theo hướng lái; có cooldown |
| `Esc` hoặc `P` | Tạm dừng / Tiếp tục | Mở/đóng `PauseMenuUI` trong lúc `Countdown` hoặc `Racing` |
| `R` | Đua lại nhanh | Khởi động lại ván đua (khi đang `Racing` không Pause hoặc `Finished`) |
| `M` | Bật / Tắt âm thanh | Mute/Unmute `RaceAudioController` |

---

## 5. Lưu trữ dữ liệu & Reset tiến trình
- **File lưu tiến trình nâng cấp & mở khóa map (JSON):** `Application.persistentDataPath/racing2d_save.json` (quản lý bởi `CarUpgradeSystem` với `PlayerSaveData`).
  - Trên Windows, đường dẫn mặc định thường nằm tại: `%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\racing2d_save.json`.
- **Tùy chọn lưu nhanh (`PlayerPrefs`):**
  - Skin đang chọn: key `RacingGame_SelectedSkin_v1` (`SkinManager`).
  - Map đang chọn: key `RacingGame_SelectedMap_v1` (`MapManager`).
  - Kỷ lục thời gian từng map: key `RacingGame_BestTime_<mapId>` (`MapManager`).
- Để xóa dữ liệu lưu khi chơi thử, nhấn nút **Xóa dữ liệu lưu** 2 lần liên tiếp trong Menu sảnh chờ (`GameMenuUI`).

---

## 6. Hướng dẫn chạy kiểm thử (EditMode Unit Tests)

### Cách 1: Chạy trực tiếp trong Unity Editor (GUI)
1. Mở **Window > General > Test Runner**.
2. Chọn tab **EditMode**.
3. Nhấn **Run All** để chạy toàn bộ test suite trong `Assets/Tests/EditMode/`.

### Cách 2: Chạy bằng dòng lệnh (Batchmode CLI)
Đảm bảo không có cửa sổ Unity Editor nào đang mở cùng project, sau đó chạy lệnh PowerShell từ thư mục gốc dự án:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "e:\UnityCodeGame\newbie" `
  -runTests -testPlatform EditMode `
  -testResults "e:\UnityCodeGame\newbie\Logs\EditModeResults.xml" `
  -logFile "e:\UnityCodeGame\newbie\Logs\editmode_test.log"
```

Sau khi chạy xong:
- Mã thoát `0` nghĩa là toàn bộ test đều `Passed`.
- Chi tiết từng test case được xuất ra `Logs/EditModeResults.xml` (định dạng NUnit XML).
- Lưu ý: Khi chạy `MapAIBenchmark3LapTests` hoặc `AIRacerDeterminismAndBenchmarkTests`, test benchmark sẽ tự động cập nhật kết quả đo vào [Docs/EXPERIMENT.md](EXPERIMENT.md).

---

## 7. Tài liệu liên quan
- [BAO_CAO_KIEN_TRUC_OOP.md](BAO_CAO_KIEN_TRUC_OOP.md) — Báo cáo kiến trúc OOP, Use Case, yêu cầu, sơ đồ UML/Mermaid, cài đặt, kiểm thử và kết luận.
- [TEST_CASES.md](TEST_CASES.md) — Danh sách đầy đủ các test case EditMode.
- [BUG_LOG.md](BUG_LOG.md) — Nhật ký kiểm thử, lỗi đã xử lý và cảnh báo biên dịch.
- [EXPERIMENT.md](EXPERIMENT.md) — Kết quả benchmark AI 3 vòng theo map và mức skill.
