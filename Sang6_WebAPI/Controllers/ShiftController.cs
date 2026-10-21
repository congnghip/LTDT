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
    public class ShiftController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ShiftController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. GET: Danh sách ca làm việc
        [HttpGet]
        public async Task<IActionResult> GetShifts()
        {
            var shifts = await _context.Shifts.ToListAsync();
            return Ok(shifts);
        }

        // 2. GET: Chi tiết 1 ca
        [HttpGet("{id}")]
        public async Task<IActionResult> GetShift(int id)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift == null) return NotFound(new { status = false, message = "Không tìm thấy ca làm việc" });
            return Ok(shift);
        }

        // 3. POST: Thêm ca mới (Admin)
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateShift([FromBody] CreateShiftDto dto)
        {
            TimeSpan.TryParse(dto.StartTime, out var start);
            TimeSpan.TryParse(dto.EndTime, out var end);

            var shift = new Shift
            {
                Name = dto.Name,
                StartTime = start,
                EndTime = end,
                LateGraceMinutes = dto.LateGraceMinutes,
                HourlyRate = dto.HourlyRate
            };

            _context.Shifts.Add(shift);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetShift), new { id = shift.Id }, shift);
        }

        // 4. PUT: Cập nhật ca làm việc (Admin)
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateShift(int id, [FromBody] CreateShiftDto dto)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift == null) return NotFound(new { status = false, message = "Không tìm thấy ca làm việc" });

            TimeSpan.TryParse(dto.StartTime, out var start);
            TimeSpan.TryParse(dto.EndTime, out var end);

            shift.Name = dto.Name;
            shift.StartTime = start;
            shift.EndTime = end;
            shift.LateGraceMinutes = dto.LateGraceMinutes;
            shift.HourlyRate = dto.HourlyRate;

            await _context.SaveChangesAsync();
            return Ok(new { status = true, message = "Cập nhật ca làm việc thành công!", data = shift });
        }

        // 5. DELETE: Xóa ca làm việc (Admin)
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteShift(int id)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift == null) return NotFound(new { status = false, message = "Không tìm thấy ca làm việc" });

            _context.Shifts.Remove(shift);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Xóa ca làm việc thành công" });
        }
    }
}
