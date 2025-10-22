﻿namespace VnDocSign.Infrastructure.Setup.Options
{
    /// <summary>
    /// Cấu hình kho lưu trữ tệp (dev: thư mục local; prod: mount/S3/MinIO).
    /// </summary>
    public sealed class FileStorageOptions
    {
        /// <summary>Thư mục gốc lưu hồ sơ, ví dụ: "./data" hoặc "/var/vndocsign/data".</summary>
        public string? Root { get; set; }
    }
}
