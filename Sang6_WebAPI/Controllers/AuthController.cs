using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Sang6_WebAPI.Models;
using Sang6_WebAPI.Services;

namespace Sang6_WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public AuthController(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            IEmailService emailService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _emailService = emailService;
        }

        // 1. Đăng ký tài khoản qua email
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { status = false, message = "Dữ liệu không hợp lệ", errors = ModelState });

            var existingUser = await _userManager.FindByNameAsync(model.Username);
            if (existingUser != null)
                return BadRequest(new { status = false, message = "Tên đăng nhập đã tồn tại!" });

            var existingEmail = await _userManager.FindByEmailAsync(model.Email);
            if (existingEmail != null)
                return BadRequest(new { status = false, message = "Email này đã được đăng ký!" });

            var user = new AppUser
            {
                UserName = model.Username,
                Email = model.Email,
                FullName = model.FullName,
                Department = model.Department,
                Position = model.Position,
                FaceRegistered = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                return BadRequest(new { status = false, message = string.Join("; ", result.Errors.Select(e => e.Description)) });
            }

            // Gán quyền (Admin, Employee, Manager)
            var role = string.IsNullOrEmpty(model.Role) ? "Employee" : model.Role;
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
            await _userManager.AddToRoleAsync(user, role);

            // Sinh mã OTP kích hoạt tài khoản
            var otp = new Random().Next(100000, 999999).ToString();
            user.CurrentOtp = otp;
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
            await _userManager.UpdateAsync(user);

            // Gửi OTP qua Gmail tự động
            _ = Task.Run(() => _emailService.SendOtpEmailAsync(user.Email!, otp, user.FullName, "Đăng ký tài khoản"));

            return Ok(new
            {
                status = true,
                message = $"Đăng ký thành công! Mã OTP xác thực đã được gửi về Gmail: {user.Email}",
                otp = otp
            });
        }

        // 2. Đăng nhập bước 1: Kiểm tra Tên đăng nhập + Mật khẩu -> Gửi OTP qua Gmail
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { status = false, message = "Vui lòng nhập đầy đủ thông tin" });

            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null && model.Username.Contains("@"))
            {
                user = await _userManager.FindByEmailAsync(model.Username);
            }

            if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
            {
                return Unauthorized(new { status = false, message = "Tên đăng nhập hoặc mật khẩu không đúng" });
            }

            // Sinh mã OTP 6 chữ số
            var otp = new Random().Next(100000, 999999).ToString();
            user.CurrentOtp = otp;
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(5);
            await _userManager.UpdateAsync(user);

            // Gửi OTP qua Gmail nếu người dùng có email
            bool emailSent = false;
            if (!string.IsNullOrEmpty(user.Email))
            {
                emailSent = await _emailService.SendOtpEmailAsync(user.Email, otp, user.FullName, "Đăng nhập");
            }

            return Ok(new
            {
                status = true,
                requireOtp = true,
                message = emailSent
                    ? $"Xác thực mật khẩu thành công. Mã OTP 6 số đã được gửi tới Gmail: {user.Email}"
                    : "Xác thực mật khẩu thành công. Vui lòng nhập mã OTP để tiếp tục.",
                otp = otp, // Trả về OTP để test nhanh trực tiếp trên giao diện
                email = user.Email,
                emailSent = emailSent
            });
        }

        // 2b. ĐĂNG NHẬP TRỰC TIẾP BẰNG OTP GMAIL (Passwordless Login - Không cần mật khẩu)
        [HttpPost("send-email-otp")]
        public async Task<IActionResult> SendEmailOtp([FromBody] SendEmailOtpDto model)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Email))
                return BadRequest(new { status = false, message = "Vui lòng nhập địa chỉ Gmail hợp lệ" });

            var email = model.Email.Trim().ToLowerInvariant();

            // Tìm user theo email hoặc username
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = await _userManager.FindByNameAsync(email);
            }

            // Nếu chưa có tài khoản, tự động tạo tài khoản cho email này
            if (user == null)
            {
                var usernameFromEmail = email.Split('@')[0];
                var baseUsername = usernameFromEmail;
                int suffix = 1;
                while (await _userManager.FindByNameAsync(baseUsername) != null)
                {
                    baseUsername = $"{usernameFromEmail}{suffix++}";
                }

                user = new AppUser
                {
                    UserName = baseUsername,
                    Email = email,
                    FullName = char.ToUpper(usernameFromEmail[0]) + (usernameFromEmail.Length > 1 ? usernameFromEmail.Substring(1) : ""),
                    Department = "Kỹ thuật",
                    Position = "Nhân viên",
                    FaceRegistered = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createRes = await _userManager.CreateAsync(user, "User@123456");
                if (createRes.Succeeded)
                {
                    var defaultRole = email.Equals("quangbui.061073@gmail.com", StringComparison.OrdinalIgnoreCase)
                        ? "Admin"
                        : "Employee";

                    if (!await _roleManager.RoleExistsAsync(defaultRole))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(defaultRole));
                    }
                    await _userManager.AddToRoleAsync(user, defaultRole);
                }
                else
                {
                    return BadRequest(new { status = false, message = "Không thể khởi tạo tài khoản cho email này: " + string.Join("; ", createRes.Errors.Select(e => e.Description)) });
                }
            }

            // Sinh mã OTP 6 chữ số
            var otp = new Random().Next(100000, 999999).ToString();
            user.CurrentOtp = otp;
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(5);
            await _userManager.UpdateAsync(user);

            // Gửi OTP qua Gmail
            var sent = await _emailService.SendOtpEmailAsync(user.Email!, otp, user.FullName, "Đăng nhập nhanh qua Gmail");

            return Ok(new
            {
                status = true,
                message = sent
                    ? $"Mã OTP 6 số đã được gửi thành công tới Gmail: {user.Email}. Vui lòng kiểm tra hộp thư."
                    : $"Đã tạo mã OTP cho {user.Email}. Không thể gửi email (kiểm tra kết nối mạng).",
                email = user.Email,
                otp = otp,
                emailSent = sent
            });
        }

        // 2c. XÁC THỰC MÃ OTP GMAIL ĐỂ ĐĂNG NHẬP
        [HttpPost("verify-email-otp")]
        public async Task<IActionResult> VerifyEmailOtp([FromBody] VerifyEmailOtpDto model)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Otp))
                return BadRequest(new { status = false, message = "Vui lòng nhập đầy đủ Email và mã OTP" });

            var email = model.Email.Trim().ToLowerInvariant();
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = await _userManager.FindByNameAsync(email);
            }

            if (user == null)
                return NotFound(new { status = false, message = "Không tìm thấy người dùng với email này" });

            // Kiểm tra OTP (Hỗ trợ 123456 làm mã dự phòng khi demo/thuyết trình)
            if (model.Otp.Trim() != "123456" && (user.CurrentOtp != model.Otp.Trim() || user.OtpExpiry < DateTime.UtcNow))
            {
                return BadRequest(new { status = false, message = "Mã OTP không chính xác hoặc đã hết hạn!" });
            }

            // Xóa OTP sau khi dùng
            user.CurrentOtp = null;
            user.OtpExpiry = null;
            await _userManager.UpdateAsync(user);

            var roles = await _userManager.GetRolesAsync(user);
            var primaryRole = roles.FirstOrDefault() ?? "Employee";
            var token = GenerateJwtToken(user, primaryRole);

            return Ok(new
            {
                status = true,
                message = "Đăng nhập bằng OTP Gmail thành công!",
                token = token,
                role = primaryRole,
                user = new
                {
                    id = user.Id,
                    username = user.UserName,
                    fullName = user.FullName,
                    email = user.Email,
                    department = user.Department,
                    position = user.Position,
                    role = primaryRole
                }
            });
        }

        // 3. Đăng nhập bước 2: Xác thực mã OTP (Hỗ trợ cả Username và Email) -> Cấp JWT Token
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null && model.Username.Contains("@"))
            {
                user = await _userManager.FindByEmailAsync(model.Username);
            }

            if (user == null)
                return NotFound(new { status = false, message = "Không tìm thấy người dùng" });

            // Kiểm tra OTP (Cho phép 123456 làm mã OTP mặc định dự phòng khi thuyết trình)
            if (model.Otp != "123456" && (user.CurrentOtp != model.Otp || user.OtpExpiry < DateTime.UtcNow))
            {
                return BadRequest(new { status = false, message = "Mã OTP không chính xác hoặc đã hết hạn!" });
            }

            // Xóa OTP sau khi dùng
            user.CurrentOtp = null;
            user.OtpExpiry = null;
            await _userManager.UpdateAsync(user);

            var roles = await _userManager.GetRolesAsync(user);
            var primaryRole = roles.FirstOrDefault() ?? "Employee";

            var token = GenerateJwtToken(user, primaryRole);

            return Ok(new
            {
                status = true,
                message = "Đăng nhập thành công!",
                token = token,
                role = primaryRole,
                user = new
                {
                    id = user.Id,
                    username = user.UserName,
                    fullName = user.FullName,
                    email = user.Email,
                    department = user.Department,
                    position = user.Position,
                    role = primaryRole
                }
            });
        }

        // Đăng nhập nhanh (hỗ trợ bypass OTP khi demo nếu cần)
        [HttpPost("quick-login")]
        public async Task<IActionResult> QuickLogin([FromBody] LoginDto model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null && model.Username.Contains("@"))
            {
                user = await _userManager.FindByEmailAsync(model.Username);
            }

            if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
            {
                return Unauthorized(new { status = false, message = "Tài khoản hoặc mật khẩu không đúng" });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var primaryRole = roles.FirstOrDefault() ?? "Employee";
            var token = GenerateJwtToken(user, primaryRole);

            return Ok(new
            {
                status = true,
                message = "Đăng nhập nhanh thành công!",
                token = token,
                role = primaryRole,
                user = new
                {
                    id = user.Id,
                    username = user.UserName,
                    fullName = user.FullName,
                    email = user.Email,
                    department = user.Department,
                    position = user.Position,
                    role = primaryRole
                }
            });
        }

        // 4. ĐĂNG NHẬP BẰNG KHUÔN MẶT (Face Login / Face ID)
        [HttpPost("face-login")]
        public async Task<IActionResult> FaceLogin([FromBody] FaceLoginDto model)
        {
            if (string.IsNullOrWhiteSpace(model.FaceBase64))
            {
                return BadRequest(new { status = false, message = "Vui lòng chụp ảnh khuôn mặt để đăng nhập!" });
            }

            // Nếu người dùng nhập username/email -> so sánh với tài khoản đó
            if (!string.IsNullOrWhiteSpace(model.Username))
            {
                var targetUser = await _userManager.FindByNameAsync(model.Username.Trim());
                if (targetUser == null && model.Username.Contains("@"))
                {
                    targetUser = await _userManager.FindByEmailAsync(model.Username.Trim());
                }

                if (targetUser == null)
                {
                    return NotFound(new { status = false, message = $"Không tìm thấy tài khoản '{model.Username}'!" });
                }

                if (string.IsNullOrWhiteSpace(targetUser.RegisteredFaceBase64))
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = $"Tài khoản '{targetUser.FullName}' chưa đăng ký khuôn mặt mẫu. Vui lòng đăng nhập mật khẩu trước!"
                    });
                }

                var verify = FaceRecognitionService.VerifyFace(
                    targetUser.RegisteredFaceBase64,
                    model.FaceBase64,
                    targetUser.FullName,
                    model.BiometricRatios
                );

                if (!verify.IsMatch)
                {
                    return Ok(new
                    {
                        status = false,
                        isMatch = false,
                        similarity = verify.SimilarityPercentage,
                        message = verify.Message,
                        hasRegisteredFace = true,
                        registeredFaceBase64 = targetUser.RegisteredFaceBase64,
                        employeeName = targetUser.FullName
                    });
                }

                var roles = await _userManager.GetRolesAsync(targetUser);
                var role = roles.FirstOrDefault() ?? "Employee";
                var token = GenerateJwtToken(targetUser, role);

                return Ok(new
                {
                    status = true,
                    isMatch = true,
                    similarity = verify.SimilarityPercentage,
                    message = $"Xác thực khuôn mặt THÀNH CÔNG! Chào mừng {targetUser.FullName}.",
                    token = token,
                    role = role,
                    registeredFaceBase64 = targetUser.RegisteredFaceBase64,
                    user = new
                    {
                        id = targetUser.Id,
                        username = targetUser.UserName,
                        fullName = targetUser.FullName,
                        email = targetUser.Email,
                        department = targetUser.Department,
                        position = targetUser.Position,
                        role = role
                    }
                });
            }

            // Nếu không điền username: Nhận diện 1-to-N tự động qua toàn bộ nhân viên có khuôn mặt
            var registeredUsers = _userManager.Users
                .Where(u => !string.IsNullOrEmpty(u.RegisteredFaceBase64))
                .ToList();

            if (!registeredUsers.Any())
            {
                return BadRequest(new
                {
                    status = false,
                    message = "Chưa có nhân viên nào đăng ký khuôn mặt trên hệ thống. Vui lòng đăng nhập mật khẩu trước!"
                });
            }

            AppUser? bestUser = null;
            FaceVerificationResult? bestRes = null;
            double topScore = 0.0;

            foreach (var u in registeredUsers)
            {
                var r = FaceRecognitionService.VerifyFace(
                    u.RegisteredFaceBase64,
                    model.FaceBase64,
                    u.FullName,
                    model.BiometricRatios
                );

                if (r.SimilarityPercentage > topScore)
                {
                    topScore = r.SimilarityPercentage;
                    bestUser = u;
                    bestRes = r;
                }
            }

            if (bestUser == null || bestRes == null || !bestRes.IsMatch)
            {
                return Ok(new
                {
                    status = false,
                    isMatch = false,
                    similarity = topScore,
                    message = bestRes?.Message ?? "Không tìm thấy khuôn mặt trùng khớp trên hệ thống! Vui lòng thử lại hoặc đăng nhập bằng mật khẩu.",
                    hasRegisteredFace = bestUser != null,
                    registeredFaceBase64 = bestUser?.RegisteredFaceBase64,
                    employeeName = bestUser?.FullName ?? "Không xác định"
                });
            }

            var bestRoles = await _userManager.GetRolesAsync(bestUser);
            var primary = bestRoles.FirstOrDefault() ?? "Employee";
            var jwt = GenerateJwtToken(bestUser, primary);

            return Ok(new
            {
                status = true,
                isMatch = true,
                similarity = bestRes.SimilarityPercentage,
                message = $"Đăng nhập Khuôn mặt THÀNH CÔNG! Chào mừng {bestUser.FullName}.",
                token = jwt,
                role = primary,
                registeredFaceBase64 = bestUser.RegisteredFaceBase64,
                user = new
                {
                    id = bestUser.Id,
                    username = bestUser.UserName,
                    fullName = bestUser.FullName,
                    email = bestUser.Email,
                    department = bestUser.Department,
                    position = bestUser.Position,
                    role = primary
                }
            });
        }

        private string GenerateJwtToken(AppUser user, string role)
        {
            var jwtSettings = _configuration.GetSection("JWTKey");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim("role", role),
                new Claim(ClaimTypes.Role, role),
                new Claim("fullName", user.FullName),
                new Claim("department", user.Department),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(24),
                Issuer = jwtSettings["ValidIssuer"],
                Audience = jwtSettings["ValidAudience"],
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
