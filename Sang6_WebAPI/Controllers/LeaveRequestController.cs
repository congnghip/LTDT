using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sang6_WebAPI.Data;
using Sang6_WebAPI.Models;

namespace Sang6_WebAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class LeaveRequestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LeaveRequestController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string CurrentUserId => 
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("nameid")?.Value 
            ?? User.FindFirst(ClaimTypes.Name)?.Value 
            ?? string.Empty;

        // 1. GET: Danh sách đơn của cá nhân
        [HttpGet("my-requests")]
        public async Task<IActionResult> GetMyRequests()
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { status = false, message = "Chưa xác thực danh tính người dùng" });
            }

            var requests = await _context.LeaveRequests
                .Include(l => l.User)
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new
                {
                    l.Id,
                    l.UserId,
                    UserName = l.User != null ? l.User.UserName : "",
                    FullName = l.User != null ? l.User.FullName : "",
                    Department = l.User != null ? l.User.Department : "",
                    l.StartDate,
                    l.EndDate,
                    l.Reason,
                    l.LeaveType,
                    l.Status,
                    l.CreatedAt,
                    l.ApprovedBy
                })
                .ToListAsync();

            return Ok(requests);
        }

        // 2. GET: Toàn bộ danh sách đơn cần duyệt (Admin & Manager)
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllRequests([FromQuery] string? status)
        {
            var query = _context.LeaveRequests
                .Include(l => l.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(l => l.Status == status);
            }

            var list = await query
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new
                {
                    l.Id,
                    l.UserId,
                    UserName = l.User != null ? l.User.UserName : "",
                    FullName = l.User != null ? l.User.FullName : "",
                    Department = l.User != null ? l.User.Department : "",
                    l.StartDate,
                    l.EndDate,
                    l.Reason,
                    l.LeaveType,
                    l.Status,
                    l.CreatedAt,
                    l.ApprovedBy
                })
                .ToListAsync();

            return Ok(list);
        }

        // 3. POST: Nhân viên tạo đơn xin nghỉ
        [HttpPost]
        public async Task<IActionResult> CreateLeaveRequest([FromBody] CreateLeaveDto dto)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { status = false, message = "Không xác định được danh tính người dùng!" });
            }

            if (string.IsNullOrWhiteSpace(dto.Reason))
            {
                return BadRequest(new { status = false, message = "Vui lòng nhập lý do xin nghỉ phép!" });
            }

            if (dto.EndDate.Date < dto.StartDate.Date)
            {
                return BadRequest(new { status = false, message = "Ngày kết thúc không thể trước ngày bắt đầu!" });
            }

            var request = new LeaveRequest
            {
                UserId = userId,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Reason = dto.Reason.Trim(),
                LeaveType = string.IsNullOrWhiteSpace(dto.LeaveType) ? "Nghỉ phép năm" : dto.LeaveType.Trim(),
                Status = "Chờ duyệt",
                CreatedAt = DateTime.UtcNow
            };

            _context.LeaveRequests.Add(request);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Gửi đơn xin nghỉ phép thành công! Đang chờ quản lý phê duyệt.", data = request });
        }

        // 4. PUT: Phê duyệt hoặc từ chối đơn (Admin & Manager)
        [Authorize(Roles = "Admin,Manager")]
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateLeaveStatusDto dto)
        {
            var request = await _context.LeaveRequests.FindAsync(id);
            if (request == null) return NotFound(new { status = false, message = "Không tìm thấy đơn" });

            request.Status = dto.Status;
            request.ApprovedBy = User.FindFirst("fullName")?.Value 
                ?? User.FindFirst(ClaimTypes.Name)?.Value 
                ?? "Manager";

            await _context.SaveChangesAsync();
            return Ok(new { status = true, message = $"Đã cập nhật trạng thái đơn thành: {dto.Status}" });
        }

        // 5. DELETE: Hủy đơn xin nghỉ
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRequest(int id)
        {
            var userId = CurrentUserId;
            var request = await _context.LeaveRequests.FindAsync(id);
            if (request == null) return NotFound(new { status = false, message = "Không tìm thấy đơn xin nghỉ phép" });

            if (request.UserId != userId && !User.IsInRole("Admin") && !User.IsInRole("Manager"))
            {
                return Forbid();
            }

            _context.LeaveRequests.Remove(request);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Đã hủy đơn xin nghỉ phép thành công" });
        }
    }
}
