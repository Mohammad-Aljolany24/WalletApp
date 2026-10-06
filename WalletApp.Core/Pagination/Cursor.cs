using System.Text;

namespace WalletApp.Core.Pagination;

/// <summary>
/// Encodes and decodes opaque cursors. The wire format is deliberately
/// opaque to clients — it can change without a breaking API change.
/// </summary>
public static class Cursor
{
    // Events sort on a monotonic long Id, so a plain string works.
    public static string EncodeEventCursor(long id) => id.ToString();

    public static long? DecodeEventCursor(string cursor) =>
        long.TryParse(cursor, out var id) ? id : null;

    // Users sort on (CreatedAt, Id) because CreatedAt isn't unique.
    // The cursor is base64 of "<ticks>|<guid>".
    public static string EncodeUserCursor(DateTime createdAt, Guid id)
    {
        var raw = $"{createdAt.Ticks}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static (DateTime CreatedAt, Guid Id)? DecodeUserCursor(string cursor)
    {
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');
            if (parts.Length != 2) return null;
            if (!long.TryParse(parts[0], out var ticks)) return null;
            if (!Guid.TryParse(parts[1], out var id)) return null;
            return (new DateTime(ticks, DateTimeKind.Utc), id);
        }
        catch
        {
            return null;
        }
    }
}