using Microsoft.AspNetCore.Identity;

namespace Sang6_WebAPI.Models
{
    public class AppUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string Department { get; set; } = "Kỹ thuật";
        public string Position { get; set; } = "Nhân viên";
        public decimal BaseSalary { get; set; } = 12000000; // Lương cơ bản để tính lương thời gian thực
        public string? AvatarUrl { get; set; }
        public string? RegisteredFaceBase64 { get; set; }
        public bool FaceRegistered { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CurrentOtp { get; set; }
        public DateTime? OtpExpiry { get; set; }
    }
}
