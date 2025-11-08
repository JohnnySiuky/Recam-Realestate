using Recam.Models.Enums;
using Recam.Services.DTOs;

namespace Recam.Services.Interfaces;

public interface IListingCaseService
{
    Task<ListingCaseDto> CreateAsync(string currentUserId, bool canCreate, CreateListingCaseRequest req, CancellationToken ct);
    Task<ListingCaseDto> UpdateAsync(int id, string currentUserId, bool isAdmin, UpdateListingCaseRequest req, CancellationToken ct);
    
    Task<PagedResult<ListingCaseDto>> GetPagedAsync(
        string currentUserId,
        IReadOnlyCollection<string> roles,
        GetListingCasesQuery query,
        CancellationToken ct);
    
    Task DeleteAsync(int id, string currentUserId, bool isAdmin, CancellationToken ct);
    
    Task<ListingCaseDetailDto> GetDetailAsync(
        int id,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        CancellationToken ct);
    
    Task<ChangeListingStatusResponse> ChangeStatusAsync(
        int id,
        string operatorUserId,
        IReadOnlyCollection<string> roles,
        ListingCaseStatus newStatus,
        string? reason,
        CancellationToken ct);
    
    Task<ListingMediaResponse> GetMediaAsync(
        int listingId,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        CancellationToken ct);
    
    Task<IReadOnlyList<CaseContactDto>> GetContactsAsync(
        int listingId,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        CancellationToken ct);
    
    Task<IReadOnlyList<MediaGroupDto>> GetListingMediaAsync(
        int listingId, string currentUserId, IReadOnlyCollection<string> roles, CancellationToken ct);
    
    Task<CaseContactDto> AddContactAsync(
        int listingId,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        AddCaseContactRequest req,
        CancellationToken ct);
    
    Task<SetCoverImageResponse> SetCoverImageAsync(
        int listingId,
        int mediaId,
        string operatorUserId,
        CancellationToken ct = default);
    
    Task<PublishListingResponse> PublishAsync(
        int listingId,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        CancellationToken ct = default);
}