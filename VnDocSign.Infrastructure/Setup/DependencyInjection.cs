using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using VnDocSign.Application.Contracts.Interfaces;
using VnDocSign.Application.Contracts.Interfaces.Integration;
using VnDocSign.Application.Contracts.Interfaces.Security;
using VnDocSign.Application.Contracts.Interfaces.Signing;
using VnDocSign.Infrastructure.Clients;
using VnDocSign.Infrastructure.Documents;
using VnDocSign.Infrastructure.Documents.Converters;
using VnDocSign.Infrastructure.Persistence;
using VnDocSign.Infrastructure.Security;
using VnDocSign.Infrastructure.Services;
using VnDocSign.Infrastructure.Setup.Options;

namespace VnDocSign.Infrastructure.Setup
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            // DbContext (MySQL 8.0.36)
            services.AddDbContext<AppDbContext>(opt =>
            {
                var cs = config.GetConnectionString("Default")
                         ?? throw new InvalidOperationException("Missing ConnectionStrings:Default");
                var sv = new MySqlServerVersion(new Version(8, 0, 36));
                opt.UseMySql(cs, sv, o => o.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            });

            // HttpClient cho SSM (lấy timeout từ cấu hình)
            services.AddHttpClient("ssm", (sp, c) =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>();
                var timeoutSec = cfg.GetValue<int?>("Ssm:TimeoutSeconds") ?? 60;
                c.Timeout = TimeSpan.FromSeconds(timeoutSec);
            });

            // Clients/Services (đúng tên service hiện có)
            services.AddScoped<ISsmClient, SsmClient>();                 // Bước SSM live sẽ chi tiết sau
            services.AddScoped<IPdfRenderService, PdfRenderService>();
            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<ISigningService, SigningService>();
            services.AddScoped<ISignActivationService, SignActivationService>();
            services.AddSingleton<IFileVersioningService, FileVersioningService>();

            // Bind options
            services.Configure<FileStorageOptions>(config.GetSection("FileStorage"));
            services.Configure<SsmOptions>(config.GetSection("Ssm"));

            // PDF Converter (LibreOffice)
            services.Configure<SofficePdfConverter.Options>(opts =>
            {
                opts.SofficePath = config["Pdf:Converter:SofficePath"]; // ví dụ: "soffice"
                if (int.TryParse(config["Pdf:Converter:TimeoutSeconds"], out var timeout) && timeout > 0)
                    opts.TimeoutSeconds = timeout;
            });
            services.AddSingleton<IPdfConverter, SofficePdfConverter>();

            return services;
        }
    }
}
