using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VnDocSign.Application.Contracts.Dtos.Signatures;

namespace VnDocSign.Application.Contracts.Interfaces.Signatures
{
    public interface ISignatureService
    {
        Task<UploadSignatureResponse> UploadAsync(UploadSignatureRequest req, CancellationToken ct = default);
        Task<GetSignatureResponse?> GetAsync(GetSignatureQuery query, CancellationToken ct = default);
    }
}
