namespace Recam.Common.Storage;

public class AzureBlobStorageOptions
{
    public string ConnectionString { get; set; } = default!;
    public string ContainerName { get; set; } = "dotnetmasterclass";
    public int SasReadMinutes { get; set; } = 60;
    public string? CdnBaseUrl { get; set; }
}