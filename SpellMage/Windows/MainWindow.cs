using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using SpellMage.Models;
using SpellMage.Services;
namespace SpellMage.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly CorrectionService corrections;
    private readonly SpellCheckDebounceService request;
    private string input = string.Empty;
    private string output = string.Empty;
    private string status = "Enter text, then check it.";
    private IReadOnlyList<SpellSuggestion> suggestions = Array.Empty<SpellSuggestion>();
    private bool hasResult;
    public MainWindow(Plugin plugin, SpellCheckApiService api, CorrectionService corrections) : base("SpellMage")
    {
        this.plugin = plugin; this.corrections = corrections; request = new(api);
        Size = new Vector2(650, 560); SizeCondition = ImGuiCond.FirstUseEver;
    }
    public void Invalidate()
    {
        request.Cancel(); suggestions = Array.Empty<SpellSuggestion>(); output = string.Empty; hasResult = false;
        status = "Text or settings changed. Check again.";
    }
    public void CheckText(string text)
    {
        input = text; IsOpen = true; Invalidate();
        request.Start(input, plugin.Configuration.GetCheckSettings()); status = "Checking...";
    }
    public override void Draw()
    {
        ImGui.TextWrapped("LanguageTool: https://languagetool.org — checking sends text to the server selected in Settings.");
        if (ImGui.Button("Settings")) plugin.ToggleConfigUi();
        ImGui.SameLine();
        if (ImGui.Button("Read current chat draft"))
        {
            var snapshot = plugin.LatestSnapshot;
            if (!plugin.Configuration.EnableNativeChatRead) status = "Enable native reading in Settings first.";
            else if (!snapshot.IsAvailable) status = "Focus the game chat input first.";
            else { input = snapshot.Text; Invalidate(); }
        }
        ImGui.Text("Message:");
        if (ImGui.InputTextMultiline("##input", ref input, 4096, new Vector2(-1, 100))) Invalidate();
        var result = request.Poll();
        if (result != null)
        {
            hasResult = result.Success;
            suggestions = result.Success ? corrections.Filter(result.OriginalText, result.Suggestions) : Array.Empty<SpellSuggestion>();
            output = result.Success ? result.OriginalText : string.Empty;
            status = result.StatusMessage;
        }
        ImGui.BeginDisabled(request.IsChecking || string.IsNullOrWhiteSpace(input));
        if (ImGui.Button("Check")) CheckText(input);
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!hasResult);
        if (ImGui.Button("Copy reviewed text")) ImGui.SetClipboardText(output);
        ImGui.EndDisabled();
        ImGui.TextWrapped(status);
        ImGui.Separator();
        ImGui.Text("Choose corrections:");
        var index = 0;
        foreach (var suggestion in suggestions)
        {
            var selected = suggestion.IsApplied;
            if (ImGui.Checkbox($"{suggestion.OriginalText} → {suggestion.SuggestedText}##suggestion{index++}", ref selected))
            {
                suggestion.IsApplied = selected;
                output = corrections.ApplyCorrections(input, suggestions.Where(s => s.IsApplied).ToArray());
            }
            ImGui.TextWrapped(suggestion.Message);
        }
        if (hasResult && suggestions.Count == 0) ImGui.Text("No applicable corrections.");
        ImGui.Separator();
        ImGui.Text("Reviewed text:");
        ImGui.InputTextMultiline("##output", ref output, 8192, new Vector2(-1, 100), ImGuiInputTextFlags.ReadOnly);
    }
    public void Dispose() => request.Dispose();
}
