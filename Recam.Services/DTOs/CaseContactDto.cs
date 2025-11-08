public record CaseContactDto(
    int ContactId,
    string FirstName,
    string LastName,
    string? CompanyName,
    string? ProfileUrl,
    string Email,
    string PhoneNumber
);