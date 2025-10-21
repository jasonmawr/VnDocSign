using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Application.Contracts.Dtos.Users;

public sealed record UserCreateRequest(string Username, string Password, string FullName, string Email, Guid DepartmentId);
public sealed record UserCreateResponse(Guid Id);
public sealed record UserListItem(Guid Id, string Username, string FullName, string Email, bool IsActive, Guid DepartmentId);
