using System.Net.Mail;

namespace Kotlet.Domain.Common;

/// <summary>
/// A syntactically valid email address, paired with its case-insensitive normalized form used for
/// lookups and uniqueness checks.
/// </summary>
public readonly record struct Email
{
    public string Value { get; }
    public string Normalized { get; }

    private Email(string value)
    {
        Value = value;
        Normalized = value.ToUpperInvariant();
    }

    public static bool TryCreate(string? input, out Email email)
    {
        var trimmed = input?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !MailAddress.TryCreate(trimmed, out _))
        {
            email = default;
            return false;
        }

        email = new Email(trimmed);
        return true;
    }

    public override string ToString() => Value;
}
