using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SamplePlugin.Models;

namespace SamplePlugin.Services;

public sealed class CorrectionService
{
    private readonly HashSet<string> ignoreSet;

    public CorrectionService()
    {
        ignoreSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FFXIV","PF","prog","reclear","glam","materia","Aetheryte","Limsa",
            "WHM","BLM","RDM","P12S","o/"
        };

        // Try to load extra ignore terms from Dictionaries/ffxiv-ignore.txt next to assembly
        try
        {
            var asmPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (asmPath != null)
            {
                var dict = Path.Combine(asmPath, "Dictionaries", "ffxiv-ignore.txt");
                if (File.Exists(dict))
                {
                    foreach (var ln in File.ReadAllLines(dict))
                    {
                        var t = ln.Trim();
                        if (t.Length > 0 && !t.StartsWith("#")) ignoreSet.Add(t);
                    }
                }
            }
        }
        catch { }
    }

    public string ApplyCorrections(string original, IReadOnlyList<SpellSuggestion> suggestions)
    {
        if (string.IsNullOrEmpty(original) || suggestions == null || suggestions.Count == 0)
            return original ?? string.Empty;

        // Work on a StringBuilder copy
        var output = original;

        // Sort by offset descending so replacements don't shift remaining offsets
        var ordered = suggestions.OrderByDescending(s => s.Offset).ToList();

        foreach (var s in ordered)
        {
            if (string.IsNullOrEmpty(s.SuggestedText)) continue;

            var frag = s.OriginalText?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(frag)) continue;

            // skip if in ignore list
            if (ignoreSet.Contains(frag)) continue;

            // skip if contains numbers (e.g., P12S)
            if (System.Text.RegularExpressions.Regex.IsMatch(frag, "\\d")) continue;

            // skip slash commands
            if (frag.StartsWith("/")) continue;

            // skip URLs
            if (frag.Contains("://")) continue;

            try
            {
                if (s.Offset >= 0 && s.Offset + s.Length <= output.Length)
                {
                    output = output.Substring(0, s.Offset) + s.SuggestedText + output.Substring(s.Offset + s.Length);
                }
            }
            catch { }
        }

        return output;
    }
}
