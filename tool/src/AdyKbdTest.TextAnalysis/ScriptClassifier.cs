namespace AdyKbdTest.TextAnalysis;

/// <summary>The writing-system class a token (or, per-character, a letter) is assigned to.</summary>
public enum ScriptClass
{
    Cyrillic,
    Latin,
    Digit,
    Mixed,
    Other,
}

/// <summary>Per-character and per-token script classification, based on Unicode code point ranges only.</summary>
public static class ScriptClassifier
{
    public static bool IsCyrillicLetter(char c) =>
        (c >= 'Ѐ' && c <= 'ӿ') || (c >= 'Ԁ' && c <= 'ԯ');

    public static bool IsLatinLetter(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
        (c >= 'À' && c <= 'ɏ') || (c >= 'Ḁ' && c <= 'ỿ');

    /// <summary>Classifies a single character as Cyrillic, Latin, Digit or Other (never Mixed).</summary>
    public static ScriptClass ClassifyChar(char c)
    {
        if (IsCyrillicLetter(c)) return ScriptClass.Cyrillic;
        if (IsLatinLetter(c)) return ScriptClass.Latin;
        if (char.IsDigit(c)) return ScriptClass.Digit;
        return ScriptClass.Other;
    }

    /// <summary>
    /// Classifies a whole token by the scripts of its letters (digits and punctuation do not
    /// determine the class on their own). A token with letters from more than one script is Mixed;
    /// a token with no letters at all (pure digits) is Digit; anything else falls back to Other.
    /// </summary>
    public static ScriptClass ClassifyToken(string token)
    {
        var sawCyrillic = false;
        var sawLatin = false;
        var sawDigit = false;
        var sawOther = false;

        foreach (var c in token)
        {
            var cls = ClassifyChar(c);
            switch (cls)
            {
                case ScriptClass.Cyrillic: sawCyrillic = true; break;
                case ScriptClass.Latin: sawLatin = true; break;
                case ScriptClass.Digit: sawDigit = true; break;
                default:
                    if (char.IsLetter(c)) sawOther = true;
                    break;
            }
        }

        var scriptCount = (sawCyrillic ? 1 : 0) + (sawLatin ? 1 : 0) + (sawOther ? 1 : 0);
        if (scriptCount > 1) return ScriptClass.Mixed;
        if (sawCyrillic) return ScriptClass.Cyrillic;
        if (sawLatin) return ScriptClass.Latin;
        if (sawOther) return ScriptClass.Other;
        if (sawDigit) return ScriptClass.Digit;
        return ScriptClass.Other;
    }

    /// <summary>Counts letters per script within a token; used by repair-scoping (dominant script of a chunk).</summary>
    public static (int Cyrillic, int Latin, int Other) CountLetters(string token)
    {
        int cyr = 0, lat = 0, other = 0;
        foreach (var c in token)
        {
            if (!char.IsLetter(c)) continue;
            if (IsCyrillicLetter(c)) cyr++;
            else if (IsLatinLetter(c)) lat++;
            else other++;
        }
        return (cyr, lat, other);
    }
}
