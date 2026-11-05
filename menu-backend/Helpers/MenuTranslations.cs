using System.Text.Json;
using menu_backend.DTOs.Menu;

namespace menu_backend.Helpers;

/// <summary>
/// Translated dish/category names, stored as JSON on the row. English (the base text) lives in the
/// normal Name/Description columns; only the other app languages are stored here.
/// </summary>
public static class MenuTranslations
{
    public static readonly string[] Languages = { "hi", "gu" };

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static Dictionary<string, MenuText> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, MenuText>>(json, Json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    /// <summary>Keeps supported languages with some text; null when nothing is left.</summary>
    public static string? Serialize(Dictionary<string, MenuText>? translations)
    {
        if (translations == null) return null;
        var clean = translations
            .Where(kv => Languages.Contains(kv.Key))
            .Select(kv => (kv.Key, Text: new MenuText { Name = Trim(kv.Value.Name), Description = Trim(kv.Value.Description) }))
            .Where(x => x.Text.Name != null || x.Text.Description != null)
            .ToDictionary(x => x.Key, x => x.Text);
        return clean.Count == 0 ? null : JsonSerializer.Serialize(clean, Json);
    }

    /// <summary>Puts the <paramref name="lang"/> text in Name/Description where it exists (English stays otherwise).</summary>
    public static void Localize(FullMenuResponse menu, string? lang)
    {
        if (string.IsNullOrEmpty(lang) || !Languages.Contains(lang)) return;
        foreach (var c in menu.Categories)
        {
            Apply(c.Translations, lang, n => c.Name = n, d => c.Description = d);
            foreach (var i in c.Items)
            {
                i.BaseName ??= i.Name;
                Apply(i.Translations, lang, n => i.Name = n, d => i.Description = d);
            }
        }
    }

    private static void Apply(Dictionary<string, MenuText>? translations, string lang, Action<string> setName, Action<string> setDescription)
    {
        if (translations == null || !translations.TryGetValue(lang, out var t)) return;
        if (!string.IsNullOrWhiteSpace(t.Name)) setName(t.Name);
        if (!string.IsNullOrWhiteSpace(t.Description)) setDescription(t.Description);
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
