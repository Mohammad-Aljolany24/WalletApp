namespace WalletApp.Core.Auth;

public record RefreshTokenData(
    Guid Id,
    Guid UserId,
    string TokenHash,
    Guid FamilyId,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    DateTime? RevokedAt,
    Guid? ReplacedByTokenId);