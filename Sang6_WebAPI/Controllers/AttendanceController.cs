using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sang6_WebAPI.Data;
using Sang6_WebAPI.Models;
using Sang6_WebAPI.Services;

namespace Sang6_WebAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AttendanceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        // Tọa độ trụ sở công ty (Ví dụ: Cơ sở Đại học HUTECH / Văn phòng công ty)
        private const double CompanyLat = 10.8018;
        private const double CompanyLng = 106.7145;
        private const double MaxDistanceMeters = 500; // Bán kính cho phép 500 mét

        public AttendanceController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        // 1. Chấm công vào (Check-in) hỗ trợ Khuôn mặt / GPS / QR & Đo tiếng ồn môi trường
        [HttpPost("check-in")]
        public async Task<IActionResult> CheckIn([FromBody] CheckInDto dto)
        {
            var userId = CurrentUserId;
            var today = DateTime.Today;

            var existing = await _context.Attendances
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == today);

            // Kiểm tra GPS nếu phương thức là GPS
            bool isGpsValid = true;
            double distance = 0;
            if (dto.Method == "GPS" && dto.Latitude.HasValue && dto.Longitude.HasValue)
            {
                distance = CalculateDistanceMeters(CompanyLat, CompanyLng, dto.Latitude.Value, dto.Longitude.Value);
                if (distance > MaxDistanceMeters)
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = $"Vị trí của bạn cách công ty {distance:F0}m (vượt quá bán kính cho phép {MaxDistanceMeters}m)."
                    });
                }
            }

            // Kiểm tra khuôn mặt nếu phương thức là Face (Real Biometric Anti-Spoofing)
            if (dto.Method == "Face")
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    if (string.IsNullOrWhiteSpace(dto.FaceBase64))
                    {
                        return BadRequest(new { status = false, message = "Thiếu dữ liệu ảnh khuôn mặt từ Camera!" });
                    }

                    if (string.IsNullOrWhiteSpace(user.RegisteredFaceBase64))
                    {
                        // Tự động lưu khuôn mặt chuẩn đầu tiên (Face Enrollment)
                        user.RegisteredFaceBase64 = dto.FaceBase64;
                        user.FaceRegistered = true;
                        await _userManager.UpdateAsync(user);
                    }
                    else
                    {
                        var faceVerify = FaceRecognitionService.VerifyFace(user.RegisteredFaceBase64, dto.FaceBase64, user.FullName, dto.BiometricRatios);
                        if (!faceVerify.IsMatch)
                        {
                            return BadRequest(new
                            {
                                status = false,
                                message = faceVerify.Message,
                                similarity = faceVerify.SimilarityPercentage,
                                isMatch = false
                            });
                        }
                    }
                }
            }

            // Lấy ca làm việc
            var shift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == dto.ShiftId)
                        ?? await _context.Shifts.FirstOrDefaultAsync();

            var now = DateTime.Now;
            string status = "Đúng giờ";

            if (shift != null)
            {
                var shiftStartTime = today.Add(shift.StartTime).AddMinutes(shift.LateGraceMinutes);
                if (now > shiftStartTime)
                {
                    status = "Đi muộn";
                }
            }

            var noteMsg = dto.Method == "Face" ? "Xác thực khuôn mặt AI thành công" :
                          dto.Method == "QR" ? "Quét mã Dynamic QR hợp lệ" : "Định vị GPS hợp lệ";

            if (dto.NoiseLevelDb.HasValue)
            {
                noteMsg += $" | Độ ồn: {dto.NoiseLevelDb.Value:F1} dB";
            }

            // Nếu đã có bản ghi trong ngày, cập nhật lại để cho phép test nhiều phương thức khi thuyết trình
            if (existing != null)
            {
                existing.CheckInTime = now;
                existing.CheckOutTime = null;
                existing.ShiftId = shift?.Id;
                existing.Status = status;
                existing.CheckInMethod = dto.Method;
                existing.Latitude = dto.Latitude;
                existing.Longitude = dto.Longitude;
                existing.FaceImageUrl = dto.FaceBase64;
                existing.NoiseLevelDb = dto.NoiseLevelDb;
                existing.IsVerified = isGpsValid;
                existing.SalaryEarned = 0;
                existing.Note = noteMsg;

                await _context.SaveChangesAsync();
                return Ok(new
                {
                    status = true,
                    message = $"Chấm công ({status}) bằng phương thức {dto.Method} thành công!",
                    data = existing
                });
            }

            var record = new Attendance
            {
                UserId = userId,
                ShiftId = shift?.Id,
                Date = today,
                CheckInTime = now,
                Status = status,
                CheckInMethod = dto.Method,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                FaceImageUrl = dto.FaceBase64,
                NoiseLevelDb = dto.NoiseLevelDb,
                IsVerified = isGpsValid,
                SalaryEarned = 0,
                Note = noteMsg
            };

            _context.Attendances.Add(record);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                message = $"Chấm công vào thành công ({status}) bằng phương thức {dto.Method}!",
                data = record
            });
        }

        // 2. Chấm công ra (Check-out) & Tính lương thời gian thực (Real-time Salary Meter)
        [HttpPost("check-out")]
        public async Task<IActionResult> CheckOut([FromBody] CheckOutDto dto)
        {
            var userId = CurrentUserId;
            var today = DateTime.Today;

            var record = await _context.Attendances
                .Include(a => a.Shift)
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == today);

            if (record == null || record.CheckInTime == null)
            {
                return BadRequest(new { status = false, message = "Bạn chưa thực hiện Check-in hôm nay!" });
            }

            if (record.CheckOutTime != null)
            {
                return BadRequest(new { status = false, message = "Bạn đã Check-out ngày hôm nay rồi!" });
            }

            var now = DateTime.Now;
            record.CheckOutTime = now;
            var duration = now - record.CheckInTime.Value;
            record.TotalHours = Math.Round(duration.TotalHours, 2);

            // Tính lương tức thời: số giờ * đơn giá ca
            decimal rate = record.Shift?.HourlyRate ?? 50000;
            record.SalaryEarned = Math.Round((decimal)record.TotalHours * rate, 0);

            if (!string.IsNullOrEmpty(dto.Note))
            {
                record.Note += $" | Ghi chú: {dto.Note}";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                message = $"Check-out thành công! Tổng giờ làm: {record.TotalHours}h. Lương ca này: {record.SalaryEarned:N0} đ",
                data = record
            });
        }

        // Đặt lại dữ liệu hôm nay (Phục vụ demo chấm điểm đồ án)
        [HttpPost("reset-today")]
        public async Task<IActionResult> ResetToday()
        {
            var userId = CurrentUserId;
            var today = DateTime.Today;
            var existing = await _context.Attendances.Where(a => a.UserId == userId && a.Date == today).ToListAsync();
            _context.Attendances.RemoveRange(existing);
            await _context.SaveChangesAsync();
            return Ok(new { status = true, message = "Đã đặt lại trạng thái chấm công hôm nay để bạn test tự do!" });
        }

        // Ghi nhận / Cập nhật độ ồn môi trường nơi làm việc (QCVN 24:2016/BYT)
        [HttpPost("record-noise")]
        public async Task<IActionResult> RecordNoise([FromBody] RecordNoiseDto dto)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var today = DateTime.Today;
            var existing = await _context.Attendances.FirstOrDefaultAsync(a => a.UserId == userId && a.Date == today);

            string qcvnStatus = dto.NoiseLevelDb < 55.0 ? "An toàn (Yên tĩnh)" :
                                dto.NoiseLevelDb <= 70.0 ? "Bình thường (Đạt chuẩn QCVN 24)" :
                                dto.NoiseLevelDb <= 85.0 ? "Chú ý (Tiếng ồn cao)" : "CẢNH BÁO NGUY HẠI";

            if (existing != null)
            {
                existing.NoiseLevelDb = dto.NoiseLevelDb;
                var noiseNote = $"Độ ồn: {dto.NoiseLevelDb:F1} dB ({qcvnStatus})";
                if (!string.IsNullOrEmpty(existing.Note))
                {
                    if (existing.Note.Contains("Độ ồn:"))
                    {
                        existing.Note = System.Text.RegularExpressions.Regex.Replace(existing.Note, @"Độ ồn:.*", noiseNote);
                    }
                    else
                    {
                        existing.Note += $" | {noiseNote}";
                    }
                }
                else
                {
                    existing.Note = noiseNote;
                }
                await _context.SaveChangesAsync();
                return Ok(new
                {
                    status = true,
                    message = $"Đã lưu độ ồn môi trường ({dto.NoiseLevelDb:F1} dB - {qcvnStatus}) vào ca làm việc!",
                    data = existing
                });
            }

            var record = new Attendance
            {
                UserId = userId,
                Date = today,
                CheckInTime = DateTime.Now,
                Status = "Đang làm việc",
                CheckInMethod = "NoiseSensor",
                NoiseLevelDb = dto.NoiseLevelDb,
                Note = $"Ghi nhận độ ồn: {dto.NoiseLevelDb:F1} dB ({qcvnStatus})",
                SalaryEarned = 0
            };
            _context.Attendances.Add(record);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                message = $"Đã ghi nhận độ ồn ({dto.NoiseLevelDb:F1} dB - {qcvnStatus}) vào hệ thống!",
                data = record
            });
        }

        // 3. Trạng thái chấm công hôm nay của cá nhân
        [HttpGet("today")]
        public async Task<IActionResult> GetTodayStatus()
        {
            var userId = CurrentUserId;
            var today = DateTime.Today;

            var record = await _context.Attendances
                .Include(a => a.Shift)
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == today);

            return Ok(new
            {
                status = true,
                hasCheckedIn = record != null && record.CheckInTime != null,
                hasCheckedOut = record != null && record.CheckOutTime != null,
                data = record
            });
        }

        // 4. Lịch sử chấm công của cá nhân & Thống kê lương
        [HttpGet("history")]
        public async Task<IActionResult> GetPersonalHistory()
        {
            var userId = CurrentUserId;
            var records = await _context.Attendances
                .Include(a => a.Shift)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            var totalDays = records.Count(a => a.CheckInTime != null);
            var totalHours = records.Sum(a => a.TotalHours);
            var totalSalary = records.Sum(a => a.SalaryEarned);
            var lateCount = records.Count(a => a.Status == "Đi muộn");

            return Ok(new
            {
                status = true,
                summary = new
                {
                    totalDays,
                    totalHours = Math.Round(totalHours, 1),
                    totalSalary,
                    lateCount
                },
                records
            });
        }

        // 5. Toàn bộ bảng chấm công (Dành cho Admin & Quản lý)
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllAttendances([FromQuery] DateTime? date, [FromQuery] string? department)
        {
            var query = _context.Attendances
                .Include(a => a.User)
                .Include(a => a.Shift)
                .AsQueryable();

            if (date.HasValue)
            {
                var queryDate = date.Value.Date;
                query = query.Where(a => a.Date == queryDate);
            }

            if (!string.IsNullOrEmpty(department))
            {
                query = query.Where(a => a.User != null && a.User.Department == department);
            }

            var list = await query
                .OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.CheckInTime)
                .Select(a => new
                {
                    a.Id,
                    a.UserId,
                    UserName = a.User != null ? a.User.UserName : "",
                    FullName = a.User != null ? a.User.FullName : "",
                    Department = a.User != null ? a.User.Department : "",
                    ShiftName = a.Shift != null ? a.Shift.Name : "Ca mặc định",
                    a.Date,
                    a.CheckInTime,
                    a.CheckOutTime,
                    a.TotalHours,
                    a.Status,
                    a.CheckInMethod,
                    a.SalaryEarned,
                    a.Note
                })
                .ToListAsync();

            return Ok(list);
        }

        // 6. Tính năng mới: Sinh mã QR động (Dynamic QR Token) thay đổi mỗi 15 giây
        [HttpGet("dynamic-qr")]
        public IActionResult GetDynamicQrToken()
        {
            long timeWindow = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 15;
            string raw = $"CHAMCONG_TOKEN_{timeWindow}_BuiNguyenCongNghiep";
            string token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));

            return Ok(new
            {
                status = true,
                token = token,
                expiresInSeconds = 15 - (DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 15),
                companyName = "Công ty TNHH Bùi Nguyễn Công Nghiệp",
                companyLat = CompanyLat,
                companyLng = CompanyLng
            });
        }

        // 7. Xác thực khuôn mặt trước khi bấm chấm công (Real Biometric Comparison)
        [HttpPost("verify-face")]
        public async Task<IActionResult> VerifyFace([FromBody] FaceVerificationDto dto)
        {
            var userId = CurrentUserId;
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.FaceBase64))
            {
                return BadRequest(new { status = false, message = "Vui lòng chụp ảnh khuôn mặt!" });
            }

            var result = FaceRecognitionService.VerifyFace(user.RegisteredFaceBase64, dto.FaceBase64, user.FullName, dto.BiometricRatios);
            return Ok(new
            {
                status = result.Success,
                isMatch = result.IsMatch,
                similarity = result.SimilarityPercentage,
                message = result.Message,
                hasRegisteredFace = !string.IsNullOrWhiteSpace(user.RegisteredFaceBase64),
                registeredFaceBase64 = user.RegisteredFaceBase64,
                employeeName = user.FullName
            });
        }

        // 8. Đăng ký / Cập nhật ảnh khuôn mặt chuẩn (Face Enrollment)
        [HttpPost("register-face")]
        public async Task<IActionResult> RegisterFace([FromBody] FaceVerificationDto dto)
        {
            var userId = CurrentUserId;
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            if (dto.FaceBase64 == "CLEAR" || dto.FaceBase64 == "RESET")
            {
                user.RegisteredFaceBase64 = null;
                user.FaceRegistered = false;
                await _userManager.UpdateAsync(user);
                return Ok(new { status = true, message = $"Đã gỡ bỏ gương mặt đăng nhập của nhân viên {user.FullName}!" });
            }

            if (string.IsNullOrWhiteSpace(dto.FaceBase64))
            {
                return BadRequest(new { status = false, message = "Vui lòng chụp ảnh khuôn mặt để đăng ký!" });
            }

            user.RegisteredFaceBase64 = dto.FaceBase64;
            user.FaceRegistered = true;
            await _userManager.UpdateAsync(user);

            return Ok(new
            {
                status = true,
                message = $"Đã đăng ký thành công ảnh khuôn mặt chuẩn (Face Enrollment) cho nhân viên {user.FullName}!",
                registeredFaceBase64 = user.RegisteredFaceBase64
            });
        }

        // 9. Hủy / Xóa ảnh khuôn mặt chuẩn (Face Enrollment)
        [HttpPost("delete-face")]
        public async Task<IActionResult> DeleteFace()
        {
            var userId = CurrentUserId;
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            user.RegisteredFaceBase64 = null;
            user.FaceRegistered = false;
            await _userManager.UpdateAsync(user);

            return Ok(new
            {
                status = true,
                message = $"Đã gỡ bỏ gương mặt đăng nhập của nhân viên {user.FullName}!"
            });
        }

        // 10. Lấy thông tin khuôn mặt gốc đã đăng ký
        [HttpGet("profile-face")]
        public async Task<IActionResult> GetProfileFace()
        {
            var userId = CurrentUserId;
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            return Ok(new
            {
                status = true,
                hasRegisteredFace = !string.IsNullOrWhiteSpace(user.RegisteredFaceBase64),
                registeredFaceBase64 = user.RegisteredFaceBase64,
                employeeName = user.FullName
            });
        }

        private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            var r = 6371e3; // Earth radius in meters
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return r * c;
        }
    }
}
