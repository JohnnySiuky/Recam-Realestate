namespace Recam.Common.Exceptions;

public sealed class ForbiddenException(string message) : DomainException(message, "FORBIDDEN");