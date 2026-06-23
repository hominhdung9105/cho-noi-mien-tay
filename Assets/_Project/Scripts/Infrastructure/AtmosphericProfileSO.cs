/**
 * AtmosphericProfileSO: ScriptableObject cấu hình bầu không khí (khí quyển) theo thời gian trong ngày.
 * [Chức năng]: Lưu Gradient màu sáng, AnimationCurve cường độ sáng / mật độ fog / mực nước /
 *              góc nắng theo trục 0..24h (chuẩn hóa 0..1). Tái hiện đúng sắc độ miền Tây:
 *              bình minh xanh lạnh + sương mù dày, hoàng hôn vàng cam gắt + fog = 0.
 *              Data-driven, chỉnh trong Inspector, không hard-code. AtmosphereSkyBridge &
 *              TideController gọi các hàm Evaluate để render ánh sáng/sương mù/màu nước/thủy triều.
 * [Dependencies]: Không có.
 */

using UnityEngine;

namespace ChoNoi.Infrastructure
{
    [CreateAssetMenu(fileName = "AtmosphericProfile", menuName = "ChoNoi/Data/Atmospheric Profile")]
    public class AtmosphericProfileSO : ScriptableObject
    {
        [Header("Ánh sáng (trục thời gian 0..1 = 0h..24h)")]
        [SerializeField] private Gradient lightColorOverDay;
        [SerializeField] private AnimationCurve lightIntensityOverDay = AnimationCurve.EaseInOut(0, 0, 1, 1);
        // Góc dốc (pitch) của mặt trời theo giờ — mô phỏng nắng lên/lặn (tùy chọn).
        [SerializeField] private AnimationCurve sunPitchOverDay = AnimationCurve.Linear(0, -90, 1, 270);

        [Header("Sương mù (Fog Density)")]
        [SerializeField] private AnimationCurve fogDensityOverDay = AnimationCurve.EaseInOut(0, 0.05f, 1, 0f);

        [Header("Màu khí quyển (Sky / Equator / Fog) — trục 0..1")]
        // Màu vòm trời (đỉnh): đêm xanh thẫm -> bình minh hồng cam -> trưa xanh trong -> hoàng hôn cam.
        [SerializeField] private Gradient skyColorGradient;
        // Màu chân trời / xích đạo (ambient equator) — sắc độ ở ngang tầm mắt.
        [SerializeField] private Gradient equatorColorGradient;
        // Màu sương mù theo giờ (sáng sớm trắng đục, hoàng hôn ám cam).
        [SerializeField] private Gradient fogColorGradient;

        [Header("Cường độ nắng riêng (Sun Intensity)")]
        // Đường cong cường độ nắng riêng cho AtmosphereSkyBridge (đỉnh ~1.5 lúc trưa, ~0.2 lúc 18h).
        [SerializeField] private AnimationCurve sunIntensityCurve = AnimationCurve.EaseInOut(0f, 0.15f, 1f, 0.15f);

        [Header("Màu nước phù sa (Silt Water) — đổi theo góc sáng")]
        // Nước phù sa nâu đục: sáng sớm xám lạnh -> trưa nâu vàng -> hoàng hôn nâu cam.
        [SerializeField] private Gradient waterColorGradient;

        [Header("Thủy triều (Tide) — Y của mặt nước")]
        [SerializeField] private float maxWaterHeight = 0f;    // Sáng: nước cao
        [SerializeField] private float minWaterHeight = -2f;   // Chiều: nước thấp (Low Tide)
        // Hệ số nội suy 0..1 theo giờ: 1 = maxWaterHeight, 0 = minWaterHeight.
        [SerializeField] private AnimationCurve waterLevelOverDay = AnimationCurve.EaseInOut(0, 1, 1, 1);

        [Header("Hậu xử lý (Post-Processing) — URP Volume")]
        // Saturation boost (đơn vị URP -100..+100): đỉnh +25 lúc 09:00 (chợ cao điểm), -10 lúc 03:00 (tiền bình minh).
        [SerializeField] private AnimationCurve saturationOverDay = new AnimationCurve(
            new Keyframe(0f,      0f),   // 00:00 — trung tính
            new Keyframe(0.125f, -10f),  // 03:00 — nhẹ desaturate
            new Keyframe(0.375f,  25f),  // 09:00 — đỉnh (chợ cao điểm, màu nông sản nổi bật)
            new Keyframe(0.583f,   5f),  // 14:00 — giảm dần
            new Keyframe(0.75f,   10f),  // 18:00 — ánh vàng hoàng hôn
            new Keyframe(1f,       0f)); // 24:00 — wrap về đêm

        // Vignette intensity (0..1): đỉnh 0.4 lúc 03:00 (mắt chưa thích nghi bóng tối), 0 ban ngày.
        [SerializeField] private AnimationCurve vignetteIntensityOverDay = new AnimationCurve(
            new Keyframe(0f,      0.30f), // 00:00 — đêm tối
            new Keyframe(0.125f,  0.40f), // 03:00 — đỉnh: mắt tiền bình minh
            new Keyframe(0.25f,   0.10f), // 06:00 — bình minh, bắt đầu tan
            new Keyframe(0.375f,  0f),    // 09:00 — ban ngày: không vignette
            new Keyframe(0.75f,   0.05f), // 18:00 — hoàng hôn nhẹ
            new Keyframe(1f,      0.30f));// 24:00 — wrap về đêm

        // Lens Distortion intensity (-1..1): âm = barrel distortion, đỉnh -0.15 lúc 03:00 (mất phương hướng).
        [SerializeField] private AnimationCurve lensDistortionOverDay = new AnimationCurve(
            new Keyframe(0f,      -0.08f),
            new Keyframe(0.125f,  -0.15f), // 03:00 — barrel distortion đỉnh
            new Keyframe(0.25f,   -0.03f),
            new Keyframe(0.375f,   0f),    // 09:00 — không méo
            new Keyframe(0.75f,   -0.02f),
            new Keyframe(1f,      -0.08f));

        public float MaxWaterHeight => maxWaterHeight;
        public float MinWaterHeight => minWaterHeight;

        /// <summary>Màu Directional Light tại thời điểm t01 (= giờ/24).</summary>
        public Color EvaluateLightColor(float t01)
            => lightColorOverDay != null ? lightColorOverDay.Evaluate(Mathf.Repeat(t01, 1f)) : Color.white;

        /// <summary>Cường độ Directional Light tại t01.</summary>
        public float EvaluateLightIntensity(float t01)
            => lightIntensityOverDay.Evaluate(Mathf.Repeat(t01, 1f));

        /// <summary>Góc dốc (pitch, độ) của mặt trời tại t01.</summary>
        public float EvaluateSunPitch(float t01)
            => sunPitchOverDay.Evaluate(Mathf.Repeat(t01, 1f));

        /// <summary>Mật độ sương mù tại t01 (không âm).</summary>
        public float EvaluateFogDensity(float t01)
            => Mathf.Max(0f, fogDensityOverDay.Evaluate(Mathf.Repeat(t01, 1f)));

        /// <summary>Cường độ nắng riêng (Sun Intensity) tại t01 — không âm.</summary>
        public float EvaluateSunIntensity(float t01)
            => Mathf.Max(0f, sunIntensityCurve.Evaluate(Mathf.Repeat(t01, 1f)));

        /// <summary>Màu vòm trời (sky) tại t01.</summary>
        public Color EvaluateSkyColor(float t01)
            => skyColorGradient != null ? skyColorGradient.Evaluate(Mathf.Repeat(t01, 1f)) : new Color(0.4f, 0.55f, 0.78f);

        /// <summary>Màu chân trời / ambient equator tại t01.</summary>
        public Color EvaluateEquatorColor(float t01)
            => equatorColorGradient != null ? equatorColorGradient.Evaluate(Mathf.Repeat(t01, 1f)) : new Color(0.55f, 0.55f, 0.5f);

        /// <summary>Màu sương mù tại t01.</summary>
        public Color EvaluateFogColor(float t01)
            => fogColorGradient != null ? fogColorGradient.Evaluate(Mathf.Repeat(t01, 1f)) : new Color(0.78f, 0.82f, 0.85f);

        /// <summary>Màu nước phù sa (silt) tại t01.</summary>
        public Color EvaluateWaterColor(float t01)
            => waterColorGradient != null ? waterColorGradient.Evaluate(Mathf.Repeat(t01, 1f)) : new Color(0.30f, 0.26f, 0.18f);

        /// <summary>
        /// Mực nước Y nội suy tuyến tính giữa min/max theo waterLevelOverDay (Tide).
        /// </summary>
        /// <param name="t01">Thời gian chuẩn hóa 0..1 (= giờ/24).</param>
        public float EvaluateWaterHeight(float t01)
            => Mathf.Lerp(minWaterHeight, maxWaterHeight, waterLevelOverDay.Evaluate(Mathf.Repeat(t01, 1f)));

        /// <summary>Mức bão hoà màu (Saturation, đơn vị URP: -100..+100) tại t01.</summary>
        public float EvaluateSaturation(float t01)
            => Mathf.Clamp(saturationOverDay.Evaluate(Mathf.Repeat(t01, 1f)), -100f, 100f);

        /// <summary>Cường độ Vignette (0..1) tại t01.</summary>
        public float EvaluateVignetteIntensity(float t01)
            => Mathf.Clamp01(vignetteIntensityOverDay.Evaluate(Mathf.Repeat(t01, 1f)));

        /// <summary>Cường độ Lens Distortion (-1..1) tại t01. Âm = barrel distortion.</summary>
        public float EvaluateLensDistortion(float t01)
            => Mathf.Clamp(lensDistortionOverDay.Evaluate(Mathf.Repeat(t01, 1f)), -1f, 1f);
    }
}
