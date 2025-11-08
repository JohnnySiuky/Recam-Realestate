namespace Recam.Common.Exceptions;

public sealed class ValidationException(string message, object? details = null)
    : DomainException(message, "VALIDATION_ERROR")
{
    public object? Details { get; } = details;
}