namespace WalletApp.Core.Exceptions;

/// <summary>
/// Maps to 400 Bad Request via the ValidationException base.
/// Carries the amounts so the handler can produce a helpful detail message.
/// </summary>
public class InsufficientFundsException : ValidationException
{
    public decimal Requested { get; }
    public decimal Available { get; }

    public InsufficientFundsException(decimal requested, decimal available)
        : base("Insufficient funds.")
    {
        Requested = requested;
        Available = available;
    }
}