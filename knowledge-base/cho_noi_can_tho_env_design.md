# TÀI LIỆU ĐẶC TẢ KỸ THUẬT MÔI TRƯỜNG & MASTER PROMPT CHO AI AGENT
**Dự án:** Chợ Nổi Miền Tây (Cần Thơ) - 3D Simulation / Management / RPG
**Định hướng kỹ thuật:** Unity Engine (URP - Universal Render Pipeline), Clean Architecture & Data-Driven.
**Mục tiêu:** Cung cấp blueprint thiết kế không gian, thông số môi trường (Mã màu HEX) và các bước lập trình chia nhỏ (Granular Tasks) để AI Agent sinh mã nguồn chuẩn xác, không phá vỡ cấu trúc dự án.

---

## 1. CẤU TRÚC ĐỊA HÌNH, KÍCH THƯỚC BẢN ĐỒ & LUỒNG DI CHUYỂN

Bản đồ được thiết kế dạng Single Open Map (Không loading khi chuyển vùng), cân bằng giữa hiệu suất Render của URP và nhịp độ di chuyển (Gameplay Pacing) bằng ghe xuồng vật lý.

### 1.1. Thông số Kích thước Toàn Bản đồ (World Bounds & Scales)
* **Tổng kích thước Terrain (Total Bounds):** `2048m x 2048m` (Tương đương 4 Full Chunks kích thước 1024m x 1024m). Cao độ đỉnh Terrain tối đa `Y = 50m`, cao độ lòng sông sâu nhất `Y = -15m`.
* **Vùng không gian tương tác (Gameplay Playable Area):** Giới hạn trong khoảng `1200m x 1200m` tại tâm bản đồ. Vùng rìa bên ngoài được bao bọc bằng hệ thống "Vạn lý trường thành dừa nước" và nhà sàn Low-Poly (Impostor/LOD) để che mắt người chơi, ngăn không cho nhìn thấy biên thế giới.
* **Camera Far Clip Plane:** Cấu hình cố định `600m` để tối ưu Draw Calls. Hệ thống sương mù (Fog) bắt đầu dày đặc từ `450m - 550m` để giấu đi các vật thể bị Culling ở biên tầm nhìn.

### 1.2. Phân vùng Sinh thái & Phân luồng Gameplay (Level Flow)
* **Vùng Sông Lớn (Trục Giao Thương - Mô phỏng Sông Hậu / Cái Răng):**
  * Lòng sông rộng từ `150m - 250m` in-game scale. Độ sâu kỹ thuật lớn để tránh lỗi kẹt Mesh Collider đáy ghe.
  * Bờ sông: Một bên là bờ kè bê tông đô thị kết hợp vựa nông sản, cầu tàu gỗ vươn xa; một bên là bãi bồi tự nhiên với nhà sàn gỗ san sát chân cọc cắm dưới nước.
* **Vùng Kênh Rạch (Hệ thống Thu Mua - Mô phỏng Kênh rạch Phong Điền / Ba Láng):**
  * Gồm 3 nhánh rạch chính uốn lượn ngoằn ngoèo đâm sâu vào đất liền, chiều dài mỗi nhánh `400m - 600m`, lòng kênh hẹp chỉ từ `15m - 30m`. Ép người chơi giảm tốc độ và xử lý bẻ lái góc hẹp.
  * Cao độ đáy thay đổi theo hệ thống Thủy Triều (`Water Level State`).
  * Bờ kênh: Bờ đất dốc tự nhiên, rặng dừa nước rủ lá, cầu khỉ gỗ, bến tre nhà vườn NPC.

---

## 2. CHI TIẾT BẢNG MÀU CHRONOLOGICAL LIGHTING & ENVIRONMENT PALETTE

Hệ thống `EnvironmentTimeDirector` sẽ trực tiếp nội suy (Lerp) các thông số của URP Volume, Directional Light và RenderSettings.Fog dựa theo thời gian thực (0.0 đến 24.0) với các mốc cấu hình HEX chuẩn sau:

### 2.1. 03:00 AM - 05:00 AM: Bình Minh Tối (Sương Mù & Tranh Bến)
* **Bầu trời:** `#050B14` | **Chân trời:** `#101B2B` | **Mặt trời:** `#1F2D42` (Int: `0.05`)
* **Sương mù (Fog):** `#0B121F` (Density: `0.08`) | **Mặt nước:** `#0A101A` | **Phù Sa:** `#141C26`

### 2.2. 05:30 AM - 07:00 AM: Hừng Đông Rực Rỡ (Bắt Đầu Giao Thương)
* **Bầu trời:** `#2B4C7E` | **Chân trời:** `#F26419` | **Mặt trời:** `#F4A261` (Int: `1.2` / Rot: `X: 8, Y: 75`)
* **Sương mù (Fog):** `#E76F51` (Density: `0.03`) | **Mặt nước:** `#8B6246` | **Phù Sa:** `#B5825F`

### 2.3. 08:00 AM - 11:30 AM: Nắng Sáng Chợ Nổi (Giao Thương Cao Điểm)
* **Bầu trời:** `#4EA8DE` | **Chân trời:** `#ADE8F4` | **Mặt trời:** `#FFF3D1` (Int: `1.5` / Rot: `X: 35, Y: 90`)
* **Sương mù (Fog):** `#CAF0F8` (Density: `0.005`) | **Mặt nước:** `#9C6644` | **Phù Sa:** `#C68A4C`

### 2.4. 13:00 PM - 15:30 PM: Đứng Bóng & Giông Nhiệt Đới (Len Lỏi Kênh Rạch)
* **Bầu trời:** `#5C677D` | **Chân trời:** `#ABC4FF` | **Mặt trời:** `#E2E2E2` (Int: `0.8` - Bóng thẳng đứng)
* **Sương mù (Fog):** `#95A5A6` (Density: `0.015`) | **Mặt nước:** `#5C4033` | **Phù Sa:** `#705335`

### 2.5. 16:30 PM - 18:00 PM: Hoàng Hôn Thượng Nguồn (Thu Mua & Sửa Ghe)
* **Bầu trời:** `#3D348B` | **Chân trời:** `#F7B267` | **Mặt trời:** `#F35B04` (Int: `1.3` / Rot: `X: 5, Y: 260`)
* **Sương mù (Fog):** `#E27396` (Density: `0.01`) | **Mặt nước:** `#7A431D` | **Phù Sa:** `#A0522D`

### 2.6. 18:30 PM - 20:00 PM: Chạng Vạng Lên Đèn (Xóm Nước Nghỉ Ngơi)
* **Bầu trời:** `#0B132B` | **Chân trời:** `#1C2541` | **Mặt trời:** Đã Tắt (Int: `0`)
* **Sương mù (Fog):** `#0B132B` (Density: `0.02`) | **Mặt nước:** `#050A14` | **Đèn phát xạ:** `#FFB703`

---

## 4. QUY TRÌNH TRIỂN KHAI KỸ THUẬT CHI TIẾT (GRANULAR TECH SPEC)

### BƯỚC 1: Khởi tạo Địa hình, Mặt nước & Vật lý Dòng chảy (Environment & Hydro-Physics)
* **Tác vụ 1.1 - Cấu hình Unity Terrain (Nặn Lòng Sông):**
  * Tạo Scene mới tại `Assets/_Project/Scenes/Main_ChoNoi.unity`. Tạo GameObject `[Environment]`.
  * Thêm Unity Terrain (Size `2048 x 2048`, Heightmap Res `1025`). Đặt vị trí Terrain tại tọa độ `X = -1024, Z = -1024` để tâm bản đồ nằm chính xác ở `0,0,0`.
  * Cấu hình cao độ gốc (Base Height) của toàn bộ mặt đất là `Y = 10m`.
  * **Sử dụng công cụ Sculpt (Đào):** Đào rãnh trục Sông Lớn rộng 200m xuyên tâm bản đồ, khoét sâu xuống `Y = -15m` (tránh ghe kẹt đáy). Đào 3 rãnh Kênh Rạch hẹp 20m uốn lượn ngoằn ngoèo, khoét sâu `Y = -5m`. Bờ kênh vuốt dốc (Smooth) để xuồng nhỏ có thể ủi bãi bùn.
  * *Texture cơ bản:* Sơn Layer 1 bằng texture đất bùn xám nâu (`Mud_Base`). Sơn viền mép nước bằng rêu xanh đen (`Moss_Dirt`).
* **Tác vụ 1.2 - Cấu hình Mesh Mặt Nước (Water Plane & Buoyancy Surface):**
  * Dưới `[Environment]`, tạo GameObject `Water_Surface`. Gắn Mesh Plane chuẩn kích thước `204.8 x 204.8` (Scale `204.8` trong Unity bằng 2048m). Set `Y = 0`.
  * Thêm Component `BoxCollider` (Đánh dấu `IsTrigger = true`, chỉnh Size Y = 20m, Center Y = -10m) bao trùm toàn bộ không gian dưới mặt nước.
  * Đặt Layer của object này là `Water`. Gắn Material nước `Water_PhuSa`.
  * Tạo script `WaterBuoyancySurface.cs` (lưu ở `Scripts/Infrastructure/Physics/`). Script này dùng `OnTriggerStay` để lấy độ sâu xâm nhập mặt nước của các vật thể (Ghe, Lục bình) và áp dụng lực đẩy lên trên (Archimedes Force) vào `Rigidbody` của chúng.
* **Tác vụ 1.3 - Hệ thống Lực đẩy Dòng chảy (Directional Current Zones):**
  * Sông uốn lượn không thể dùng 1 lực chung. Phân vùng dòng chảy bằng các Trigger Zones.
  * Tạo script `RiverCurrentZone.cs` (lưu ở `Scripts/Infrastructure/Physics/`). Script khai báo `public Vector3 flowDirection` và `public float flowForce`.
  * Đặt các `BoxCollider` tàng hình (IsTrigger) dọc theo các khúc cua của Kênh Rạch và Sông Lớn. Xoay (Rotate) các BoxCollider này uốn lượn theo hướng nước chảy mong muốn. Khi Ghe/Lục bình đi vào Zone nào, sẽ bị AddForce đẩy xuôi dòng theo trục Z cục bộ (local Z) của Zone đó.
* **Tác vụ 1.4 - Thiết lập Giới hạn Biên & Che Khuất Tầm Nhìn (Set Dressing Borders):**
  * Tạo GameObject `Border_Blockers`. Dùng 4 `BoxCollider` tàng hình cao 50m chặn lại thành hình vuông kích thước `1200m x 1200m` tại tâm map. Gắn layer `EnvironmentBoundary` (Cấu hình Physics Matrix chỉ chặn Ghe người chơi, bỏ qua Camera/Raycast).
  * Chạy dọc mép ngoài hàng rào tàng hình này, trồng dày đặc các Prefab dừa nước Low-Poly (chỉ có mesh, không có collider) và các túp lều lá mục nát để tạo hiệu ứng "Vạn lý trường thành miền Tây", đánh lừa thị giác người chơi rằng bản đồ còn kéo dài bất tận.

### BƯỚC 2: Phát triển Hệ thống Thời gian & Ánh sáng (Time & Weather Architecture)
* **Tác vụ 2.1 - Data Layer (Cấu hình Dữ liệu):** Tạo `TimeOfDayConfigSO.cs` (`Scripts/ScriptableObjects/Configs/`) kế thừa ScriptableObject, dùng Gradient và AnimationCurve định nghĩa thông số 24h.
* **Tác vụ 2.2 - Domain Layer:** Tạo `ITimeSystem.cs` (`Scripts/Domain/Systems/`) chứa thuộc tính CurrentTime và event.
* **Tác vụ 2.3 - Systems Layer:** Tạo `EnvironmentTimeDirector.cs` (`Scripts/Systems/Environment/`). Đọc ConfigSO để Lerp giá trị `RenderSettings.fog`, `RenderSettings.ambientLight` và `Light.intensity` của Directional Light, xoay Mặt trời từ Đông sang Tây.

### BƯỚC 3: Hệ thống Quản lý Thực vật & Vật cản (Object Pooling System)
* **Tác vụ 3.1 - Core Pooling System:** Khởi tạo `IPoolable.cs` và `GameObjectPool.cs` (`Scripts/Infrastructure/Pool/`) để tái sử dụng Lục bình/Rác.
* **Tác vụ 3.2 - Spawner Môi Trường:** Tạo `RiverDebrisSpawner.cs`. Đặt SpawnPoints ở đầu nguồn kênh rạch. Tự động spawn lục bình, để hệ thống `RiverCurrentZone` đẩy trôi dọc dòng sông, sau đó tự hủy (Return Pool) ở hạ lưu sông lớn.

### BƯỚC 4: Lập trình Shader Nước Phù Sa URP (Water Shader Graph Pipeline)
* Thiết lập `Water_PhuSa.shadergraph`. Dùng `Scene Depth` trừ `Screen Position W` để tính độ đục (Alpha min 0.7).
* Kết hợp Albedo `#9C6644` và Subsurface `#C68A4C`. Dùng node `Time` kéo `Normal Map` gợn sóng chạy dọc mặt nước.

---