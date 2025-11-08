namespace Recam.Services.DTOs;

public record CreateAgentResponse(
    string UserId,
    string Email,
    string TemporaryPassword);  // for test, can remove when delivered, password