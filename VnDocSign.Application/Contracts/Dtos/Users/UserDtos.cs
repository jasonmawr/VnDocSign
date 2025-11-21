namespace VnDocSign.Application.Contracts.Dtos.Users;

public sealed record UserCreateRequest(
    string Username,
    string Password,
    string FullName,
    string Email,
    Guid DepartmentId
);

// Khi tạo user, BE chưa cần EmployeeCode (sẽ được cập nhật sau).
public sealed record UserCreateResponse(Guid Id);

// Danh sách user trả ra FE: có thêm EmployeeCode để hiển thị nếu đã có.
public sealed record UserListItem(
    Guid Id,
    string Username,
    string FullName,
    string Email,
    bool IsActive,
    Guid DepartmentId,
    string? EmployeeCode
);

public sealed record AssignRolesRequest(List<string> Roles);

public sealed record UserWithRolesDto(
    Guid Id,
    string Username,
    string FullName,
    string Email,
    bool IsActive,
    Guid DepartmentId,
    string? EmployeeCode,
    IReadOnlyList<string> Roles
);
