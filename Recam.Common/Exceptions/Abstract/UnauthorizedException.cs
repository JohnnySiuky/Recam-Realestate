namespace Recam.Common.Exceptions;

public sealed class UnauthorizedException(string message) : DomainException(message, "UNAUTHORIZED");
