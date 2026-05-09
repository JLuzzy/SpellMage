namespace SamplePlugin.Models;

public class ChatInputSnapshot
{
    public bool IsAvailable { get; set; }
    public bool IsFocused { get; set; }
    public string Text { get; set; } = string.Empty;
    public int? CursorPosition { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}
