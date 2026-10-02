using Microsoft.AspNetCore.Authorization;
using WalletApp.Core.Auth;
using WalletApp.Core.Auth.Requirements;

namespace WalletApp.API.Authorization;

public class CanTradeHandler : AuthorizationHandler<CanTradeRequirement>
{
    private readonly IUserStore _userStore;

    public CanTradeHandler(IUserStore userStore) => _userStore = userStore;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CanTradeRequirement requirement)
    {
        var userId = GetUserId(context);
        if (userId is null) return;

        var user = await _userStore.GetByIdAsync(userId.Value);
        if (user is null) return;

        if (user.IsVerified && !user.IsFrozen)
            context.Succeed(requirement);
    }

    private static Guid? GetUserId(AuthorizationHandlerContext context)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

public class IsVerifiedHandler : AuthorizationHandler<IsVerifiedRequirement>
{
    private readonly IUserStore _userStore;

    public IsVerifiedHandler(IUserStore userStore) => _userStore = userStore;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IsVerifiedRequirement requirement)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId)) return;

        var user = await _userStore.GetByIdAsync(userId);
        if (user is null) return;

        if (user.IsVerified)
            context.Succeed(requirement);
    }
}

public class AccountNotFrozenHandler : AuthorizationHandler<AccountNotFrozenRequirement>
{
    private readonly IUserStore _userStore;

    public AccountNotFrozenHandler(IUserStore userStore) => _userStore = userStore;

  protected override async Task HandleRequirementAsync(
    AuthorizationHandlerContext context,
    AccountNotFrozenRequirement requirement)
{
  

    var sub = context.User.FindFirst("sub")?.Value;
   
    if (!Guid.TryParse(sub, out var userId)) return;

    var user = await _userStore.GetByIdAsync(userId);
    

    if (user is null) return;

    if (!user.IsFrozen)
        context.Succeed(requirement);
}
}