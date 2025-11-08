namespace Recam.Common.Exceptions;

public sealed class BadRequestException(string message) : DomainException(message, "BAD_REQUEST");