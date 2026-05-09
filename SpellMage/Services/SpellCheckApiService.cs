using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SamplePlugin.Models;
using Dalamud.Plugin;

namespace SamplePlugin.Services;

public sealed class SpellCheckApiService : IDisposable
{
    private readonly HttpClient client;

    public SpellCheckApiService()
    {
        client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SpellMage/0.1");
    }

    public async Task<SpellCheckResult> CheckAsync(string text, string language = "en-US", CancellationToken cancellationToken = default)
    {
        var result = new SpellCheckResult
        {
            OriginalText = text ?? string.Empty,
            CorrectedText = string.Empty,
            Suggestions = Array.Empty<SpellSuggestion>(),
            Success = false,
            StatusMessage = string.Empty,
            UsedApi = false
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            result.Success = true;
            result.CorrectedText = text ?? string.Empty;
            return result;
        }

        try
        {
            var values = new List<KeyValuePair<string, string>> {
                new("text", text),
                new("language", language ?? "en-US")
            };

            using var content = new FormUrlEncodedContent(values);
            using var resp = await client.PostAsync("https://api.languagetool.org/v2/check", content, cancellationToken).ConfigureAwait(false);
            result.UsedApi = true;

            if (!resp.IsSuccessStatusCode)
            {
                result.StatusMessage = $"HTTP {resp.StatusCode}";
                Plugin.Log.Warning($"LanguageTool returned {resp.StatusCode}");
                return result;
            }

            var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var lt = await JsonSerializer.DeserializeAsync<LanguageToolResponse>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (lt == null)
            {
                result.StatusMessage = "Empty response";
                return result;
            }

            var suggestions = new List<SpellSuggestion>();
            if (lt.matches != null)
            {
                foreach (var m in lt.matches)
                {
                    var replacement = string.Empty;
                    if (m.replacements != null && m.replacements.Length > 0)
                    {
                        replacement = m.replacements[0].value ?? string.Empty;
                    }

                    var s = new SpellSuggestion
                    {
                        OriginalText = text.Substring(m.offset, Math.Min(m.length, Math.Max(0, text.Length - m.offset))),
                        SuggestedText = replacement,
                        Offset = m.offset,
                        Length = m.length,
                        Message = m.message,
                        RuleId = m.rule?.id,
                        RuleDescription = m.rule?.description,
                        Category = m.rule?.category?.name,
                        IsApplied = false
                    };

                    suggestions.Add(s);
                }
            }

            result.Suggestions = suggestions.AsReadOnly();
            result.CorrectedText = text; // leave to CorrectionService
            result.Success = true;
            result.StatusMessage = "OK";
            return result;
        }
        catch (OperationCanceledException)
        {
            result.StatusMessage = "Canceled";
            Plugin.Log.Warning("Spell check canceled");
            return result;
        }
        catch (Exception ex)
        {
            result.StatusMessage = ex.Message;
            Plugin.Log.Error(ex, "SpellCheckApiService.CheckAsync failed");
            return result;
        }
    }

    public void Dispose()
    {
        client.Dispose();
    }
}
