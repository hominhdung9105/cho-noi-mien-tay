/**
 * PostProcessingVolumeDriver: Lái URP Volume post-processing theo thời gian trong ngày.
 * [Chức năng]: Nghe event OnTimeNormalized của TimeManager, đọc AtmosphericProfileSO để nội suy
 *              Saturation (ColorAdjustments), Vignette intensity, LensDistortion intensity.
 *              Cache TryGet<T> một lần trong Awake() — zero GC alloc trong hot path.
 *              Pattern giống hệt AtmosphereSkyBridge: OnEnable/OnDisable subscribe, không Update().
 * [Dependencies]: TimeManager (Application); AtmosphericProfileSO (Infrastructure); URP Volume.
 */

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ChoNoi.Application;
using ChoNoi.Infrastructure;

namespace ChoNoi.Presentation.Environment
{
    public class PostProcessingVolumeDriver : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private TimeManager timeManager;
        [SerializeField] private AtmosphericProfileSO profile;
        // Global Volume trong scene (IsGlobal=true, Weight=1, Priority>=10).
        [SerializeField] private Volume postProcessVolume;

        [Header("Editor Preview — kéo để xem hiệu ứng tức thì")]
        [SerializeField, Range(0f, 24f)] private float editorPreviewHour = 3f;
        [SerializeField] private bool applyInEditor = true;

        // Cached một lần trong Awake() — tránh TryGet / alloc mỗi event.
        private ColorAdjustments _colorAdj;
        private Vignette         _vignette;
        private LensDistortion   _lensDistortion;
        private bool _hasColorAdj;
        private bool _hasVignette;
        private bool _hasLens;

        private void Awake()
        {
            if (postProcessVolume == null)
            {
                Debug.LogWarning("[PostProcessingVolumeDriver] postProcessVolume chưa được assign.");
                return;
            }

            // .profile (không phải .sharedProfile) tự clone runtime instance → không ghi shared asset trên đĩa.
            VolumeProfile rtp = postProcessVolume.profile;
            _hasColorAdj = rtp.TryGet(out _colorAdj);
            _hasVignette = rtp.TryGet(out _vignette);
            _hasLens     = rtp.TryGet(out _lensDistortion);

            if (!_hasColorAdj)
                Debug.LogWarning("[PostProcessingVolumeDriver] ColorAdjustments không tìm thấy trong Volume Profile — thêm Override trong asset.");
            if (!_hasVignette)
                Debug.LogWarning("[PostProcessingVolumeDriver] Vignette không tìm thấy trong Volume Profile — thêm Override trong asset.");
            if (!_hasLens)
                Debug.LogWarning("[PostProcessingVolumeDriver] LensDistortion không tìm thấy trong Volume Profile — thêm Override trong asset.");
        }

        private void OnEnable()
        {
            if (timeManager != null)
                timeManager.OnTimeNormalized += ApplyPostProcessing;
        }

        private void OnDisable()
        {
            if (timeManager != null)
                timeManager.OnTimeNormalized -= ApplyPostProcessing;
        }

        /// <summary>
        /// Hot path: chỉ gán .value trên cached component references — zero GC alloc.
        /// Gọi bởi TimeManager.OnTimeNormalized mỗi phút game.
        /// </summary>
        private void ApplyPostProcessing(float t01)
        {
            if (profile == null) return;

            if (_hasColorAdj)
                _colorAdj.saturation.value = profile.EvaluateSaturation(t01);

            if (_hasVignette)
                _vignette.intensity.value = profile.EvaluateVignetteIntensity(t01);

            if (_hasLens)
                _lensDistortion.intensity.value = profile.EvaluateLensDistortion(t01);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!applyInEditor || UnityEngine.Application.isPlaying) return;
            if (postProcessVolume == null || profile == null) return;

            // Edit mode: dùng sharedProfile trực tiếp (không có runtime clone).
            VolumeProfile editorProfile = postProcessVolume.sharedProfile;
            if (editorProfile == null) return;

            float t01 = Mathf.Repeat(editorPreviewHour, 24f) / 24f;

            if (editorProfile.TryGet(out ColorAdjustments ca))
                ca.saturation.value = profile.EvaluateSaturation(t01);

            if (editorProfile.TryGet(out Vignette vig))
                vig.intensity.value = profile.EvaluateVignetteIntensity(t01);

            if (editorProfile.TryGet(out LensDistortion ld))
                ld.intensity.value = profile.EvaluateLensDistortion(t01);
        }
#endif
    }
}
