using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VnDocSign.Domain.Entities.Core;

namespace VnDocSign.Domain.Entities.Dossiers
{
    public sealed class DossierContent
    {
        public int Id { get; set; }
        public Guid DossierId { get; set; }
        public Dossier? Dossier { get; set; }

        public string? TemplateCode { get; set; } //PHIEU_TRINH,....
        public string DataJson { get; set; } = "{}"; // alias -> value (DON_VI, VU_VIEC,....)
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public Guid? UpdatedById { get; set; }
        public User? UpdatedBy { get; set; }
    }
}
