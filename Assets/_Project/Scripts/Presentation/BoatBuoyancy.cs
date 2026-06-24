/**
 * BoatBuoyancy: Lực nổi giữ ghe bám theo mặt nước (Tide).
 * [Chức năng]: Mỗi FixedUpdate, đo độ chìm so với mặt nước (waterSurface.position.y) và đẩy ghe
 *              lên bằng mô hình lò xo–giảm chấn (Archimedes đơn giản). Khi thủy triều hạ, ghe hạ
 *              theo; khi nước xuống dưới đáy sông, Collider đáy chặn lại -> ghe mắc cạn (grounded).
 *              Ghe cần bật Use Gravity để có thể lắng xuống chạm đáy.
 * [Dependencies]: Rigidbody; Transform mặt nước (do EnvironmentController điều khiển Y).
 */

using UnityEngine;

namespace ChoNoi.Presentation
{
    [RequireComponent(typeof(Rigidbody))]
    public class BoatBuoyancy : MonoBehaviour
    {
        [Header("Tham chiếu mặt nước")]
        // Cùng Transform mà EnvironmentController dịch chuyển trục Y theo thủy triều.
        [SerializeField] private Transform waterSurface;

        [Header("Thông số nổi")]
        // Mớn nước: phần thân ghe chìm dưới mặt nước khi nổi cân bằng.
        [SerializeField] private float floatHeight = 0.2f;
        // Độ cứng lò xo nổi — càng lớn ghe càng "bật" lên mặt nước nhanh.
        [SerializeField] private float buoyancyStrength = 20f;
        // Giảm chấn theo vận tốc Y — chống ghe nảy lên xuống mãi.
        [SerializeField] private float damping = 2f;

        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (waterSurface == null) return;

            // depth > 0 nghĩa là điểm nổi của ghe đang nằm DƯỚI mặt nước -> cần đẩy lên.
            float waterY = waterSurface.position.y;
            float depth = (waterY - floatHeight) - transform.position.y;

            if (depth > 0f)
            {
                // F = k * depth - c * v_y  (lò xo trừ giảm chấn), bỏ qua khối lượng (Acceleration).
                float upForce = depth * buoyancyStrength - rb.linearVelocity.y * damping;
                rb.AddForce(Vector3.up * upForce, ForceMode.Acceleration);
            }
            // depth <= 0: ghe ở trên mặt nước -> để trọng lực (Use Gravity) kéo xuống tự nhiên,
            // tới khi chạm đáy sông (Collider) thì BoatController xử lý mắc cạn.
        }
    }
}
