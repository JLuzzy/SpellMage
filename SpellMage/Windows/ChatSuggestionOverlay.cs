using System;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using SpellMage.Models;
namespace SpellMage.Windows;

public sealed class ChatSuggestionOverlay : Window, IDisposable
{
    private IReadOnlyList<SpellSuggestion> suggestions = Array.Empty<SpellSuggestion>();
    private string status = string.Empty;
    public Vector2 Anchor { get; set; }
    public ChatSuggestionOverlay() : base("SpellMage native suggestions")
    {
        Flags = ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoInputs
            | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
        Size = new Vector2(400, 180);
    }
    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        var position = viewport.Pos + Anchor - new Vector2(0, 190);
        var upper = Vector2.Max(viewport.WorkPos, viewport.WorkPos + viewport.WorkSize - new Vector2(400, 180));
        ImGui.SetNextWindowPos(Vector2.Clamp(position, viewport.WorkPos, upper), ImGuiCond.Always);
    }
    public void SetSuggestions(IReadOnlyList<SpellSuggestion> value, string message) { suggestions = value; status = message; }
    public void Clear() { suggestions = Array.Empty<SpellSuggestion>(); status = string.Empty; IsOpen = false; }
    public override void Draw()
    {
        ImGui.TextWrapped(status);
        foreach (var suggestion in suggestions) ImGui.TextWrapped($"{suggestion.OriginalText} → {suggestion.SuggestedText}");
    }
    public void Dispose() => Clear();
}
