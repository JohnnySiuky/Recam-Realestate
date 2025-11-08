using System.Net.Mime;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Recam.Common.Storage;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Recam.Services.Interfaces;

namespace Recam.Services.Email;

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _svc;
    private readonly BlobContainerClient _container;
    private readonly AzureBlobStorageOptions  _opt;

    public AzureBlobStorageService(IOptions<AzureBlobStorageOptions> opt)
    {
        _opt = opt.Value;
        _svc = new BlobServiceClient(_opt.ConnectionString);
        _container = _svc.GetBlobContainerClient(_opt.ContainerName);
        _container.CreateIfNotExists(PublicAccessType.None);
    }
    
    private static string Normalize(string p) => p.Replace('\\', '/').TrimStart('/');   //unix
    
    private (string container, string name) Resolve(string blobUrlOrPath)
    {
        if (blobUrlOrPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(blobUrlOrPath);
            // 格式: /{container}/{...name}
            var segs = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var container = segs[0];
            var name = string.Join('/', segs.Skip(1));
            return (container, name);
        }
        return (_container.Name, Normalize(blobUrlOrPath));
    }
    
    public async Task<(string Url, string ContentType, long Size)> UploadAsync(Stream content, string contentType,
        string blobPath, CancellationToken ct = default)
    {
        var blobName = Normalize(blobPath);
        var blob = _container.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders
        {
            ContentType = string.IsNullOrWhiteSpace(contentType) ? MediaTypeNames.Application.Octet : contentType,
        };

        await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, ct);
        
        var props = await blob.GetPropertiesAsync(cancellationToken: ct);
        var url = _opt.CdnBaseUrl?.Length > 0
            ? $"{_opt.CdnBaseUrl.TrimEnd('/')}/{blobName}"
            : blob.Uri.ToString();
        
        return (url, headers.ContentType!, props.Value.ContentLength);
    }

    public async Task<(Stream Content, string ContentType, string FileName)> DownloadFileAsync(
        string blobUrlOrPath, CancellationToken ct = default)
    {
        var (containerName, name) = Resolve(blobUrlOrPath);
        var container = containerName == _container.Name ? _container : _svc.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(name);

        if (!await blob.ExistsAsync(ct))
            throw new FileNotFoundException("Blob not found", name);

        var props = await blob.GetPropertiesAsync(cancellationToken: ct);
        var stream = await blob.OpenReadAsync(cancellationToken: ct);
        var fileName = Path.GetFileName(name);

        return (stream, props.Value.ContentType ?? MediaTypeNames.Application.Octet, fileName);
    }
    
    public async Task<bool> DeleteAsync(string blobUrlOrPath, CancellationToken ct = default)
    {
        var (containerName, name) = Resolve(blobUrlOrPath);
        var container = containerName == _container.Name ? _container : _svc.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(name);
        var resp = await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
        return resp.Value;
    }

    public string GetReadOnlySasUrl(string blobUrlOrPath, TimeSpan? ttl = null)
    {
        var (containerName, name) = Resolve(blobUrlOrPath);
        var container = containerName == _container.Name ? _container : _svc.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(name);

        if (!blob.CanGenerateSasUri)
            throw new InvalidOperationException("Cannot generate SAS. Ensure you use an account key connection string.");

        var b = new BlobSasBuilder
        {
            BlobContainerName = container.Name,
            BlobName = name,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(ttl ?? TimeSpan.FromMinutes(_opt.SasReadMinutes))
        };
        b.SetPermissions(BlobSasPermissions.Read);

        var sas = blob.GenerateSasUri(b);
        return sas.ToString();
    }
    
    
}