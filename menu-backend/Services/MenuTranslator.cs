using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using menu_backend.DTOs.Menu;

namespace menu_backend.Services;

/// <summary>Translations from one OpenAI call, with the tokens it used (for cost logging).</summary>
public record MenuTranslationBatch(
    Dictionary<string, Dictionary<string, MenuText>> Texts, string Model, int InputTokens, int OutputTokens);

/// <summary>
/// AI translation of menu names/descriptions (OpenAI). Only needs OpenAI:ApiKey, so the menu's
/// auto-translate works even where the marketing features' other services aren't configured.
/// </summary>
public class MenuTranslator
{
    private static readonly Dictionary<string, string> LanguageNames = new()
    {
        ["hi"] = "Hindi (Devanagari script)",
        ["gu"] = "Gujarati (Gujarati script)"
    };

    /// <summary>Cheap model is plenty for dish names; override with OpenAI:TranslationModel.</summary>
    private const string DefaultModel = "gpt-4o-mini";

    /// <summary>USD per million tokens (input, output), for the cost estimate in logs. Update if OpenAI changes prices.</summary>
    private static readonly Dictionary<string, (decimal Input, decimal Output)> PricePerMillion = new()
    {
        ["gpt-4o-mini"] = (0.15m, 0.60m),
        ["gpt-4o"] = (2.50m, 10.00m)
    };

    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly ILogger<MenuTranslator> _logger;

    public MenuTranslator(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<MenuTranslator> logger)
    {
        _config = config;
        _logger = logger;
        _http = httpClientFactory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(90);
    }

    /// <summary>Translates texts (keyed by any id) into each language. Result: id → language → text.</summary>
    public async Task<MenuTranslationBatch> TranslateAsync(Dictionary<string, MenuText> texts, string[] languages)
    {
        var apiKey = _config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI API key is not configured (OpenAI:ApiKey).");

        var targets = string.Join(", ", languages.Select(l => $"\"{l}\" = {LanguageNames.GetValueOrDefault(l, l)}"));
        var prompt =
            $"Translate these Indian restaurant menu entries into: {targets}.\n" +
            "Keep dish names recognisable: write well-known dish names (Paneer Tikka, Biryani, Masala Dosa) in the target script " +
            "as people say them, don't translate them literally. Translate descriptions naturally and keep them short. " +
            "Leave out a description that is missing.\n" +
            "Reply with a JSON object shaped {\"<id>\": {\"<lang>\": {\"name\": \"...\", \"description\": \"...\"}}}.\n\n" +
            JsonSerializer.Serialize(texts, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var model = _config["OpenAI:TranslationModel"] ?? DefaultModel;
        var body = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = "You translate restaurant menus for Indian customers. Respond with valid JSON only." },
                new { role = "user", content = prompt }
            },
            temperature = 0.2,
            max_tokens = 4000,
            response_format = new { type = "json_object" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI returned {(int)response.StatusCode}: {OpenAiError(json)}");

        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
            ?? throw new InvalidOperationException("OpenAI returned an empty reply.");

        var usage = doc.RootElement.TryGetProperty("usage", out var u) ? u : default;
        var inputTokens = usage.ValueKind == JsonValueKind.Object ? usage.GetProperty("prompt_tokens").GetInt32() : 0;
        var outputTokens = usage.ValueKind == JsonValueKind.Object ? usage.GetProperty("completion_tokens").GetInt32() : 0;
        _logger.LogInformation("[MenuTranslate] {Model}: {Count} entries, {In} input + {Out} output tokens, ~${Cost:0.0000}",
            model, texts.Count, inputTokens, outputTokens, EstimateUsd(model, inputTokens, outputTokens));

        var translated = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, MenuText>>>(content,
                             new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                         ?? new();
        return new MenuTranslationBatch(translated, model, inputTokens, outputTokens);
    }

    /// <summary>Estimated USD cost; null for a model without a known price.</summary>
    public static decimal? EstimateUsd(string model, int inputTokens, int outputTokens)
    {
        var price = PricePerMillion.FirstOrDefault(p => model.StartsWith(p.Key, StringComparison.OrdinalIgnoreCase)
                                                         && (p.Key != "gpt-4o" || !model.Contains("mini")));
        if (price.Key == null) return null;
        return (inputTokens * price.Value.Input + outputTokens * price.Value.Output) / 1_000_000m;
    }

    private static string OpenAiError(string json)
    {
        try { return JsonDocument.Parse(json).RootElement.GetProperty("error").GetProperty("message").GetString() ?? json; }
        catch { return json.Length > 200 ? json[..200] : json; }
    }
}
