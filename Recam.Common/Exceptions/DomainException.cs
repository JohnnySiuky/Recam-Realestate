namespace Recam.Common.Exceptions;

public abstract class DomainException(string message, string code = "DOMAIN_ERROR") : Exception(message)
{
    public string code { get; } = code;
}
