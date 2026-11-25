# Bug Log, Playtest End-to-End & Kịch bản Fault-Injection

---

## 1. Bảng Nhật Ký Sửa Lỗi (Bug Log — Chuẩn 6 Cột)

| Bug-ID | Bước tái hiện | Nguyên nhân gốc (Root Cause) | Commit / Bản sửa | Regression Test | Kết quả |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **BUG-01** | Bật tùy chọn tắt Domain Reload trên Unity Editor rồi hủy hoặc nạp lại scene nhiều lần; các prop đường đua mất texture (`null`). | `RoadRashPropSprites` giữ bộ nhớ đệm `static Dictionary` trỏ tới `Sprite`/`Texture2D` đã bị `DestroyImmediate`, nhưng chỉ kiểm tra `ContainsKey` thay vì kiểm tra lifetime `sprite == null` của `UnityEngine.Object`. | `3c07a82` (`RoadRashPropSprites.cs`) | `TC-33` (`PropSprites_RegenerateWhenDestroyedWithoutDomainReload`) | **FIXED & PASS** |
| **BUG-02** | Trên map đô thị `CityDistrict` có góc cua vuông `90°`, xe AI chạy chế độ cố định bám tâm hay cắt góc hoặc lao ra lề ngoài đường. | `AIRacerDriver` ban đầu chỉ nhìn 1 waypoint kế tiếp (`LegacyFixed`), không ước lượng độ gắt góc cua phía trước để giảm tốc sớm và không nội suy đa điểm spline. | `ba87f50` (`AIRacerDriver.cs` — `SmoothedSplinePath`) | `TC-58` (`CompareNavigationModes_OnCityAndSharp90DegreeCorners_LogsMetricsAndVerifiesBest`), `TC-69`, `TC-70` | **FIXED & PASS** |
| **BUG-03** | Nạp tệp save `racing2d_save.json` bị sửa tay thành tiền âm (`-250`) hoặc cấp nâng cấp vượt trần (`99`, `-3`) gây lỗi tính giá và thống kê sai. | `CarUpgradeSystem.Load()` tin tưởng dữ liệu sau `JsonUtility.FromJson` mà chưa kẹp biên domain invariants và chưa có cơ chế `SaveDataCorruptedException` ở chế độ strict. | `27d0a95` & bản nâng cấp Pure Core (`ProgressionRuleEngine.ClampLoadedLevels`, `SaveDataCorruptedException`) | `TC-29` (`UpgradeSystem_LoadCorruptedJsonClampsOutOfRangeLevelsToValidBounds`), `TC-93` | **FIXED & PASS** |
| **BUG-04** | Hủy `RaceHUD` hoặc tắt component (`OnDisable`) trong lúc trận đấu vẫn phát sự kiện va chạm vật cản gây `MissingReferenceException` / `NullReferenceException`. | Các sự kiện `ObstacleHit`, `LapCompleted`, `RacerFinished` chỉ được đăng ký trong `Bind()` nhưng thiếu cặp hủy đăng ký đối xứng `-=` tại `OnDisable()` và `OnDestroy()`. | `37c59fd` (`RaceHUD.cs`) | `TC-82` (`RaceHudOverhaul_BuildsFourCornerTmpLayout_HandlesObserverEvents_AndUnsubscribesInOnDisableAndOnDestroy`) | **FIXED & PASS** |
| **BUG-05** | Đâm chéo góc gắt (`70°`) vào rào chắn biên ngoài ở tốc độ thấp làm xe kẹt dính mũi vào tường không trượt thoát ra mặt đường được. | `Car.EnforceOuterBarrier` chỉ chặn vị trí và triệt tiêu vận tốc pháp tuyến mà không xoay mượt đầu xe trượt theo tiếp tuyến đường (`wall-slide`) và lực đẩy thoát biên. | `ed52afd` (`Car.cs`, `VehicleCollisionPhysics.cs`) | `TC-85` (`OuterBarrier_SteepAngleAtZeroSpeed_WallSlidesAndReturnsToTrackQuickly`), `TC-86`, `TC-89` | **FIXED & PASS** |
| **BUG-06** | Khi lưu và nạp trạng thái giữa ván đua (`SaveMidRaceState` → `LoadMidRaceState`) với `Car` AI khởi tạo độc lập, xe AI ghi đè vị trí lên `PlayerCar`. | `Car.isPlayer` mặc định là `true`, trong khi `SaveMidRaceState()` kiểm tra `c == playerCar \|\| c.IsPlayer` nên cả AI chưa gán cờ đều bị đánh dấu `isPlayer = true` trong JSON. | Nâng cấp bản hiện tại (`GameManager.SaveMidRaceState` chỉ gắn `c == playerCar` & `SpawnOrSyncOpponents` chuẩn hóa `ConfigureIdentity(false, ...)`) | `TC-106` (`GameManager_SaveAndLoadMidRaceState_RestoresPositionsVelocitiesLapsAndCheckpoints`) | **FIXED & PASS** |
| **BUG-07** | Khi người chơi lái bình thường ở làn ngoài hoặc đi off-road, xe tự động bị kéo ngược / giật về tâm đường đua hoặc vô tình bị teleport khi nhấn nhầm `[F]`; đồng thời đụng Hố/Vết dầu lại bị văng ngang, còn đụng Vật cản cứng không nảy lùi lại phía sau và off-road có nguy cơ tụt momentum. | 1) `Car.StepRoadRashArcadePhysics` ép heading/vận tốc theo Catmull-Rom tangent rồi `WaypointTrack.ProjectPosition` chiếu lại lên đa giác thẳng kết hợp `centrifugalLatVel`, gây trôi/giật về tâm; 2) `InputHandler` + `RacingState` + `GameManager.RecoverPlayerToTrack` cho phép `[F]/[Backspace]` dịch chuyển xe; 3) `Car.StepPhysics` tự kéo vào trong cho mọi xe không riêng AI; 4) `TrackObstacleSystem.ApplyObstacleImpact` xử lý đồng nhất Hố và Vật cản cứng qua `ReceiveKick` (không bật lùi, không immunity). | `fix(physics): remove player auto-return to track, differentiate pothole vs solid obstacle knockback, and keep off-road speed above zero` (`Car.cs`, `VehiclePhysics.cs`, `WaypointTrack.cs`, `TrackObstacleSystem.cs`, `TrackObstacleFactory.cs`, `IObstacleFactory.cs`, `ObstacleKind.cs`, `RoadRashPropSprites.cs`, `Driver.cs`, `AIRacerDriver.cs`, `PoliceRacerDriver.cs`, `GameManager.cs`, `GameState.cs`, `InputHandler.cs`, `RaceHUD.cs`, `RoadRashRenderer2D.cs`) | `TC-86`, `TC-110`, `TC-111` (2 cases), `TC-112` (2 cases) | **FIXED & PASS** |

### Ghi nhận dọn cảnh báo biên dịch (`UAC1001` & `CS0067`)
- Trước khi nâng cấp, `Logs/Editor.log` từng ghi nhận 3 cảnh báo analyzer Unity `UAC1001` do `struct VehicleInput` thiếu `[Serializable]`, và khi vô hiệu hóa `RecoverPlayerToTrack` phát sinh cảnh báo `CS0067` trên event `PlayerRecoveredToTrack`. Sau khi chuẩn hóa `[Serializable]` cho `VehicleInput` và accessor trống cho `PlayerRecoveredToTrack`, toàn bộ project biên dịch với **0 lỗi và 0 cảnh báo** (`114/114 PASS`, `Logs/editmode_test.log`).

---

## 2. Bảng Kiểm Thử Chơi Thử End-to-End (Playtest — 10 Kịch bản)

| Mã | Kịch bản kiểm thử | Thao tác thực hiện | Kỳ vọng hệ thống | Kết quả & Hiệu năng quan sát |
| :-: | :--- | :--- | :--- | :--- |
| **PT-01** | Khởi động Clean-room & Sảnh chờ (`LobbyScene`) | Mở `Builds/RoadRash2D/RoadRash2D.exe` trên môi trường sạch không có save cũ. | Vào `LobbyScene` ≤ 2s, hiển thị đúng 4 skin, 4 map JSON, 200 XU, nhạc nền BGM phát mượt, font tiếng Việt đủ dấu không vỡ chữ. | **PASS** (60 FPS ổn định, 0 lỗi Console) |
| **PT-02** | Chọn Skin & Mở khóa / Chọn Map ngoài JSON | Đổi lần lượt 4 skin (`Xanh SM`, `Shopee Food`, `Grab`, `Ahamove`), duyệt carousel 4 map; sửa `rewardMultiplier` trong JSON ngoài rồi mở lại app. | Preview và màu áo/xe cập nhật đúng tức thì; sửa JSON ngoài thay đổi ngay dữ liệu map khi chạy lại mà không biên dịch lại. | **PASS** |
| **PT-03** | Garage Nâng cấp & Kiểm tra giới hạn tiền/cấp | Vào Garage nâng cấp `Engine` (150 XU) từ 200 XU còn 50 XU, bấm tiếp `Engine` Lv2 (225 XU) để kiểm tra chặn giao dịch. | Lv1 tăng chỉ số MAX SPD / POWER, trừ còn 50 XU; Lv2 từ chối sạch, hiện thông báo thiếu xu. | **PASS** |
| **PT-04** | Luồng Countdown → Đua xe & Đá đối thủ (`Kick`) | Chọn 3 vòng, bấm `PLAY`, chờ `3..2..1..GO`, tăng ga `W`, chuyển làn `A/D`, áp sát xe AI bấm `Space`. | Xe AI tách làn gọn, đòn đá tự chọn phía đối thủ gần nhất, hất văng đối thủ kèm hiệu ứng tia lửa URP Bloom và âm thanh `KickHit`. | **PASS** (60 FPS, va chạm Newton bật nảy mượt) |
| **PT-05** | Truy đuổi Cảnh sát & Va chạm Vật cản | Giữ tốc độ vừa trên `city_district` (có `ParkedBus`, hầm chui, cầu vượt, ngã tư) khi `PoliceCar_Unit01` áp sát. | Cảnh sát tự tăng tốc bám đuôi, bật còi xanh-đỏ trên HUD, va chạm `ParkedBus`/`OilBarrel` làm chao đảo & giảm tốc có hồi phục. | **PASS** |
| **PT-06** | Chạy Off-road & Phục hồi Rào chắn biên ngoài | Lái lệch ra lề cỏ (`OFF ROAD`) giữ ga không đánh lái; ép góc `70°` vào rào chắn ngoài rồi tự đánh lái `A/D` vào mặt đường. | Xe tuyệt đối không tự kéo về tâm hay teleport (tốc độ off-road `> 0` và giới hạn `<= 60% MaxSpeed`), trượt song song biên ngoài và tự đánh lái mượt trở lại mặt đường. | **PASS** |
| **PT-07** | Chống đi tắt (`Checkpoint`) & Cảnh báo Ngược chiều | Quay đầu xe chạy ngược chiều tốc độ `> 4 m/s`, thử bỏ qua Checkpoint 2 để băng về vạch đích. | HUD bật cảnh báo `NGƯỢC CHIỀU`, tắt ngay khi quay đúng hướng; bỏ qua Checkpoint 2 không được cộng vòng đua. | **PASS** |
| **PT-08** | Pause, Hướng dẫn phím, Bật/tắt CRT & Âm thanh | Trong lúc đang đua nhấn `Esc`/`P`, bấm `M` tắt/bật âm thanh, bật/tắt `CRT SCANLINE`, mở modal Hướng dẫn phím. | `Time.timeScale = 0` đóng băng xe, âm thanh & CRT lưu bền vững qua `PlayerPrefs`, Controls Modal hiển thị đủ 7 phím thật. | **PASS** |
| **PT-09** | Lưu giữa ván đua → Tắt hẳn App → Nạp lại | Đang ở vòng 2 bấm `Esc` → bấm `LƯU VÁN ĐUA` → thoát hẳn `RoadRash2D.exe` → mở lại → bấm `TIẾP TỤC VÁN`. | Mở lại thấy nút `TIẾP TỤC VÁN`; bấm vào khôi phục đúng map, vị trí, vận tốc, số vòng và thời gian đang đua 100%. | **PASS** |
| **PT-10** | Về đích, HighScores, Replay & Xóa dữ liệu | Hoàn thành đủ 3 vòng để hiện bảng Kết quả + tiền thưởng, bấm `Đua lại [R]` 2 lần liên tiếp; về Lobby test `XÓA DỮ LIỆU` 2 lần. | Tiền thưởng và HighScore lưu vào `racing2d_save.json`; `Replay` reset sạch xe/vòng/vật cản không sót rác cũ; `XÓA DỮ LIỆU` khôi phục mặc định sạch. | **PASS** |

---

## 3. Kịch bản Thực hành Live Fault-Injection & Debug (Chuẩn bị Vấn đáp)

1. **Tiêm lỗi 1 — Lệch điều kiện biên Checkpoint (`CheckpointGateMath.cs`):**
   - Sửa tạm `t >= 0f && t <= 1f` thành `t > 1f` trong `CheckpointGateMath.IsSegmentCrossingGateForward`.
   - Chạy EditMode Test Runner → **`TC-14`, `TC-15`, `TC-18`, `TC-98`, `TC-106` chuyển FAIL đỏ ngay lập tức**.
   - Đặt Breakpoint tại `Checkpoint.IsCrossedForward` → xem Watch (`t`, `u`, `dotForward`) → khôi phục đúng điều kiện → `109/109 PASS`.
2. **Tiêm lỗi 2 — Hỏng giới hạn cấp nâng cấp (`ProgressionRuleEngine.cs`):**
   - Sửa `if (IsMaxLevel(type)) return false;` thành cho mua vượt cấp 5.
   - Chạy EditMode Test Runner → **`TC-23`, `TC-28`, `TC-94` bắt lỗi FAIL ngay lập tức**.
3. **Tiêm lỗi 3 — Đóng gói thiếu trường `racers` trong `MidRaceSaveService`:**
   - Bỏ lưu danh sách `racers` hoặc sửa `snapshot.raceTime` thành `0f`.
   - Chạy EditMode Test Runner → **`TC-105`, `TC-106` chuyển FAIL đỏ ngay**.
