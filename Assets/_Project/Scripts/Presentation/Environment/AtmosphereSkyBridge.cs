/**
 * AtmosphereSkyBridge: Cầu nối thời gian -> khí quyển (ánh sáng, sương mù, ambient, màu nước).
 * [Chức năng]: Nghe event OnTimeNormalized của TimeManager, đọc AtmosphericProfileSO để nội suy
 *              theo giờ (t01 = 0..1): màu/cường độ + góc xoay Directional Light (góc Isometric đổ
 *              bóng), RenderSettings.fog (density + color), ambient Sky/Equator/Ground, và màu nước
 *              phù sa (_BaseColor của Material mặt nước). Thay thế EnvironmentController cũ.
 *              Áp trực tiếp theo event (mỗi phút game) — TUYỆT ĐỐI không dùng Update().
 *              KHÔNG tham chiếu UI (đúng dev1-systems-rules) — chỉ render môi trường.
 * [Dependencies]: TimeManager (Application); AtmosphericProfileSO (Infrastructure).
 */

using UnityEngine;
using ChoNoi.Application;
using ChoNoi.Infrastructure;

namespace ChoNoi.Presentation.Environment
{
    public class AtmosphereSkyBridge : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private TimeManager timeManager;
        [SerializeField] private AtmosphericProfileSO profile;
        [SerializeField] private Light directionalLight;
        // Renderer mặt nước phù sa — sẽ đổi màu _BaseColor theo giờ.
        [SerializeField] private Renderer waterRenderer;

        [Header("Cấu hình")]
        // Góc quay quanh trục Y của mặt trời — tạo bóng đổ chéo kiểu Isometric.
        [SerializeField] private float sunYaw = 35f;
        // Tên property màu trên shader nước (URP Lit = "_BaseColor", Built-in Standard = "_Color").
        [SerializeField] private string waterColorProperty = "_BaseColor";
        // Bật ghi đè màu ambient (Sky/Equator/Ground) theo profile.
        [SerializeField] private bool driveAmbient = true;

        // Material instance của nước (lấy 1 lần để tránh tạo material mỗi lần đổi màu).
        private Material waterMaterialInstance;
        private int waterColorId;

        [Header("Editor Preview — kéo để xem khí quyển tức thì")]
        [SerializeField, Range(0f, 24f)] private float editorPreviewHour = 6f;
        [SerializeField] private bool applyInEditor = true;

        private void Awake()
        {
            waterColorId = Shader.PropertyToID(waterColorProperty);
            if (waterRenderer != null)
                waterMaterialInstance = waterRenderer.material; // instance riêng để SetColor an toàn
        }

        private void OnEnable()
        {
            if (timeManager != null)
                timeManager.OnTimeNormalized += ApplyAtmosphere;
        }

        private void OnDisable()
        {
            if (timeManager != null)
                timeManager.OnTimeNormalized -= ApplyAtmosphere;
        }

        /// <summary>
        /// Áp toàn bộ thông số khí quyển tại thời điểm chuẩn hoá t01 (= giờ/24).
        /// </summary>
        /// <param name="t01">Thời gian trong ngày [0,1).</param>
        private void ApplyAtmosphere(float t01)
        {
            if (profile == null) return;

            // 1) Directional Light: màu + cường độ + xoay theo góc nắng (đổ bóng Isometric).
            if (directionalLight != null)
            {
                directionalLight.color = profile.EvaluateLightColor(t01);
                directionalLight.intensity = profile.EvaluateSunIntensity(t01);
                directionalLight.transform.rotation = Quaternion.Euler(profile.EvaluateSunPitch(t01), sunYaw, 0f);
            }

            // 2) Sương mù: bật + màu + mật độ (dày lúc rạng sáng, tan vào trưa).
            RenderSettings.fog = true;
            RenderSettings.fogColor = profile.EvaluateFogColor(t01);
            RenderSettings.fogDensity = profile.EvaluateFogDensity(t01);

            // 3) Ánh sáng môi trường (ambient) theo 3 vùng Sky/Equator/Ground.
            if (driveAmbient)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                Color sky = profile.EvaluateSkyColor(t01);
                Color equator = profile.EvaluateEquatorColor(t01);
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = equator * 0.5f;
            }

            // 4) Màu nước phù sa đổi theo góc sáng (_BaseColor).
            if (waterMaterialInstance != null)
                waterMaterialInstance.SetColor(waterColorId, profile.EvaluateWaterColor(t01));
        }

#if UNITY_EDITOR
        /// <summary>Preview khí quyển ngoài Play mode khi kéo slider editorPreviewHour.</summary>
        private void OnValidate()
        {
            if (!applyInEditor || UnityEngine.Application.isPlaying) return;
            waterColorId = Shader.PropertyToID(waterColorProperty);
            if (waterRenderer != null && waterMaterialInstance == null)
                waterMaterialInstance = waterRenderer.sharedMaterial;
            ApplyAtmosphere(Mathf.Repeat(editorPreviewHour, 24f) / 24f);
        }
#endif
    }
}
