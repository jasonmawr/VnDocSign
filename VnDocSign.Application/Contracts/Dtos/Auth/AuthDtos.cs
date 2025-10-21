using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Application.Contracts.Dtos.Auth;

public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(string AccessToken, Guid UserId, string Username, string FullName, string[] Roles);

