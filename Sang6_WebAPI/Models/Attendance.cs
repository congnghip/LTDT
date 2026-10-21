using System.ComponentModel.DataAnnotations.Schema;

namespace Sang6_WebAPI.Models
{
    public class Attendance
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }
        public int? ShiftId { get; set; }
        public Shift? Shift { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public double TotalHours { get; set; } = 0;
        public string Status { get; set; } = "Đúng giờ"; // Đúng giờ, Đi muộn, Về sớm, Nghỉ
        public string CheckInMethod { get; set; } = "GPS"; // Face, GPS, QR
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? FaceImageUrl { get; set; }
        public double? NoiseLevelDb { get; set; } // Độ ồn môi trường làm việc (dB)
        public bool IsVerified { get; set; } = true;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal SalaryEarned { get; set; } = 0; // Lương tích lũy thời gian thực
        public string? Note { get; set; }
    }
}
