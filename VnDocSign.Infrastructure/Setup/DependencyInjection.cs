using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using VnDocSign.Application.Contracts.Interfaces;
using VnDocSign.Application.Contracts.Interfaces.Integration;
using VnDocSign.Application.Contracts.Interfaces.Security;
using VnDocSign.Application.Contracts.Interfaces.Signing;
using VnDocSign.Application.Contracts.Interfaces.Auth;
using VnDocSign.Application.Contracts.Interfaces.Departments;
using VnDocSign.Application.Contracts.Interfaces.Dossiers;
using VnDocSign.Application.Contracts.Interfaces.Signatures;
using VnDocSign.Application.Contracts.Interfaces.Users;
using VnDocSign.Application.Contracts.Interfaces.Documents;

using VnDocSign.Infrastructure.Clients;
using VnDocSign.Infrastructure.Documents;
using VnDocSign.Infrastructure.Documents.Converters;
using VnDocSign.Infrastructure.Persistence;
using VnDocSign.Infrastructure.Security;
using VnDocSign.Infrastructure.Services;
using VnDocSign.Infrastructure.Setup.Options;
using VnDocSign.Application.Contracts.Interfaces.DigitalIdentities;
using VnDocSign.Application.Contracts.Interfaces.Configs;

namespace VnDocSign.Infrastructure.Setup
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            // === DbContext (MySQL 8.0.36) ===
            services.AddDbContext<AppDbContext>(opt =>
            {
                var cs = config.GetConnectionString("Default")
                         ?? throw new InvalidOperationException("Missing ConnectionStrings:Default");
                var sv = new MySqlServerVersion(new Version(8, 0, 36));
                opt.UseMySql(cs, sv, o => o.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            });

            // === SSM Client ===
            var ssmMode = config["Ssm:Mode"]; // "Live" (default) hoặc "Mock"
            if (string.Equals(ssmMode, "Mock", StringComparison.OrdinalIgnoreCase))
            {
                // MockSsmClient chỉ để dev/test offline (chưa gọi thật ra SSM)
                services.AddScoped<ISsmClient, MockSsmClient>();
            }
            else
            {
                // 1) Named HttpClient cho SSM (BaseAddress/Timeout/Bearer)
                services.AddHttpClient("ssm", (sp, c) =>
                {
                    var cfg = sp.GetRequiredService<IConfiguration>();
                    var baseUrl = cfg["Ssm:BaseUrl"];
                    if (!string.IsNullOrWhiteSpace(baseUrl))
                        c.BaseAddress = new Uri(baseUrl);

                    c.Timeout = TimeSpan.FromSeconds(cfg.GetValue<int?>("Ssm:TimeoutSeconds") ?? 60);

                    var useBearer = cfg.GetValue<bool>("Ssm:UseBearer");
                    var staticBearer = cfg["Ssm:StaticBearer"];
                    if (useBearer && !string.IsNullOrWhiteSpace(staticBearer))
                    {
                        c.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", staticBearer);
                    }
                });

                // 2) Factory: tạo SsmClient với HttpClient + auto-wire các deps còn lại
                services.AddScoped<ISsmClient>(sp =>
                {
                    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ssm");
                    // ActivatorUtilities sẽ tự inject IOptions<SsmOptions>, ILogger<SsmClient>, ...
                    return ActivatorUtilities.CreateInstance<SsmClient>(sp, http);
                });
            }

            // === Documents & File Versioning ===
            services.AddScoped<IPdfRenderService, PdfRenderService>();
            services.AddSingleton<IFileVersioningService, FileVersioningService>();

            // === PDF Converter (LibreOffice) ===
            services.Configure<SofficePdfConverter.Options>(opts =>
            {
                opts.SofficePath = config["Pdf:Converter:SofficePath"]; // ví dụ: "soffice"
                if (int.TryParse(config["Pdf:Converter:TimeoutSeconds"], out var timeout) && timeout > 0)
                    opts.TimeoutSeconds = timeout;
            });
            services.AddSingleton<IPdfConverter, SofficePdfConverter>();

            // === Security / Auth ===
            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<IAuthService, AuthService>();

            // === Core Application Services ===
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IDepartmentService, DepartmentService>();
            services.AddScoped<IDossierService, DossierService>();
            services.AddScoped<ISignatureService, SignatureService>();
            services.AddScoped<ISigningService, SigningService>();
            services.AddScoped<ISignActivationService, SignActivationService>();
            services.AddScoped<ITemplateService, TemplateService>();
            services.AddScoped<IDossierContentService, DossierContentService>();
            services.AddScoped<IDigitalIdentityService, DigitalIdentityService>();
            services.AddScoped<ISystemConfigService, SystemConfigService>();
            services.AddScoped<IUserDelegationService, UserDelegationService>();

            // === Options binding ===
            services.Configure<FileStorageOptions>(config.GetSection("FileStorage"));
            services.Configure<SsmOptions>(config.GetSection("Ssm"));

            return services;
        }
    }
}
