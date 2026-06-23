/**
 * ITimeSystem: Interface cho hệ thống thời gian tự quản (standalone director).
 * [Chức năng]: Định nghĩa hợp đồng cho EnvironmentTimeDirector — một hệ thống thời gian
 *              độc lập (không phụ thuộc TimeManager/Application layer). Hỗ trợ Pause/Resume,
 *              đọc giờ hiện tại (0-24), điều chỉnh tốc độ thời gian và đăng ký callback.
 *              Namespace ChoNoi.Domain.Systems tách biệt với ChoNoi.Domain.ITimeSystem
 *              (interface cũ dùng bởi TimeManager) — không xung đột.
 * [Dependencies]: Không có (pure C# interface).
 */

using System;

namespace ChoNoi.Domain.Systems
{
    public interface ITimeSystem
    {
        /// <summary>Giờ hiện tại trong ngày, 0.0 – 24.0 (liên tục, không làm tròn).</summary>
        float CurrentTime { get; }

        /// <summary>Tốc độ thời gian (phút game / giây thực). 0 = dừng, 60 = 1s thực = 1h game.</summary>
        float TimeScale { get; set; }

        /// <summary>True khi TimeScale == 0 (đang tạm dừng).</summary>
        bool IsPaused { get; }

        /// <summary>
        /// Bắn mỗi khi giờ thay đổi đáng kể. Tham số: giờ hiện tại 0.0–24.0.
        /// Frequency phụ thuộc TimeScale và logic throttle của implementation.
        /// </summary>
        event Action<float> OnTimeChanged;

        /// <summary>Tạm dừng đồng hồ (lưu TimeScale trước đó để Resume khôi phục).</summary>
        void Pause();

        /// <summary>Tiếp tục đồng hồ từ TimeScale đã lưu lúc Pause.</summary>
        void Resume();
    }
}
