using Recam.Models.Enums;

namespace Recam.Services.Interfaces;

public interface IUploadPolicy
{
    bool IsExtensionAllowed(MediaType type, string ext);
    long MaxSizeBytes(MediaType type);
    string FolderOf(MediaType type);
}