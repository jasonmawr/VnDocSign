namespace VnDocSign.Domain.Entities.Core
{
    public sealed class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = default!;
        public string PasswordHash { get; set; } = default!;
        public string FullName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public bool IsActive { get; set; } = true;

        // ===== NEW: Employee code (mã nhân viên) =====
        // Hiện tại cho phép null/empty, sau này HRM sẽ cập nhật.
        public string? EmployeeCode { get; set; }

        public Guid DepartmentId { get; set; }
        public Department? Department { get; set; }

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
