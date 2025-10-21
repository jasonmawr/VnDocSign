using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VnDocSign.Domain.Entities.Signing;

namespace VnDocSign.Domain.Entities.Config
{
    public sealed class SystemConfig
    {
        public int Id { get; set; }
        public SlotKey Slot { get; set; }
        public Guid? UserId { get; set; }         // PGD1..3, GiamDoc, có thể là User
        public Guid? DepartmentId { get; set; }   // KHTH, HCQT, TCCB, TCKT, CTCD -> map phòng
        public bool IsActive { get; set; } = true;
    }
}
