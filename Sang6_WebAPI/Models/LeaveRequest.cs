namespace Sang6_WebAPI.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string LeaveType { get; set; } = "Nghỉ phép năm"; // Nghỉ phép năm, Nghỉ ốm, Việc riêng
        public string Status { get; set; } = "Chờ duyệt"; // Chờ duyệt, Đã duyệt, Từ chối
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ApprovedBy { get; set; }
    }
}
