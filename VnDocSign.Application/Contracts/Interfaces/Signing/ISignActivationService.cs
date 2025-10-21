using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Application.Contracts.Interfaces.Signing
{
    public interface ISignActivationService
    {
        Task RecomputeAsync(Guid dossierId, CancellationToken ct = default);
    }
}
