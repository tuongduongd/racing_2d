# Road Rash 2D: Đại Chiến Shipper (`newbie`)

Game đua xe mô tô 2D góc nhìn từ sau xe (Pseudo-3D kiểu *Road Rash*) xây dựng trên **Unity 6 (`6000.6.9f1`)**, áp dụng kiến trúc phân tầng **3 tầng (Pure C# Core ← Unity Adapters ← View/Input/Audio)**, **6 Design Patterns GoF thuộc đủ 3 nhóm** (Creational, Structural, Behavioral), dữ liệu hóa bản đồ & thông số cân bằng bằng **JSON ngoài (`StreamingAssets`)**, lưu/nạp trạng thái giữa trận đua và **114 EditMode Unit & Integration Tests PASS 100%**.

---

## 1. Chạy nhanh Bản phát hành Clean-Room (≤ 1 phút)
1. Mở thư mục `Builds/RoadRash2D/` và chạy trực tiếp tệp **`Builds/RoadRash2D/RoadRash2D.exe`** (không cần cài đặt thêm công cụ).
2. Tại **Sảnh chờ (`LobbyScene`)**:
   - Chọn thương hiệu Shipper (**Xanh SM**, **Shopee Food**, **Grab**, **Ahamove**).
   - Chọn **1 trong 4 bản đồ JSON** (*Phố Sáng Ban Mai*, *Đại Lộ Ven Biển*, *Cao Tốc Đêm Neon*, *Thành Phố Đêm Neon*).
   - Tùy chỉnh độ khó AI (`Dễ / Vừa / Khó`), số vòng (`1V / 2V / 3V / 5V`), hiệu ứng CRT Scanline và vào **Garage nâng cấp**.
   - Bấm **`CHƠI NGAY (PLAY)`** để vào đường đua, hoặc **`TIẾP TỤC VÁN`** nếu trước đó đã lưu trạng thái giữa chặng.
3. Có thể chỉnh trực tiếp thông số bản đồ hoặc cân bằng trong `Builds/RoadRash2D/RoadRash2D_Data/StreamingAssets/Maps/*.json` và `Config/game_balance.json` rồi mở lại game — thay đổi áp dụng ngay không cần biên dịch lại.

---

## 2. Điều khiển trong Game

| Phím / Nút | Chức năng | Lớp xử lý |
| :--- | :--- | :--- |
| `W` hoặc `↑` | Tăng ga | `InputHandler` (`IInputProvider`) → `PlayerDriver` |
| `S` hoặc `↓` | Phanh / Lùi nhẹ | `InputHandler` (`IInputProvider`) → `PlayerDriver` |
| `A` / `←` và `D` / `→` | Lái trái / phải mượt | `InputHandler` (`IInputProvider`) → `VehiclePhysics` |
| `Space` | Đá đối thủ (*Road Rash Kick*) | `InputHandler` → `RaceCombatSystem` |
| `Esc` hoặc `P` | Tạm dừng / Tiếp tục / Đóng modal | `InputHandler` → `GameState` / `GameManager` / `PauseMenuUI` |
| `R` | Đua lại nhanh | `InputHandler` → `RacingState` / `FinishedState` |
| `M` | Bật / Tắt toàn bộ âm thanh (BGM + SFX) | `InputHandler` → `GameManager` → `RaceAudioController` (`IAudioService`) |

---

## 3. Mở & Chạy từ Mã Nguồn (Unity `6000.6.9f1`)
1. Mở **Unity Hub → Add → Add project from disk** và trỏ tới thư mục gốc `newbie/`.
2. Mở bằng đúng **Unity `6000.6.9f1`**.
3. Mở `Assets/Scenes/LobbyScene.unity` và nhấn **Play (▶)** *(Scene Index `0` trong Build Settings là `LobbyScene.unity`, Index `1` là `SampleScene.unity`)*.
4. Các lệnh tiện ích trong thanh menu Unity:
   - `RacingGame > Build Windows Standalone`: Đóng gói lại bản chạy sạch `Builds/RoadRash2D/RoadRash2D.exe`.
   - `RacingGame > Setup All Scenes & Skin Assets`: Tái tạo/đồng bộ các `ScriptableObject` và 2 Scene.

---

## 4. Chạy Kiểm Thử Tự Động (114 EditMode Tests) & Build CLI

Đảm bảo đóng cửa sổ Unity Editor đang mở project trước khi chạy batchmode:

```powershell
# Chạy toàn bộ 114 EditMode Unit & Integration Tests
$p = Start-Process -FilePath "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe" `
  -ArgumentList @(
    "-batchmode", "-nographics",
    "-projectPath", "e:\UnityCodeGame\newbie",
    "-runTests", "-testPlatform", "EditMode",
    "-testResults", "e:\UnityCodeGame\newbie\Logs\EditModeResults.xml",
    "-logFile", "e:\UnityCodeGame\newbie\Logs\editmode_test.log"
  ) -Wait -PassThru -NoNewWindow
Write-Host "TEST_EXIT_CODE=$($p.ExitCode)"
```

Xuất bản Build Windows Standalone sạch bằng dòng lệnh:

```powershell
$p = Start-Process -FilePath "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe" `
  -ArgumentList @(
    "-batchmode", "-nographics", "-quit",
    "-projectPath", "e:\UnityCodeGame\newbie",
    "-executeMethod", "RacingGame.Editor.RoadRashSceneSetup.BuildWindowsRelease",
    "-logFile", "e:\UnityCodeGame\newbie\Logs\windows_build.log"
  ) -Wait -PassThru -NoNewWindow
Write-Host "BUILD_EXIT_CODE=$($p.ExitCode)"
```

---

## 5. Kiến trúc 3 Tầng, Dữ liệu JSON Ngoài & Lưu trữ
- **`RacingGame.Core`** (`Assets/Scripts/Core/Pure/RacingGame.Core.asmdef`, **`noEngineReferences: true`**, `25` tệp C# thuần, `0` `using UnityEngine`):
  - **Contracts (ISP/DIP):** `IInputProvider`, `ISaveStorage`, `IJsonCodec`, `IAudioService`, `ITrackDataProvider`, `IObstacleFactory`, `IObstacleBlueprint`, `IVehicleStatsProvider`.
  - **Custom Exceptions:** `GameDomainException` → `SaveDataCorruptedException`, `InvalidTrackConfigurationException`, `InvalidUpgradeTransactionException`, `InvalidRaceStateTransitionException`.
  - **Pure Domain & GoF:** `ProgressionRuleEngine`, `MidRaceSaveService`, `TrackObstacleFactory` (Factory), `VehicleStatsDecorators` (Decorator), `CheckpointGateMath`, `CollisionSolverMath`, `RaceStateTransitionRules`, `GameBalanceConfig`, `TrackJsonDto`.
- **`RacingGame`** (`Assets/Scripts/RacingGame.asmdef`, `46` tệp Adapters/Engine/UI):
  - `Core/Adapters/`: `FileSaveStorageAdapter`, `UnityJsonCodec`, `ExternalJsonTrackLoader`, `ExternalBalanceConfigLoader`, `RaceCombatSystem`.
  - Gameplay, Vật lý 2D, AI, Cảnh sát, Renderer Pseudo-3D, VFX/CRT Scanline, BGM + SFX procedural và uGUI + TextMeshPro tiếng Việt.
- **`RacingGame.Editor`** (`3` tệp) & **`RacingGame.Tests`** (`21` tệp test, `114/114 PASS` trong `4.556s`, `0` warning).
- **Dữ liệu JSON ngoài (`Assets/StreamingAssets/`):**
  - Maps: `Maps/01_city_morning.json` .. `Maps/04_city_district.json`
  - Cân bằng: `Config/game_balance.json`
- **Tệp lưu người dùng (`Application.persistentDataPath`):**
  - Tiến trình Garage, mở khóa map & HighScores: `racing2d_save.json`
  - Trạng thái ván đang đua (lưu từ Pause Menu): `midrace_save.json`

---

## 6. Tài liệu chi tiết trong `Docs/`
- [Báo cáo Kiến trúc & Thiết kế OOP](Docs/BAO_CAO_KIEN_TRUC_OOP.md) — Use Case, Balancing Table, UML 6/6 quan hệ, Package Diagram 3 tầng, GoF & đánh đổi thiết kế, State/Sequence Diagram, thuật toán `O(·)` & kết quả kiểm thử.
- [Danh sách Test Case (`TC-01`..`TC-112`)](Docs/TEST_CASES.md) — Ma trận truy vết & phân loại EP / BVA / State / Exception / Integration.
- [Nhật ký Lỗi, Playtest & Fault-Injection](Docs/BUG_LOG.md) — Bug Log 6 cột, bảng Playtest 10 kịch bản End-to-End và kịch bản kiểm định Debugger.
- [Kết quả Benchmark AI 3 vòng](Docs/EXPERIMENT.md) — Thực nghiệm 3 vòng × 3 mức skill trên Valley & City.
