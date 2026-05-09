using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using SamplePlugin.Services;
using SamplePlugin.Models;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly SpellCheckApiService apiService;
    private readonly CorrectionService correctionService;
    private readonly ChatSender chatSender;
    private readonly ChatSuggestionOverlay overlay;
    private readonly string goatImagePath;

    private bool apiEnabled = true;
    private string language = "en-US";
    private string inputText = "teh party is redy for pull";
    private string status = "Idle";
    private string correctedText = string.Empty;
    private IReadOnlyList<SpellSuggestion> suggestions = Array.Empty<SpellSuggestion>();
    private bool isChecking = false;

    public MainWindow(Plugin plugin, SpellCheckApiService apiService, CorrectionService correctionService, ChatSender chatSender, ChatSuggestionOverlay overlay, string goatImagePath)
        : base("SpellMage")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
        this.apiService = apiService;
        this.correctionService = correctionService;
        this.chatSender = chatSender;
        this.overlay = overlay;
        this.goatImagePath = goatImagePath;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextWrapped("Privacy: When API checking is enabled, text is sent to LanguageTool (https://languagetool.org).");
        ImGui.Spacing();

        ImGui.Checkbox("API enabled", ref apiEnabled);
        ImGui.SameLine();
        var overlayEnabled = overlay != null && overlay.IsOpen;
        if (ImGui.Button(overlayEnabled ? "Hide Overlay" : "Show Overlay (test)"))
        {
            if (overlay != null)
                overlay.IsOpen = !overlay.IsOpen;
        }
        ImGui.SameLine();
        if (ImGui.Button("Inspect Chat Addon"))
        {
            try
            {
                var report = plugin.NativeChatService.InspectAddons();
                Plugin.Log.Information(report);
                status = report.Length > 200 ? report.Substring(0, 200) + "..." : report;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Inspect button failed");
                status = "Inspect failed";
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Inspect Native Chat Input"))
        {
            try
            {
                if (!plugin.Configuration.EnableNativeChatRead)
                {
                    status = "Native chat read disabled in config.";
                    Plugin.Log.Information("Inspect Native Chat Input skipped: EnableNativeChatRead is false in configuration.");
                }
                else
                {
                    var report = plugin.NativeChatService.InspectNativeAddons();
                    Plugin.Log.Information(report);
                    status = report.Length > 200 ? report.Substring(0, 200) + "..." : report;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Inspect Native Chat Input failed");
                status = "Inspect native failed";
            }
        }
        ImGui.SameLine();
        ImGui.InputText("Language", ref language, 64);

        ImGui.Separator();

        ImGui.Text("Input text:");
        ImGui.InputTextMultiline("##input", ref inputText, 4096, new Vector2(-1, 100));

        if (ImGui.Button("Check"))
        {
            _ = DoCheckAsync();
        }

        ImGui.SameLine();
        if (ImGui.Button("Copy Corrected to Clipboard"))
        {
            try { ImGui.SetClipboardText(correctedText); } catch { }
        }

        ImGui.SameLine();
        if (ImGui.Button("Send Corrected Message"))
        {
            try
            {
                chatSender.SendMessage(correctedText);
                status = "Send requested (log-only)";
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Send failed");
                status = "Send failed";
            }
        }

        ImGui.Separator();

        ImGui.Text($"Status: {status}");
        ImGui.Separator();

        ImGui.Text("Suggestions:");
        using (var child = ImRaii.Child("suggChild", new Vector2(0, 120), true))
        {
            if (child.Success)
            {
                foreach (var s in suggestions)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0.9f,0.6f,0.0f,1.0f), $"[{s.Offset}] '{s.OriginalText}' -> '{s.SuggestedText}'");
                    ImGui.TextWrapped($"  {s.Message} ({s.RuleId} / {s.Category})");
                }
            }
        }

        ImGui.Separator();
        ImGui.Text("Corrected output:");
        ImGui.InputTextMultiline("##corrected", ref correctedText, 8192, new Vector2(-1, 80), ImGuiInputTextFlags.ReadOnly);
    }

    private async Task DoCheckAsync()
    {
        if (isChecking) return;
        isChecking = true;
        status = "Checking...";

        try
        {
            if (!apiEnabled)
            {
                status = "API disabled";
                isChecking = false;
                return;
            }

            var res = await apiService.CheckAsync(inputText, language);
            if (!res.Success)
            {
                status = $"Error: {res.StatusMessage}";
                suggestions = Array.Empty<SpellSuggestion>();
                correctedText = inputText;
            }
            else
            {
                suggestions = res.Suggestions;
                    correctedText = correctionService.ApplyCorrections(res.OriginalText, res.Suggestions);
                    // update overlay if present
                    try
                    {
                        overlay?.SetSuggestions(res.Suggestions, "");
                    }
                    catch { }
                status = "OK";
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Check failed");
            status = "Exception during check";
        }
        finally
        {
            isChecking = false;
        }
    }
}
