# Bug Log — EditMode Tests Verification

## Tóm tắt kiểm tra
- **Phạm vi:** Toàn bộ các bài test trong `Assets/Tests/EditMode/` (`Phase1And2Tests.cs`, `Loop4AITests.cs`, `Loop5LapCheckpointTests.cs`, `Loop6And7Tests.cs`, `Loop8KickAndGraphicsTests.cs`).
- **Quy tắc tuân thủ:** Không chỉnh sửa bất kỳ file test hiện có nào.
- **Trạng thái biên dịch:** Biên dịch thành công với **0 lỗi**; các assembly `RacingGame.dll`, `RacingGame.Editor.dll`, `RacingGame.Tests.dll` đã được Unity Editor biên dịch trong `Library/ScriptAssemblies/` (cập nhật sau lần chỉnh sửa `.cs` cuối cùng).
- **Kết quả EditMode Test Runner (`EditModeResults.xml`):**
  - Tổng số `[Test]` trong mã nguồn: **33**
  - Tổng số `test-case` đã chạy: **33**
  - **Passed:** 33 | **Failed:** 0 | **Skipped:** 0 | **Inconclusive:** 0

## Danh sách lỗi đã sửa
Không phát hiện lỗi biên dịch hoặc test fail nào trong `Assets/Tests/EditMode/` — không cần sửa đổi code.

## Ghi nhận cảnh báo không chặn (chỉ đọc, không sửa file test)
- `Logs/Editor.log` ghi nhận 2 cảnh báo analyzer Unity (`UAC1001`) tại `Assets/Tests/EditMode/Phase1And2Tests.cs:10` và `Assets/Tests/EditMode/Loop8KickAndGraphicsTests.cs:10` (`Field 'NextInput' type 'RacingGame.VehicleInput' is skipped by serialization (missing the [Serializable] attribute)`). Các cảnh báo này không gây lỗi biên dịch hay làm fail test, và được giữ nguyên theo yêu cầu không sửa file test hiện có.

---

## Cập nhật kiểm tra toàn bộ EditMode Suite — 2026-11-15
- **Phạm vi:** Toàn bộ **9 file test** trong `Assets/Tests/EditMode/`:
  - `Phase1And2Tests.cs` (`4/4` pass)
  - `Loop4AITests.cs` (`9/9` pass)
  - `Loop5LapCheckpointTests.cs` (`9/9` pass)
  - `Loop6And7Tests.cs` (`7/7` pass)
  - `Loop8KickAndGraphicsTests.cs` (`4/4` pass)
  - `AIRacerDeterminismAndBenchmarkTests.cs` (`4/4` pass)
  - `LobbyAndSkinSystemTests.cs` (`3/3` pass)
  - `GameplayAndPauseTests.cs` (`4/4` pass)
  - `MapSystemTests.cs` (`6/6` pass — bổ sung cho hệ thống Map & `MapTheme_Valley`, không sửa bất kỳ file test hiện có nào)
- **Trạng thái biên dịch:** Thành công với **0 lỗi** (`Logs/test_valley.log`).
- **Kết quả Unity EditMode Test Runner (`TestResults.xml`):**
  - Tổng số `[Test]` trong mã nguồn: **50** (không giảm so với mốc ban đầu 33; toàn bộ 44 test trước đó + 6 test mới đều chạy).
  - Tổng số `test-case` đã chạy: **50**
  - **Passed:** 50 | **Failed:** 0 | **Skipped:** 0 | **Inconclusive:** 0

### Các lỗi đã phát hiện & khắc phục trong quá trình tích hợp hệ thống Map / tách `MapTheme_Valley`
1. **Lỗi biên dịch UI trong `Assets/Scripts/UI/GameMenuUI.cs`:**
   - Thay `RoadRashUiSprites.WhiteSprite` không tồn tại bằng `RoadRashUiSprites.GetPanelSprite()` (`Image.Type.Sliced`), và sửa thứ tự tham số `RoadRashUiSprites.CreateStyledText(...)` (`TextAnchor` đứng trước `FontStyle`).
2. **Assertion & khởi tạo UI trong EditMode (`Assets/Tests/EditMode/MapSystemTests.cs` & `Assets/Scripts/UI/GameMenuUI.cs`):**
   - Tách phương thức công khai `InitializeStandaloneLobby()` trên `GameMenuUI` (được `Start()` gọi trong runtime và test gọi trực tiếp) để tránh Unity assert `ShouldRunBehaviour()` khi gọi `SendMessage("Start")` trong EditMode.
   - Đồng bộ kỳ vọng `MapId` (`"neon_night"`) và giới hạn sai số ngang vật cản trên đường 2 làn (`TrackWidth * 0.36f`) khớp với `TrackObstacleSystem`.

### Cảnh báo không chặn (chỉ đọc)
- Hai cảnh báo analyzer `UAC1001` tại `Phase1And2Tests.cs:10` và `Loop8KickAndGraphicsTests.cs:10` tiếp tục được giữ nguyên do ràng buộc không chỉnh sửa các file test hiện có.

---

## Cập nhật xác minh toàn bộ EditMode Test Suite — Hiện trạng codebase (14 files / 70 tests)
- **Thời điểm chạy:** 2026-11-15 (Unity `6000.6.9f1` batchmode `-runTests -testPlatform EditMode`)
- **Phạm vi:** Toàn bộ **14 file test** trong `Assets/Tests/EditMode/`:
  - `Phase1And2Tests.cs` (`4/4` pass)
  - `Loop4AITests.cs` (`9/9` pass)
  - `Loop5LapCheckpointTests.cs` (`9/9` pass)
  - `Loop6And7Tests.cs` (`7/7` pass)
  - `Loop8KickAndGraphicsTests.cs` (`4/4` pass)
  - `AIRacerDeterminismAndBenchmarkTests.cs` (`4/4` pass)
  - `LobbyAndSkinSystemTests.cs` (`3/3` pass)
  - `GameplayAndPauseTests.cs` (`4/4` pass)
  - `MapSystemTests.cs` (`8/8` pass)
  - `CityMapTests.cs` (`5/5` pass)
  - `CityCornerNavigationComparisonTests.cs` (`1/1` pass)
  - `GameplayFeelAndOffRoadRecoveryTests.cs` (`4/4` pass)
  - `LaneLayoutTests.cs` (`6/6` pass)
  - `MapAIBenchmark3LapTests.cs` (`2/2` pass)
- **Trạng thái biên dịch:** Thành công với **0 lỗi** (`Tundra build success`).
- **Kết quả Unity EditMode Test Runner (`EditModeResults.xml`):**
  - Tổng số `[Test]` trong mã nguồn: **70**
  - Tổng số `test-case` đã chạy: **70**
  - **Passed:** 70 | **Failed:** 0 | **Skipped:** 0 | **Inconclusive:** 0
  - **Thời gian thực thi (`duration`):** `7.7263525s`

### Cảnh báo không chặn (chỉ đọc)
- Ghi nhận **3** cảnh báo analyzer Unity (`UAC1001`) trong các file test (không phải lỗi biên dịch, giữ nguyên theo nguyên tắc không sửa file test sẵn có):
  - `Assets/Tests/EditMode/Phase1And2Tests.cs:10` (`Field 'NextInput' type 'RacingGame.VehicleInput' is skipped by serialization`)
  - `Assets/Tests/EditMode/Loop8KickAndGraphicsTests.cs:10` (`Field 'NextInput' type 'RacingGame.VehicleInput' is skipped by serialization`)
  - `Assets/Tests/EditMode/GameplayFeelAndOffRoadRecoveryTests.cs:11` (`Field 'Input' type 'RacingGame.VehicleInput' is skipped by serialization`)
