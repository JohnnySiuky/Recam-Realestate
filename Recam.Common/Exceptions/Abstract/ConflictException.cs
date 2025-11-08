namespace Recam.Common.Exceptions;

public sealed class ConflictException(string message) : DomainException(message, "CONFLICT");