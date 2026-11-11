namespace Trekky.Modules.WorkItems.Domain;

/// <summary>
/// Board order within a column (US-11 AC4): ranks compare ordinally (Postgres collation "C"), and a card moves by getting
/// one new rank between its neighbours, so no other card is rewritten. Fractional indexing (David Greenspan's algorithm):
/// an integer part whose first character gives its length, then a base-62 fraction that never ends in '0'. Appending at
/// either end steps the integer part, so ranks stay short when cards keep going to the top or bottom.
/// </summary>
internal static class BoardRank
{
    public const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>The rank of the first card in an empty column.</summary>
    public const string First = "a0";

    private static readonly string SmallestInteger = "A" + new string(Digits[0], 26);

    /// <summary>
    /// A rank strictly between <paramref name="before"/> (the card above, <c>null</c> for the top) and
    /// <paramref name="after"/> (the card below, <c>null</c> for the bottom).
    /// </summary>
    /// <exception cref="ArgumentException">A rank is not valid, or <paramref name="before"/> is not below <paramref name="after"/>.</exception>
    public static string Between(string? before, string? after)
    {
        if (before is not null)
        {
            Validate(before);
        }

        if (after is not null)
        {
            Validate(after);
        }

        if (before is not null && after is not null && string.CompareOrdinal(before, after) >= 0)
        {
            throw new ArgumentException($"Rank '{before}' must sort before '{after}'.", nameof(before));
        }

        if (before is null)
        {
            if (after is null)
            {
                return First;
            }

            var integerAfter = IntegerPart(after);
            var fractionAfter = after[integerAfter.Length..];
            if (integerAfter == SmallestInteger)
            {
                return integerAfter + Midpoint(string.Empty, fractionAfter);
            }

            if (integerAfter.Length < after.Length)
            {
                return integerAfter;
            }

            return Decrement(integerAfter) ?? throw new ArgumentException("No rank fits above the top card.", nameof(after));
        }

        var integerBefore = IntegerPart(before);
        var fractionBefore = before[integerBefore.Length..];
        if (after is null)
        {
            return Increment(integerBefore) ?? integerBefore + Midpoint(fractionBefore, null);
        }

        var integerOfAfter = IntegerPart(after);
        if (integerBefore == integerOfAfter)
        {
            return integerBefore + Midpoint(fractionBefore, after[integerOfAfter.Length..]);
        }

        var next = Increment(integerBefore) ?? throw new ArgumentException("No rank fits below the bottom card.", nameof(before));
        return string.CompareOrdinal(next, after) < 0 ? next : integerBefore + Midpoint(fractionBefore, null);
    }

    /// <summary>Whether <paramref name="rank"/> is one this class produces.</summary>
    public static bool IsValid(string rank)
    {
        try
        {
            Validate(rank);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>A fraction strictly between <paramref name="a"/> and <paramref name="b"/> (<c>null</c> = 1); neither ends in '0'.</summary>
    private static string Midpoint(string a, string? b)
    {
        if (b is not null)
        {
            // The common prefix stays; the midpoint is found in what follows it.
            var n = 0;
            while (n < b.Length && (n < a.Length ? a[n] : Digits[0]) == b[n])
            {
                n++;
            }

            if (n > 0)
            {
                return b[..n] + Midpoint(n < a.Length ? a[n..] : string.Empty, b[n..]);
            }
        }

        var digitA = a.Length > 0 ? Digits.IndexOf(a[0], StringComparison.Ordinal) : 0;
        var digitB = b is not null ? Digits.IndexOf(b[0], StringComparison.Ordinal) : Digits.Length;
        if (digitB - digitA > 1)
        {
            return Digits[(int)Math.Round(0.5 * (digitA + digitB), MidpointRounding.AwayFromZero)].ToString();
        }

        // Adjacent digits: keep a's first digit and go deeper, or take b's first digit when b has more after it.
        return b is not null && b.Length > 1
            ? b[..1]
            : Digits[digitA] + Midpoint(a.Length > 0 ? a[1..] : string.Empty, null);
    }

    private static void Validate(string rank)
    {
        if (rank.Length == 0 || rank == SmallestInteger)
        {
            throw new ArgumentException($"'{rank}' is not a rank.", nameof(rank));
        }

        var integer = IntegerPart(rank);
        foreach (var c in rank)
        {
            if (Digits.IndexOf(c, StringComparison.Ordinal) < 0)
            {
                throw new ArgumentException($"'{rank}' is not a rank.", nameof(rank));
            }
        }

        if (rank.Length > integer.Length && rank[^1] == Digits[0])
        {
            throw new ArgumentException($"'{rank}' is not a rank: its fraction ends in '0'.", nameof(rank));
        }
    }

    private static int IntegerLength(char head) => head switch
    {
        >= 'a' and <= 'z' => head - 'a' + 2,
        >= 'A' and <= 'Z' => 'Z' - head + 2,
        _ => throw new ArgumentException($"'{head}' cannot start a rank."),
    };

    private static string IntegerPart(string rank)
    {
        var length = IntegerLength(rank[0]);
        return length <= rank.Length ? rank[..length] : throw new ArgumentException($"'{rank}' is not a rank.", nameof(rank));
    }

    private static string? Increment(string integer)
    {
        var head = integer[0];
        var digits = integer[1..].ToCharArray();
        var carry = true;
        for (var i = digits.Length - 1; carry && i >= 0; i--)
        {
            var d = Digits.IndexOf(digits[i], StringComparison.Ordinal) + 1;
            if (d == Digits.Length)
            {
                digits[i] = Digits[0];
            }
            else
            {
                digits[i] = Digits[d];
                carry = false;
            }
        }

        if (!carry)
        {
            return head + new string(digits);
        }

        if (head == 'Z')
        {
            return "a" + Digits[0];
        }

        if (head == 'z')
        {
            return null;
        }

        var nextHead = (char)(head + 1);
        var rest = new string(digits);
        // Positive integers get one digit longer per head step; negative ones one shorter.
        return nextHead > 'a' ? nextHead + rest + Digits[0] : nextHead + rest[..^1];
    }

    private static string? Decrement(string integer)
    {
        var head = integer[0];
        var digits = integer[1..].ToCharArray();
        var borrow = true;
        for (var i = digits.Length - 1; borrow && i >= 0; i--)
        {
            var d = Digits.IndexOf(digits[i], StringComparison.Ordinal) - 1;
            if (d == -1)
            {
                digits[i] = Digits[^1];
            }
            else
            {
                digits[i] = Digits[d];
                borrow = false;
            }
        }

        if (!borrow)
        {
            return head + new string(digits);
        }

        if (head == 'a')
        {
            return "Z" + Digits[^1];
        }

        if (head == 'A')
        {
            return null;
        }

        var previousHead = (char)(head - 1);
        var rest = new string(digits);
        return previousHead < 'Z' ? previousHead + rest + Digits[^1] : previousHead + rest[..^1];
    }
}
