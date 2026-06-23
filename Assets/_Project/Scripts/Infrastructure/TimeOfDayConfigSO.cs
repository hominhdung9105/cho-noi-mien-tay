/**
 * TimeOfDayConfigSO: ScriptableObject cấu hình ánh sáng & môi trường 24h cho EnvironmentTimeDirector.
 * [Chức năng]: Lưu trữ Gradient màu và AnimationCurve cường độ/góc theo trục thời gian 0..1
 *              (= giờ/24). EnvironmentTimeDirector đọc các Evaluate* method để nội suy
 *              Directional Light (màu, cường độ, góc), Fog (màu, mật độ) và Ambient (sky, equator).
 *              Tách biệt với AtmosphericProfileSO.cs (dùng bởi Presentation/AtmosphereSkyBridge) —
 *              file này phục vụ Systems layer (EnvironmentTimeDirector).
 *              Cấu hình mã màu HEX theo bảng design doc cho_noi_can_tho_env_design.md:
 *                03:00 Sky #050B14 | 05:30 #2B4C7E | 08:00 #4EA8DE | 13:00 #5C677D | 16:30 #3D348B | 18:30 #0B132B
 * [Dependencies]: Không có (data-only ScriptableObject).
 */

using UnityEngine;

namespace ChoNoi.Infrastructure
{
    [CreateAssetMenu(fileName = "TimeOfDayConfig", menuName = "ChoNoi/Data/Time Of Day Config")]
    public class TimeOfDayConfigSO : ScriptableObject
    {
        [Header("Đồng hồ in-game")]
        // Số phút game trôi qua trên mỗi 1 giây thực. 60 = 1s thực → 1 phút game (24h/24p thực).
        [SerializeField] private float timeScale = 60f;
        // Giờ khởi đầu khi bắt đầu Scene (0-24).
        [SerializeField, Range(0f, 24f)] private float startHour = 3f;

        [Header("Directional Light — Mặt Trời")]
        // Màu ánh nắng theo giờ: đêm tím thẫm → bình minh cam vàng → trưa trắng → hoàng hôn đỏ.
        [SerializeField] private Gradient sunColor;
        // Cường độ (Intensity) mặt trời: 0.05 lúc 3h, đỉnh 1.5 lúc 10h, tắt lúc 18:30.
        [SerializeField] private AnimationCurve sunIntensity = new AnimationCurve(
            new Keyframe(0f,      0.02f),  // 00:00 đêm sâu
            new Keyframe(0.125f,  0.05f),  // 03:00 Bình Minh Tối
            new Keyframe(0.229f,  1.2f),   // 05:30 Hừng Đông
            new Keyframe(0.333f,  1.5f),   // 08:00 Nắng Sáng đỉnh
            new Keyframe(0.542f,  0.8f),   // 13:00 Đứng Bóng (bị mây che)
            new Keyframe(0.688f,  1.3f),   // 16:30 Hoàng Hôn
            new Keyframe(0.771f,  0f),     // 18:30 Tắt nắng
            new Keyframe(1f,      0.02f)); // 24:00 = đêm

        // Góc Pitch của mặt trời (độ): -90 = dưới chân trời đông, 0 = mặt trời mọc, 90 = đỉnh đầu.
        [SerializeField] private AnimationCurve sunPitchDegrees = new AnimationCurve(
            new Keyframe(0f,      -90f),   // 00:00 — dưới chân trời
            new Keyframe(0.125f,  -85f),   // 03:00 — chưa mọc
            new Keyframe(0.229f,    8f),   // 05:30 — mới ló (Rot X:8 theo design doc)
            new Keyframe(0.333f,   35f),   // 08:00 — nắng sáng (Rot X:35)
            new Keyframe(0.458f,   90f),   // 11:00 — đỉnh đầu
            new Keyframe(0.542f,   85f),   // 13:00 — bóng gần thẳng đứng
            new Keyframe(0.688f,    5f),   // 16:30 — gần lặn (Rot X:5)
            new Keyframe(0.771f,  -10f),   // 18:30 — đã lặn
            new Keyframe(1f,      -90f));  // 24:00 — về đêm

        // Góc Yaw (trục Y) của mặt trời — giữ cố định tạo bóng chéo kiểu Isometric.
        [SerializeField] private float sunYawDegrees = 35f;

        [Header("Sương Mù (Exponential Fog)")]
        // Màu sương mù: 3h tối xanh đen → 5:30 cam hồng → 8h trắng xanh → 13h xám → 18:30 tối.
        [SerializeField] private Gradient fogColor;
        // Mật độ sương mù: 0.08 lúc 3h dày đặc → 0.005 lúc 8h tan hết → 0.08 lại lúc đêm.
        [SerializeField] private AnimationCurve fogDensity = new AnimationCurve(
            new Keyframe(0f,      0.07f),  // 00:00
            new Keyframe(0.125f,  0.08f),  // 03:00 — Sương dày nhất (0.08)
            new Keyframe(0.229f,  0.03f),  // 05:30 — sương lam mỏng (0.03)
            new Keyframe(0.333f,  0.005f), // 08:00 — hầu như tan hết (0.005)
            new Keyframe(0.542f,  0.015f), // 13:00 — mưa lâm râm (0.015)
            new Keyframe(0.688f,  0.01f),  // 16:30 — hơi sương chiều (0.01)
            new Keyframe(0.771f,  0.02f),  // 18:30 — sương đêm (0.02)
            new Keyframe(1f,      0.07f)); // 24:00

        [Header("Ánh sáng Ambient")]
        // Màu vòm trời (sky): ảnh hưởng bề mặt hướng lên.
        [SerializeField] private Gradient ambientSkyColor;
        // Màu chân trời / equator: ảnh hưởng bề mặt đứng (vách nhà, thân cây).
        [SerializeField] private Gradient ambientEquatorColor;

        // --- Public API ---
        public float TimeScale
        {
            get => timeScale;
            set => timeScale = Mathf.Max(0f, value);
        }

        public float StartHour => startHour;

        /// <summary>Màu Directional Light tại t01 (= giờ/24).</summary>
        public Color EvaluateSunColor(float t01)
            => sunColor != null ? sunColor.Evaluate(Mathf.Repeat(t01, 1f)) : Color.white;

        /// <summary>Cường độ Directional Light tại t01 (không âm).</summary>
        public float EvaluateSunIntensity(float t01)
            => Mathf.Max(0f, sunIntensity.Evaluate(Mathf.Repeat(t01, 1f)));

        /// <summary>Góc Pitch của mặt trời (độ) tại t01.</summary>
        public float EvaluateSunPitch(float t01)
            => sunPitchDegrees.Evaluate(Mathf.Repeat(t01, 1f));

        /// <summary>Góc Yaw cố định của mặt trời — dùng để đổ bóng chéo Isometric.</summary>
        public float SunYawDegrees => sunYawDegrees;

        /// <summary>Màu sương mù tại t01.</summary>
        public Color EvaluateFogColor(float t01)
            => fogColor != null ? fogColor.Evaluate(Mathf.Repeat(t01, 1f)) : new Color(0.7f, 0.75f, 0.8f);

        /// <summary>Mật độ sương mù tại t01 (không âm).</summary>
        public float EvaluateFogDensity(float t01)
            => Mathf.Max(0f, fogDensity.Evaluate(Mathf.Repeat(t01, 1f)));

        /// <summary>Màu ambient vòm trời (sky) tại t01.</summary>
        public Color EvaluateAmbientSky(float t01)
            => ambientSkyColor != null ? ambientSkyColor.Evaluate(Mathf.Repeat(t01, 1f)) : new Color(0.2f, 0.3f, 0.5f);

        /// <summary>Màu ambient chân trời (equator) tại t01.</summary>
        public Color EvaluateAmbientEquator(float t01)
            => ambientEquatorColor != null ? ambientEquatorColor.Evaluate(Mathf.Repeat(t01, 1f)) : new Color(0.3f, 0.28f, 0.22f);
    }
}
