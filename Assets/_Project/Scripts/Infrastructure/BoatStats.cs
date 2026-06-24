/**
 * BoatStats: ScriptableObject chứa toàn bộ chỉ số vật lý của ghe.
 * [Chức năng]: Lưu trữ tham số cấu hình theo nguyên tắc Data-Driven.
 *              Mọi chỉ số đều chỉnh được trong Inspector, không hard-code.
 *              Gồm 6 nhóm: lực đẩy, lực cản nước, lực cản ngang, mô-men lái, tải trọng, vận tốc tối đa.
 * [Dependencies]: Không có.
 */

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
    }
}
