namespace Recam.Common.Exceptions;

public sealed class NotFoundException(string message) : DomainException(message, "not found");