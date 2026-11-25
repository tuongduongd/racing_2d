# Road Rash 2D: Đại Chiến Shipper

**Road Rash 2D: Đại Chiến Shipper** là tựa game đua xe mô tô 2D góc nhìn từ sau xe (phong cách Pseudo-3D kinh điển của *Road Rash*), được phát triển trên nền tảng **Unity 6 (`6000.6.9f1`)**.

---

## 1. Giới thiệu về Game

Trong game, bạn sẽ hóa thân thành các tài xế giao hàng công nghệ quen thuộc để tranh tài tốc độ và so tài kỹ năng trên đường phố:
* **4 thương hiệu Shipper:** Xanh SM, Shopee Food, Grab, Ahamove.
* **4 bản đồ đa dạng:** *Phố Sáng Ban Mai*, *Đại Lộ Ven Biển*, *Cao Tốc Đêm Neon*, và *Thành Phố Đêm Neon*.
* **Tính năng nổi bật:**
  * Nâng cấp chỉ số xe tại **Garage**.
  * Tùy chỉnh linh hoạt độ khó AI (`Dễ / Vừa / Khó`), số vòng đua (`1V / 2V / 3V / 5V`) và hiệu ứng màn hình cổ điển **CRT Scanline**.
  * Hỗ trợ **lưu và tiếp tục ván đua** ngay giữa chặng từ menu tạm dừng.

---

## 2. Cách chơi & Điều khiển

Tại **Sảnh chờ (`LobbyScene`)**, hãy chọn hãng xe yêu thích, bản đồ, số vòng đua và nâng cấp xe trong Garage trước khi bấm **`CHƠI NGAY (PLAY)`** (hoặc **`TIẾP TỤC VÁN`** nếu có dữ liệu lưu trước đó). Trên đường đua, hãy làm chủ tốc độ, né chướng ngại vật và tung cú đá để hạ gục các đối thủ cạnh tranh.

| Phím / Nút | Chức năng |
| :--- | :--- |
| `W` hoặc `↑` | Tăng ga |
| `S` hoặc `↓` | Phanh / Giảm tốc |
| `A` / `←` và `D` / `→` | Lái sang trái / phải |
| `Space` | Đá đối thủ (*Road Rash Kick*) |
| `Esc` hoặc `P` | Tạm dừng / Tiếp tục / Đóng cửa sổ |
| `R` | Đua lại nhanh |
| `M` | Bật / Tắt toàn bộ âm thanh (Nhạc nền & Hiệu ứng) |

---

## 3. Hướng dẫn mở & chạy trên Unity

1. **Yêu cầu phiên bản:** Cài đặt đúng phiên bản **Unity `6000.6.9f1`** thông qua Unity Hub.
2. **Mở dự án:**
   * Mở **Unity Hub** → chọn **Add → Add project from disk**.
   * Trỏ đường dẫn tới thư mục gốc của dự án (`newbie/`).
   * Chọn phiên bản **Unity `6000.6.9f1`** để mở project.
3. **Chạy game:**
   * Trong cửa sổ *Project*, mở Scene **`Assets/Scenes/LobbyScene.unity`** *(trong Build Settings, Index `0` là `LobbyScene.unity` và Index `1` là `SampleScene.unity`)*.
   * Nhấn nút **Play (▶)** ở phía trên thanh công cụ Editor để bắt đầu chơi.
4. **Menu tiện ích hỗ trợ trong Unity (tùy chọn):**
   * `RacingGame > Setup All Scenes & Skin Assets`: Tái tạo hoặc đồng bộ lại dữ liệu xe và các Scene nếu cần.
   * `RacingGame > Build Windows Standalone`: Đóng gói game ra tệp chạy độc lập trên Windows (`.exe`).
