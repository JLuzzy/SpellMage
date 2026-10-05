using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace SpellMage.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Configuration config;
    private readonly Action changed;
    public ConfigWindow(Configuration config, Action changed) : base("SpellMage Settings")
    {
        this.config = config;
        this.changed = changed;
        Size = new Vector2(600, 360);
        SizeCondition = ImGuiCond.FirstUseEver;
    }
    public override void Draw()
    {
        ImGui.TextWrapped("Checking sends your text to the configured LanguageTool server. LanguageTool: https://languagetool.org");
        var dirty = false;
        var enabled = config.ApiEnabled;
        if (ImGui.Checkbox("Enable API checking", ref enabled)) { config.ApiEnabled = enabled; dirty = true; }
        var language = config.Language;
        if (ImGui.InputText("Language (e.g. en-US or auto)", ref language, 64)) { config.Language = language; dirty = true; }
        var endpoint = config.Endpoint;
        if (ImGui.InputText("Check endpoint", ref endpoint, 512)) { config.Endpoint = endpoint; dirty = true; }
        ImGui.TextWrapped("Use HTTPS, or HTTP on localhost. The public endpoint supports manual checks only. Live checking requires your own LanguageTool server.");
        var native = config.EnableNativeChatRead;
        if (ImGui.Checkbox("Read native chat input (experimental)", ref native)) { config.EnableNativeChatRead = native; dirty = true; }
        var live = config.EnableLiveChecking;
        if (ImGui.Checkbox("Check while typing (private server only)", ref live)) { config.EnableLiveChecking = live; dirty = true; }
        var overlay = config.ShowOverlay;
        if (ImGui.Checkbox("Show native suggestions overlay", ref overlay)) { config.ShowOverlay = overlay; dirty = true; }
        if (dirty) { config.Save(); changed(); }
    }
    public void Dispose() { }
}
