namespace Recam.Services.DTOs;

public record AgentSummaryDto(
    string Id,
    string? FirstName,
    string? LastName,
    string? Email,         
    string? CompanyName,
    string? AvatarUrl
);