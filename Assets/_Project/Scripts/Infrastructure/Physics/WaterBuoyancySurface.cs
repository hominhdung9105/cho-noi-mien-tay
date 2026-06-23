/**
 * WaterBuoyancySurface: Lực nổi Archimedes từ phía mặt nước — đẩy MỌI vật thể lên.
 * [Chức năng]: Gắn trên GameObject mặt nước (BoxCollider IsTrigger). Dùng OnTriggerStay
 *              để phát hiện Rigidbody xâm nhập, tính độ chìm (submergedDepth), rồi áp
 *              lực đẩy lên (Archimedes) + giảm chấn theo vận tốc Y. Hoạt động với mọi
 *              vật có Rigidbody: ghe, lục bình, rác hữu cơ, hàng hoá rơi xuống sông.
 *              Khác BoatBuoyancy.cs (gắn trên ghe, spring-damper) — file này gắn trên NƯỚC.
 *              TideController gọi SetWaterLevel() khi mực nước thay đổi.
 *              Không dùng Update() — toàn bộ logic trong OnTriggerStay (Physics callback).
 * [Dependencies]: Rigidbody (vật thể trong nước); TideController (tuỳ chọn, gọi SetWaterLevel).
 */

using UnityEngine;

namespace ChoNoi.Infrastructure.Physics
{
    [RequireComponent(typeof(Collider))]
    public class WaterBuoyancySurface : MonoBehaviour
    {
        [Header("Mặt nước")]
        // Cao độ Y của mặt nước world-space. TideController gọi SetWaterLevel() để cập nhật.
        [SerializeField] private float waterSurfaceY = 0f;

        [Header("Thông số Archimedes")]
        // Mật độ nước hiệu dụng (kg/m³). Tăng để đẩy mạnh hơn — 1000 = nước ngọt chuẩn.
        [SerializeField] private float buoyancyDensity = 1000f;
        // Hệ số giảm chấn theo vận tốc Y — chống vật bập bênh mãi trên mặt nước.
        [SerializeField] private float dragCoefficient = 0.8f;
        // Giới hạn lực đẩy tối đa mỗi vật (N) — tránh vật nhẹ bị bắn tung lên trời.
        [SerializeField] private float maxBuoyancyForce = 5000f;

        [Header("Layer Filter")]
        // Chỉ áp lực nổi cho các layer này (e.g. Boat, FloatingObject). 0 = tất cả.
        [SerializeField] private LayerMask affectedLayers = ~0;

        private void Awake()
        {
            // Đảm bảo Collider là trigger — nếu không engine sẽ dùng làm va chạm rắn.
            Collider col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning("[WaterBuoyancySurface] Collider đã được đặt IsTrigger=true tự động.");
            }
        }

        /// <summary>
        /// Gọi bởi TideController mỗi khi mực nước thay đổi (thủy triều).
        /// Cập nhật cao độ Y dùng để tính độ chìm.
        /// </summary>
        public void SetWaterLevel(float y) => waterSurfaceY = y;

        private void OnTriggerStay(Collider other)
        {
            // Lọc layer — nếu layer không được phép, bỏ qua.
            if ((affectedLayers.value & (1 << other.gameObject.layer)) == 0) return;

            Rigidbody rb = other.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;

            // Tính độ sâu xâm nhập: dương = phần vật nằm DƯỚI mặt nước, cần đẩy lên.
            float submergedDepth = waterSurfaceY - other.transform.position.y;
            if (submergedDepth <= 0f) return;

            // Lực nổi Archimedes: F_b = ρ * g * V_submersed ≈ density * g * depth (xấp xỉ tuyến tính).
            float rawBuoyancy = submergedDepth * buoyancyDensity * Mathf.Abs(UnityEngine.Physics.gravity.y);
            float clampedBuoyancy = Mathf.Min(rawBuoyancy, maxBuoyancyForce);
            rb.AddForce(Vector3.up * clampedBuoyancy, ForceMode.Force);

            // Giảm chấn ngược chiều vận tốc Y: tắt dao động lên xuống sau khi đạt cân bằng.
            float dampingForce = -rb.linearVelocity.y * rb.mass * dragCoefficient;
            rb.AddForce(Vector3.up * dampingForce, ForceMode.Force);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Vẽ mặt phẳng nước màu xanh mờ tại waterSurfaceY để dễ kiểm tra trong Scene view.
            Collider col = GetComponent<Collider>();
            if (col == null) return;

            Bounds b = col.bounds;
            Gizmos.color = new Color(0.1f, 0.5f, 0.9f, 0.25f);
            Vector3 center = new Vector3(b.center.x, waterSurfaceY, b.center.z);
            Vector3 size   = new Vector3(b.size.x, 0.05f, b.size.z);
            Gizmos.DrawCube(center, size);

            Gizmos.color = new Color(0.1f, 0.5f, 0.9f, 0.8f);
            Gizmos.DrawWireCube(center, size);
        }
#endif
    }
}
