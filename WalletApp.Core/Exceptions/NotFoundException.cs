namespace WalletApp.Core.Exceptions;

/// <summary>Maps to 404 Not Found.</summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }
}