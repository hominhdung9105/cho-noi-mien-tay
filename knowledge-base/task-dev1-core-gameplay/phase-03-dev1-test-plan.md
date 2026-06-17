# Phase 3 Test Plan: Environment & Resource Systems

## Test Case 01: Thời gian thực và Đồng bộ khí quyển
- **Môi trường Test:** Scene `Dev1_CoreTest.unity`, bật Component `TimeManager`.
- **Hành động:** Kéo thanh Slider thời gian từ `04:00` sang `12:00` rồi sang `17:00`.
- **Tiêu chuẩn nghiệm thu:**
  - Cửa sổ Game và Scene của Unity phải thay đổi ánh sáng mượt mà. Đèn Directional Light tự xoay trục tạo bóng đổ dài ngắn khác nhau.
  - Ở mốc `17:00`, toàn bộ không gian nhuốm màu vàng cam rực rỡ như ảnh hoàng hôn miền Tây, mật độ sương mù về bằng 0.

## Test Case 02: Thủy triều rút và Mắc cạn cục bộ
- **Môi trường Test:** Đặt một chiếc ghe tại vị trí kênh nhỏ có đặt sẵn `Mudflat Collider` ngầm.
- **Hành động:** Điều chỉnh Slider thời gian về mốc `15:00` chiều (Thủy triều xuống thấp nhất).
- **Tiêu chuẩn nghiệm thu:**
  - GameObject Nước tịnh tiến đi xuống theo trục Y rõ rệt.
  - Ghe bị hạ trọng tâm theo nước, đáy ghe chạm vào `Mudflat Collider`.
  - Nhấn nút di chuyển ghe -> Ghe không thể nhúc nhích hoặc tốc độ bị giảm 90% do lực cản ma sát vật lý của bãi bùn, Console log liên tục báo va chạm vật lý với Layer `RiverBed`.

## Test Case 03: Đứt chuỗi tiêu hao (Fuel Exhaustion)
- **Hành động:** Giữ phím di chuyển ghe liên tục cho đến khi thông số `CurrentFuel` trên Debug UI về 0.
- **Tiêu chuẩn nghiệm thu:**
  - Lực đẩy động cơ (`AddForce`) bị khóa hoàn toàn.
  - Ghe chỉ còn chuyển động trôi theo quán tính cũ và dừng hẳn. Khóa toàn bộ các Input điều khiển máy cho đến khi nạp lại nhiên liệu.