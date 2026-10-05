using System;
using System.Threading;
using System.Threading.Tasks;
using SpellMage.Models;
namespace SpellMage.Services;

// Owned and polled by the UI/framework thread. Worker tasks never mutate UI state.
public sealed class SpellCheckDebounceService : IDisposable
{
    private readonly SpellCheckApiService api;
    private CancellationTokenSource? cancellation;
    private Task<SpellCheckResult>? pending;
    public bool IsChecking => pending != null;
    public SpellCheckDebounceService(SpellCheckApiService api) => this.api = api;
    public void Start(string text, CheckSettings settings, bool debounce = false)
    {
        Cancel();
        var source = new CancellationTokenSource();
        cancellation = source;
        pending = RunAsync(text, settings, debounce, source.Token);
    }
    private async Task<SpellCheckResult> RunAsync(string text, CheckSettings settings, bool debounce, CancellationToken token)
    {
        try
        {
            if (debounce && !settings.IsPrivateEndpoint)
                return new SpellCheckResult { OriginalText = text, StatusMessage = "Live checking requires a private LanguageTool server." };
            if (debounce) await Task.Delay(900, token).ConfigureAwait(false);
            return await api.CheckAsync(text, settings, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return new SpellCheckResult { OriginalText = text, StatusMessage = "Canceled" }; }
        catch (Exception) { return new SpellCheckResult { OriginalText = text, StatusMessage = "Spell check failed." }; }
    }
    public SpellCheckResult? Poll()
    {
        if (pending == null || !pending.IsCompleted) return null;
        var result = pending.GetAwaiter().GetResult();
        pending = null;
        cancellation?.Dispose();
        cancellation = null;
        return result;
    }
    public void Cancel()
    {
        var source = cancellation;
        var task = pending;
        cancellation = null;
        pending = null;
        if (source == null) return;
        source.Cancel();
        if (task == null || task.IsCompleted) source.Dispose();
        else _ = task.ContinueWith(_ => source.Dispose(), TaskScheduler.Default);
    }
    public void Dispose() => Cancel();
}
