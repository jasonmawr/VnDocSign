namespace VnDocSign.Application.Contracts.Interfaces.Integration
{
    public interface IPdfRenderService
    {
        /// <summary>
        /// Render toàn bộ Dossier -> PDF gốc (Source.pdf) để bắt đầu luồng ký.
        /// </summary>
        Task<string> RenderDossierToPdf(Guid dossierId, CancellationToken ct = default);

        /// <summary>
        /// Đảm bảo đầu vào là PDF:
        /// - Nếu anyPath đã là PDF thì trả lại đường dẫn tuyệt đối.
        /// - Nếu là DOC/DOCX thì dùng bộ chuyển đổi (soffice) để convert sang PDF.
        /// - Các định dạng khác hiện chưa hỗ trợ trong giai đoạn này.
        /// </summary>
        Task<string> EnsurePdf(string anyPath, CancellationToken ct = default);

        /// <summary>
        /// Sinh bản Signed_vN.docx/pdf cho ký MOCK (chèn ảnh PNG chữ ký vào các alias SIG_*).
        /// Trả về đường dẫn tuyệt đối tới Signed_vN.pdf.
        /// </summary>
        Task<string> RenderSignedMockAsync(Guid dossierId, CancellationToken ct = default);
    }
}
