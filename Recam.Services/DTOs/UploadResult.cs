namespace Recam.Services.DTOs;

public record UploadResult(
    string BlobPath,
    string Url,
    string ContentType,
    long Size,
    string OriginalFileName
);