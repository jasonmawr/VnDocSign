using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VnDocSign.Application.Contracts.Interfaces.Security;
using VnDocSign.Infrastructure.Security;

namespace VnDocSign.Infrastructure.Setup;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Đăng ký Jwt service
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // TODO: Đăng ký DbContext, Repository, Seeder, Services... ở các giai đoạn sau
        return services;
    }
}
