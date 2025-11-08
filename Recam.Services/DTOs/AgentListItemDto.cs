namespace Recam.Services.DTOs;

public record AgentListItemDto(
    string Id,
    string FirstName,
    string LastName,
    string? Email,
    string? CompanyName,
    string? AvatarUrl
);