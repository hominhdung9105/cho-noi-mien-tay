# Technical Rules & Architecture Constraints for Dev 1

## 1. Quy tắc Tách biệt UI (UI Decoupling Rule)
- **Tuyệt đối không:** Khai báo `using UnityEngine.UI;` hoặc `using TMPro;` trong các file thuộc vùng làm việc của Dev 1.
- Dev 1 chỉ chịu trách nhiệm tính toán con số thô (Raw Data) và bắn Event. Việc hiển thị các thanh Bar hay Text nội thoại là trách nhiệm của Dev 2 trên nhánh riêng.

## 2. Quy tắc Nội suy Ánh sáng & Sương mù (Smooth Atmospheric Rule)
- Dựa trên ảnh tham chiếu: 
  - Sáng sớm (EarlyMorning): Sương mù dày (`RenderSettings.fogDensity` cao), sắc xanh lạnh trộn cam nhạt.
  - Chiều chạng vạng (Dusk): Ánh sáng vàng cam gắt (như ảnh hoàng hôn sông nước), sương mù bằng 0.
- **Cấm:** Thay đổi ánh sáng đột ngột bằng các lệnh gán cứng trị số trong hàm rẽ nhánh `if-else`.
- **Bắt buộc:** Sử dụng thuộc tính `Gradient` cho màu sắc và `AnimationCurve` cho cường độ sáng/độ dày sương mù trong Unity để nội suy mượt mà theo tỷ lệ `currentTime / 24f`.

## 3. Quy tắc Vật lý Mắc cạn (Anti-Tunneling & Physics Grounding)
- Việc mắc cạn phải diễn ra tự nhiên bằng va chạm vật lý: Ghe (`Rigidbody` sử dụng Continuous Detection) hạ thấp theo mực nước và chạm vào `Collider` của bãi bùn dưới đáy sông.
- Sử dụng `PhysicMaterial` có độ ma sát (`Friction`) cao cho các bãi bùn để tự động kìm hãm vận tốc của ghe mà không cần code can thiệp thô bạo vào vận tốc.