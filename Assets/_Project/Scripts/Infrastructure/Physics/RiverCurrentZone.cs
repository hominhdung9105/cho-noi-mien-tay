/**
 * RiverCurrentZone: Vùng dòng chảy sông — đẩy vật thể xuôi dòng.
 * [Chức năng]: BoxCollider IsTrigger phủ dọc khúc sông/kênh. Khi Rigidbody đi vào Zone,
 *              AddForce theo hướng dòng chảy cục bộ (local Z → world) với lực cấu hình được.
 *              Thêm nhiễu turbulence nhỏ ngẫu nhiên để dòng chảy trông tự nhiên hơn,
 *              không đẩy thẳng băng như trên đường ray.
 *              Xoay (Rotate) GameObject Zone theo hướng kênh rạch uốn lượn để dòng chảy đúng.
 *              Không dùng Update() — toàn bộ logic trong OnTriggerStay (Physics callback).
 * [Dependencies]: Rigidbody (vật thể bị ảnh hưởng — ghe, lục bình, rác).
 */

using UnityEngine;

namespace ChoNoi.Infrastructure.Physics
{
    [RequireComponent(typeof(Collider))]
    public class RiverCurrentZone : MonoBehaviour
    {
        [Header("Thông số Dòng Chảy")]
        // Hướng dòng chảy trong không gian cục bộ (local space) của Zone.
        // Local Vector3.forward = chiều dương Z cục bộ → xoay GameObject để điều hướng dòng chảy.
        [SerializeField] private Vector3 flowDirection = Vector3.forward;
        // Lực đẩy (Newton). 5N = nhẹ như rạch nhỏ; 20N = mạnh như dòng sông Hậu thời lũ.
        [SerializeField] private float flowForce = 5f;
        // Biên độ nhiễu ngẫu nhiên (0 = dòng thẳng, 0.2 = cuộn xoáy nhẹ, >0.5 = rối loạn).
        [SerializeField, Range(0f, 0.5f)] private float turbulenceFactor = 0.08f;

        [Header("Layer Filter")]
        // Chỉ tác động lên các layer này (e.g. Boat, FloatingObject). 0 = tất cả.
        [SerializeField] private LayerMask affectedLayers = ~0;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning("[RiverCurrentZone] Collider đã được đặt IsTrigger=true tự động.");
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if ((affectedLayers.value & (1 << other.gameObject.layer)) == 0) return;

            Rigidbody rb = other.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;

            // Chuyển hướng từ local sang world space (theo chiều xoay của Zone trên bản đồ).
            Vector3 worldFlow = transform.TransformDirection(flowDirection).normalized;

            // Thêm nhiễu Perlin/Random nhỏ trên mặt phẳng ngang — giữ trục Y nguyên.
            Vector3 turbulence = new Vector3(
                Random.Range(-1f, 1f),
                0f,
                Random.Range(-1f, 1f)) * turbulenceFactor;

            rb.AddForce((worldFlow + turbulence) * flowForce, ForceMode.Force);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Vẽ mũi tên cyan chỉ hướng dòng chảy trong Scene view để kiểm tra luồng di chuyển.
            Vector3 origin = transform.position;
            Vector3 worldDir = transform.TransformDirection(flowDirection).normalized;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + worldDir * 3f);

            // Đầu mũi tên (arrowhead) thủ công.
            Vector3 tip  = origin + worldDir * 3f;
            Vector3 right = Vector3.Cross(worldDir, Vector3.up).normalized;
            Gizmos.DrawLine(tip, tip - worldDir * 0.5f + right * 0.3f);
            Gizmos.DrawLine(tip, tip - worldDir * 0.5f - right * 0.3f);

            // Viền BoxCollider bằng màu xanh mờ để xác nhận vùng phủ.
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.12f);
                Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            }
        }
#endif
    }
}
