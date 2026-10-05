using Dalamud.Configuration;
using System;
using SpellMage.Services;
namespace SpellMage;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool ApiEnabled { get; set; } = false;
    public string Language { get; set; } = "en-US";
    public string Endpoint { get; set; } = CheckSettings.PublicEndpoint;
    public bool EnableNativeChatRead { get; set; } = false;
    public bool EnableLiveChecking { get; set; } = false;
    public bool ShowOverlay { get; set; } = true;
    public CheckSettings GetCheckSettings() => new(ApiEnabled, Language, Endpoint);
    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
