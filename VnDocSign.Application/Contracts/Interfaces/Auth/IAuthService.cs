using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VnDocSign.Application.Contracts.Dtos.Auth;

namespace VnDocSign.Application.Contracts.Interfaces.Auth
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default);
    }
}
