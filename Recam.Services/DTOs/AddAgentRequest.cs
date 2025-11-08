namespace Recam.Services.DTOs;

public record AddAgentRequest(string AgentEmail, string? CompanyId = null);