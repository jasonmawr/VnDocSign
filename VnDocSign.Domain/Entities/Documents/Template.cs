using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Domain.Entities.Documents
{
    public sealed class Template
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!; //duy nhất trong hệ thống
        public string Name { get; set; } = default!;
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? CreatedById { get; set; }
        public Core.User? CreatedBy { get; set; }

        public ICollection<TemplateVersion> Versions { get; set; } = new List<TemplateVersion>();
    }
    public sealed class TemplateVersion
    {
        public int Id { get; set; }
        public int TemplateId { get; set; }
        public Template? Template { get; set; }

        public int VersionNo { get; set; }                 // tăng dần: 1,2,3...
        public string FileNameDocx { get; set; } = default!;
        public string FileNamePdf { get; set; } = default!;
        public string VisiblePatternsJson { get; set; } = "[]";  // JSON array string
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? CreatedById { get; set; }
        public Core.User? CreatedBy { get; set; }
    }
}
