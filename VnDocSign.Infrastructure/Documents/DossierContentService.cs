using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VnDocSign.Application.Common;
using VnDocSign.Application.Contracts.Dtos.Dossiers;
using VnDocSign.Application.Contracts.Interfaces.Dossiers;
using VnDocSign.Domain.Entities.Dossiers;
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
            var dc = await _db.DossierContents.AsNoTracking().FirstOrDefaultAsync(x => x.DossierId == dossierId, ct);
            var dict = dc is null ? new Dictionary<string, string?>():
                (JsonSerializer.Deserialize<Dictionary<string, string?>>(dc.DataJson) ?? new());
            
            return new DossierContentDto(dc?.TemplateCode, dict);
        }

        public async Task<DossierContentDto> UpsertAsync(Guid dossierId, DossierContentUpsertRequest req, Guid? userId, CancellationToken ct = default)
        {
            var aliasDict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in req.Fields ?? new Dictionary<string, string?>())
            {
                if (TemplateAliasMaps.Fields.TryGetValue(kv.Key, out var alias))
                    aliasDict[alias] = kv.Value?? string.Empty;
            }

            var json = JsonSerializer.Serialize(aliasDict);

            var dc = await _db.DossierContents.FirstOrDefaultAsync(x => x.DossierId == dossierId, ct);
            if (dc == null)
            {
                dc = new DossierContent
                {
                    DossierId = dossierId,
                    TemplateCode = req.TemplateCode,
                    DataJson = json,
                    UpdatedById = userId
                };
                _db.DossierContents.Add(dc);
            }
            else
            {
                dc.TemplateCode = req.TemplateCode ?? dc.TemplateCode;
                dc.DataJson = json;
                dc.UpdatedAt = DateTime.Now;
                dc.UpdatedById = userId;
            }
            
            await _db.SaveChangesAsync();
            return new DossierContentDto(dc.TemplateCode, aliasDict);
        }
    }
}
