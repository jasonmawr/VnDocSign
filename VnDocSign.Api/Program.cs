using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VnDocSign.Infrastructure.Setup;
using VnDocSign.Domain.Security;
using VnDocSign.Infrastructure.Persistence;
using VnDocSign.Application.Contracts.Interfaces.Signing;
using VnDocSign.Infrastructure.Services;
using VnDocSign.Application.Contracts.Interfaces.Signatures;
using VnDocSign.Application.Contracts.Interfaces.Users;
using VnDocSign.Application.Contracts.Interfaces.Auth;
using VnDocSign.Application.Contracts.Interfaces.Departments;
using VnDocSign.Application.Contracts.Interfaces.Dossiers;
using VnDocSign.Application.Contracts.Interfaces.Security;
using VnDocSign.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// Ensure FileStorage root exists
var storageRoot = builder.Configuration["FileStorage:Root"] ?? "./data";
Directory.CreateDirectory(storageRoot);


// MySQL
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Default");
    var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
    opt.UseMySql(cs, serverVersion, o => o.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
});

// Bind JWT options + service
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Services
builder.Services.AddScoped<ISignActivationService, SignActivationService>();
builder.Services.AddScoped<ISignatureService, SignatureService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDossierService, DossierService>();
builder.Services.AddScoped<ISigningService, SigningService>();

// AuthN/AuthZ
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        var keyStr = jwt["Key"] ?? throw new InvalidOperationException("Missing Jwt:Key");
        byte[] keyBytes;
        try { keyBytes = Convert.FromBase64String(keyStr); } catch { keyBytes = Encoding.UTF8.GetBytes(keyStr); }

        o.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", p => p.RequireRole(RoleNames.Admin));
    options.AddPolicy("RequireApprover", p => p.RequireRole(RoleNames.TruongKhoa, RoleNames.PhoKhoa, RoleNames.TruongPhong, RoleNames.PhoPhong, RoleNames.PhoGiamDoc, RoleNames.GiamDoc));
    options.AddPolicy("RequireClerk", p => p.RequireRole(RoleNames.VanThu));
});

// CORS (khoá origin khi lên PROD)
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()

        .AllowAnyMethod());
});

////PROD
//builder.Services.AddCors(opt =>
//{
//    opt.AddPolicy("ProdCors", p => p
//        .WithOrigins("https://trinhky-ui.benhvien.local", "https://10.10.10.20") // thay bằng domain/IP UI thật
//        .AllowAnyHeader()
//        .AllowAnyMethod());
//});


// Controllers + Swagger (JWT)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Thêm nút Authorize (Bearer) trong Swagger
    c.SwaggerDoc("v1", new() { Title = "VnDocSign API", Version = "v1" });
    var jwtSecurityScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        BearerFormat = "JWT",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        Description = "Nhập: Bearer {token}",
        Reference = new Microsoft.OpenApi.Models.OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme
        }
    };
    c.AddSecurityDefinition(jwtSecurityScheme.Reference.Id, jwtSecurityScheme);
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        { jwtSecurityScheme, Array.Empty<string>() }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health
app.MapGet("/health", () => Results.Ok(new { ok = true, tsUtc = DateTime.UtcNow }));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // migrate + seed theo thứ tự
    await db.Database.MigrateAsync();

    // truyền delegate hash: BCrypt ở lớp Api (đúng kiến trúc)
    Func<string, string> hash = s => BCrypt.Net.BCrypt.HashPassword(s);
    
    await DbSeeder.SeedDepartmentsAsync(db);
    await DbSeeder.SeedRolesAsync(db);
    await DbSeeder.SeedAdminAsync(db, hash);                  // lấy PWD từ ENV nếu có
    await DbSeeder.SeedLeadersAsync(db, hash, "123");   // mật khẩu mặc định cho lãnh đạo
    await DbSeeder.SeedSignSlotsAsync(db);
    await DbSeeder.SeedSystemConfigAsync(db);
}

app.Run();
