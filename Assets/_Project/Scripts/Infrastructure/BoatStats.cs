/**
 * BoatStats: ScriptableObject chứa toàn bộ chỉ số vật lý của ghe.
 * [Chức năng]: Lưu trữ tham số cấu hình theo nguyên tắc Data-Driven.
 *              Mọi chỉ số đều chỉnh được trong Inspector, không hard-code.
 *              Gồm các nhóm: lực đẩy, lực cản nước, lực cản ngang, mô-men lái, tải trọng,
 *              vận tốc tối đa và Nhiên Liệu (Fuel — Phase 3).
 *              Fuel phát event OnFuelChanged (Reactive) để UI của Dev 2 lắng nghe, BoatStats
 *              KHÔNG tham chiếu UI (tách lớp theo dev1-systems-rules.md).
 *              LƯU Ý: currentFuel là runtime-state nằm trong SO dùng chung -> BoatController phải
 *              gọi InitializeFuel() lúc Awake để reset mỗi phiên Play (tránh giá trị "dính").
 * [Dependencies]: System (Action event).
 */

using System;
using UnityEngine;

namespace ChoNoi.Infrastructure
{
    [CreateAssetMenu(fileName = "BoatStats", menuName = "ChoNoi/Data/Boat Stats")]
    public class BoatStats : ScriptableObject
    {
        [Header("Lực đẩy")]
        // Lực đẩy tiến áp lên Rigidbody theo transform.forward (ForceMode.Acceleration)
        [SerializeField] private float thrustForce = 10f;

        [Header("Lực cản nước")]
        // Hệ số cản theo chiều vận tốc tổng — mô phỏng ma sát nước
        [SerializeField] private float waterDrag = 2f;

        [Header("Lực cản ngang")]
        // Hệ số cản trượt ngang — giữ ghe chạy đúng hướng mũi, không trượt sang bên
        [SerializeField] private float sidewaysDrag = 5f;

        [Header("Mô-men lái")]
        // Mô-men xoắn bẻ lái, nhân thêm vận tốc để ghe chỉ quay khi đang chạy
        [SerializeField] private float turnTorque = 3f;

        [Header("Dịch chuyển phụ")]
        // Lực lướt ngang nhẹ để A/D vẫn tạo cảm giác ghe dịch 4 hướng, không chỉ quay tại chỗ.
        [SerializeField] private float lateralThrust = 2.25f;

        [Header("Độ trôi sông")]
        // Hệ số cản khi người chơi đang có input đẩy ghe.
        [SerializeField] private float activeDragFactor = 1f;
        // Hệ số cản khi thả phím. Thấp hơn 1 -> ghe trôi thêm một đoạn rồi mới dừng.
        [SerializeField] private float coastDragFactor = 0.45f;
        // Dòng chảy nền của sông, áp theo trục world-space.
        [SerializeField] private Vector3 riverCurrent = new Vector3(0.12f, 0f, 0.22f);

        [Header("Tải trọng")]
        // Hệ số phạt hiệu suất tối đa khi ghe đầy tải (0-1).
        // VD 0.4 = đầy hàng thì thrust/torque chỉ còn 60% (Performance = 1 - ratio*0.4).
        [SerializeField, Range(0f, 1f)] private float maxPenaltyFactor = 0.4f;

        [Header("Vận tốc tối đa")]
        // Trần tốc độ (m/s) khi độ bền đầy. Độ bền giảm sẽ khóa trần này thấp xuống
        // (tối thiểu 30%). Lưu ý: độ bền KHÔNG giảm thrustForce, chỉ giới hạn vận tốc.
        [SerializeField] private float baseMaxSpeed = 10f;

        [Header("Nhiên liệu (Fuel) — Phase 3")]
        // Dung tích bình xăng tối đa (đơn vị tùy ý, mặc định 100).
        [SerializeField] private float maxFuel = 100f;
        // Lượng xăng tiêu hao mỗi giây khi máy đuôi tôm chạy hết ga (throttle = 1).
        // Mức tiêu hao thực tế = fuelConsumptionRate * |throttle| * fixedDeltaTime.
        [SerializeField] private float fuelConsumptionRate = 2.5f;
        // Lượng xăng hiện tại (runtime). Reset về maxFuel qua InitializeFuel() lúc Awake.
        [SerializeField] private float currentFuel = 100f;

        /// <summary>
        /// Bắn ra mỗi khi lượng xăng thay đổi. Tham số: (currentFuel, maxFuel).
        /// UI Dev 2 subscribe để vẽ thanh Fuel — BoatStats tuyệt đối không đụng UI.
        /// </summary>
        public event Action<float, float> OnFuelChanged;

        public float ThrustForce      => thrustForce;
        public float WaterDrag        => waterDrag;
        public float SidewaysDrag     => sidewaysDrag;
        public float TurnTorque       => turnTorque;
        public float LateralThrust    => lateralThrust;
        public float ActiveDragFactor => activeDragFactor;
        public float CoastDragFactor  => coastDragFactor;
        public Vector3 RiverCurrent   => riverCurrent;
        public float MaxPenaltyFactor => maxPenaltyFactor;
        public float BaseMaxSpeed     => baseMaxSpeed;

        public float MaxFuel              => maxFuel;
        public float CurrentFuel          => currentFuel;
        public float FuelConsumptionRate  => fuelConsumptionRate;
        // True khi hết xăng -> BoatController ngắt lực đẩy động cơ.
        public bool  IsOutOfFuel          => currentFuel <= 0f;

        /// <summary>
        /// Nạp đầy bình và phát event. Gọi từ BoatController.Awake để reset runtime-state
        /// của SO dùng chung mỗi lần vào Play (tránh xăng "dính" từ phiên trước).
        /// </summary>
        public void InitializeFuel()
        {
            currentFuel = maxFuel;
            OnFuelChanged?.Invoke(currentFuel, maxFuel);
        }

        /// <summary>
        /// Trừ một lượng xăng (clamp >= 0) và phát OnFuelChanged nếu có thay đổi.
        /// </summary>
        /// <param name="amount">Lượng xăng tiêu hao (>= 0).</param>
        public void ConsumeFuel(float amount)
        {
            if (amount <= 0f || currentFuel <= 0f) return;

            currentFuel = Mathf.Max(0f, currentFuel - amount);
            OnFuelChanged?.Invoke(currentFuel, maxFuel);
        }

        /// <summary>
        /// Nạp thêm xăng (clamp <= maxFuel) và phát OnFuelChanged.
        /// </summary>
        /// <param name="amount">Lượng xăng nạp thêm (>= 0).</param>
        public void Refuel(float amount)
        {
            if (amount <= 0f) return;

            currentFuel = Mathf.Min(maxFuel, currentFuel + amount);
            OnFuelChanged?.Invoke(currentFuel, maxFuel);
        }
    }
}
