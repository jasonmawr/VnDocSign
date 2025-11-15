namespace VnDocSign.Application.Contracts.Interfaces.Integration;

public interface ISsmClient
{
    Task<SignPdfResult> SignPdfAsync(SignPdfRequest req, CancellationToken ct = default);
}

public sealed record SignPdfRequest(
    string EmpCode,
    string Pin,
    string CertName,
    string Company,
    string Title,
    string Name,
    string InputPdfPath,
    string OutputPdfPath,
    int SignType,           // theo chuẩn SSM
    int SignLocationType,   // 2: SearchPattern, 1: Coordinates (theo đặc tả nội bộ SSM)
    string? SearchPattern = null,
    int? Page = null,
    float? PositionX = null,
    float? PositionY = null,
    string? BearerToken = null
);

public sealed record SignPdfResult(
    bool Success,
    string? Error,
    string OutputPdfPath
);
