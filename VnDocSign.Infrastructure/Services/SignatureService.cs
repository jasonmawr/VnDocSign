using Microsoft.EntityFrameworkCore;
using VnDocSign.Application.Contracts.Dtos.Signatures;
using VnDocSign.Application.Contracts.Interfaces.Signatures;
using VnDocSign.Domain.Entities;
using VnDocSign.Infrastructure.Persistence;

namespace VnDocSign.Infrastructure.Services;

public sealed class SignatureService : ISignatureService
{
    private readonly AppDbContext _db;
    public SignatureService(AppDbContext db) => _db = db;

    // ============================
    // UPLOAD SIGNATURE IMAGE (PNG)
    // ============================
    public async Task<UploadSignatureResponse> UploadAsync(UploadSignatureRequest req, CancellationToken ct = default)
    {
        if (req.Data is null || req.Data.Length == 0)
            throw new InvalidOperationException("File chữ ký trống.");

        if (!req.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ chấp nhận tệp hình ảnh.");

        var sig = await _db.UserSignatures
            .FirstOrDefaultAsync(x => x.UserId == req.UserId, ct);

        // Nếu chưa có → tạo mới
        if (sig is null)
        {
            sig = new UserSignature
            {
                UserId = req.UserId,
                FileName = req.FileName,
                ContentType = req.ContentType,
                Data = req.Data,
                UploadedAt = DateTime.UtcNow
            };

            _db.UserSignatures.Add(sig);
        }
        else
        {
            // Nếu có rồi → cập nhật
            sig.FileName = req.FileName;
            sig.ContentType = req.ContentType;
            sig.Data = req.Data;
            sig.UploadedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return new UploadSignatureResponse(sig.Id);
    }

    // ============================
    // GET SIGNATURE IMAGE
    // ============================
    public async Task<GetSignatureResponse?> GetAsync(GetSignatureQuery q, CancellationToken ct = default)
    {
        var sig = await _db.UserSignatures
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == q.UserId, ct);

        return sig is null
            ? null
            : new GetSignatureResponse(sig.UserId, sig.Id, sig.FileName, sig.ContentType, sig.Data);
    }
}
