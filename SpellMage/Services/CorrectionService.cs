using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SpellMage.Models;

namespace SpellMage.Services;

public sealed class CorrectionService
{
    private readonly HashSet<string> ignoreSet = new(StringComparer.OrdinalIgnoreCase)
    { "FFXIV", "PF", "prog", "reclear", "glam", "materia", "Aetheryte", "Limsa", "WHM", "BLM", "RDM", "P12S", "o/" };
    public CorrectionService(string? dictionaryPath = null)
    {
        var path = dictionaryPath ?? Path.Combine(AppContext.BaseDirectory, "Dictionaries", "ffxiv-ignore.txt");
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadLines(path))
        {
            var term = line.Trim();
            if (term.Length > 0 && !term.StartsWith('#')) ignoreSet.Add(term);
        }
    }

    public static bool IsValidRange(string text, int offset, int length) => offset >= 0 && length >= 0
        && offset <= text.Length && length <= text.Length - offset
        && IsBoundary(text, offset) && IsBoundary(text, offset + length);

    private static bool IsBoundary(string text, int i) => i == 0 || i == text.Length
        || !(char.IsHighSurrogate(text[i - 1]) && char.IsLowSurrogate(text[i]));

    public IReadOnlyList<SpellSuggestion> Filter(string text, IReadOnlyList<SpellSuggestion> suggestions)
    {
        var protectedRanges = Regex.Matches(text, @"\S+").Cast<Match>()
            .Where(m => ignoreSet.Contains(m.Value.Trim(',', '.', '!', '?', ':', ';'))
                || m.Value.Any(char.IsDigit) || m.Value.StartsWith('/')
                || m.Value.Contains("://") || m.Value.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                || m.Value.Contains('@') || (m.Value.StartsWith('<') && m.Value.EndsWith('>'))).ToList();
        var accepted = new List<SpellSuggestion>();
        foreach (var s in suggestions.OrderBy(s => s.Offset).ThenByDescending(s => s.Length))
        {
            if (!IsValidRange(text, s.Offset, s.Length) || s.OriginalText != text.Substring(s.Offset, s.Length)) continue;
            if (protectedRanges.Any(m => s.Offset < m.Index + m.Length && s.Offset + s.Length >= m.Index)) continue;
            if (accepted.Any(a => s.Offset < a.Offset + a.Length || s.Offset == a.Offset)) continue;
            accepted.Add(s);
        }
        return accepted;
    }

    public string ApplyCorrections(string original, IReadOnlyList<SpellSuggestion> suggestions)
    {
        var output = original;
        foreach (var s in Filter(original, suggestions).OrderByDescending(s => s.Offset))
            output = output.Remove(s.Offset, s.Length).Insert(s.Offset, s.SuggestedText);
        return output;
    }
}
