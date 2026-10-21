using System.ComponentModel.DataAnnotations;

namespace Sang6_WebAPI.Models
{
    public class RegisterDto
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string Department { get; set; } = "Kỹ thuật";
        public string Position { get; set; } = "Nhân viên";
        public string Role { get; set; } = "Employee"; // Admin, Employee, Manager
    }

    public class LoginDto
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class VerifyOtpDto
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Otp { get; set; } = string.Empty;
    }

    public class SendEmailOtpDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class VerifyEmailOtpDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Otp { get; set; } = string.Empty;
    }

    public class CheckInDto
    {
        public string Method { get; set; } = "GPS"; // "Face", "GPS", "QR"
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? FaceBase64 { get; set; }
        public List<double>? BiometricRatios { get; set; }
        public string? QrToken { get; set; }
        public int? ShiftId { get; set; }
        public double? NoiseLevelDb { get; set; }
    }

    public class RecordNoiseDto
    {
        public double NoiseLevelDb { get; set; }
        public string? Note { get; set; }
    }

    public class FaceVerificationDto
    {
        public string FaceBase64 { get; set; } = string.Empty;
        public List<double>? BiometricRatios { get; set; }
    }

    public class FaceLoginDto
    {
        public string? Username { get; set; }
        public string FaceBase64 { get; set; } = string.Empty;
        public List<double>? BiometricRatios { get; set; }
    }

    public class CheckOutDto
    {
        public int? AttendanceId { get; set; }
        public string? Note { get; set; }
    }

    public class CreateLeaveDto
    {
        [Required]
        public DateTime StartDate { get; set; }
        [Required]
        public DateTime EndDate { get; set; }
        [Required]
        public string Reason { get; set; } = string.Empty;
        public string LeaveType { get; set; } = "Nghỉ phép năm";
    }

    public class UpdateLeaveStatusDto
    {
        [Required]
        public string Status { get; set; } = "Đã duyệt"; // Đã duyệt, Từ chối
    }

    public class CreateShiftDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public string StartTime { get; set; } = "08:00";
        public string EndTime { get; set; } = "17:00";
        public int LateGraceMinutes { get; set; } = 15;
        public decimal HourlyRate { get; set; } = 50000;
    }
}
