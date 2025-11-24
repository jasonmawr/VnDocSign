using System.Net;
using System.Text.Json;
using VnDocSign.Application.Common;

namespace VnDocSign.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext http)
    {
        try
        {
            await _next(http);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteError(http, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            await WriteError(http, HttpStatusCode.NotFound, ex.Message);
        }
        catch (ArgumentException ex)
        {
            await WriteError(http, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Lỗi nghiệp vụ (vd: bước ký chưa active, không tìm thấy DigitalIdentity...)
            await WriteError(http, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            await WriteError(http, HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    private static async Task WriteError(HttpContext http, HttpStatusCode code, string message)
    {
        http.Response.ContentType = "application/json";
        http.Response.StatusCode = (int)code;

        var payload = ApiResponse.FailResponse(message);
        var json = JsonSerializer.Serialize(payload);

        await http.Response.WriteAsync(json);
    }
}
