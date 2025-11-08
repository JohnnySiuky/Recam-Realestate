namespace Recam.Services.DTOs;

public record AddCaseContactRequest(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string? CompanyName,
    string? ProfileUrl
);