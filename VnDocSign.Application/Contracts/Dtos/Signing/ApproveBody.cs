using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Application.Contracts.Dtos.Signing;

public sealed class ApproveBody
{
    public string? Pin { get; set; }
    public string? Comment { get; set; }
    public string? CertName { get; set; }
    public string? Company {  get; set; }
}


