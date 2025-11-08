using Recam.Models.Enums;

namespace Recam.Services.DTOs;

public class GetListingCasesQuery
{
    // paging
    public int Page { get; set; } = 1;          // 1-based
    public int PageSize { get; set; } = 20;     

    // sorting
    public string? SortBy { get; set; } = "CreatedAt"; // Title|City|Price|Bedrooms|CreatedAt
    public string? SortDir { get; set; } = "desc";     // asc|desc

    // filters
    public string? Q { get; set; }              // 關鍵字: Title/Desc/Street/City/State
    public string? City { get; set; }
    public string? State { get; set; }
    public PropertyType? PropertyType { get; set; }
    public SaleCategory? SaleCategory { get; set; }
    public ListingCaseStatus? Status { get; set; }
    public int? MinBedrooms { get; set; }
    public int? MaxBedrooms { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public DateTime? CreatedFromUtc { get; set; }
    public DateTime? CreatedToUtc { get; set; }
}