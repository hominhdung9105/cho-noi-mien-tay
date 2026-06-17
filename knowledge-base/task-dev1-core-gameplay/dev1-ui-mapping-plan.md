# Detailed UI Debug & Atmospheric Mapping Design

## 1. Cấu trúc Hệ thống Dữ liệu Môi trường (Environment Data-Driven)
Để tái hiện chính xác không gian từ ảnh chợ nổi, Dev 1 tạo ScriptableObject `AtmosphericProfileSO.cs` lưu trữ cấu hình sau:

| Thuộc tính (Property) | Loại dữ liệu (Type) | Mô phỏng Thực tế (Từ Ảnh Tham Chiếu) |
| :--- | :--- | :--- |
| `SkyboxGradient` | Gradient | Chuyển từ Xanh thẫm (Đêm) -> Vàng hồng (Ảnh 1 - Bình minh) -> Xanh trong (Trưa) -> Cam rực (Ảnh 2 - Hoàng hôn). |
| `SunIntensityCurve` | AnimationCurve | Đạt đỉnh 1.5 vào lúc 12:00, hạ về 0.2 vào lúc 18:00. |
| `FogDensityCurve` | AnimationCurve | Đạt đỉnh 0.05 vào lúc 05:00 sáng (Sương mù giăng sông), về 0 vào lúc 10:00 trưa. |
| `WaterHeightCurve` | AnimationCurve | Mực nước cao nhất lúc 08:00 sáng (Chợ đông - Ảnh 1), rút thấp nhất xuống -2.0m lúc 16:00 chiều (Mắc cạn). |

## 2. Thiết kế Giao diện Kiểm thử (Debug UI Layout)
Tạo một Canvas tạm thời đặt tên là `Environment_Debug_Canvas.prefab` trong thư mục cá nhân để Test độc lập trên Unity:

+-----------------------------------------------------------------------+
|  [DEBUG SYSTEM CONTROLLER]                                             |
|                                                                       |
|  Time Slider: [======|----------------------------------] 06:30 AM    |
|  Time Speed:  (X1)  ((X10))  (X100)  (PAUSE)                          |
|                                                                       |
|  [ENVIRONMENT STATS LIVE]                                              |
|  - Active Phase: EarlyMorning       - Water Level Y: -0.24m           |
|  - Light Color: #FFCC99             - Fog Density: 0.035              |
|                                                                       |
|  [PLAYER/BOAT RESOURCE SIMULATOR]                                     |
|  Fuel Bar   : [====================..........] 65.4%                  |
|  Stamina Bar: [==============================.] 92.1%                  |
|                                                                       |
|  Buttons: [Simulate Ramming Mud]  [Drain Fuel Instantly]              |
+-----------------------------------------------------------------------+

- **Yêu cầu Kỹ thuật UI Debug:**
  - Toàn bộ Text hiển thị liên kết trực tiếp với các Event từ `TimeManager` và `BoatController` thông qua cơ chế Subscribe/Unsubscribe để xác định Event hoạt động chuẩn 100%.