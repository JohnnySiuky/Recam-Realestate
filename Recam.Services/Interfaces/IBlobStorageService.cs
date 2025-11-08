namespace Recam.Services.Interfaces;

public interface IBlobStorageService
{
    Task<(string Url, string ContentType, long Size)> UploadAsync(
        Stream content, string contentType, string blobPath, CancellationToken ct = default);

    Task<(Stream Content, string ContentType, string FileName)> DownloadFileAsync(
        string blobUrlOrPath, CancellationToken ct = default);

    Task<bool> DeleteAsync(string blobUrlOrPath, CancellationToken ct = default);

    string GetReadOnlySasUrl(string blobUrlOrPath, TimeSpan? ttl = null);
}