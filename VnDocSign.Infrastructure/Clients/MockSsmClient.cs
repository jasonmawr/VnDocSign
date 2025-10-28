using System;
using System.Threading;
using System.Threading.Tasks;
using VnDocSign.Application.Contracts.Interfaces.Integration;

namespace VnDocSign.Infrastructure.Clients
{
    /// Mock ký số offline cho DEV (không gọi SSM thật)
    public class MockSsmClient : ISsmClient
    {
        public Task<SignPdfResult> SignPdfAsync(SignPdfRequest request, CancellationToken ct)
        {
            var res = Activator.CreateInstance<SignPdfResult>()!;

            // helper: set property nếu tồn tại & gán được
            void Set(string name, object? val)
            {
                var p = res.GetType().GetProperty(name);
                if (p?.CanWrite == true && (val == null || p.PropertyType.IsInstanceOfType(val)))
                    p.SetValue(res, val);
            }

            // lấy PDF input từ request: ưu tiên PdfBytes, fallback PdfBase64
            byte[] Bytes()
            {
                var t = request!.GetType();
                var pb = t.GetProperty("PdfBytes");
                if (pb?.PropertyType == typeof(byte[]))
                    return (byte[]?)pb.GetValue(request) ?? Array.Empty<byte>();

                var p64 = t.GetProperty("PdfBase64");
                if (p64?.PropertyType == typeof(string))
                {
                    var s = (string?)p64.GetValue(request);
                    if (!string.IsNullOrWhiteSpace(s))
                        try { return Convert.FromBase64String(s!); } catch { }
                }
                return Array.Empty<byte>();
            }

            var bytes = Bytes();
            Set("SignedBytes", bytes);
            Set("SignedBase64", Convert.ToBase64String(bytes));
            Set("Success", true);
            Set("IsSuccess", true);
            Set("Message", "[MockSsmClient] MOCK_OK");

            return Task.FromResult(res);
        }
    }
}
