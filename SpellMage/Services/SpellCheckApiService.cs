using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SpellMage.Models;

namespace SpellMage.Services;

public sealed record CheckSettings(bool Enabled, string Language, string Endpoint)
{
    public const string PublicEndpoint = "https://api.languagetool.org/v2/check";
    public bool IsPrivateEndpoint => Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
        && !uri.Host.Equals("api.languagetool.org", StringComparison.OrdinalIgnoreCase);
}

public sealed class SpellCheckApiService : IDisposable
{
    private readonly HttpClient client;
    public SpellCheckApiService(HttpMessageHandler? handler = null)
    {
        client = handler == null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SpellMage/0.1");
    }

    public async Task<SpellCheckResult> CheckAsync(string text, CheckSettings settings, CancellationToken token = default)
    {
        var result = new SpellCheckResult { OriginalText = text, CorrectedText = text };
        if (!settings.Enabled) { result.StatusMessage = "API checking is disabled in settings."; return result; }
        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != "https" && !(endpoint.Scheme == "http" && endpoint.IsLoopback))
            || !string.IsNullOrEmpty(endpoint.UserInfo))
        { result.StatusMessage = "Use an HTTPS endpoint (HTTP is allowed for localhost)."; return result; }
        if (string.IsNullOrWhiteSpace(settings.Language))
        { result.StatusMessage = "Choose a language in settings."; return result; }
        if (Encoding.UTF8.GetByteCount(text) > 19000)
        { result.StatusMessage = "Text is too long (maximum 19,000 UTF-8 bytes)."; return result; }
        if (string.IsNullOrWhiteSpace(text)) { result.Success = true; result.StatusMessage = "No text to check."; return result; }
        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["text"] = text, ["language"] = settings.Language });
            result.UsedApi = true;
            using var response = await client.PostAsync(endpoint, content, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                result.StatusMessage = response.StatusCode == HttpStatusCode.TooManyRequests
                    ? "Rate limit reached. Wait before checking again." : $"Spell-check server returned HTTP {(int)response.StatusCode}.";
                return result;
            }
            using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            var parsed = await JsonSerializer.DeserializeAsync<LanguageToolResponse>(stream, cancellationToken: token).ConfigureAwait(false);
            if (parsed?.matches == null) { result.StatusMessage = "Invalid spell-check response."; return result; }
            var suggestions = new List<SpellSuggestion>();
            foreach (var match in parsed.matches)
            {
                if (match == null || !CorrectionService.IsValidRange(text, match.offset, match.length)) continue;
                if (match.replacements == null || match.replacements.Length == 0) continue;
                var replacement = match.replacements[0]?.value;
                if (replacement == null) continue;
                suggestions.Add(new SpellSuggestion {
                    OriginalText = text.Substring(match.offset, match.length), SuggestedText = replacement,
                    Offset = match.offset, Length = match.length, Message = match.message ?? string.Empty,
                    RuleId = match.rule?.id, RuleDescription = match.rule?.description, Category = match.rule?.category?.name
                });
            }
            result.Suggestions = suggestions;
            result.Success = true;
            result.StatusMessage = "Check complete. Select the corrections you want to use.";
        }
        catch (OperationCanceledException) { result.StatusMessage = token.IsCancellationRequested ? "Canceled" : "Spell-check request timed out."; }
        catch (HttpRequestException) { result.StatusMessage = "Cannot reach the spell-check server."; }
        catch (JsonException) { result.StatusMessage = "Invalid spell-check response."; }
        return result;
    }
    public void Dispose() => client.Dispose();
}
