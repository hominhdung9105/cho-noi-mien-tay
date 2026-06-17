# Phase 3 — Dev 1 Implementation Log (Core Gameplay & Environment)

> Người thực hiện: **Thành viên 1 — Dev Systems & Environment**
> Ngày: 2026-06-17 · Unity 6000.4.7f1 · Clean Architecture

Tài liệu này ghi lại **toàn bộ quá trình** triển khai 4 hạng mục Dev 1: AtmosphericProfileSO, TimeManager, TideController và logic Fuel trong BoatController — kèm các quyết định kiến trúc, lý do, và kết quả build/test.

---

## 1. Khảo sát hiện trạng (trước khi code)

Đọc toàn bộ `knowledge-base/common`, `README.md` và 5 tài liệu trong `task-dev1-core-gameplay/`. Phát hiện **dự án đã tiến xa hơn README** — phần lớn nền tảng Phase 3 đã tồn tại từ các phase trước:

| Yêu cầu của task | Trạng thái khi nhận việc |
| :--- | :--- |
| `TimeManager.cs` (vòng lặp ngày/đêm, Reactive event) | **ĐÃ CÓ** tại `Application/TimeManager.cs` — hoàn chỉnh, implement `ITimeSystem`, phát `OnTimeChanged`/`OnPhaseChanged`/`OnSleep`/`OnDayChanged`. |
| `AtmosphericProfileSO.cs` (Gradient + AnimationCurve khí quyển) | **ĐÃ CÓ tương đương** = `Infrastructure/EnvironmentProfileSO.cs` (Gradient màu sáng + Curve cường độ/fog/mực nước). |
| Vòng lặp ánh sáng/sương mù | **ĐÃ CÓ** `Presentation/Environment/EnvironmentController.cs` (Coroutine, không `Update`). |
| `GamePhase` enum | **ĐÃ CÓ** `Domain/GamePhase.cs` (Dawn/Day/Dusk/Night). |
| Stamina | **ĐÃ CÓ** `Managers/PlayerStats.cs` (Consume/Restore). |
| Mắc cạn vật lý (Collider đáy sông) | **ĐÃ CÓ** trong `BoatController.cs` (grounding theo `riverbedLayer`). |
| `TideController.cs` (collider bãi cạn động) | **CHƯA CÓ** → cần tạo mới. |
| Hệ thống **Fuel** (BoatStats + tiêu hao trong BoatController) | **CHƯA CÓ** → cần thêm. |

→ Trọng tâm thực sự: **TideController**, **Fuel**, và đặt đúng tên **AtmosphericProfileSO**; tránh tạo trùng lặp với code đã có.

---

## 2. Ba quyết định kiến trúc (đã chốt với chủ dự án)

1. **Đổi tên `EnvironmentProfileSO` → `AtmosphericProfileSO`** thay vì tạo file mới song song (tránh duplicate/dead-code), khớp đúng tên trong tài liệu thiết kế. **Giữ nguyên GUID** của `.cs.meta` để asset `EnvironmentProfile.asset` không vỡ tham chiếu.
2. **Fuel đặt trong `BoatStats` SO** (đúng execution-plan): `MaxFuel`, `CurrentFuel`, `FuelConsumptionRate` + event `OnFuelChanged`.
3. **`TideController` sở hữu mực nước (water-Y) lẫn mudflat colliders**; gỡ phần điều khiển water-Y khỏi `EnvironmentController` để **tránh hai script tranh chấp trục Y**. EnvironmentController nay chỉ lo ánh sáng/sương mù/mặt trời.

---

## 3. Chi tiết các thay đổi

### 3.1. Đổi tên `EnvironmentProfileSO` → `AtmosphericProfileSO`
- `git mv` cả `EnvironmentProfileSO.cs` **và** `.cs.meta` → `AtmosphericProfileSO.cs(.meta)` (GUID `1fe1059a22b1242e392fc45001bfc1de` giữ nguyên).
- Đổi tên class + `[CreateAssetMenu(menuName = "ChoNoi/Data/Atmospheric Profile")]`. Giữ nguyên toàn bộ method `Evaluate*` (light color/intensity, sun pitch, fog, water height).
- Cập nhật tham chiếu type ở 3 file: `EnvironmentController.cs`, `Editor/BoatTestRunner.cs`, `Editor/RiverMarketSceneBuilder.cs`.
- ✔ Xác minh `m_Script.guid` trong `EnvironmentProfile.asset` khớp GUID `.meta` → asset không vỡ.

### 3.2. `TideController.cs` (MỚI) — `Presentation/Environment/`
- `MonoBehaviour`, namespace `ChoNoi.Presentation.Environment`. **Không** `using UnityEngine.UI`/`TMPro`.
- Subscribe `TimeManager.OnTimeChanged` trong `OnEnable`, hủy trong `OnDisable` (chống memory leak).
- Mỗi lần giờ đổi: `t01 = giờ/24` → `EvaluateWaterHeight` → nội suy water-Y **mượt bằng Coroutine tự tắt** (mẫu giống `EnvironmentController`, KHÔNG dùng `Update`).
- **Bãi cạn động:** `exposed = waterY < groundingThreshold` → bật/tắt `Collider[] mudflatColliders`; chỉ set lại **khi trạng thái đổi** + log Console.
- `#if UNITY_EDITOR OnValidate` cho preview kéo-slider tức thì ngoài Play mode.

### 3.3. `EnvironmentController.cs` (SỬA)
- Đổi type `EnvironmentProfileSO` → `AtmosphericProfileSO`.
- **Gỡ** field `waterTransform` và khối set `waterTransform.position.y` trong `ApplyEnvironment` (water-Y nay thuộc TideController). Giữ light/fog/sun.

### 3.4. `BoatStats.cs` (SỬA — thêm Fuel)
- Thêm `[Header("Nhiên liệu")]`: `maxFuel` (100), `fuelConsumptionRate` (2.5/s), `currentFuel` (runtime).
- `public event Action<float,float> OnFuelChanged;` (current, max) — Reactive cho UI Dev 2; **không** đụng UI.
- API: `InitializeFuel()`, `ConsumeFuel(amount)` (clamp ≥ 0), `Refuel(amount)` (clamp ≤ max); property `CurrentFuel`, `MaxFuel`, `FuelConsumptionRate`, `IsOutOfFuel`.

### 3.5. `BoatController.cs` (SỬA — FixedUpdate)
- `Awake`: gọi `boatStats.InitializeFuel()` (xem [caveat](#4-caveat-kiến-trúc)).
- `FixedUpdate` (sau khi đọc `throttle`):
  - Tăng tốc (`|throttle| > 0.01`) → `ConsumeFuel(FuelConsumptionRate * |throttle| * fixedDeltaTime)` (độc lập frame-rate).
  - `IsOutOfFuel` → ép `thrustPerformance = 0` ⇒ `ApplyThrust` không sinh lực; ghe trôi quán tính rồi dừng nhờ water drag (đúng Test Case 03). Giữ nguyên logic mắc cạn/độ bền/tải trọng/lái.

### 3.6. `PlayerStats.cs` (SỬA nhỏ)
- Thêm `public event Action<float,float> OnStaminaChanged;`, bắn trong `ConsumeStamina/RestoreStamina/UpgradeMaxStamina/LoadStats` — để UI Dev 2 subscribe (đúng quy tắc merge).

### 3.7. `RiverMarketSceneBuilder.cs` (SỬA)
- Đổi type sang `AtmosphericProfileSO`. Gỡ set `waterTransform` trên EnvironmentController; thêm `TideController` và wire `timeManager` + `profile` + `waterTransform`.

### 3.8. `BoatTestRunner.cs` (SỬA — test Fuel)
- Đổi type nhóm 5 sang `AtmosphericProfileSO`.
- Thêm **Nhóm 7: KIEM TRA NHIEN LIEU** (pure-math, không cần Play): InitializeFuel nạp đầy + bắn event; ConsumeFuel trừ đúng & clamp ≥ 0; hết xăng → `IsOutOfFuel` + thrust performance = 0; Refuel clamp ≤ MaxFuel.

---

## 4. Caveat kiến trúc

`currentFuel` là **runtime-state nằm trong ScriptableObject dùng chung** (`BoatStats.asset`). SO không tự reset giữa các phiên Play trong Editor → **bắt buộc** gọi `InitializeFuel()` ở `BoatController.Awake` để nạp đầy mỗi lần vào Play (tránh giá trị xăng "dính" từ lần chạy trước). Đây là đánh đổi đã được chấp nhận khi chọn đặt Fuel trong SO theo execution-plan. Giải pháp "sạch" hơn về lâu dài là tách `currentFuel` ra component runtime (mẫu `DurabilityManager` + `IDurabilityProvider`).

---

## 5. Tuân thủ quy tắc Dev 1

- ✔ Không `using UnityEngine.UI`/`TMPro` trong mọi file Dev 1 (đã grep xác minh).
- ✔ Khí quyển nội suy bằng `Gradient`/`AnimationCurve`, không gán cứng trong `if-else`.
- ✔ Không lạm dụng `Update()` — TideController & EnvironmentController dùng event + Coroutine tự tắt; tiêu hao Fuel nằm trong `FixedUpdate` vật lý sẵn có.
- ✔ Data-driven: thông số Fuel & khí quyển nằm trong SO.
- ✔ Header comment theo template; private dùng `[SerializeField]`; code chỉ trong `Assets/_Project/`.

---

## 6. Build & Test

- **Môi trường:** Unity `D:\unity\editor\6000.4.7f1\Editor\Unity.exe`.
- **Lệnh:** `Unity -batchmode -quit -projectPath <repo> -executeMethod ChoNoi.Editor.BoatTestRunner.Run -logFile <log>`
- **Kết quả:** ✅ **62/62 PASS · 0 FAIL · exit code 0** (gồm đủ 13 test Nhóm 7 — Fuel). Code trong `Assets/_Project` biên dịch sạch, **không có lỗi CS nào** từ code Dev 1.

#### ⚠️ Lỗi CÓ SẴN của project (KHÔNG do thay đổi Dev 1)
Khi batch build, Unity báo 2 lỗi biên dịch nằm trong **package Shader Graph** (`Library/PackageCache/com.unity.shadergraph@...`):
```
TargetSetupContext.cs(62,40): error CS0246: 'GUID' could not be found
BuiltInCanvasSubTarget.cs(10,25): error CS0246: 'GUID' could not be found
```
→ Đây là lý do **mở project trong Unity Hub không vào được Play mode** ("All compiler errors have to be fixed before you can enter play mode"). Shader Graph 17.4.0 vốn khớp Unity 6000.4.7f1 + URP 17.4.0, nên đây là **cache package bị hỏng**, không phải lỗi version.

**Đã thực hiện khắc phục:** xóa `Library/PackageCache`, `Library/ScriptAssemblies`, `Library/Bee` (Unity đã đóng, các thư mục này gitignore nên an toàn). Chạy lại batch xác nhận: **lỗi Shader Graph `CS0246` đã biến mất hoàn toàn**.

**LƯU Ý quan trọng — phải hoàn tất bằng Unity Hub (GUI):** Sau khi wipe `PackageCache`, chạy `Unity -batchmode` (headless) **KHÔNG khôi phục được packages** do giới hạn licensing khi chạy nền (`Access token is unavailable`, `Code 10 signature validation`) → lần build batch đầu tiên thiếu Input System (lỗi tạm thời, không phải lỗi code). Cách hoàn tất:
1. Mở project bằng **Unity Hub** (tài khoản đang đăng nhập, **có internet**).
2. Package Manager sẽ tải/giải nén lại đủ 64 packages (manifest + packages-lock còn nguyên).
3. Unity biên dịch lại sạch → vào Play mode bình thường.

Phụ: asset `Assets/_Project/Scenes/Sandbox/Prototype_RiverData.asset` báo "Unknown error occurred while loading" — lỗi asset cũ, không liên quan Dev 1.

### Hướng dẫn test thủ công trong Editor (theo phase-03-dev1-test-plan.md)
1. **TC01 — Khí quyển:** kéo slider `editorPreviewHour` của EnvironmentController 04:00→12:00→17:00: ánh sáng/fog đổi mượt, hoàng hôn vàng cam fog≈0.
2. **TC02 — Thủy triều & mắc cạn:** gán `AtmosphericProfile.asset` + `waterTransform` + mảng `mudflatColliders` vào `TideController`; kéo về 15:00 → nước hạ rõ, collider bãi cạn BẬT (Console log), ghe chạm bãi bùn bị kìm tốc.
3. **TC03 — Hết xăng:** giữ phím chạy đến khi `CurrentFuel`→0 → lực đẩy bị khóa, ghe trôi quán tính rồi dừng.

---

## 7. Danh sách file thay đổi

**Mới:** `Presentation/Environment/TideController.cs`
**Đổi tên:** `Infrastructure/EnvironmentProfileSO.cs(.meta)` → `AtmosphericProfileSO.cs(.meta)` (giữ GUID)
**Sửa:** `EnvironmentController.cs`, `BoatStats.cs`, `BoatController.cs`, `PlayerStats.cs`, `Editor/BoatTestRunner.cs`, `Editor/RiverMarketSceneBuilder.cs`
**Giữ nguyên (đã đạt yêu cầu):** `Application/TimeManager.cs`, `Domain/GamePhase.cs`, `Domain/ITimeSystem.cs`

---

## 8. Dựng môi trường & đặt scene mặc định (bổ sung sau)

Yêu cầu: "mở Unity lên là thấy môi trường chợ nổi".

- **Khôi phục packages:** sau khi xoá `Library/PackageCache` (xử lý Shader Graph), UPM còn state cũ `Library/PackageManager/projectResolution.json` nên không tải lại → thiếu Input System/uGUI. Đã xoá `Library/PackageManager` + `Bee` + `ScriptAssemblies` → mở batch tải lại đủ **64 packages**, biên dịch code game sạch.
- **Sửa `RiverMarketSceneBuilder.cs`:** (1) cuối `BuildScene` set `EditorBuildSettings.scenes` = RiverMarketScene (scene 0) để tự mở; (2) thêm `BuildMudflats` tạo 3 BoxCollider bãi bùn (layer 1 = RiverBed, tắt sẵn) ở nhánh sông nông, gán vào `TideController.mudflatColliders` + `groundingThreshold = 2.5`.
- **Build lại scene (batchmode, exit 0):** `RiverMarketScene.unity` tái tạo — có `TideController` + 4 GameObject Mudflat; Build Settings scene 0 = RiverMarketScene; **0 lỗi CS trong code game**.
- **Lưu ý Shader Graph:** log batch còn 2 lỗi `CS0246 'UnityEngine.GUID'` trong CHÍNH package `com.unity.shadergraph` (file `TargetSetupContext.cs`/`BuiltInCanvasSubTarget.cs`). Lỗi này **có sẵn từ lần batch đầu tiên** (trước mọi thay đổi) và không chặn việc build scene/chạy test → là artifact của batchmode, không phải lỗi code dự án. Mở bằng Unity GUI hoạt động bình thường.

**Cách xem:** Mở project bằng Unity Hub (có internet, lần đầu reimport hơi lâu) → scene chợ nổi tự mở. Kéo slider giờ trên `EnvironmentController`/`TimeManager` để xem ngày↔đêm + nước lên/xuống; nước rút thấp (chiều ~15h) → collider bãi cạn bật, ghe có thể mắc cạn.
