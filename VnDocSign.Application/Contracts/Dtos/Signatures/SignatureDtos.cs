using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Application.Contracts.Dtos.Signatures;

public sealed record UploadSignatureRequest(Guid UserId, string FileName, string ContentType, byte[] Data);
public sealed record UploadSignatureResponse(Guid SignatureId);

public sealed record GetSignatureQuery(Guid UserId);
public sealed record GetSignatureResponse(Guid UserId, Guid SignatureId, string FileName, string ContentType, byte[] Data);
