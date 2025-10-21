using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VnDocSign.Application.Contracts.Dtos.Users;

namespace VnDocSign.Application.Contracts.Interfaces.Users
{
    public interface IUserService
    {
        Task<UserCreateResponse> CreateAsync(UserCreateRequest req, CancellationToken ct = default);
        Task<IReadOnlyList<UserListItem>> GetAllAsync(CancellationToken ct = default);
    }
}
