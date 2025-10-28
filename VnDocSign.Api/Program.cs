using VnDocSign.Infrastructure.Setup;           // AddInfrastructure()
using VnDocSign.Domain.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using VnDocSign.Infrastructure.Persistence;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Controllers
builder.Services.AddControllers();

// Authorization Policies
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

// JWT
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
        options.RequireHttpsMetadata = false; // DEV
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = false,
            ValidateAudience = false,
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

// Swagger + Bearer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "VnDocSign API", Version = "v1" });

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

// (DEV) Auto-migrate để tránh lỗi bảng chưa tạo
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// /health
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
