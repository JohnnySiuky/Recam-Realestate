namespace Recam.Models.Entities;

public class CaseContact
{
    public int ContactId { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? CompanyName { get; set; }
    public string? ProfileUrl { get; set; }
    public string Email { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    
    public int ListingCaseId { get; set; }
    public ListingCase ListingCase { get; set; } = default!;
    
}