namespace AdyKbdTest.TextAnalysis.Checks;

/// <summary>
/// The four checks that run per token after repair and tokenisation: PAL_CASE, MIXED_SCRIPT,
/// NON_ALPHABET and CONFUSABLE. PAL_LOOKALIKE, NORMALIZATION and INVISIBLE are produced earlier,
/// by <see cref="Repairer"/> and <see cref="TextNormalizer"/>, and are not duplicated here.
///
/// The Adyghe-specific checks (PAL_CASE, NON_ALPHABET, CONFUSABLE) are scoped to
/// Cyrillic-dominant tokens: strictly more Cyrillic than Latin letters, the same test
/// <see cref="Repairer"/> uses to decide where palochka look-alikes get fixed. This is
/// deliberately looser than "purely Cyrillic", because a single stray Latin letter inside an
/// otherwise-Cyrillic word ("Прогрaмма") is exactly the case CONFUSABLE exists to catch; if the
/// scope required a pure-Cyrillic token, the presence of that one Latin letter would itself
/// disqualify the token from being checked. MIXED_SCRIPT is script-agnostic and runs on every
/// token regardless of dominance, because it is measuring script composition itself, not content.
/// </summary>
public static class TokenChecks
{
    public static IEnumerable<Finding> Run(TokenSpan token, bool isFirstTokenOfSentence, LanguageProfile profile)
    {
        var (cyr, lat, _) = ScriptClassifier.CountLetters(token.Text);
        var isCyrillicDominant = cyr > lat;

        if (isCyrillicDominant)
        {
            foreach (var f in PalochkaCase(token, isFirstTokenOfSentence, profile)) yield return f;
            foreach (var f in NonAlphabet(token, profile)) yield return f;
            foreach (var f in Confusable(token, profile)) yield return f;
        }

        foreach (var f in MixedScript(token)) yield return f;
    }

    private static IEnumerable<Finding> PalochkaCase(TokenSpan token, bool isFirstTokenOfSentence, LanguageProfile profile)
    {
        var text = token.Text;
        var upper = profile.Alphabet.CharactersOfNote.PalochkaUpper;
        var lower = profile.Alphabet.CharactersOfNote.PalochkaLower;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i].ToString();
            if (i > 0 && string.Equals(ch, upper, StringComparison.Ordinal))
            {
                yield return new Finding
                {
                    Code = FindingCodes.PalochkaCase,
                    Severity = FindingSeverity.Low,
                    Offset = token.StartOffset + i,
                    Length = 1,
                    Found = upper,
                    Expected = lower,
                    Note = "upper-case palochka mid-word",
                };
            }
            else if (i == 0 && isFirstTokenOfSentence && string.Equals(ch, lower, StringComparison.Ordinal))
            {
                yield return new Finding
                {
                    Code = FindingCodes.PalochkaCase,
                    Severity = FindingSeverity.Low,
                    Offset = token.StartOffset + i,
                    Length = 1,
                    Found = lower,
                    Expected = upper,
                    Note = "lower-case palochka at the start of a sentence-initial word",
                };
            }
        }
    }

    private static IEnumerable<Finding> NonAlphabet(TokenSpan token, LanguageProfile profile)
    {
        var text = token.Text;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (ScriptClassifier.ClassifyChar(c) != ScriptClass.Cyrillic) continue;
            var chStr = c.ToString();
            if (profile.Alphabet.IsAlphabetCharacter(chStr)) continue;

            yield return new Finding
            {
                Code = FindingCodes.NonAlphabet,
                Severity = FindingSeverity.Medium,
                Offset = token.StartOffset + i,
                Length = 1,
                Found = chStr,
                Expected = "(not in the alphabet source's \"characters\" inventory)",
            };
        }
    }

    private static IEnumerable<Finding> Confusable(TokenSpan token, LanguageProfile profile)
    {
        var text = token.Text;
        for (var i = 0; i < text.Length; i++)
        {
            var chStr = text[i].ToString();
            if (!profile.TryGetConfusable(chStr, out var pair)) continue;

            yield return new Finding
            {
                Code = FindingCodes.Confusable,
                Severity = FindingSeverity.High,
                Offset = token.StartOffset + i,
                Length = 1,
                Found = pair.Latin,
                Expected = pair.Cyrillic,
                Note = "Latin homoglyph inside a Cyrillic-dominant word",
            };
        }
    }

    private static IEnumerable<Finding> MixedScript(TokenSpan token)
    {
        if (ScriptClassifier.ClassifyToken(token.Text) != ScriptClass.Mixed) yield break;

        var weight = ClassifyWeight(token.Text);
        var severity = weight == MixedScriptWeight.ProperNounAffix ? FindingSeverity.Low : FindingSeverity.High;

        yield return new Finding
        {
            Code = FindingCodes.MixedScript,
            Severity = severity,
            Offset = token.StartOffset,
            Length = token.Length,
            Found = token.Text,
            Expected = "(single script)",
            Weight = weight,
            Note = weight == MixedScriptWeight.ProperNounAffix
                ? "Latin stem with a Cyrillic grammatical affix"
                : "scripts interleaved within the word",
        };
    }

    private static MixedScriptWeight ClassifyWeight(string token)
    {
        var parts = token.Split('-');
        if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
        {
            var stem = parts[0];
            var suffix = parts[1];
            var stemIsLatin = ScriptClassifier.ClassifyToken(stem) == ScriptClass.Latin;
            var suffixIsCyrillic = ScriptClassifier.ClassifyToken(suffix) == ScriptClass.Cyrillic;
            if (stemIsLatin && suffixIsCyrillic && char.IsUpper(stem[0]))
            {
                return MixedScriptWeight.ProperNounAffix;
            }
        }

        return MixedScriptWeight.Interleaved;
    }
}
