using VnDocSign.Infrastructure.Setup;           // AddInfrastructure()
using VnDocSign.Domain.Security;
using VnDocSign.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using VnDocSign.Infrastructure.Persistence;
using Microsoft.OpenApi.Models;


// RoleNames

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Controllers + Swagger
builder.Services.AddControllers();

builder.Services.AddAuthorization(options =>
{
    // Admin hệ thống
    options.AddPolicy("RequireAdmin", p => p.RequireRole(RoleNames.Admin));

    // Văn thư (xác nhận mở bước Giám đốc)
    options.AddPolicy("RequireClerk", p => p.RequireRole(RoleNames.VanThu, RoleNames.Admin));

    // Tất cả những người có thể ký/duyệt trong luồng
    options.AddPolicy("RequireApprover", p => p.RequireRole(
        RoleNames.ChuyenVien,
        RoleNames.TruongPhong, RoleNames.PhoPhong,
        RoleNames.TruongKhoa, RoleNames.PhoKhoa,
        RoleNames.KeToanTruong, RoleNames.ChuTichCongDoan,
        RoleNames.DieuDuongTruong, RoleNames.KTVTruong,
        RoleNames.PhoGiamDoc, RoleNames.GiamDoc
    ));

    // (Tuỳ chọn) Nhóm lãnh đạo phòng/khoa
    options.AddPolicy("RequireDeptHead", p => p.RequireRole(
        RoleNames.TruongPhong, RoleNames.PhoPhong,
        RoleNames.TruongKhoa, RoleNames.PhoKhoa
    ));

    // (Tuỳ chọn) Nhóm chức năng phối hợp
    options.AddPolicy("RequireFunctionalHeads", p => p.RequireRole(
        RoleNames.KeToanTruong, RoleNames.ChuTichCongDoan,
        RoleNames.DieuDuongTruong, RoleNames.KTVTruong
    ));

    // (Tuỳ chọn) Ban Giám đốc
    options.AddPolicy("RequireBoard", p => p.RequireRole(
        RoleNames.PhoGiamDoc, RoleNames.GiamDoc
    ));
});

// JWT (đọc key từ ENV trước, dev fallback)
var jwtKey = Environment.GetEnvironmentVariable("VN_DOCSIGN_JWT_KEY")
             ?? builder.Configuration["Jwt:Key"]
             ?? throw new InvalidOperationException("Missing JWT key (ENV VN_DOCSIGN_JWT_KEY or Jwt:Key)");

// AUTH (JWT)
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;      // DEV có thể false; PROD nên true nếu có HTTPS reverse proxy
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

            // Hệ thống hiện tại chưa dùng Issuer/Audience chuẩn hoá → tắt validate 2 mục này
            ValidateIssuer = false,
            ValidateAudience = false,

            // Bắt buộc kiểm lifetime (hết hạn là từ chối), cho lệch giờ nhẹ
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

// CORS
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]?>()
                     ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "VnDocSign API", Version = "v1" });
});

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// app.MapHealthChecks("/health"); // nếu có HealthChecks
app.MapGet("/health", async (IServiceProvider sp) =>
{
    using var scope = sp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var can = await db.Database.CanConnectAsync();
    return Results.Json(new { status = can ? "Healthy" : "Degraded" });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
