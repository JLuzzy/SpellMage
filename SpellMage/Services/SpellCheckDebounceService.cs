using System;
using System.Threading;
using System.Threading.Tasks;
using SamplePlugin.Models;

namespace SamplePlugin.Services;

public sealed class SpellCheckDebounceService : IDisposable
{
    private readonly SpellCheckApiService apiService;
    private readonly TimeSpan debounceInterval;
    private string lastCheckedText = string.Empty;
    private CancellationTokenSource? cts;

    public SpellCheckDebounceService(SpellCheckApiService apiService, TimeSpan? debounce = null)
    {
        this.apiService = apiService;
        debounceInterval = debounce ?? TimeSpan.FromMilliseconds(700);
    }

    public async Task<SpellCheckResult?> MaybeCheckAsync(ChatInputSnapshot snapshot, CancellationToken token)
    {
        try
        {
            if (snapshot == null) return null;
            if (!snapshot.IsAvailable || !snapshot.IsFocused) return null;
            var text = snapshot.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text) || text.Length < 3) return null;
            if (text == lastCheckedText) return null;

            cts?.Cancel();
            cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var linked = cts.Token;

            await Task.Delay(debounceInterval, linked).ConfigureAwait(false);

            if (linked.IsCancellationRequested) return null;

            var res = await apiService.CheckAsync(text, "en-US", linked).ConfigureAwait(false);
            if (res != null && res.Success)
            {
                lastCheckedText = text;
            }

            return res;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "SpellCheckDebounceService failed");
            return null;
        }
    }

    public void Dispose()
    {
        cts?.Cancel();
        cts?.Dispose();
    }
}
