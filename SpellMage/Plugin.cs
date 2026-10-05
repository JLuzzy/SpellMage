using System;
using System.IO;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SpellMage.Models;
using SpellMage.Services;
using SpellMage.Windows;
namespace SpellMage;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    public Configuration Configuration { get; }
    public ChatInputSnapshot LatestSnapshot { get; private set; } = new();
    private readonly WindowSystem windows = new("SpellMage");
    private readonly SpellCheckApiService api = new();
    private readonly CorrectionService corrections;
    private readonly NativeChatInputService native;
    private readonly SpellCheckDebounceService live;
    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;
    private readonly ChatSuggestionOverlay overlay = new();
    private string liveText = string.Empty;
    private DateTime nextRead;
    private bool nativeFaulted;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new();
        // Existing experimental settings never silently opt users into network access.
        if (Configuration.Version < 1) { Configuration.Version = 1; Configuration.EnableNativeChatRead = false; Configuration.Save(); }
        corrections = new(Path.Combine(PluginInterface.AssemblyLocation.Directory!.FullName, "Dictionaries", "ffxiv-ignore.txt"));
        native = new(GameGui); live = new(api);
        mainWindow = new(this, api, corrections);
        configWindow = new(Configuration, SettingsChanged);
        windows.AddWindow(mainWindow); windows.AddWindow(configWindow); windows.AddWindow(overlay);
        CommandManager.AddHandler("/spellmage", new CommandInfo((_, _) => ToggleMainUi()) { HelpMessage = "Open SpellMage." });
        CommandManager.AddHandler("/spell", new CommandInfo((_, text) => mainWindow.CheckText(text)) { HelpMessage = "Check text and review results in SpellMage: /spell <message>" });
        PluginInterface.UiBuilder.Draw += windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        Framework.Update += Update;
    }
    private void SettingsChanged()
    {
        mainWindow.Invalidate(); ResetLive(); LatestSnapshot = new(); nativeFaulted = false;
    }
    private void ResetLive() { live.Cancel(); liveText = string.Empty; overlay.Clear(); }
    private void Update(IFramework framework)
    {
        if (!Configuration.EnableNativeChatRead || nativeFaulted) { LatestSnapshot = new(); ResetLive(); return; }
        if (DateTime.UtcNow < nextRead) return;
        nextRead = DateTime.UtcNow.AddMilliseconds(100);
        try { LatestSnapshot = native.Read(); }
        catch (Exception)
        {
            nativeFaulted = true; LatestSnapshot = new(); ResetLive();
            Log.Warning("SpellMage native reading stopped after an error. Re-enable it in settings to retry.");
            return;
        }
        var settings = Configuration.GetCheckSettings();
        var snapshot = LatestSnapshot;
        if (!Configuration.EnableLiveChecking || !settings.Enabled || !settings.IsPrivateEndpoint
            || !snapshot.IsAvailable || !snapshot.IsFocused || snapshot.Text.Length < 3)
        { ResetLive(); return; }
        overlay.Anchor = snapshot.Anchor;
        if (snapshot.Text != liveText)
        {
            liveText = snapshot.Text; overlay.Clear();
            live.Start(liveText, settings, debounce: true);
        }
        var result = live.Poll();
        if (result != null)
        {
            overlay.SetSuggestions(result.Success ? corrections.Filter(result.OriginalText, result.Suggestions) : Array.Empty<SpellSuggestion>(), result.StatusMessage);
            overlay.IsOpen = Configuration.ShowOverlay;
        }
    }
    public void ToggleConfigUi() => configWindow.Toggle();
    public void ToggleMainUi() => mainWindow.Toggle();
    public void Dispose()
    {
        Framework.Update -= Update;
        PluginInterface.UiBuilder.Draw -= windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        CommandManager.RemoveHandler("/spellmage"); CommandManager.RemoveHandler("/spell");
        live.Dispose(); mainWindow.Dispose(); configWindow.Dispose(); overlay.Dispose();
        windows.RemoveAllWindows(); api.Dispose();
    }
}
