using Recam.Models.Enums;

namespace Recam.Models.Entities;

public class MediaAsset
{
    public int Id { get; set; }
    public MediaType MediaType { get; set; }
    public string MediaUrl { get; set; } = default!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsSelected { get; set; }
    public bool IsHero { get; set; }
    public bool IsDeleted { get; set; }
    
    public int ListingCaseId { get; set; }
    public ListingCase ListingCase { get; set; } = default!;

    public string UserId { get; set; } = default!;    //uploader
    public ApplicationUser User { get; set; } = default!;
    
}