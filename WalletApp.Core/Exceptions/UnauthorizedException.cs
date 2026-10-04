namespace WalletApp.Core.Exceptions;

/// <summary>
/// Maps to 401 Unauthorized. Named this way deliberately — the BCL already
/// has System.Security.Authentication.AuthenticationException, and a
/// same-named type would force every consumer to disambiguate.
/// </summary>
public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message) : base(message) { }
}