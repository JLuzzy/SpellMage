using System;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using SamplePlugin.Models;

namespace SamplePlugin.Windows;

public class ChatSuggestionOverlay : Window, IDisposable
{
    private IReadOnlyList<SpellSuggestion> suggestions = new List<SpellSuggestion>();
    private string status = string.Empty;
    private Vector2 position = new Vector2(20, 700);

    public ChatSuggestionOverlay()
        : base("Spell Suggestions##ChatOverlay")
    {
        Flags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings;
        Size = new Vector2(360, 120);
    }

    public void SetSuggestions(IReadOnlyList<SpellSuggestion> suggs, string status)
    {
        this.suggestions = suggs ?? new List<SpellSuggestion>();
        this.status = status ?? string.Empty;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.SetNextWindowPos(position, ImGuiCond.FirstUseEver);

        using (var child = ImRaii.Child("overlayChild", new Vector2(-1, -1), true))
        {
            if (!child.Success) return;

            ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.9f, 1f, 1f), "SpellMage suggestions:");
            ImGui.Separator();
            foreach (var s in suggestions)
            {
                ImGui.Text($"{s.OriginalText} → {s.SuggestedText}");
                ImGui.TextWrapped($"  {s.Message}");
            }

            if (!string.IsNullOrEmpty(status))
            {
                ImGui.Separator();
                ImGui.TextColored(new System.Numerics.Vector4(1f, 0.6f, 0.6f, 1f), status);
            }
        }
    }
}
