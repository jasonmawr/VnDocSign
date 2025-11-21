using System;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using VnDocSign.Domain.Security;
using VnDocSign.Infrastructure.Persistence;
using VnDocSign.Infrastructure.Setup;

var builder = WebApplication.CreateBuilder(args);

// ========== Infrastructure ==========
builder.Services.AddInfrastructure(builder.Configuration);

// ========== Controllers ==========
builder.Services.AddControllers();

// ========== Authorization Policies ==========
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", p => p.RequireRole(RoleNames.Admin));
    options.AddPolicy("RequireClerk", p => p.RequireRole(RoleNames.VanThu, RoleNames.Admin));
    options.AddPolicy("RequireApprover", p => p.RequireRole(
        RoleNames.ChuyenVien,
        RoleNames.TruongPhong, RoleNames.PhoPhong,
        RoleNames.TruongKhoa, RoleNames.PhoKhoa,
        RoleNames.KeToanTruong, RoleNames.ChuTichCongDoan,
        RoleNames.DieuDuongTruong, RoleNames.KTVTruong,
        RoleNames.PhoGiamDoc, RoleNames.GiamDoc
    ));
    options.AddPolicy("RequireDeptHead", p => p.RequireRole(
        RoleNames.TruongPhong, RoleNames.PhoPhong,
        RoleNames.TruongKhoa, RoleNames.PhoKhoa
    ));
    options.AddPolicy("RequireFunctionalHeads", p => p.RequireRole(
        RoleNames.KeToanTruong, RoleNames.ChuTichCongDoan,
        RoleNames.DieuDuongTruong, RoleNames.KTVTruong
    ));
    options.AddPolicy("RequireBoard", p => p.RequireRole(
        RoleNames.PhoGiamDoc, RoleNames.GiamDoc
    ));
});

// ========== JWT ==========
var jwtKey = Environment.GetEnvironmentVariable("VN_DOCSIGN_JWT_KEY")
             ?? builder.Configuration["Jwt:Key"]
             ?? throw new InvalidOperationException("Missing JWT key (ENV VN_DOCSIGN_JWT_KEY or Jwt:Key)");

var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];

        options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

        // ===== NEW: bật xác thực Issuer/Audience =====
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidIssuer = issuer,
        ValidAudience = audience,

        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

// ========== CORS ==========
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

// ========== Swagger ==========
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "VnDocSign API", Version = "v1" });

    // Tránh xung đột schema khi có nhiều lớp trùng tên (chuẩn thực tế, không vá tạm)
    c.CustomSchemaIds(t => t.FullName);

    // Chỉ include XML docs nếu file tồn tại để không ném lỗi khi chưa bật GenerateDocumentationFile
    var apiXml = Path.Combine(AppContext.BaseDirectory, "VnDocSign.Api.xml");
    if (File.Exists(apiXml)) c.IncludeXmlComments(apiXml, includeControllerXmlComments: true);
    var appXml = Path.Combine(AppContext.BaseDirectory, "VnDocSign.Application.xml");
    if (File.Exists(appXml)) c.IncludeXmlComments(appXml, includeControllerXmlComments: true);

    // Bearer security
    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Nhập Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtScheme, Array.Empty<string>() } });
});

var app = builder.Build();

// (DEV) Auto-migrate để tránh lỗi bảng chưa tạo khi chạy local
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Middleware order
app.UseCors();
// Add global exception handling
app.UseMiddleware<VnDocSign.Api.Middlewares.ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// Swagger chỉ bật ở DEV
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map controllers
app.MapControllers();

// /health endpoint
app.MapGet("/health", async (IServiceProvider sp) =>
{
    using var scope = sp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var can = await db.Database.CanConnectAsync();
    return Results.Json(new { status = can ? "Healthy" : "Degraded" });
});

app.Run();
