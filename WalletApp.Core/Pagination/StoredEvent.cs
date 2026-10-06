using WalletApp.Core.Events;

namespace WalletApp.Core.Pagination;

/// <summary>
/// An event together with its storage identity. Required for cursor
/// pagination because <see cref="IEvent"/> has no Id — the identity
/// lives only on the persistence record.
/// </summary>
public record StoredEvent(long Id, IEvent Event);