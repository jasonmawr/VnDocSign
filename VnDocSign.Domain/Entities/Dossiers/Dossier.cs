using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VnDocSign.Domain.Entities.Core;

namespace VnDocSign.Domain.Entities.Dossiers
{
    public enum DossierStatus { Draft, Submitted, InProgress, Approved, Rejected }

    public sealed class Dossier
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = default!;
        public string Code { get; set; } = default!;
        public DossierStatus Status { get; set; } = DossierStatus.Draft;

        public Guid CreatedById { get; set; }
        public User? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<SignTask> SignTasks { get; set; } = new List<SignTask>();
    }
}
