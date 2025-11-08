namespace Recam.Services.DTOs;

public record CreateAgentRequest(
    string Email,
    string? FirstName,
    string? LastName,
    string? CompanyName
);