using Recam.Models.Enums;

namespace Recam.Models.Entities;

public class ListingCase
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }

    public string Street { get; set; } = default!;
    public string City { get; set; } = default!;
    public string State { get; set; } = default!;
    public int PostalCode { get; set; }
    
    public decimal Longitude { get; set; }
    public decimal Latitude { get; set; }
    
    public decimal? Price { get; set; }
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public int Garages { get; set; }
    public decimal? FloorArea { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    
    public PropertyType PropertyType { get; set; }
    public SaleCategory SaleCategory { get; set; }
    public ListingCaseStatus ListingCaseStatus { get; set; }
    
    public string? CoverImageUrl { get; set; }    // add one cover pic
    public string? PublicUrl { get; set; }   // share link after publish
    // builder
    public string UserId { get; set; } = default!;
    
    
    public ApplicationUser User { get; set; } = default!;
    
    public ICollection<CaseContact> Contacts { get; set; } = new List<CaseContact>();
    public ICollection<MediaAsset> Media { get; set; } = new List<MediaAsset>();
    public ICollection<AgentListingCase> AgentListingCases { get; set; } = new List<AgentListingCase>();
    
}