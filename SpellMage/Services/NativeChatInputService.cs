using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using SpellMage.Models;
namespace SpellMage.Services;

// Call only on the framework thread; never retain native pointers across frames.
public sealed class NativeChatInputService(IGameGui gameGui)
{
    public unsafe ChatInputSnapshot Read()
    {
        var handle = gameGui.GetAddonByName("ChatLog");
        if (handle.IsNull || !handle.IsVisible) return new();
        var addon = (AddonChatLog*)handle.Address;
        if (!addon->IsReady) return new();
        var input = addon->TextInput;
        if (input == null) return new();
        var text = input->RawString.ToString();
        // Payloads (auto-translate, links) must not be interpreted as plain text.
        if (text.Contains('\u0002') || text.Contains('\u0003'))
            return new() { StatusMessage = "Chat payloads are not supported." };
        return new() {
            IsAvailable = true, IsFocused = input->IsActive, Text = text,
            Anchor = new Vector2(addon->X, addon->Y), StatusMessage = "Native chat input available"
        };
    }
}
