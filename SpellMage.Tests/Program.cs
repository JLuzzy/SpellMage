using System.Net;
using System.Text;
using SpellMage.Models;
using SpellMage.Services;

var passed = 0;
void Assert(bool condition, string name)
{
    if (!condition) throw new Exception($"FAIL: {name}");
    Console.WriteLine($"PASS: {name}"); passed++;
}
SpellSuggestion Suggest(string original, int offset, int length, string replacement) => new() {
    Offset = offset, Length = length, OriginalText = original.Substring(offset, length), SuggestedText = replacement
};
var corrections = new CorrectionService("/nonexistent/dictionary.txt");
var text = "teh cat is redy";
Assert(corrections.ApplyCorrections(text, [Suggest(text, 0, 3, "the"), Suggest(text, 11, 4, "ready")]) == "the cat is ready", "Multiple replacements preserve offsets");
text = "/party teh https://teh.example www.teh.com P12S prog WHM <t>";
var protectedSuggestions = new List<SpellSuggestion>();
foreach (var word in new[] { "party", "https", "www", "P12S", "prog", "WHM", "<t>" })
    protectedSuggestions.Add(Suggest(text, text.IndexOf(word, StringComparison.Ordinal), word.Length, "WRONG"));
protectedSuggestions.Add(Suggest(text, text.IndexOf("teh", text.IndexOf("https", StringComparison.Ordinal), StringComparison.Ordinal), 3, "the"));
Assert(corrections.ApplyCorrections(text, protectedSuggestions) == text, "Protect commands, URL fragments, numbers, terms and placeholders");
Assert(corrections.ApplyCorrections(text, [Suggest(text, 7, 3, "the")]).StartsWith("/party the "), "Allow message correction after command");
text = "abcdef";
Assert(corrections.Filter(text, [Suggest(text, 0, 3, "x"), Suggest(text, 2, 3, "y")]).Count == 1, "Reject overlapping suggestions");
Assert(!CorrectionService.IsValidRange(text, int.MaxValue, 2) && !CorrectionService.IsValidRange(text, 0, -1), "Reject invalid and overflowing ranges");
Assert(!CorrectionService.IsValidRange("😀teh", 1, 1), "Never split surrogate pairs");
Assert(corrections.ApplyCorrections("😀teh", [Suggest("😀teh", 2, 3, "the")]) == "😀the", "Unicode offsets remain valid");
Assert(corrections.ApplyCorrections("a  b", [Suggest("a  b", 1, 1, "")]) == "a b", "Allow deletion suggestions");
Assert(corrections.ApplyCorrections("ab", [Suggest("ab", 1, 0, " ")]) == "a b", "Allow insertion suggestions");
Assert(corrections.Filter("abc", [new() { Offset = 0, Length = 1, OriginalText = "z", SuggestedText = "q" }]).Count == 0, "Reject suggestions for mismatched source");
var dictionary = Path.GetTempFileName();
try {
    File.WriteAllText(dictionary, "# comment\nEorzea\n");
    Assert(new CorrectionService(dictionary).ApplyCorrections("Eorzea", [Suggest("Eorzea", 0, 6, "wrong")]) == "Eorzea", "Load packaged dictionary terms");
} finally { File.Delete(dictionary); }

var settings = new CheckSettings(true, "en-GB", CheckSettings.PublicEndpoint);
var calls = 0;
using var api = new SpellCheckApiService(new FakeHandler(async (request, token) => {
    calls++;
    var body = await request.Content!.ReadAsStringAsync(token);
    Assert(body.Contains("language=en-GB"), "Send selected language");
    return new(HttpStatusCode.OK) { Content = new StringContent("""{"matches":[{"offset":0,"length":3,"replacements":[{"value":"the"}]},{"offset":-1,"length":2,"replacements":[{"value":"x"}]},{"offset":999,"length":3,"replacements":[{"value":"x"}]}]}""") };
}));
Assert(!(await api.CheckAsync("teh", settings with { Enabled = false })).Success && calls == 0, "Disabled API never sends text");
Assert(!(await api.CheckAsync("teh", settings with { Endpoint = "http://example.com/check" })).Success && calls == 0, "Reject non-local plaintext endpoints");
Assert(!(await api.CheckAsync(new string('é', 10000), settings)).Success && calls == 0, "Enforce UTF-8 request size");
var result = await api.CheckAsync("teh", settings);
Assert(result.Success && result.Suggestions.Count == 1, "Skip malformed API ranges");
Assert(!settings.IsPrivateEndpoint && !(settings with { Endpoint = "https://API.LANGUAGETOOL.ORG:443/v2/check?x=1" }).IsPrivateEndpoint, "Public endpoint cannot enable automated checking");
foreach (var response in new[] { "not json", "null", "{}" }) {
    using var invalid = new SpellCheckApiService(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) })));
    Assert(!(await invalid.CheckAsync("teh", settings)).Success, $"Reject malformed response: {response}");
}
using (var limited = new SpellCheckApiService(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)))))
    Assert((await limited.CheckAsync("teh", settings)).StatusMessage.Contains("Rate limit"), "Explain rate limiting");
using (var timeout = new SpellCheckApiService(new FakeHandler((_, _) => throw new TaskCanceledException())))
    Assert((await timeout.CheckAsync("teh", settings)).StatusMessage.Contains("timed out"), "Distinguish timeout from cancellation");
using (var offline = new SpellCheckApiService(new FakeHandler((_, _) => throw new HttpRequestException())))
    Assert(!(await offline.CheckAsync("teh", settings)).Success, "Handle offline server");

var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var requestCount = 0;
using var delayedApi = new SpellCheckApiService(new FakeHandler(async (_, token) => {
    if (Interlocked.Increment(ref requestCount) == 1) { firstStarted.SetResult(); await releaseFirst.Task; }
    return new(HttpStatusCode.OK) { Content = new StringContent("{\"matches\":[]}") };
}));
using var session = new SpellCheckDebounceService(delayedApi);
session.Start("old", settings);
await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
session.Start("new", settings);
releaseFirst.SetResult();
SpellCheckResult? latest = null;
for (var i = 0; i < 100 && latest == null; i++) { latest = session.Poll(); await Task.Delay(10); }
Assert(latest?.OriginalText == "new", "Superseded requests cannot publish stale results");
session.Start("pending", settings, debounce: true);
session.Cancel();
await Task.Delay(30);
Assert(session.Poll() == null && !session.IsChecking, "Cancel removes a pending result");
var before = requestCount;
session.Start("public draft", settings, debounce: true);
await Task.Delay(20);
Assert(session.Poll()?.Success == false && requestCount == before, "Request layer blocks automated public API calls");
var privateSettings = settings with { Endpoint = "http://localhost:8081/v2/check" };
session.Start("private draft", privateSettings, debounce: true);
await Task.Delay(50);
Assert(requestCount == before && session.Poll() == null, "Debounce does not send immediately");
session.Cancel();
await Task.Delay(950);
Assert(requestCount == before && session.Poll() == null, "Canceled debounce never reaches the server");
session.Start("stable private draft", privateSettings, debounce: true);
SpellCheckResult? stable = null;
for (var i = 0; i < 200 && stable == null; i++) { stable = session.Poll(); await Task.Delay(10); }
Assert(stable?.Success == true && requestCount == before + 1, "Stable private draft checks exactly once after debounce");
using (var nullMatches = new SpellCheckApiService(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
    Content = new StringContent("{\"matches\":[null,{\"offset\":0,\"length\":3,\"replacements\":[null]}]}")
}))))
    Assert((await nullMatches.CheckAsync("teh", settings)).Suggestions.Count == 0, "Skip null matches and replacements");
Console.WriteLine($"All {passed} checks passed.");

sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
}
