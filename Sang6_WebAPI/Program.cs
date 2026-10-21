using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Sang6_WebAPI.Data;
using Sang6_WebAPI.Models;
using Sang6_WebAPI.Services;

// Bật legacy timestamp behavior cho Npgsql để tương thích với DateTime.Now và DateTime.Today
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Database PostgreSQL (Tên database cá nhân: buinguyencongnghiep_chamcong)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Cấu hình Identity với AppUser
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 3. Đăng ký Controllers & Services
builder.Services.AddControllers();
builder.Services.AddScoped<IEmailService, EmailService>();

// 4. Cấu hình CORS để Flutter Mobile và Flutter Web gọi được API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 5. Cấu hình JWT Bearer Authentication
var jwtSettings = builder.Configuration.GetSection("JWTKey");
var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["ValidIssuer"],
        ValidAudience = jwtSettings["ValidAudience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddAuthorization();

// 6. Cấu hình Swagger UI với tên cá nhân Bùi Nguyễn Công Nghiệp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Bùi Nguyễn Công Nghiệp - Hệ Thống Chấm Công API",
        Version = "v1",
        Description = "Web API chấm công thông minh (Face, GPS, QR, Đo tiếng ồn môi trường) cho nhân viên"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập token JWT theo định dạng: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer"),
            new List<string>()
        }
    });
});

var app = builder.Build();

// 7. Tự động khởi tạo Database, Roles và dữ liệu mẫu chuẩn cá nhân
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    
    // Tự động sinh bảng trong PostgreSQL
    await context.Database.EnsureCreatedAsync();

    // Đảm bảo cột NoiseLevelDb tồn tại trong PostgreSQL
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Attendances\" ADD COLUMN IF NOT EXISTS \"NoiseLevelDb\" double precision;"
        );
    }
    catch { }

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    // Khởi tạo 3 Roles: Admin, Employee, Manager
    string[] roles = { "Admin", "Employee", "Manager" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // 1. Tạo tài khoản Admin (Bùi Nguyễn Công Nghiệp)
    var adminUser = await userManager.FindByNameAsync("admin");
    if (adminUser == null)
    {
        adminUser = new AppUser
        {
            UserName = "admin",
            Email = "buinguyencongnghiep@gmail.com",
            FullName = "Bùi Nguyễn Công Nghiệp",
            Department = "Ban Giám Đốc",
            Position = "Giám đốc điều hành",
            BaseSalary = 30000000,
            FaceRegistered = true,
            CreatedAt = DateTime.UtcNow
        };
        var res = await userManager.CreateAsync(adminUser, "Admin@123");
        if (res.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }

    // 1b. Tạo tài khoản Quang Bùi (quangbui.061073@gmail.com)
    var quangUser = await userManager.FindByEmailAsync("quangbui.061073@gmail.com");
    if (quangUser == null)
    {
        quangUser = await userManager.FindByNameAsync("quangbui");
    }
    if (quangUser == null)
    {
        quangUser = new AppUser
        {
            UserName = "quangbui",
            Email = "quangbui.061073@gmail.com",
            FullName = "Quang Bùi",
            Department = "Ban Quản Trị",
            Position = "Quản trị viên hệ thống",
            BaseSalary = 25000000,
            FaceRegistered = true,
            CreatedAt = DateTime.UtcNow
        };
        var res = await userManager.CreateAsync(quangUser, "Quang@123456");
        if (res.Succeeded)
        {
            await userManager.AddToRoleAsync(quangUser, "Admin");
            await userManager.AddToRoleAsync(quangUser, "Employee");
        }
    }

    // 2. Tạo tài khoản Nhân viên mẫu (Employee)
    var empUser = await userManager.FindByNameAsync("nhanvien1");
    if (empUser == null)
    {
        empUser = new AppUser
        {
            UserName = "nhanvien1",
            Email = "nhanvien1@gmail.com",
            FullName = "Nguyễn Văn An",
            Department = "Kỹ thuật",
            Position = "Kỹ sư phần mềm",
            BaseSalary = 15000000,
            FaceRegistered = true,
            CreatedAt = DateTime.UtcNow
        };
        var res = await userManager.CreateAsync(empUser, "Staff@123");
        if (res.Succeeded)
        {
            await userManager.AddToRoleAsync(empUser, "Employee");
        }
    }

    // 3. Tạo tài khoản Quản lý mẫu (Manager)
    var mgrUser = await userManager.FindByNameAsync("quanly1");
    if (mgrUser == null)
    {
        mgrUser = new AppUser
        {
            UserName = "quanly1",
            Email = "quanly1@gmail.com",
            FullName = "Trần Thị Bình",
            Department = "Nhân sự",
            Position = "Trưởng phòng Nhân sự",
            BaseSalary = 20000000,
            FaceRegistered = true,
            CreatedAt = DateTime.UtcNow
        };
        var res = await userManager.CreateAsync(mgrUser, "Manager@123");
        if (res.Succeeded)
        {
            await userManager.AddToRoleAsync(mgrUser, "Manager");
        }
    }

    // 4. Khởi tạo Ca làm việc mẫu (Shifts)
    if (!await context.Shifts.AnyAsync())
    {
        var shifts = new[]
        {
            new Shift { Name = "Ca Hành Chính (08:00 - 17:00)", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(17, 0, 0), LateGraceMinutes = 15, HourlyRate = 60000 },
            new Shift { Name = "Ca Sáng (08:00 - 12:00)", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(12, 0, 0), LateGraceMinutes = 10, HourlyRate = 50000 },
            new Shift { Name = "Ca Chiều (13:30 - 17:30)", StartTime = new TimeSpan(13, 30, 0), EndTime = new TimeSpan(17, 30, 0), LateGraceMinutes = 10, HourlyRate = 50000 }
        };
        await context.Shifts.AddRangeAsync(shifts);
        await context.SaveChangesAsync();
    }

    // 5. Khởi tạo một số bản ghi chấm công mẫu
    if (!await context.Attendances.AnyAsync() && empUser != null)
    {
        var shift = await context.Shifts.FirstOrDefaultAsync();
        var yesterday = DateTime.Today.AddDays(-1);
        context.Attendances.Add(new Attendance
        {
            UserId = empUser.Id,
            ShiftId = shift?.Id,
            Date = yesterday,
            CheckInTime = yesterday.AddHours(8).AddMinutes(5),
            CheckOutTime = yesterday.AddHours(17).AddMinutes(2),
            TotalHours = 8.5,
            Status = "Đúng giờ",
            CheckInMethod = "Face",
            SalaryEarned = 510000,
            Note = "Nhận diện khuôn mặt AI thành công"
        });
        await context.SaveChangesAsync();
    }

    // 6. Khởi tạo đơn xin nghỉ phép mẫu
    if (!await context.LeaveRequests.AnyAsync() && empUser != null)
    {
        context.LeaveRequests.Add(new LeaveRequest
        {
            UserId = empUser.Id,
            StartDate = DateTime.Today.AddDays(3),
            EndDate = DateTime.Today.AddDays(4),
            LeaveType = "Nghỉ phép năm",
            Reason = "Giải quyết việc gia đình",
            Status = "Chờ duyệt",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }
}

// 8. Cấu hình Middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "BuiNguyenCongNghiep API v1");
    c.RoutePrefix = string.Empty;
});

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
