# Tài Liệu Kiến Trúc & Thiết Kế OOP — Game Đua Xe Road Rash 2D (Unity)

## 1. Giới thiệu & Phạm vi (Giai đoạn 1)
- **Thể loại:** Đua xe 2D góc nhìn từ sau xe (Pseudo-3D kiểu *Road Rash*) dựng trên mô hình vật lý 2D phẳng `(x, y)` và đường đua vòng kín `WaypointTrack`.
- **Mục tiêu:** Điều khiển xe vượt qua 4 đối thủ AI tự hành, hoàn thành đủ số vòng qua đúng thứ tự checkpoint để giành thứ hạng cao, kiếm tiền thưởng nâng cấp xe và lưu tiến trình bằng JSON.
- **Công cụ:** Unity 6 (`6000.6.9f1`), C#, Unity Input System, EditMode Test Framework, Git/GitHub.
- **Ngoài phạm vi:** Không chơi mạng online, không dùng mô hình 3D nặng.

---

## 2. Tuân thủ 4 tính chất Lập trình Hướng đối tượng (OOP)

| Tính chất OOP | Cách áp dụng thực tế trong code |
|---|---|
| **Trừu tượng hóa (Abstraction)** | Lớp cha trừu tượng `Driver` (`Assets/Scripts/Core/Driver.cs`) khai báo `abstract VehicleInput DecideInput(Car, WaypointTrack)`; lớp `RaceStateBase` (`Assets/Scripts/Core/GameState.cs`) trừu tượng hóa hành vi trạng thái game; `Checkpoint` ẩn toàn bộ phép toán giao cắt đoạn thẳng. |
| **Kế thừa (Inheritance)** | `PlayerDriver : Driver`, `AIRacerDriver : Driver`; 5 trạng thái game (`MainMenuState`, `CountdownState`, `RacingState`, `FinishedState`, `UpgradeShopState`) kế thừa `RaceStateBase`. |
| **Đa hình (Polymorphism)** | `Car` chỉ giữ tham chiếu kiểu lớp cha `Driver` và gọi `_driver.DecideInput(this, track)` — `VehiclePhysics` không biết người chơi hay AI đang lái. `GameManager` điều phối trạng thái qua `_currentState.Update(...)` / `FixedUpdate(...)`. `RacerRankingComparer` cài đặt `IComparer<RacerProgress>`. |
| **Đóng gói (Encapsulation)** | Trạng thái nội bộ của `VehiclePhysics`, `Car`, `WaypointTrack`, `AIRacerDriver`, `Checkpoint`, `RacerProgress`, `CarUpgradeSystem` được đặt `private` / `[SerializeField] private`, chỉ đọc qua property (`Position`, `IsOnTrack`, `Money`, `Waypoints`...) và thay đổi qua phương thức kiểm soát (`ApplyThrottle`, `BuyUpgrade`, `ProcessRacerMovement`...). |

---

## 3. Sơ đồ lớp (Class Diagram)

```mermaid
classDiagram
    class Driver {
        <<abstract>>
        -string driverName
        +string DriverName
        +DecideInput(Car car, WaypointTrack track)* VehicleInput
        +OnReset() void
    }

    class PlayerDriver {
        -InputHandler inputHandler
        +DecideInput(Car car, WaypointTrack track) VehicleInput
    }

    class AIRacerDriver {
        -int targetWaypoint
        -float skillLevel
        -float preferredLateralOffset
        +DecideInput(Car car, WaypointTrack track) VehicleInput
    }

    class InputHandler {
        -float steerResponseSpeed
        +ReadInput() VehicleInput
    }

    class VehiclePhysics {
        -Vector2 position
        -Vector2 velocity
        -float heading
        -float mass
        -float enginePower
        -float brakePower
        -float grip
        -float drag
        +ApplyThrottle(float) void
        +ApplyBrake(float) void
        +ApplySteering(float) void
        +Update(float dt) void
    }

    class Car {
        -VehiclePhysics physics
        -Driver _driver
        -bool _isOnTrack
        +Initialize(Driver, Vector2, float, WaypointTrack) void
        +StepPhysics(float dt, WaypointTrack) void
    }

    class WaypointTrack {
        -Vector2[] waypoints
        -float trackWidth
        +GetNextWaypoint(int i) Vector2
        +GetTrackDirection(int i) Vector2
        +IsOnTrack(Vector2 pos) bool
        +SampleAtDistance(float s) TrackSample
    }

    class LapCheckpointManager {
        -List~Checkpoint~ _checkpoints
        -List~RacerProgress~ _racers
        -int totalLaps
        +Tick(float dt) void
        +GetRanking() IReadOnlyList~RacerProgress~
    }

    class CarUpgradeSystem {
        -PlayerSaveData _data
        +int Money
        +BuyUpgrade(UpgradeType) bool
        +ApplyToCar(VehiclePhysics) void
        +Save() void
        +Load() bool
    }

    class RaceStateBase {
        <<abstract>>
        +GameFlowState StateType*
        +Enter(GameManager) void
        +Update(GameManager, float) void
        +FixedUpdate(GameManager, float) void
        +Exit(GameManager) void
    }

    class GameManager {
        -List~Car~ _allCars
        -CarUpgradeSystem _upgradeSystem
        -RaceStateBase _currentState
        +TransitionTo(GameFlowState) void
        +StepRaceSimulation(float dt) void
    }

    Driver <|-- PlayerDriver
    Driver <|-- AIRacerDriver
    PlayerDriver --> InputHandler : uses
    Car *-- VehiclePhysics : composition
    Car o-- Driver : polymorphic driver
    GameManager *-- CarUpgradeSystem
    GameManager o-- LapCheckpointManager
    GameManager o-- WaypointTrack
    GameManager o-- RaceStateBase
    GameManager "1" --> "*" Car : manages
```

---

## 4. Sơ đồ tuần tự (Sequence Diagrams)

### 4.1 Luồng mỗi bước vật lý (FixedUpdate)
```mermaid
sequenceDiagram
    participant GM as GameManager
    participant State as RacingState
    participant Car as Car
    participant Drv as Driver (Player/AI)
    participant Phys as VehiclePhysics
    participant Lap as LapCheckpointManager

    GM->>State: FixedUpdate(gm, dt)
    State->>GM: StepRaceSimulation(dt)
    loop Cho mỗi Car trong _allCars
        GM->>Car: StepPhysics(dt, track)
        Car->>Drv: DecideInput(car, track)
        Drv-->>Car: VehicleInput(throttle, brake, steer)
        Car->>Phys: ApplyThrottle / ApplyBrake / ApplySteering
        Car->>Phys: Update(dt)
    end
    GM->>Lap: Tick(dt)
    Lap->>Lap: ProcessRacerMovement & UpdateRanking
```

### 4.2 Luồng nâng cấp xe
```mermaid
sequenceDiagram
    participant Player as Người chơi (Garage UI)
    participant GM as GameManager
    participant Upg as CarUpgradeSystem
    participant Phys as VehiclePhysics

    Player->>GM: TryBuyUpgrade(Engine)
    GM->>Upg: BuyUpgrade(Engine)
    alt Đủ tiền và chưa Max Level
        Upg->>Upg: Trừ tiền, tăng cấp, Save() JSON
        Upg-->>GM: true
        GM->>Upg: ApplyToCar(playerCar.Physics)
        Upg->>Phys: ConfigureBaseStats(...)
    else Không đủ tiền hoặc đã Max
        Upg-->>GM: false
    end
```

---

## 5. Sơ đồ trạng thái (State Diagram)

```mermaid
stateDiagram-v2
    [*] --> MainMenu
    MainMenu --> Countdown : Bấm Bắt đầu đua
    MainMenu --> UpgradeShop : Bấm Cửa hàng nâng cấp
    UpgradeShop --> MainMenu : Quay lại Menu
    UpgradeShop --> Countdown : Bấm Đua ngay
    Countdown --> Racing : Đếm ngược 3..2..1..GO xong
    Racing --> MainMenu : Nhấn Esc / Menu
    Racing --> Countdown : Nhấn Đua lại (R)
    Racing --> Finished : Người chơi hoàn thành đủ vòng
    Finished --> Countdown : Đua lại
    Finished --> UpgradeShop : Vào nâng cấp xe
    Finished --> MainMenu : Về Menu
```

---

## 6. Lưu đồ thuật toán AI & Đếm vòng

### 6.1 AI bám Waypoint (`AIRacerDriver`)
```mermaid
flowchart TD
    A[Bắt đầu DecideInput] --> B{Speed < 2.5 quá 2.5s?}
    B -- Có --> C[ResetToTrackPoint về waypoint gần nhất]
    B -- Không --> D{Khoảng cách tới targetWaypoint <= 14m?}
    D -- Có --> E[Tăng targetWaypoint mod N]
    D -- Không --> F[Giữ nguyên targetWaypoint]
    E --> G[Tính né xe phía trước nếu gần]
    F --> G
    G --> H[Tính góc lệch tới aimPoint -> Steer]
    H --> I[Đo góc cua sắp tới -> tính targetSpeed]
    I --> J{Tốc độ > targetSpeed?}
    J -- Vượt nhiều --> K[Nhả ga + Phanh]
    J -- Thấp hơn --> L[Tăng ga theo skillLevel]
```

### 6.2 Đếm vòng chống gian lận (`LapCheckpointManager`)
```mermaid
flowchart TD
    A[Xe di chuyển từ A -> B] --> B[Lấy Checkpoint kỳ vọng tiếp theo]
    B --> C{Đoạn A->B cắt cổng đúng hướng xuôi?}
    C -- Không (đi tắt / ngược chiều) --> D[Bỏ qua, cập nhật khoảng cách & cảnh báo ngược chiều]
    C -- Có --> E[Tăng NextCheckpointIndex]
    E --> F{Có phải vạch đích Checkpoint 0?}
    F -- Không --> G[Phát sự kiện CheckpointPassed]
    F -- Có --> H[CompletedLaps++, lưu Last/BestLapTime]
    H --> I{CompletedLaps >= TotalLaps?}
    I -- Có --> J[Đánh dấu về đích & xếp FinishPosition]
    I -- Không --> K[Tiếp tục vòng mới]
```

---

## 7. Cấu trúc lưu file JSON (`CarUpgradeSystem`)
Đường dẫn mặc định: `Application.persistentDataPath/racing2d_save.json`
```json
{
  "money": 580,
  "engineLevel": 2,
  "brakesLevel": 1,
  "gripLevel": 1,
  "weightLevel": 0,
  "racesPlayed": 3,
  "racesWon": 2,
  "bestRaceTime": 48.72
}
```
