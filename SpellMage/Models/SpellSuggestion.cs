namespace SamplePlugin.Models;

public class SpellSuggestion
{
    public string OriginalText { get; set; } = string.Empty;
    public string SuggestedText { get; set; } = string.Empty;
    public int Offset { get; set; }
    public int Length { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? RuleId { get; set; }
    public string? RuleDescription { get; set; }
    public string? Category { get; set; }
    public double Confidence { get; set; }
    public bool IsApplied { get; set; }
}
