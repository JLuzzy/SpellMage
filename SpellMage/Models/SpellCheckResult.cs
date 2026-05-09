using System.Collections.Generic;

namespace SamplePlugin.Models;

public class SpellCheckResult
{
    public string OriginalText { get; set; } = string.Empty;
    public string CorrectedText { get; set; } = string.Empty;
    public IReadOnlyList<SpellSuggestion> Suggestions { get; set; } = new List<SpellSuggestion>();
    public bool Success { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public bool UsedApi { get; set; }
}
