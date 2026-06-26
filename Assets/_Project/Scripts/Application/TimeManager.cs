/**
 * TimeManager: Đồng hồ in-game điều phối thời gian và chuyển Phase.
 * [Chức năng]: Mỗi frame cộng dồn thời gian theo timeScale, quy đổi ra giờ/phút,
 *              phát event OnTimeChanged (mỗi khi phút đổi) và OnPhaseChanged
 *              (mỗi khi bước sang khung giờ mới). TUYỆT ĐỐI không gọi UI — chỉ
 *              phát Action và in Console log (theo time-physics-rules.md).
 * [Dependencies]: ITimeSystem, GamePhase (Domain).
 */

using System;
using UnityEngine;
using ChoNoi.Domain;

namespace ChoNoi.Application
{
    public class TimeManager : MonoBehaviour, ITimeSystem
    {
        [Header("Cấu hình thời gian")]
        // Số phút in-game trôi qua trên mỗi 1 giây thực. timeScale=60 → 1s thực = 1h game.
        [SerializeField] private float timeScale = 1f;
        // Giờ bắt đầu trong ngày (mặc định 3h = Bình Minh).
        [SerializeField, Range(0f, 24f)] private float startHour = 3f;

        // Mốc giờ kích hoạt từng Phase (theo execution-plan).
        private const int DawnHour  = 3;
        private const int DayHour   = 10;
        private const int DuskHour  = 13;
        private const int NightHour = 18;

        // Tổng số phút in-game đã trôi trong ngày hiện tại [0, 1440).
        private float minutesOfDay;
        // Phút (làm tròn) lần cuối đã phát event — để chỉ bắn khi phút đổi.
        private int lastMinute = -1;
        // Phase lần cuối đã phát event — để chỉ bắn khi phase đổi.
        private GamePhase currentPhase;
        private bool initialized;

        public event Action<int, int> OnTimeChanged;
        public event Action<GamePhase> OnPhaseChanged;
        public event Action OnSleep;
        public event Action<int> OnDayChanged;

        public GamePhase CurrentPhase => currentPhase;
        public int CurrentDay { get; private set; } = 1;

        public float MinutesOfDay
        {
            get => minutesOfDay;
            set
            {
                minutesOfDay = Mathf.Repeat(value, 1440f);
                lastMinute = -1; // Force time update
                currentPhase = GetPhaseForHour(Hour);
                initialized = true;
                OnTimeChanged?.Invoke(Hour, Minute);
                OnPhaseChanged?.Invoke(currentPhase);
            }
        }

        public void Sleep()
        {
            // Cho phép ngủ vào buổi tối hoặc chạng vạng
            if (currentPhase == GamePhase.Dusk || currentPhase == GamePhase.Night)
            {
                Debug.Log("[TimeManager] Nhan vat di ngu. Chuyen sang ngay moi.");
                OnSleep?.Invoke(); // Trigger save

                CurrentDay++;
                minutesOfDay = DawnHour * 60f;
                lastMinute = -1; // Force time update
                currentPhase = GamePhase.Dawn;
                OnDayChanged?.Invoke(CurrentDay);
                OnTimeChanged?.Invoke(Hour, Minute);
                OnPhaseChanged?.Invoke(currentPhase);
            }
            else
            {
                Debug.LogWarning("[TimeManager] Chi co the ngu vao buoi toi hoac chang vang!");
            }
        }

        public int CurrentHour => Hour;
        public int CurrentMinute => Minute;

        public void LoadDay(int savedDay)
        {
            CurrentDay = Mathf.Max(1, savedDay);
            OnDayChanged?.Invoke(CurrentDay);
        }

        public void ResetTime()
        {
            CurrentDay = 1;
            minutesOfDay = DawnHour * 60f;
            lastMinute = -1;
            currentPhase = GamePhase.Dawn;
            OnDayChanged?.Invoke(CurrentDay);
            OnTimeChanged?.Invoke(Hour, Minute);
            OnPhaseChanged?.Invoke(currentPhase);
        }

        private void Start()
        {
            // Khởi tạo đồng hồ tại startHour và phát Phase ban đầu ngay lập tức.
            minutesOfDay = Mathf.Repeat(startHour, 24f) * 60f;
            currentPhase = GetPhaseForHour(Hour);
            initialized = true;

            OnTimeChanged?.Invoke(Hour, Minute);
            OnPhaseChanged?.Invoke(currentPhase);
            OnDayChanged?.Invoke(CurrentDay);
            lastMinute = Minute;
        }

        private void Update()
        {
            if (!initialized) return;

            // Bước 1: Cộng dồn thời gian theo timeScale (phút game / giây thực) và wrap 24h.
            minutesOfDay += Time.deltaTime * timeScale;
            if (minutesOfDay >= 1440f)
                minutesOfDay = Mathf.Repeat(minutesOfDay, 1440f);

            // Bước 2: Chỉ phát OnTimeChanged khi phút (số nguyên) thực sự thay đổi.
            if (Minute != lastMinute)
            {
                lastMinute = Minute;
                OnTimeChanged?.Invoke(Hour, Minute);

                // Bước 3: Nếu giờ mới rơi vào Phase khác → phát OnPhaseChanged.
                GamePhase phase = GetPhaseForHour(Hour);
                if (phase != currentPhase)
                {
                    currentPhase = phase;
                    Debug.Log($"[TimeManager] Chuyen Phase -> {phase} luc {Hour:00}:{Minute:00}");
                    OnPhaseChanged?.Invoke(phase);
                }
            }
        }

        // Giờ hiện tại 0-23.
        private int Hour => Mathf.FloorToInt(minutesOfDay / 60f) % 24;
        // Phút hiện tại 0-59.
        private int Minute => Mathf.FloorToInt(minutesOfDay % 60f);

        /// <summary>
        /// Quy đổi giờ (0-23) sang GamePhase tương ứng.
        /// Night kéo dài từ 18h tối tới trước 3h sáng hôm sau.
        /// </summary>
        /// <param name="hour">Giờ trong ngày, 0-23.</param>
        private GamePhase GetPhaseForHour(int hour)
        {
            if (hour >= DawnHour && hour < DayHour)  return GamePhase.Dawn;   // 03:00 - 09:59
            if (hour >= DayHour  && hour < DuskHour) return GamePhase.Day;    // 10:00 - 12:59
            if (hour >= DuskHour && hour < NightHour) return GamePhase.Dusk;  // 13:00 - 17:59
            return GamePhase.Night;                                           // 18:00 - 02:59
        }
    }
}
