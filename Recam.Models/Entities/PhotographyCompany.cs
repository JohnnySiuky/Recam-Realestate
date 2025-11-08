namespace Recam.Models.Entities;

public class PhotographyCompany
{
    public string Id { get; set; } = default!;   // Pk + FK to ApplicationUser
    public string PhotographyCompanyName { get; set; } = default!;

    public ApplicationUser User { get; set; } = default!;
    
    public ICollection<AgentPhotographyCompany> AgentPhotographyCompanies { get; set; } = new List<AgentPhotographyCompany>();
}