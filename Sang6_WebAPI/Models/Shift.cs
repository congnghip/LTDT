using System.ComponentModel.DataAnnotations.Schema;

namespace Sang6_WebAPI.Models
{
    public class Shift
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // Ca Sáng, Ca Chiều, Ca Hành Chính
        public TimeSpan StartTime { get; set; } = new TimeSpan(8, 0, 0); // 08:00
        public TimeSpan EndTime { get; set; } = new TimeSpan(17, 0, 0); // 17:00
        public int LateGraceMinutes { get; set; } = 15; // Cho phép muộn 15 phút
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; } = 50000; // 50,000 đ/giờ
    }
}
