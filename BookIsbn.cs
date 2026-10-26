using System.Text.RegularExpressions;

namespace Coflnet.Ane;

/// <summary>Validates an ISBN and normalizes ISBN-10 to its equivalent ISBN-13.</summary>
public static class BookIsbn
{
    public static string? Normalize(string? value)
    {
        value = Regex.Replace(value ?? "", @"[-\s]", "").ToUpperInvariant();
        if (!Regex.IsMatch(value, @"^(?:[0-9]{9}[0-9X]|97[89][0-9]{10})$") || !HasValidChecksum(value))
            return null;
        if (value.Length == 13)
            return value;
        var prefix = "978" + value[..9];
        return prefix + (10 - prefix.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum() % 10) % 10;
    }

    public static bool HasValidChecksum(string value)
    {
        if (value.Length == 13 && value.All(char.IsDigit))
            return value.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum() % 10 == 0;
        if (value.Length != 10 || !value[..9].All(char.IsDigit) || !(char.IsDigit(value[9]) || value[9] == 'X'))
            return false;
        return value.Select((c, i) => (c == 'X' ? 10 : c - '0') * (10 - i)).Sum() % 11 == 0;
    }
}
