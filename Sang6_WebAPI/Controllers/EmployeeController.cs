using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sang6_WebAPI.Models;

namespace Sang6_WebAPI.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeeController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public EmployeeController(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // 1. GET: Danh sách tất cả nhân viên
        [HttpGet]
        public async Task<IActionResult> GetEmployees()
        {
            var users = await _userManager.Users.ToListAsync();
            var result = new List<object>();

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                result.Add(new
                {
                    u.Id,
                    u.UserName,
                    u.FullName,
                    u.Email,
                    u.Department,
                    u.Position,
                    u.BaseSalary,
                    u.FaceRegistered,
                    Role = roles.FirstOrDefault() ?? "Employee",
                    u.CreatedAt
                });
            }

            return Ok(result);
        }

        // 2. GET: Chi tiết 1 nhân viên
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEmployee(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { status = false, message = "Không tìm thấy nhân viên" });

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new
            {
                user.Id,
                user.UserName,
                user.FullName,
                user.Email,
                user.Department,
                user.Position,
                user.BaseSalary,
                Role = roles.FirstOrDefault() ?? "Employee"
            });
        }

        // 3. POST: Thêm nhân viên mới (Chỉ Admin)
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateEmployee([FromBody] RegisterDto dto)
        {
            var user = new AppUser
            {
                UserName = dto.Username,
                Email = dto.Email,
                FullName = dto.FullName,
                Department = dto.Department,
                Position = dto.Position,
                FaceRegistered = true,
                CreatedAt = DateTime.UtcNow
            };

            var res = await _userManager.CreateAsync(user, dto.Password);
            if (!res.Succeeded)
            {
                return BadRequest(new { status = false, message = string.Join("; ", res.Errors.Select(e => e.Description)) });
            }

            var role = string.IsNullOrEmpty(dto.Role) ? "Employee" : dto.Role;
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
            await _userManager.AddToRoleAsync(user, role);

            return Ok(new { status = true, message = "Thêm nhân viên thành công!", data = user });
        }

        // 4. PUT: Cập nhật thông tin nhân viên
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEmployee(string id, [FromBody] RegisterDto dto)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { status = false, message = "Không tìm thấy nhân viên" });

            user.FullName = dto.FullName;
            user.Email = dto.Email;
            user.Department = dto.Department;
            user.Position = dto.Position;

            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
            {
                return BadRequest(new { status = false, message = "Cập nhật không thành công" });
            }

            if (!string.IsNullOrEmpty(dto.Role))
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!await _roleManager.RoleExistsAsync(dto.Role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(dto.Role));
                }
                await _userManager.AddToRoleAsync(user, dto.Role);
            }

            return Ok(new { status = true, message = "Cập nhật thông tin nhân viên thành công!" });
        }

        // 5. DELETE: Xóa nhân viên (Chỉ Admin)
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmployee(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { status = false, message = "Không tìm thấy nhân viên" });

            if (user.UserName?.ToLower() == "admin")
            {
                return BadRequest(new { status = false, message = "Không được xóa tài khoản Quản trị viên tối cao!" });
            }

            var res = await _userManager.DeleteAsync(user);
            if (!res.Succeeded)
            {
                return BadRequest(new { status = false, message = "Xóa không thành công" });
            }

            return Ok(new { status = true, message = "Đã xóa nhân viên khỏi hệ thống" });
        }
    }
}
