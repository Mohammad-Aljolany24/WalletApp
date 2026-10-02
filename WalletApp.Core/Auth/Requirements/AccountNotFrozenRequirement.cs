using Microsoft.AspNetCore.Authorization;

namespace WalletApp.Core.Auth.Requirements;

public class AccountNotFrozenRequirement : IAuthorizationRequirement
{
}