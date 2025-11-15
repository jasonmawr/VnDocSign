using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using VnDocSign.Application.Common;                        // TemplateAliasMaps, TemplateAliasGuards
using VnDocSign.Application.Contracts.Dtos.Dossiers;
using VnDocSign.Application.Contracts.Interfaces.Dossiers;
using VnDocSign.Domain.Entities.Dossiers;
using VnDocSign.Domain.Entities.Signing;                  // SlotKey, SignTaskStatus
using VnDocSign.Infrastructure.Persistence;

namespace VnDocSign.Infrastructure.Documents
{
    public sealed class DossierContentService : IDossierContentService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<DossierContentService> _log;

        public DossierContentService(AppDbContext db, ILogger<DossierContentService> log)
        {
            _db = db;
            _log = log;
        }

        public async Task<DossierContentDto> GetAsync(Guid dossierId, CancellationToken ct = default)
        {
            var dc = await _db.DossierContents
                              .AsNoTracking()
                              .FirstOrDefaultAsync(x => x.DossierId == dossierId, ct);

            var dict = dc is null
                ? new Dictionary<string, string?>()
                : (JsonSerializer.Deserialize<Dictionary<string, string?>>(dc.DataJson) ?? new());

            return new DossierContentDto(dc?.TemplateCode, dict);
        }

        public async Task<DossierContentDto> UpsertAsync(Guid dossierId, DossierContentUpsertRequest req, Guid? userId, CancellationToken ct = default)
        {
            // 1) Map key FE -> alias DOCX
            var aliasDict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in (req.Fields ?? new Dictionary<string, string?>()))
            {
                if (TemplateAliasMaps.Fields.TryGetValue(kv.Key, out var alias))
                    aliasDict[alias] = kv.Value ?? string.Empty;
            }

            // 2) Lấy user + vai trò
            var user = userId.HasValue
                ? await _db.Users
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Id == userId.Value, ct)
                : null;

            bool isClerk = user?.UserRoles?.Any(ur => ur.RoleId == RoleIds.VanThu) == true;

            // 3) Lấy dossier + kiểm “GD đã ký cuối”
            var dossier = await _db.Dossiers
                                   .Include(d => d.SignTasks)
                                   .FirstOrDefaultAsync(d => d.Id == dossierId, ct)
                         ?? throw new InvalidOperationException("Dossier not found.");

            bool approvedByStatus = dossier.Status == DossierStatus.Approved;
            bool directorTaskApproved = dossier.SignTasks?.Any(t =>
                    t.SlotKey == SlotKey.GiamDoc && t.Status == SignTaskStatus.Approved
                ) == true;

            bool gdSignedFinal = approvedByStatus || directorTaskApproved;

            // 4) Nếu request đụng tới alias “chỉ Văn thư” -> enforce quyền & thời điểm
            bool touchesClerkOnly = aliasDict.Keys.Any(k => TemplateAliasGuards.ClerkOnlyAliases.Contains(k));
            if (touchesClerkOnly && (!isClerk || !gdSignedFinal))
                throw new UnauthorizedAccessException("Chỉ Văn thư được cập nhật sau khi Giám đốc đã ký cuối.");

            // 5) Tải content hiện tại để MERGE (tránh overwrite mất field cũ)
            var dc = await _db.DossierContents.FirstOrDefaultAsync(x => x.DossierId == dossierId, ct);

            Dictionary<string, string?> current;
            if (dc is null)
            {
                current = new(StringComparer.OrdinalIgnoreCase);
                dc = new DossierContent
                {
                    DossierId = dossierId,
                    TemplateCode = req.TemplateCode,
                    UpdatedById = userId
                };
                _db.DossierContents.Add(dc);
            }
            else
            {
                current = JsonSerializer.Deserialize<Dictionary<string, string?>>(dc.DataJson)
                          ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                dc.TemplateCode = req.TemplateCode ?? dc.TemplateCode;
                dc.UpdatedAt = DateTime.Now;
                dc.UpdatedById = userId;
            }

            // 6) MERGE alias cũ với alias mới
            foreach (var kv in aliasDict)
                current[kv.Key] = kv.Value;

            // 7) Đồng bộ mirror field (SoLuuTru/NgayLuuTru) + parse ngày linh hoạt
            current.TryGetValue("SO_LUU_TRU", out var soLuuTruRaw);
            current.TryGetValue("NGAY_LUU_TRU", out var ngayLuuTruRaw);

            dc.SoLuuTru = string.IsNullOrWhiteSpace(soLuuTruRaw) ? null : soLuuTruRaw.Trim();

            DateTime? parsedDate = null;
            if (!string.IsNullOrWhiteSpace(ngayLuuTruRaw))
            {
                var s = ngayLuuTruRaw.Trim();
                string[] formats = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy", "yyyyMMdd" };
                if (DateTime.TryParseExact(s, formats, CultureInfo.GetCultureInfo("vi-VN"),
                                           DateTimeStyles.None, out var d))
                    parsedDate = d.Date;
                else if (DateTime.TryParse(s, out var d2))
                    parsedDate = d2.Date;
            }
            dc.NgayLuuTru = parsedDate;

            // 8) Lưu JSON đã merge
            dc.DataJson = JsonSerializer.Serialize(current);

            await _db.SaveChangesAsync(ct);

            // Trả lại những gì FE vừa gửi (aliasDict) để FE dễ phản hồi UI
            return new DossierContentDto(dc.TemplateCode, current);
        }
    }
}
