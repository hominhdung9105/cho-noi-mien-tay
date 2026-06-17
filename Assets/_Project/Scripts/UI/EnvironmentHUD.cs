/**
 * EnvironmentHUD: UI hiển thị trạng thái môi trường chợ nổi khi chạy game (Observer/Consumer).
 * [Chức năng]: Tự dựng Canvas uGUI bằng code, panel góc trái hiển thị Giờ/Phase/Ngày, mực nước
 *              (thuỷ triều), độ sương mù & ánh sáng (đọc từ AtmosphericProfileSO theo giờ), thanh
 *              Nhiên liệu & Thể lực, và nút tăng tốc thời gian (x1/x10/x60/Pause). Chỉ SUBSCRIBE
 *              event từ systems — không sửa logic systems (đúng quy tắc tách lớp UI của Dev 1).
 * [Dependencies]: TimeManager (Application), AtmosphericProfileSO/BoatStats (Infrastructure),
 *                 PlayerStats (ChoNoiMienTay.Presentation), uGUI.
 */

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ChoNoi.Application;
using ChoNoi.Domain;
using ChoNoi.Infrastructure;
using ChoNoiMienTay.Presentation;

namespace ChoNoiMienTay.UI
{
    public class EnvironmentHUD : MonoBehaviour
    {
        [SerializeField] private TimeManager timeManager;
        [SerializeField] private AtmosphericProfileSO profile;
        [SerializeField] private BoatStats boatStats;
        [SerializeField] private PlayerStats playerStats;

        private Canvas canvas;
        private Text timeText;
        private Text tideText;
        private Text weatherText;
        private Image fuelFill;
        private Text fuelLabel;
        private Image staminaFill;
        private Text staminaLabel;

        // Lưu giờ/phút gần nhất để re-render khi chỉ đổi Phase.
        private int lastHour = 6;
        private int lastMinute = 0;

        public void Configure(TimeManager timeSource, AtmosphericProfileSO atmosphericProfile, BoatStats stats, PlayerStats player)
        {
            timeManager = timeSource;
            profile = atmosphericProfile;
            boatStats = stats;
            playerStats = player;
        }

        private void OnEnable() => SubscribeEvents();
        private void OnDisable() => UnsubscribeEvents();

        private void Start()
        {
            if (timeManager == null) timeManager = FindAnyObjectByType<TimeManager>();
            if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();

            BuildUIIfNeeded();
            SubscribeEvents();
            RefreshAll();
        }

        private void SubscribeEvents()
        {
            if (timeManager != null)
            {
                timeManager.OnTimeChanged -= HandleTimeChanged;
                timeManager.OnTimeChanged += HandleTimeChanged;
                timeManager.OnPhaseChanged -= HandlePhaseChanged;
                timeManager.OnPhaseChanged += HandlePhaseChanged;
            }

            if (boatStats != null)
            {
                boatStats.OnFuelChanged -= HandleFuelChanged;
                boatStats.OnFuelChanged += HandleFuelChanged;
            }

            if (playerStats != null)
            {
                playerStats.OnStaminaChanged -= HandleStaminaChanged;
                playerStats.OnStaminaChanged += HandleStaminaChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (timeManager != null)
            {
                timeManager.OnTimeChanged -= HandleTimeChanged;
                timeManager.OnPhaseChanged -= HandlePhaseChanged;
            }
            if (boatStats != null) boatStats.OnFuelChanged -= HandleFuelChanged;
            if (playerStats != null) playerStats.OnStaminaChanged -= HandleStaminaChanged;
        }

        private void HandleTimeChanged(int hour, int minute)
        {
            lastHour = hour;
            lastMinute = minute;
            RenderTimeBlock();
        }

        private void HandlePhaseChanged(GamePhase _) => RenderTimeBlock();
        private void HandleFuelChanged(float current, float max) => SetBar(fuelFill, fuelLabel, "Nhien lieu", current, max);
        private void HandleStaminaChanged(float current, float max) => SetBar(staminaFill, staminaLabel, "The luc", current, max);

        private void RefreshAll()
        {
            // Khởi tạo giờ ban đầu từ NormalizedTime (trước khi có event đầu tiên).
            if (timeManager != null)
            {
                float t01 = timeManager.NormalizedTime;
                lastHour = Mathf.FloorToInt(t01 * 24f) % 24;
                lastMinute = Mathf.FloorToInt(Mathf.Repeat(t01 * 1440f, 60f));
            }
            RenderTimeBlock();

            if (boatStats != null) SetBar(fuelFill, fuelLabel, "Nhien lieu", boatStats.CurrentFuel, boatStats.MaxFuel);
            if (playerStats != null) SetBar(staminaFill, staminaLabel, "The luc", playerStats.CurrentStamina, playerStats.MaxStamina);
        }

        /// <summary>Cập nhật khối Giờ/Phase + mực nước + sương mù/ánh sáng theo giờ hiện tại.</summary>
        private void RenderTimeBlock()
        {
            if (timeText == null) return;

            float t01 = (lastHour + lastMinute / 60f) / 24f;
            string phase = timeManager != null ? timeManager.CurrentPhase.ToString() : "--";
            int day = timeManager != null ? timeManager.CurrentDay : 1;

            timeText.text = $"Ngay {day}   {lastHour:00}:{lastMinute:00}   [{phase}]";

            if (profile != null)
            {
                tideText.text = $"Muc nuoc (tide): {profile.EvaluateWaterHeight(t01):0.00} m";
                weatherText.text =
                    $"Suong mu: {profile.EvaluateFogDensity(t01):0.000}   |   Anh sang: {profile.EvaluateLightIntensity(t01):0.00}";
            }
        }

        private void SetBar(Image fill, Text label, string title, float current, float max)
        {
            if (fill == null || label == null) return;

            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            Vector2 anchorMax = fill.rectTransform.anchorMax;
            anchorMax.x = ratio;
            fill.rectTransform.anchorMax = anchorMax;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            label.text = $"{title}: {current:0}/{max:0}  ({ratio * 100f:0}%)";
        }

        // ──────────────────────────────────────────────
        // DỰNG UI
        // ──────────────────────────────────────────────

        private void BuildUIIfNeeded()
        {
            if (canvas != null) return;
            EnsureEventSystem();

            GameObject canvasObject = new GameObject("EnvironmentHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panel = CreatePanel("EnvPanel", canvasObject.transform, new Color(0.04f, 0.10f, 0.14f, 0.78f));
            Stretch(panel.GetComponent<RectTransform>(), new Vector2(0.015f, 0.50f), new Vector2(0.235f, 0.89f));

            Text title = CreateText("Title", panel.transform, 24, TextAnchor.MiddleCenter);
            title.text = "MOI TRUONG CHO NOI";
            title.color = new Color(0.96f, 0.86f, 0.55f, 1f);
            Stretch(title.rectTransform, new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.99f));

            timeText = CreateText("TimeText", panel.transform, 22, TextAnchor.MiddleLeft);
            Stretch(timeText.rectTransform, new Vector2(0.06f, 0.79f), new Vector2(0.96f, 0.90f));

            tideText = CreateText("TideText", panel.transform, 20, TextAnchor.MiddleLeft);
            Stretch(tideText.rectTransform, new Vector2(0.06f, 0.70f), new Vector2(0.96f, 0.79f));

            weatherText = CreateText("WeatherText", panel.transform, 18, TextAnchor.MiddleLeft);
            Stretch(weatherText.rectTransform, new Vector2(0.06f, 0.61f), new Vector2(0.96f, 0.70f));

            // Thanh Nhiên liệu
            fuelLabel = CreateText("FuelLabel", panel.transform, 18, TextAnchor.MiddleLeft);
            Stretch(fuelLabel.rectTransform, new Vector2(0.06f, 0.51f), new Vector2(0.96f, 0.60f));
            fuelFill = CreateBar("FuelBar", panel.transform, new Vector2(0.06f, 0.45f), new Vector2(0.96f, 0.50f), new Color(0.92f, 0.55f, 0.18f));

            // Thanh Thể lực
            staminaLabel = CreateText("StaminaLabel", panel.transform, 18, TextAnchor.MiddleLeft);
            Stretch(staminaLabel.rectTransform, new Vector2(0.06f, 0.35f), new Vector2(0.96f, 0.44f));
            staminaFill = CreateBar("StaminaBar", panel.transform, new Vector2(0.06f, 0.29f), new Vector2(0.96f, 0.34f), new Color(0.32f, 0.78f, 0.45f));

            // Nút điều khiển tốc độ thời gian
            Text speedTitle = CreateText("SpeedTitle", panel.transform, 16, TextAnchor.MiddleLeft);
            speedTitle.text = "Toc do thoi gian:";
            Stretch(speedTitle.rectTransform, new Vector2(0.06f, 0.18f), new Vector2(0.96f, 0.25f));

            CreateActionButton(panel.transform, "x1", new Vector2(0.04f, 0.05f), new Vector2(0.26f, 0.16f), () => SetTimeScale(1f));
            CreateActionButton(panel.transform, "x10", new Vector2(0.28f, 0.05f), new Vector2(0.50f, 0.16f), () => SetTimeScale(10f));
            CreateActionButton(panel.transform, "x60", new Vector2(0.52f, 0.05f), new Vector2(0.74f, 0.16f), () => SetTimeScale(60f));
            CreateActionButton(panel.transform, "Pause", new Vector2(0.76f, 0.05f), new Vector2(0.96f, 0.16f), () => SetTimeScale(0f));
        }

        private void SetTimeScale(float scale)
        {
            if (timeManager != null)
                timeManager.TimeScale = scale;
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        // Tạo thanh bar: nền tối + lớp fill màu, trả về Image fill để cập nhật theo tỷ lệ.
        private Image CreateBar(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color fillColor)
        {
            GameObject bg = new GameObject(name, typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(parent, false);
            bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            Stretch(bg.GetComponent<RectTransform>(), anchorMin, anchorMax);

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(bg.transform, false);
            Image fill = fillObject.GetComponent<Image>();
            fill.color = fillColor;
            RectTransform rt = fill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return fill;
        }

        private Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void CreateActionButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Stretch(buttonObject.GetComponent<RectTransform>(), anchorMin, anchorMax);
            buttonObject.GetComponent<Image>().color = new Color(0.88f, 0.71f, 0.34f, 1f);
            buttonObject.GetComponent<Button>().onClick.AddListener(onClick);

            Text text = CreateText("Label", buttonObject.transform, 18, TextAnchor.MiddleCenter);
            text.text = label;
            text.color = new Color(0.10f, 0.08f, 0.05f, 1f);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        }

        private void Stretch(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
