namespace Recam.Models.Entities;

public class AgentListingCase
{
    public string AgentId { get; set; } = default!;
    public int ListingCaseId { get; set; }

    public Agent Agent { get; set; } = default!;
    public ListingCase ListingCase { get; set; } = default!;
}