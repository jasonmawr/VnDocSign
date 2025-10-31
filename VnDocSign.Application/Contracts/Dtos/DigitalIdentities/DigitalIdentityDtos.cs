namespace VnDocSign.Application.Contracts.Dtos.DigitalIdentities;

public sealed record DigitalIdentityDto(
    Guid Id,
    Guid UserId,
    string EmpCode,
    string CertName,
    string Company,
    string? DisplayName,
    string? Title,
    bool IsActive,
    DateTime CreatedAtUtc,
    //NEW
    string? Provider,
    DateTime? NotBefore,
    DateTime? NotAfter,
    string? SerialNo,
    string? Issuer,
    string? Subject
);

public sealed record DigitalIdentityCreateRequest(
    Guid UserId,
    string EmpCode,
    string CertName,
    string Company,
    string? DisplayName,
    string? Title,
    bool IsActive,
    //NEW
    string? Provider,
    DateTime? NotBefore,
    DateTime? NotAfter,
    string? SerialNo,
    string? Issuer,
    string? Subject
);

public sealed record DigitalIdentityUpdateRequest(
    string? EmpCode,
    string? CertName,
    string? Company,
    string? DisplayName,
    string? Title,
    bool? IsActive,
    // NEW
    string? Provider,
    DateTime? NotBefore,
    DateTime? NotAfter,
    string? SerialNo,
    string? Issuer,
    string? Subject
);
