using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Game.Gui;
using SamplePlugin.Windows;
using SamplePlugin.Services;
using System.Threading.Tasks;

namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    private const string MainCommand = "/spellmage";
    private const string SpellCommand = "/spell";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SpellMage");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    // Services
    private SpellCheckApiService ApiService { get; init; }
    private CorrectionService CorrectionService { get; init; }
    private ChatSender ChatSender { get; init; }
    internal NativeChatInputService NativeChatService { get; init; }
    private SpellCheckDebounceService DebounceService { get; init; }
    private ChatSuggestionOverlay OverlayWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        // initialize services
        ApiService = new SpellCheckApiService();
        CorrectionService = new CorrectionService();
        ChatSender = new ChatSender();

        NativeChatService = new NativeChatInputService();
        DebounceService = new SpellCheckDebounceService(ApiService);

        OverlayWindow = new ChatSuggestionOverlay();

        ConfigWindow = new ConfigWindow(this);
        // create overlay before main window so it can be toggled from the main UI
        OverlayWindow = new ChatSuggestionOverlay();
        MainWindow = new MainWindow(this, ApiService, CorrectionService, ChatSender, OverlayWindow, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(OverlayWindow);

        CommandManager.AddHandler(MainCommand, new CommandInfo(OnMainCommand)
        {
            HelpMessage = "Opens the SpellMage window"
        });

        CommandManager.AddHandler(SpellCommand, new CommandInfo(OnSpellCommand)
        {
            HelpMessage = "Check a message: /spell <message>"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information($"=== SpellMage loaded: {PluginInterface.Manifest.Name} ===");
    }

    public void Dispose()
    {
        try
        {
            PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
            PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
            PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

            WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();
            MainWindow.Dispose();

            CommandManager.RemoveHandler(MainCommand);
            CommandManager.RemoveHandler(SpellCommand);

            ApiService.Dispose();
            DebounceService.Dispose();
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "Error while disposing SpellMage");
        }
    }

    private void OnMainCommand(string command, string args)
    {
        try
        {
            MainWindow.Toggle();
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "Error handling /spellmage");
        }
    }

    private void OnSpellCommand(string command, string args)
    {
        _ = HandleSpellCommandAsync(args);
    }

    private async Task HandleSpellCommandAsync(string args)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Log.Information("Usage: /spell <message>");
                return;
            }

            var result = await ApiService.CheckAsync(args, "en-US");
            if (!result.Success)
            {
                Log.Warning($"Spell check failed: {result.StatusMessage}");
                return;
            }

            var corrected = CorrectionService.ApplyCorrections(result.OriginalText, result.Suggestions);
            Log.Information($"[SpellMage] Corrected: {corrected}");
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "/spell command failed");
        }
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
