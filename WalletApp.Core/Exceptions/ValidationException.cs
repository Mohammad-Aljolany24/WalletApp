namespace WalletApp.Core.Exceptions;

/// <summary>Maps to 400 Bad Request.</summary>
public class ValidationException : DomainException
{
    public ValidationException(string message) : base(message) { }
}