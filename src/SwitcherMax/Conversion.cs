using System.Text;

namespace SwitcherMax;

public enum ConversionMode { Auto, EnglishToRussian, RussianToEnglish }

/// <summary>Converts the physical US/RU keyboard positions; never stores or transmits text.</summary>
public static class Conversion
{
    private const string En = "`qwertyuiop[]asdfghjkl;'zxcvbnm,./";
    private const string Ru = "ёйцукенгшщзхъфывапролджэячсмитьбю.";
    private static readonly Dictionary<char, char> EnRu = new();
    private static readonly Dictionary<char, char> RuEn = new();

    static Conversion()
    {
        if (En.Length != Ru.Length) throw new InvalidOperationException("Layout tables are not aligned.");
        for (var i = 0; i < En.Length; i++)
        {
            Add(En[i], Ru[i]);
            if (char.IsLetter(En[i])) Add(char.ToUpperInvariant(En[i]), char.ToUpperInvariant(Ru[i]));
        }
        // Shift variants for the same physical keyboard positions.
        const string shiftedEn = "~{}:\"<>?";
        const string shiftedRu = "ЁХЪЖЭБЮ,";
        for (var i = 0; i < shiftedEn.Length; i++) Add(shiftedEn[i], shiftedRu[i]);
        // Shift+number: US symbols versus Russian keyboard symbols. Some are ambiguous;
        // the smart-punctuation option below can preserve sentence punctuation.
        Add('@', '"'); Add('#', '№'); Add('$', ';'); Add('^', ':'); Add('&', '?');
    }

    private static void Add(char en, char ru)
    {
        EnRu[en] = ru;
        // Collisions are possible for punctuation; priority goes to the first physical key.
        RuEn.TryAdd(ru, en);
    }

    public static ConversionMode? Detect(string source)
    {
        var en = 0;
        var ru = 0;
        foreach (var c in source)
        {
            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z') en++;
            else if (c is >= 'а' and <= 'я' or >= 'А' and <= 'Я' or 'ё' or 'Ё') ru++;
        }
        if (en == ru) return null; // Ambiguous / no letters: do not change anything.
        return en > ru ? ConversionMode.EnglishToRussian : ConversionMode.RussianToEnglish;
    }

    public static string? Convert(string source, ConversionMode mode, bool smartPunctuation = true)
    {
        if (string.IsNullOrEmpty(source)) return null;
        var direction = mode == ConversionMode.Auto ? Detect(source) : mode;
        if (direction is null) return null;
        var table = direction == ConversionMode.EnglishToRussian ? EnRu : RuEn;
        var output = new StringBuilder(source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            // English Shift+/ produces '?', but the same physical key in Russian produces ','.
            // Prefer physical-key conversion in EN -> RU, even when smart punctuation is enabled.
            // A literal question mark is ambiguous; this rule deliberately favors the comma key.
            if (smartPunctuation
                && !(direction == ConversionMode.EnglishToRussian && c == '?')
                && IsSentencePunctuation(source, i))
            {
                output.Append(c);
                continue;
            }
            // For mixed texts, only transform the letters from the chosen source layout.
            // Punctuation without an unambiguous keyboard equivalent is preserved.
            if (table.TryGetValue(c, out var mapped)) output.Append(mapped);
            else output.Append(c);
        }
        var result = output.ToString();
        return string.Equals(result, source, StringComparison.Ordinal) ? null : result;
    }

    private static bool IsSentencePunctuation(string input, int pos)
    {
        var c = input[pos];
        if (c is not (',' or '.' or '!' or '?' or ';' or ':')) return false;
        // Treat punctuation between a word and a separator/end as intentional punctuation.
        // Strict physical-key conversion can be selected in settings.
        if (pos == 0 || !char.IsLetterOrDigit(input[pos - 1])) return false;
        return pos == input.Length - 1 || char.IsWhiteSpace(input[pos + 1]);
    }
}
