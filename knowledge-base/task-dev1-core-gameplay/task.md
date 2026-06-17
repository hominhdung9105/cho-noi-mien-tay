# Kế Hoạch Phân Công Công Việc & Quy Tắc Ghép Code (Merge)

Dự án được xây dựng theo kiến trúc Clean Architecture, điều này rất lý tưởng để chia việc mà không bị dẫm chân lên nhau. Dưới đây là đề xuất phân chia cho 4 thành viên (2 Dev, 1 3D Artist, 1 Animator/Tech Art) và các quy tắc để đảm bảo ghép (merge) vào thành một game hoàn chỉnh mà không bị "bể" dự án.

---

## BẢNG PHÂN CÔNG (4 THÀNH VIÊN)

### 🧑‍💻 Thành viên 1: Dev Systems & Environment (Core Gameplay)
**Nhiệm vụ:**
- **Hệ thống Thủy Triều:** Viết script cho phép mặt nước hạ xuống, sinh ra các vùng kẹt cạn (Collider động) ở kênh rạch nhỏ.
- **Vòng lặp Ngày/Đêm thực sự:** Code logic kích hoạt các pha (Phases).
- **Hệ thống Thể lực & Nhiên liệu (Stamina & Fuel):** Thêm biến `Fuel` vào `BoatStats`, viết logic tiêu hao nhiên liệu trong `BoatController`. Thêm biến `Stamina` vào `PlayerStats`.

**Vùng làm việc (Folder):**
- `Scripts/Domain/`, `Scripts/Presentation/Environment/`, `Scripts/Presentation/BoatController.cs`

### 🧑‍💻 Thành viên 2: Dev UI & Economy (Giao diện & Kinh tế)
**Nhiệm vụ:**
- **Hệ thống Hội Thoại (Dialogue):** Xây dựng hệ thống hội thoại Data-driven (đọc từ ScriptableObject) có phân nhánh.
- **Cơ chế Thu Mua Nhà Vườn (Phase 2):** Viết logic khi người chơi tới nhà vườn rạch nhỏ có thể mua hàng giá gốc.
- **Cập nhật UI:** Hiển thị thanh Thể Lực, thanh Nhiên Liệu, và UI Hội thoại.

**Vùng làm việc (Folder):**
- `Scripts/Presentation/UI/`, `Scripts/Systems/`, `Scripts/Infrastructure/` (Tạo ScriptableObject mới).

### 🎨 Thành viên 3: 3D Artist (Môi trường & Vật phẩm)
**Nhiệm vụ:**
- **Model Vật Phẩm:** Tạo 3D model Khóm, Bí đao, Củ sắn, Cây bẹo...
- **Model Môi Trường & Chướng ngại vật:** Tạo nhà vườn, quán cà phê võng, Lục bình, cọc tiêu.
- **Material/Texture:** Áp material đất bùn, cỏ, rêu cho địa hình (Terrain) và đảm bảo tính nhất quán (Art Style) cho tất cả các ghe và bến bãi.

**Vùng làm việc (Folder):**
- `Art/Models/`, `Art/Materials/`, `Art/Textures/`

### 🎬 Thành viên 4: Animator & VFX (Sức sống của game)
**Nhiệm vụ:**
- **Animation Nhân vật:** Gắn xương (Rigging) và làm chuyển động cho NPC (chèo ghe, vẫy tay gọi khách, cãi giá).
- **Animation Người chơi:** Chuyển động lấy hàng, treo hàng lên cây bẹo.
- **VFX & Particles:** Tạo hiệu ứng rẽ nước (sóng) đuôi ghe, khói nhả từ máy đuôi tôm tạch tạch, bọt nước khi mắc cạn.

**Vùng làm việc (Folder):**
- `Animations/`, `Art/VFX/`, `Prefabs/` (Gắn Animator và ParticleSystem vào Prefab ghe/nhân vật).

---

## 🛑 QUY TẮC LÀM VIÊC NHÓM (CHỐNG CONFLICT KHI MERGE)

Để 4 người cùng làm một Project Unity mà không bị đè file của nhau dẫn đến hỏng Scene, BẮT BUỘC tuân thủ các quy tắc sau:

### 1. Quy tắc KHÔNG ĐƯỢC CHẠM VÀO SCENE CHÍNH
- **Tuyệt đối cấm:** Hai người cùng lúc mở file `MainScene.unity` và lưu lại. Nếu làm vậy, khi Push code lên Git sẽ bị Conflict vỡ file Scene không thể cứu được.
- **Giải pháp:** Mọi người đều làm việc trên **Prefab** và **Scene Nháp (Test Scene)**.
  - *Ví dụ:* Thành viên 2 làm UI thì mở Scene trắng, tạo UI, xong kéo thả cục UI đó thành file `BargainUI.prefab`. Khi merge, chỉ việc mở Scene chính kéo file Prefab đó vào.

### 2. Quy tắc Chia File Script (Clean Architecture)
- Do đã tách lớp rõ ràng, Dev 1 và Dev 2 hạn chế sửa chung một file `Controller.cs`.
- Nếu Dev 1 cần gửi dữ liệu Thể lực cho UI của Dev 2, Dev 1 chỉ cần viết event `public event Action<float> OnStaminaChanged;` vào file `PlayerStats.cs`. Dev 2 sẽ tự sang file `UIManager.cs` để lắng nghe event đó, không ai phải đụng vào file của ai.

### 3. Quy tắc ScriptableObject (Data-Driven)
- Dữ liệu như Giá cả hàng hóa, Nội dung câu thoại, Thông số Ghe... đều phải lưu thành `ScriptableObject` (file `.asset`).
- Khi sửa cân bằng game hay thêm câu thoại, chỉ cần sửa file `.asset`, không cần đụng vào file code C#. Điều này giúp Dev và Game Designer làm việc song song mà không sợ hỏng code.

### 4. Quy tắc quản lý Git (Branching)
- Không ai được commit thẳng vào nhánh `main`.
- Mỗi người tự tạo nhánh riêng cho tính năng của mình (Ví dụ: `feature/tide-system`, `art/models`, `feature/dialogue-ui`).
- Khi hoàn thành tính năng, tạo Pull Request để thành viên khác xem qua (Review) rồi mới được Merge vào nhánh chính. Mọi thư mục làm việc đã chia rất rạch ròi nên khi Merge tự động sẽ không báo lỗi.
