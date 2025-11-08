namespace Recam.Models.Entities;

public class AgentPhotographyCompany
{
    public string AgentId { get; set; } = default!;
    public string PhotographyCompanyId { get; set; } = default!;

    public Agent Agent { get; set; } = default!;
    public PhotographyCompany PhotographyCompany { get; set; } = default!;
}