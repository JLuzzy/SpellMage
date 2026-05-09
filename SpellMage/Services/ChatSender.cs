using System;

namespace SamplePlugin.Services;

public sealed class ChatSender
{
    public ChatSender()
    {
    }

    // For safety, this first version only logs the message. Intentionally not sending to game.
    public void SendMessage(string message)
    {
        try
        {
            Plugin.Log.Information($"[SpellMage] SendMessage (log-only): {message}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "ChatSender.SendMessage failed");
        }
    }
}
