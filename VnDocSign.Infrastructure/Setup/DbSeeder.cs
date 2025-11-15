using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VnDocSign.Domain.Entities;
using VnDocSign.Domain.Security;
using VnDocSign.Infrastructure.Persistence;
using VnDocSign.Domain.Entities.Signing;
using VnDocSign.Domain.Entities.Config;
using VnDocSign.Domain.Entities.Core;
using VnDocSign.Application.Common;

namespace VnDocSign.Infrastructure.Setup;

public static class DbSeeder
{
    // ===== 1) Departments (đã lấy từ file bạn gửi) =====
    private static readonly (string Code, string Name)[] DefaultDepartments = new[]
    {
        ("DAOTAO","Phòng Đào tạo"),
        ("DIEUDUONG","Phòng Điều dưỡng"),
        ("VATTU","Phòng Vật tư, thiết bị y tế"),
        ("CTXH","Phòng Công tác xã hội"),
        ("CNTT","Phòng Công nghệ thông tin"),
        ("TCCB","Phòng Tổ chức cán bộ"),
        ("KHTH","Phòng Kế hoạch tổng hợp"),
        ("QLCL","Phòng Quản lý chất lượng"),
        ("HCQT","Phòng Hành chánh quản trị"),
        ("TCKT","Phòng Tài chính kế toán"),
        ("BGD","Ban Giám đốc"),
        ("VANTHU","Tổ Văn thư"),

        ("NOITMTK","Khoa Nội Tim mạch, Thần kinh"),
        ("HSCCD","Khoa Hồi sức tích cực, Chống độc"),
        ("NOIUNGBUOU","Khoa Nội Ung bướu"),
        ("NOICXK","Khoa Nội Cơ xương khớp"),
        ("NGOAITH","Khoa Ngoại tổng hợp"),
        ("KPHU","Khoa Phụ"),
        ("NOITH","Khoa Nội tổng hợp"),
        ("CCDS","Khoa Châm cứu, Dưỡng sinh"),
        ("KKB","Khoa Khám bệnh đa khoa"),
        ("VLTL","Khoa Vật lý trị liệu"),
        ("CDHA","Khoa Chẩn đoán hình ảnh"),
        ("DINHDUONG","Khoa Dinh dưỡng"),
        ("KSNK","Khoa Kiểm soát nhiễm khuẩn"),
        ("XETNGHIEM","Khoa Xét nghiệm"),
        ("THUCNGHIEM","Khoa Thực nghiệm"),
        ("DUOC","Khoa Dược"),
        ("NHATHUOC","Nhà thuốc"),
        ("TRUNGTAM","Trung tâm"),
        ("BDHNG","Ban điều hành ngoài giờ"),
        ("BHYT","Tổ Bảo hiểm y tế"),
    };

    public static async Task SeedDepartmentsAsync(AppDbContext db)
    {
        foreach (var (code, name) in DefaultDepartments)
        {
            var dep = await db.Departments.FirstOrDefaultAsync(d => d.Code == code);
            if (dep == null) db.Departments.Add(new Department { Code = code, Name = name, IsActive = true });
            else
            {
                if (!string.Equals(dep.Name, name, StringComparison.Ordinal)) dep.Name = name;
                if (!dep.IsActive) dep.IsActive = true;
            }
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
    }

    // ===== 2) Roles =====
    public static async Task SeedRolesAsync(AppDbContext db)
    {
        // Bảng role chuẩn: Name (từ RoleNames) ↔ Id (từ RoleIds)
        var must = new (string Name, Guid Id)[]
        {
        (RoleNames.Admin,            RoleIds.Admin),
        (RoleNames.VanThu,           RoleIds.VanThu),
        (RoleNames.ChuyenVien,       RoleIds.ChuyenVien),
        (RoleNames.TruongPhong,      RoleIds.TruongPhong),
        (RoleNames.PhoPhong,         RoleIds.PhoPhong),
        (RoleNames.TruongKhoa,       RoleIds.TruongKhoa),
        (RoleNames.PhoKhoa,          RoleIds.PhoKhoa),
        (RoleNames.KeToanTruong,     RoleIds.KeToanTruong),
        (RoleNames.PhoGiamDoc,       RoleIds.PhoGiamDoc),
        (RoleNames.GiamDoc,          RoleIds.GiamDoc),
        (RoleNames.ChuTichCongDoan,  RoleIds.ChuTichCongDoan),
        (RoleNames.DieuDuongTruong,  RoleIds.DieuDuongTruong),
        (RoleNames.KTVTruong,        RoleIds.KTVTruong),
        };

        // Lấy hiện trạng theo Name
        var existing = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Name, r => r);

        foreach (var (name, id) in must)
        {
            if (!existing.TryGetValue(name, out var role))
            {
                // Chưa có → tạo mới với GUID cố định
                db.Roles.Add(new Role { Id = id, Name = name });
            }
            else
            {
                // ĐÃ có: nếu khác Id, KHÔNG đổi PK tại đây (tránh ảnh hưởng dữ liệu đang chạy).
                // Với DB mới (drop & migrate), Id sẽ đúng theo RoleIds.
                // Nếu cần ép đồng bộ trên DB đang chạy, làm migration/SQL riêng để cập nhật PK và các FK user_roles.
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();
    }

    // ===== 3) Admin mặc định (idempotent) =====
    public static async Task SeedAdminAsync(AppDbContext db, Func<string, string> hash,
        string username = "admin", string? password = null, string fullName = "System Admin")
    {
        // lấy mật khẩu từ ENV nếu chưa truyền
        var pwd = password ?? Environment.GetEnvironmentVariable("VN_DOCSIGN_ADMIN_PWD") ?? "Admin@123";

        // đảm bảo role Admin tồn tại
        var roleAdmin = await db.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Admin);
        if (roleAdmin == null) { roleAdmin = new Role { Name = RoleNames.Admin }; db.Roles.Add(roleAdmin); await db.SaveChangesAsync(); }

        var admin = await db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Username == username);
        if (admin == null)
        {
            admin = new User
            {
                Username = username,
                FullName = fullName,
                PasswordHash = hash(pwd),
                Email = "admin@benhvien.local",
                // DepartmentId: có thể để một phòng mặc định nếu muốn
                IsActive = true
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }
        else
        {
            bool changed = false;
            if (!admin.IsActive) { admin.IsActive = true; changed = true; }
            if (string.IsNullOrWhiteSpace(admin.FullName)) { admin.FullName = fullName; changed = true; }
            if (changed) await db.SaveChangesAsync();
        }

        if (!admin.UserRoles.Any(ur => ur.RoleId == roleAdmin.Id))
        {
            db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = roleAdmin.Id });
            await db.SaveChangesAsync();
        }
    }

    // ===== 4) Lãnh đạo (idempotent; map theo dữ liệu thực tế của bạn) =====
    private record SeedLeader(string FullName, string UnitName, params string[] Roles);
    // (rút gọn cho ví dụ; bạn bổ sung tiếp theo danh sách ở file bạn gửi)
    private static readonly SeedLeader[] Leaders =
    {
        new("Hồ Văn Hân","Ban Giám đốc",RoleNames.GiamDoc),
        new("Nguyễn Thanh Tuyên","Ban Giám đốc",RoleNames.PhoGiamDoc),
        // ... (bạn copy tiếp phần còn lại từ file bạn gửi)
    };

    public static async Task SeedLeadersAsync(AppDbContext db, Func<string, string> hash, string defaultPassword = "P@ssw0rd!")
    {
        var deptIndex = await BuildDeptIndexAsync(db);
        var roleIds = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Name, r => r.Id);

        foreach (var s in Leaders)
        {
            var dept = await EnsureDeptByNameAsync(db, deptIndex, s.UnitName);
            var username = ToUserNameSlug(s.FullName);
            username = await EnsureUniqueUsernameAsync(db, username);

            var user = await db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Username == username)
                ?? await db.Users.Include(u => u.UserRoles)
                    .FirstOrDefaultAsync(u => u.FullName == s.FullName && u.DepartmentId == dept.Id);

            if (user == null)
            {

                user = new User
                {
                    Username = username,
                    FullName = s.FullName,
                    PasswordHash = hash(defaultPassword),
                    DepartmentId = dept.Id,
                    Email = $"{username}@benhvien.local",
                    IsActive = true
                };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }
            else
            {
                bool changed = false;
                if (user.Username != username) { user.Username = username; changed = true; }
                if (user.FullName != s.FullName) { user.FullName = s.FullName; changed = true; }
                if (user.DepartmentId != dept.Id) { user.DepartmentId = dept.Id; changed = true; }
                if (!user.IsActive) { user.IsActive = true; changed = true; }
                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    user.Email = $"{user.Username}@benhvien.local";
                    changed = true;
                }
                if (changed) await db.SaveChangesAsync();
            }

            foreach (var roleName in s.Roles.Distinct())
            {
                if (!roleIds.TryGetValue(roleName, out var roleId)) continue;
                if (!user.UserRoles.Any(ur => ur.RoleId == roleId))
                    db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
            }
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();

            // dọn trùng (cùng họ tên + phòng ban)
            var duplicates = await db.Users
                .Where(u => u.Id != user.Id && u.FullName == s.FullName && u.DepartmentId == dept.Id)
                .ToListAsync();
            foreach (var du in duplicates) if (du.IsActive) du.IsActive = false;
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
        }
    }

    public static async Task SeedSignSlotsAsync(AppDbContext db)
    {
        var must = new (SlotKey Key, SlotPhase Phase, int Ord, bool Opt)[]
        {
        (SlotKey.NguoiTrinh,    SlotPhase.Vung1, 1, false),
        (SlotKey.LanhDaoPhong,  SlotPhase.Vung1, 2, false),
        (SlotKey.DonViLienQuan, SlotPhase.Vung1, 3, true),

        (SlotKey.KHTH, SlotPhase.Vung2, 1, false),
        (SlotKey.HCQT, SlotPhase.Vung2, 2, false),
        (SlotKey.TCCB, SlotPhase.Vung2, 3, false),
        (SlotKey.TCKT, SlotPhase.Vung2, 4, false),
        (SlotKey.CTCD, SlotPhase.Vung2, 5, false),

        (SlotKey.PGD1, SlotPhase.Vung3, 1, false),
        (SlotKey.PGD2, SlotPhase.Vung3, 2, false),
        (SlotKey.PGD3, SlotPhase.Vung3, 3, false),

        (SlotKey.VanThuCheck, SlotPhase.Clerk, 1, false),
        (SlotKey.GiamDoc,     SlotPhase.Director, 1, false),
        };

        foreach (var m in must)
            if (!await db.SignSlotDefs.AnyAsync(x => x.Key == m.Key))
                db.SignSlotDefs.Add(new SignSlotDef { Key = m.Key, Phase = m.Phase, OrderInPhase = m.Ord, Optional = m.Opt });

        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
    }

    public static async Task SeedSystemConfigAsync(AppDbContext db)
    {
        // map 5 phòng chức năng về DepartmentId theo mã phòng đã seed của bạn
        var mapDept = new Dictionary<SlotKey, string> // SlotKey -> Department.Code
        {
            [SlotKey.KHTH] = "KHTH",
            [SlotKey.HCQT] = "HCQT",
            [SlotKey.TCCB] = "TCCB",
            [SlotKey.TCKT] = "TCKT",
            [SlotKey.CTCD] = "CTXH" // nếu CTCD là Công đoàn -> đổi mã cho đúng
        };

        foreach (var kv in mapDept)
        {
            var code = kv.Value;
            var dept = await db.Departments.FirstOrDefaultAsync(d => d.Code == code);
            if (dept == null) continue;
            var cfg = await db.SystemConfigs.FirstOrDefaultAsync(c => c.Slot == kv.Key);
            if (cfg == null)
                db.SystemConfigs.Add(new SystemConfig { Slot = kv.Key, DepartmentId = dept.Id, IsActive = true });
            else
            {
                cfg.DepartmentId = dept.Id; cfg.UserId = null; cfg.IsActive = true;
            }
        }

        // PGD1..3 và GĐ: tạm thời chưa gán (điền sau khi có tài khoản)
        foreach (var slot in new[] { SlotKey.PGD1, SlotKey.PGD2, SlotKey.PGD3, SlotKey.GiamDoc })
            if (!await db.SystemConfigs.AnyAsync(c => c.Slot == slot))
                db.SystemConfigs.Add(new SystemConfig { Slot = slot, IsActive = true });

        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
    }

    // ===== Helpers =====
    private static async Task<Dictionary<string, Department>> BuildDeptIndexAsync(AppDbContext db)
    {
        var all = await db.Departments.AsNoTracking().ToListAsync();
        return all.ToDictionary(d => NormKey(d.Name), d => d);
    }

    private static async Task<Department> EnsureDeptByNameAsync(AppDbContext db, Dictionary<string, Department> idx, string name)
    {
        var key = NormKey(name);
        if (idx.TryGetValue(key, out var d)) return d;

        var code = AutoDeptCodeFromName(name);
        d = new Department { Code = code, Name = name, IsActive = true };
        db.Departments.Add(d);
        await db.SaveChangesAsync();
        idx[key] = d;
        return d;
    }

    private static string NormKey(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = s.Replace("\r", " ").Replace("\n", " ").Trim();
        while (t.Contains("  ")) t = t.Replace("  ", " ");
        var noDia = RemoveDiacritics(t).ToUpperInvariant();
        var sb = new StringBuilder(noDia.Length);
        foreach (var ch in noDia) if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    private static string AutoDeptCodeFromName(string name)
    {
        var up = RemoveDiacritics(name).ToUpperInvariant();
        var sb = new StringBuilder(up.Length);
        foreach (var ch in up) if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        var code = sb.ToString();
        return string.IsNullOrWhiteSpace(code) ? "DEPT" : code;
    }

    private static string ToUserNameSlug(string fullName)
    {
        var normalized = RemoveDiacritics(fullName).ToLowerInvariant();
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "user";
        var last = parts[^1];
        var initials = string.Concat(parts.Take(parts.Length - 1).Select(p => p[0]));
        return $"{initials}{last}";
    }

    private static string RemoveDiacritics(string s)
    {
        var formD = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(s.Length);
        foreach (var ch in formD)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC)
                 .Replace("đ", "d").Replace("Đ", "D");
    }

    private static async Task<string> EnsureUniqueUsernameAsync(AppDbContext db, string baseName)
    {
        var u = baseName;
        int i = 1;
        while (await db.Users.AnyAsync(x => x.Username == u))
            u = $"{baseName}{++i}";
        return u;
    }
}
