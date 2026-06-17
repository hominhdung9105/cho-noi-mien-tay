/**
 * TideController: Hệ thống Thủy Triều — hạ/nâng mực nước và sinh bãi cạn vật lý động.
 * [Chức năng]: Nghe event OnTimeChanged của TimeManager, đọc đường cong mực nước trong
 *              AtmosphericProfileSO để tịnh tiến trục Y của GameObject Nước (Water Mesh).
 *              Khi mực nước rút xuống dưới ngưỡng an toàn (groundingThreshold), BẬT các
 *              Collider bãi bùn ngầm (mudflat) ở kênh rạch nông để khóa lối đi; khi nước
 *              dâng lại thì TẮT chúng. Là chủ sở hữu DUY NHẤT của water-Y (EnvironmentController
 *              chỉ lo ánh sáng/sương mù) để tránh hai script tranh chấp trục Y.
 *              Nội suy mượt bằng Coroutine tự tắt — TUYỆT ĐỐI không dùng Update() mỗi frame.
 *              KHÔNG tham chiếu UI (UnityEngine.UI / TMPro) — chỉ tính số thô + đổi vật lý.
 * [Dependencies]: TimeManager (Application); AtmosphericProfileSO (Infrastructure).
 */

using System.Collections;
using UnityEngine;
using ChoNoi.Application;
using ChoNoi.Infrastructure;

namespace ChoNoi.Presentation.Environment
{
    public class TideController : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private TimeManager timeManager;
        [SerializeField] private AtmosphericProfileSO profile;
        // GameObject Nước (Mesh + Collider mặt nước). Chỉ trục Y bị tịnh tiến theo thủy triều.
        [SerializeField] private Transform waterTransform;

        [Header("Bãi cạn động (Mudflat)")]
        // Các Collider bãi bùn ngầm dưới rạch nông. Tắt khi nước cao, bật khi nước rút thấp.
        [SerializeField] private Collider[] mudflatColliders;
        // Ngưỡng mực nước (Y, world-space): nước < ngưỡng này -> lộ bãi cạn, bật collider.
        [SerializeField] private float groundingThreshold = -1f;

        [Header("Chuyển tiếp")]
        // Tốc độ "đuổi" mực nước hiển thị tới mục tiêu (đơn vị giờ-ảo / giây-thực).
        [SerializeField] private float transitionSpeed = 6f;

        [Header("Editor Preview — kéo để xem thủy triều tức thì")]
        [SerializeField, Range(0f, 24f)] private float editorPreviewHour = 8f;
        [SerializeField] private bool applyInEditor = true;

        // Giờ đang hiển thị và giờ mục tiêu (0..24) — phục vụ nội suy mượt qua nửa đêm.
        private float displayedHour = 8f;
        private float targetHour = 8f;
        private Coroutine transition;
        // Trạng thái lộ bãi cạn lần cuối — chỉ set lại collider khi trạng thái thực sự đổi.
        private bool mudflatExposed;
        private bool mudflatStateInitialized;

        private void OnEnable()
        {
            if (timeManager != null)
                timeManager.OnTimeChanged += HandleTimeChanged;
        }

        private void OnDisable()
        {
            if (timeManager != null)
                timeManager.OnTimeChanged -= HandleTimeChanged;
            if (transition != null) { StopCoroutine(transition); transition = null; }
        }

        /// <summary>
        /// Nhận thời gian thô (giờ, phút) từ TimeManager -> quy về giờ thực 0..24
        /// và khởi động Coroutine nội suy nếu chưa chạy.
        /// </summary>
        /// <param name="hour">Giờ trong ngày 0-23.</param>
        /// <param name="minute">Phút trong giờ 0-59.</param>
        private void HandleTimeChanged(int hour, int minute)
        {
            targetHour = hour + minute / 60f;
            if (transition == null)
                transition = StartCoroutine(TransitionRoutine());
        }

        /// <summary>
        /// Coroutine nội suy displayedHour -> targetHour rồi TỰ TẮT khi tới nơi.
        /// Tránh tính toán mỗi frame trong Update() (theo dev1-systems-rules.md).
        /// </summary>
        private IEnumerator TransitionRoutine()
        {
            while (true)
            {
                // Đường ngắn nhất trên vòng 24h (xử lý wrap 23h -> 0h) qua phép quy về độ.
                float deltaDeg = Mathf.DeltaAngle(displayedHour / 24f * 360f, targetHour / 24f * 360f);
                if (Mathf.Abs(deltaDeg) < 0.05f) break;

                float maxStep = transitionSpeed * Time.deltaTime;                  // giờ/giây
                float stepHours = Mathf.Min(maxStep, Mathf.Abs(deltaDeg) / 360f * 24f);
                displayedHour = Mathf.Repeat(displayedHour + Mathf.Sign(deltaDeg) * stepHours, 24f);
                ApplyTide(displayedHour);
                yield return null;
            }

            displayedHour = targetHour;
            ApplyTide(displayedHour);
            transition = null;   // tắt coroutine — không còn tốn CPU
        }

        /// <summary>
        /// Áp mực nước + trạng thái bãi cạn tại 1 mốc giờ (instant) — dùng cho cả runtime & editor.
        /// </summary>
        /// <param name="hour">Giờ trong ngày 0..24.</param>
        private void ApplyTide(float hour)
        {
            if (profile == null) return;

            // Bước 1: Tính mực nước Y theo đường cong thủy triều của profile (chuẩn hóa giờ/24).
            float t = Mathf.Repeat(hour, 24f) / 24f;
            float waterY = profile.EvaluateWaterHeight(t);

            // Bước 2: Tịnh tiến trục Y của mặt nước (giữ nguyên X/Z).
            if (waterTransform != null)
            {
                Vector3 p = waterTransform.position;
                p.y = waterY;
                waterTransform.position = p;
            }

            // Bước 3: Lộ bãi cạn khi nước rút dưới ngưỡng -> bật/tắt collider (chỉ khi trạng thái đổi).
            UpdateMudflatColliders(waterY);
        }

        /// <summary>
        /// Bật Collider bãi bùn khi mực nước rút dưới ngưỡng (lộ bãi cạn), tắt khi nước dâng.
        /// Chỉ ghi lại trạng thái khi nó thực sự thay đổi để tránh set collider thừa mỗi bước nội suy.
        /// </summary>
        /// <param name="waterY">Mực nước Y hiện tại (world-space).</param>
        private void UpdateMudflatColliders(float waterY)
        {
            bool exposed = waterY < groundingThreshold;
            if (mudflatStateInitialized && exposed == mudflatExposed) return;

            mudflatExposed = exposed;
            mudflatStateInitialized = true;

            if (mudflatColliders != null)
            {
                for (int i = 0; i < mudflatColliders.Length; i++)
                {
                    if (mudflatColliders[i] != null)
                        mudflatColliders[i].enabled = exposed;
                }
            }

            Debug.Log($"[TideController] Muc nuoc Y={waterY:0.00}m -> bai can {(exposed ? "LO (collider BAT)" : "ngap (collider TAT)")}");
        }

#if UNITY_EDITOR
        /// <summary>
        /// Preview trong Edit mode: kéo slider editorPreviewHour -> thấy nước lên/xuống tức thì.
        /// Chỉ chạy ngoài Play mode để không can thiệp đồng hồ game lúc Play.
        /// </summary>
        private void OnValidate()
        {
            if (!applyInEditor || UnityEngine.Application.isPlaying) return;
            displayedHour = targetHour = editorPreviewHour;
            ApplyTide(editorPreviewHour);
        }
#endif
    }
}
