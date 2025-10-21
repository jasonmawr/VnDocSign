using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Application.Contracts.Interfaces.Security;

public interface IJwtTokenService
{
    string CreateToken(Guid userId, string username, IEnumerable<string> roles);
}

