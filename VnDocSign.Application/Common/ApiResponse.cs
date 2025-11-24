namespace VnDocSign.Application.Common;

public sealed class ApiResponse
{
    /// <summary>Thành công hay thất bại.</summary>
    public bool Success { get; init; }

    /// <summary>Thông điệp cho FE hiển thị.</summary>
    public string Message { get; init; } = "";

    /// <summary>Dữ liệu trả về (nếu có).</summary>
    public object? Data { get; init; }

    /// <summary>Mã lỗi chuẩn hoá (VD: "AUTH_INVALID_TOKEN", "VALIDATION_ERROR"...).</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Chi tiết lỗi theo field (dùng cho validation).</summary>
    public IDictionary<string, string[]>? Errors { get; init; }

    // ===== SUCCESS =====

    public static ApiResponse SuccessResponse(object? data = null, string message = "")
        => new()
        {
            Success = true,
            Message = message,
            Data = data
        };

    // ===== FAIL =====

    public static ApiResponse FailResponse(string message, string? errorCode = null,
        IDictionary<string, string[]>? errors = null)
        => new()
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            Errors = errors
        };

    public static ApiResponse Fail(string message, string? errorCode = null,
        IDictionary<string, string[]>? errors = null)
        => FailResponse(message, errorCode, errors);
}

// =====================
// Generic version
// =====================
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public T? Data { get; init; }

    public string? ErrorCode { get; init; }
    public IDictionary<string, string[]>? Errors { get; init; }

    // ===== SUCCESS =====

    public static ApiResponse<T> SuccessResponse(T? data, string message = "")
        => new()
        {
            Success = true,
            Message = message,
            Data = data
        };

    // ===== FAIL =====

    public static ApiResponse<T> FailResponse(string message, string? errorCode = null,
        IDictionary<string, string[]>? errors = null)
        => new()
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            Errors = errors
        };

    public static ApiResponse<T> Fail(string message, string? errorCode = null,
        IDictionary<string, string[]>? errors = null)
        => FailResponse(message, errorCode, errors);
}

// =====================
// Helper cho phân trang
// =====================
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int PageIndex { get; init; }   // 1-based
    public int PageSize { get; init; }
    public long TotalCount { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public PagedResult() { }

    public PagedResult(IReadOnlyList<T> items, int pageIndex, int pageSize, long totalCount)
    {
        Items = items;
        PageIndex = pageIndex;
        PageSize = pageSize;
        TotalCount = totalCount;
    }
}
