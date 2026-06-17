# Phase 3 Execution Plan: Dev 1 - Core Gameplay & Environment Systems

## 1. Mục tiêu hệ thống
- Xây dựng Vòng lặp Thời gian (Day/Night Loop) điều khiển chu kỳ ánh sáng, sương mù theo đúng sắc độ thực tế của miền Tây.
- Thực hiện cơ chế Thủy triều vật lý: Nước rút sinh ra bãi cạn, trực tiếp tương tác vật lý chặn ghe.
- Thiết lập hệ thống tiêu hao tài nguyên cốt lõi (Fuel & Stamina) dạng Reactive (bắn Event).

## 2. Quy trình triển khai chi tiết
### Bước 1: Setup Domain Models & Stats (`Scripts/Domain/`)
- Mở rộng `BoatStats` (hoặc cấu hình ScriptableObject chứa thông số gốc của ghe):
  - `float MaxFuel`, `float CurrentFuel`, `float FuelConsumptionRate`.
- Tạo mới `PlayerStats` (Quản lý trạng thái nhân vật):
  - `float MaxStamina`, `float CurrentStamina`, `float StaminaRegenRate`.
- Cài đặt cơ chế bảo vệ đóng gói (Encapsulation) và các C# Events:
  - `public event Action<float, float> OnFuelChanged; // (current, max)`
  - `public event Action<float, float> OnStaminaChanged;`

### Bước 2: Lập trình Vòng lặp Thời gian (`Scripts/Presentation/Environment/`)
- Tạo `TimeManager.cs` điều khiển biến `float currentTime` (từ 0.0 đến 24.0).
- Tạo Enum `GamePhase { EarlyMorning, MidDay, Dusk, Night }` để phân tách mốc thời gian.
- Phát Event `public event Action<GamePhase> OnPhaseChanged;` và `public event Action<float> OnTimeUpdated;` (phát ra phần trăm thời gian trôi qua trong ngày).

### Bước 3: Hiện thực hóa Thủy Triều & Bãi Cạn Vật Lý
- Tạo `TideController.cs` kế thừa `MonoBehaviour`. 
- Thao tác trực tiếp trên trục Y của GameObject Nước (Water Mesh). Khi thời gian chuyển dịch về chiều (`Dusk`), hạ trục Y xuống theo đồ thị cấu hình sẵn.
- **Quản lý Collider Động:** Tạo mảng `Collider[] mudflatColliders`. Khi mức nước xuống thấp hơn ngưỡng an toàn (`WaterY < Threshold`), kích hoạt `enabled = true` cho các Collider này để khóa bến rạch nông.

### Bước 4: Khớp logic tiêu hao vào `BoatController.cs`
- Trong hàm `FixedUpdate`, đọc tín hiệu từ hệ thống Input (`InputSystem_Actions`).
- Nếu lực đẩy máy đuôi tôm hoạt động (`thrustInput > 0`), thực hiện trừ `CurrentFuel`.
- Nếu `CurrentFuel <= 0`, ép vận tốc và lực đẩy về 0, kích hoạt trạng thái "Tắt máy, trôi tự do".