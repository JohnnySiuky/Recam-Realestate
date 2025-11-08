namespace Recam.Services.DTOs;

public record UserMeDto(
    string UserId,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<int> AssignedListingIds,  
    IReadOnlyList<int> CreatedListingIds    
);