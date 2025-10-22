using VnDocSign.Infrastructure.Setup;           // AddInfrastructure()
using VnDocSign.Domain.Security;
using VnDocSign.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;


// RoleNames

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Authorization (RBAC)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", p => p.RequireRole(RoleNames.Admin));
    options.AddPolicy("RequireClerk", p => p.RequireRole(RoleNames.VanThu));
    options.AddPolicy("RequireApprover", p => p.RequireRole(
        RoleNames.TruongPhong, RoleNames.PhoPhong,
        RoleNames.TruongKhoa, RoleNames.PhoKhoa,
        RoleNames.KeToanTruong, RoleNames.ChuTichCongDoan,
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
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// app.MapHealthChecks("/health"); // nếu có HealthChecks

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
