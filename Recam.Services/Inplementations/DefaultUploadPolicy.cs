using System.Collections.Immutable;
using Recam.Models.Enums;
using Recam.Services.Interfaces;

namespace Recam.Services.Email;

public class DefaultUploadPolicy : IUploadPolicy
{
    private static readonly IImmutableSet<string> ImageExt =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg",".jpeg",".png",".webp",".gif",".bmp",".tif",".tiff",".heic",".heif" }.ToImmutableHashSet();

    private static readonly IImmutableSet<string> VideoExt =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".mp4",".mov",".m4v",".avi",".mkv",".webm",".wmv" }.ToImmutableHashSet();

    private static readonly IImmutableSet<string> VrExt =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".zip",".gltf",".glb",".usdz",".obj" }.ToImmutableHashSet();

    public bool IsExtensionAllowed(MediaType type, string ext)
        => type switch
        {
            MediaType.Photo  => ImageExt.Contains(ext),
            MediaType.Video    => VideoExt.Contains(ext),
            MediaType.VRTour   => VrExt.Contains(ext),
            MediaType.FloorPlan=> ImageExt.Contains(ext) || ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    public long MaxSizeBytes(MediaType type) => type switch
    {
        MediaType.Photo   => 25L * 1024 * 1024,   // 25MB
        MediaType.FloorPlan => 25L * 1024 * 1024,
        MediaType.VRTour    => 800L * 1024 * 1024,  // 800MB
        MediaType.Video     => 1_500L * 1024 * 1024,// 1.5GB
        _ => 50L * 1024 * 1024
    };

    public string FolderOf(MediaType type) => type switch
    {
        MediaType.Photo   => "pictures",
        MediaType.Video     => "videos",
        MediaType.FloorPlan => "floorplans",
        MediaType.VRTour    => "vrtours",
        _ => "others"
    };
}