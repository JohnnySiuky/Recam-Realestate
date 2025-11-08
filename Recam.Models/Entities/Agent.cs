namespace Recam.Models.Entities;

public class Agent
{
    public string Id { get; set; } = default!;   // PK and FK For ApplicationUser
    public string AgentFirstName { get; set; } = default!;
    public string AgentLastName { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public string? CompanyName { get; set; }

    public ApplicationUser User { get; set; } = default!;

    public ICollection<AgentListingCase> AgentListingCases { get; set; } = new List<AgentListingCase>();
    public ICollection<AgentPhotographyCompany> AgentPhotographyCompanies { get; set; } = new List<AgentPhotographyCompany>();
    
}