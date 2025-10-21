namespace VnDocSign.Application.Contracts.Interfaces.Integration;

public interface IPdfRenderService
{
    /// <summary>
    /// Render toàn bộ Dossier -> PDF gốc để bắt đầu luồng ký.
    /// </summary>
    Task<string> RenderDossierToPdf(Guid dossierId, CancellationToken ct = default);

    /// <summary>
    /// Nếu đầu vào là doc/docx/png/jpg… thì convert đảm bảo ra PDF; nếu đã là PDF thì trả lại path.
    /// </summary>
    Task<string> EnsurePdf(string anyPath, CancellationToken ct = default);
}
