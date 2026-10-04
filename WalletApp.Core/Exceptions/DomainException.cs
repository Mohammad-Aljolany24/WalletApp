namespace WalletApp.Core.Exceptions;

/// <summary>
/// Base class for exceptions that represent expected business failures.
/// The API's global exception handler maps these to specific HTTP status
/// codes. Anything that does NOT derive from this type is treated as an
/// unexpected server error (500) and never leaks its message to clients.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception inner) : base(message, inner) { }
}