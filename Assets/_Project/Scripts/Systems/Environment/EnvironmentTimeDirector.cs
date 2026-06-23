/**
 * EnvironmentTimeDirector: Bộ điều phối thời gian & môi trường tự quản (standalone).
 * [Chức năng]: Hệ thống thời gian độc lập (không phụ thuộc TimeManager/Application).
 *              Update() chỉ để tick đồng hồ — sau đó gọi ApplyEnvironment() theo batch:
 *              chỉ áp dụng khi giờ thay đổi đủ ngưỡng (changeThreshold), tránh set RenderSettings
 *              mỗi frame. Nội suy Directional Light (màu, cường độ, góc xoay), Fog (màu, mật độ)
 *              và Ambient Sky/Equator/Ground từ TimeOfDayConfigSO.
 *              Implements ChoNoi.Domain.Systems.ITimeSystem để hệ thống khác Subscribe event.
 *              Editor preview: kéo editorPreviewHour trong Inspector → thấy ánh sáng đổi tức thì.
 * [Dependencies]: TimeOfDayConfigSO (Infrastructure); ITimeSystem (Domain.Systems).
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;
using ChoNoi.Infrastructure;
using ChoNoi.Domain.Systems;

namespace ChoNoi.Systems.Environment
{
    public class EnvironmentTimeDirector : MonoBehaviour, ITimeSystem
    {
        [Header("Cấu hình")]
        [SerializeField] private TimeOfDayConfigSO config;

        [Header("Tham chiếu Scene")]
        [SerializeField] private Light directionalLight;

        [Header("Cấu hình ban đầu")]
        // Giờ bắt đầu — ghi đè startHour trong config nếu startFromConfig = false.
        [SerializeField, Range(0f, 24f)] private float startHour = 3f;
        [SerializeField] private bool startFromConfig = true;

        [Header("Ngưỡng cập nhật")]
        // Chỉ gọi ApplyEnvironment khi giờ thay đổi ít nhất ngần này (đơn vị giờ).
        // 0.016 ≈ 1 phút game — đủ mịn mà không set RenderSettings mỗi frame.
        [SerializeField] private float changeThreshold = 0.016f;

        [Header("Editor Preview — kéo để xem tức thì")]
        [SerializeField, Range(0f, 24f)] private float editorPreviewHour = 6f;
        [SerializeField] private bool applyInEditor = true;

        // Phút game tích lũy từ khi scene bắt đầu.
        private float minutesOfDay;
        // Giờ hiện tại 0..24.
        private float currentTime;
        // Giờ ở lần ApplyEnvironment cuối — để throttle theo changeThreshold.
        private float lastAppliedTime = -999f;
        // TimeScale lưu trước khi Pause().
        private float pausedTimeScale;
        private bool initialized;

        // ITimeSystem
        public event Action<float> OnTimeChanged;
        public float CurrentTime => currentTime;
        public float TimeScale
        {
            get => config != null ? config.TimeScale : 0f;
            set { if (config != null) config.TimeScale = Mathf.Max(0f, value); }
        }
        public bool IsPaused => Mathf.Approximately(TimeScale, 0f);

        public void Pause()
        {
            pausedTimeScale = TimeScale;
            TimeScale = 0f;
        }

        public void Resume()
        {
            TimeScale = pausedTimeScale > 0f ? pausedTimeScale : 60f;
        }

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("[EnvironmentTimeDirector] Chưa assign TimeOfDayConfigSO — dừng hoạt động.");
                enabled = false;
                return;
            }

            float hour = startFromConfig ? config.StartHour : startHour;
            minutesOfDay = Mathf.Repeat(hour, 24f) * 60f;
            currentTime = hour;
            initialized = true;
        }

        private void Start()
        {
            if (!initialized) return;
            // Áp ngay lập tức thay vì chờ frame đầu tiên thay đổi threshold.
            ApplyEnvironment(currentTime / 24f);
            lastAppliedTime = currentTime;
        }

        private void Update()
        {
            if (!initialized || config == null) return;

            // Tick đồng hồ (phút game / giây thực) và wrap 24h.
            minutesOfDay += Time.deltaTime * config.TimeScale;
            if (minutesOfDay >= 1440f)
                minutesOfDay = Mathf.Repeat(minutesOfDay, 1440f);

            float newTime = minutesOfDay / 60f;

            // Throttle: chỉ áp environment khi giờ thay đổi đủ ngưỡng — tránh set RenderSettings mỗi frame.
            float delta = Mathf.Abs(newTime - lastAppliedTime);
            // Xử lý wrap 24h (e.g. 23.9h → 0.1h: delta thực là 0.2h, không phải 23.8h).
            if (delta > 12f) delta = 24f - delta;

            if (delta >= changeThreshold)
            {
                currentTime = newTime;
                ApplyEnvironment(currentTime / 24f);
                OnTimeChanged?.Invoke(currentTime);
                lastAppliedTime = currentTime;
            }
        }

        /// <summary>
        /// Áp toàn bộ thông số môi trường tại mốc thời gian t01 (= giờ/24).
        /// Gọi cả bởi Update() (runtime) và OnValidate() (Editor preview).
        /// </summary>
        private void ApplyEnvironment(float t01)
        {
            if (config == null) return;

            ApplyDirectionalLight(t01);
            ApplyFog(t01);
            ApplyAmbient(t01);
        }

        private void ApplyDirectionalLight(float t01)
        {
            if (directionalLight == null) return;

            directionalLight.color     = config.EvaluateSunColor(t01);
            directionalLight.intensity = config.EvaluateSunIntensity(t01);

            float pitch = config.EvaluateSunPitch(t01);
            directionalLight.transform.rotation = Quaternion.Euler(pitch, config.SunYawDegrees, 0f);
        }

        private void ApplyFog(float t01)
        {
            RenderSettings.fog        = true;
            RenderSettings.fogMode    = FogMode.Exponential;
            RenderSettings.fogColor   = config.EvaluateFogColor(t01);
            RenderSettings.fogDensity = config.EvaluateFogDensity(t01);
        }

        private void ApplyAmbient(float t01)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;

            Color sky     = config.EvaluateAmbientSky(t01);
            Color equator = config.EvaluateAmbientEquator(t01);

            RenderSettings.ambientSkyColor     = sky;
            RenderSettings.ambientEquatorColor = equator;
            // Ground: nửa sắc equator (ánh sáng phản xạ từ nước & đất).
            RenderSettings.ambientGroundColor  = equator * 0.5f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!applyInEditor || UnityEngine.Application.isPlaying) return;
            if (config == null) return;

            // Áp preview tại editorPreviewHour mà không cần Play.
            ApplyEnvironment(Mathf.Repeat(editorPreviewHour, 24f) / 24f);
        }
#endif
    }
}
