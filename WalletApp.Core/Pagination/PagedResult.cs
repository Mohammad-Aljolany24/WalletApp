namespace WalletApp.Core.Pagination;

/// <summary>
/// Standard cursor-paginated response. <see cref="NextCursor"/> is null
/// when the caller has reached the end of the collection.
/// </summary>
public record PagedResult<T>(IReadOnlyList<T> Items, string? NextCursor);