using System.Text.Json;

namespace menu_backend.Helpers;

/// <summary>
/// Public DNS lookups over HTTPS (Cloudflare, falling back to Google). Used to check the records a
/// restaurant adds for its own domain: answers come straight from the authoritative servers via a
/// public resolver, not from this server's (possibly stale) local cache.
/// </summary>
public class DnsOverHttps
{
    private static readonly string[] Resolvers =
    {
        "https://cloudflare-dns.com/dns-query?name={0}&type={1}",
        "https://dns.google/resolve?name={0}&type={1}"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DnsOverHttps> _logger;

    public DnsOverHttps(IHttpClientFactory httpClientFactory, ILogger<DnsOverHttps> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Records of <paramref name="type"/> (A, AAAA, CNAME, TXT) for <paramref name="name"/>:
    /// TXT values unquoted, host names without the trailing dot. Empty when none exist.
    /// Null when no resolver could be reached.
    /// </summary>
    public async Task<List<string>?> LookupAsync(string name, string type)
    {
        var typeCode = type switch { "A" => 1, "CNAME" => 5, "TXT" => 16, "AAAA" => 28, _ => throw new ArgumentException(type) };

        foreach (var template in Resolvers)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("doh");
                using var request = new HttpRequestMessage(HttpMethod.Get, string.Format(template, Uri.EscapeDataString(name), type));
                request.Headers.Accept.ParseAdd("application/dns-json");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var response = await client.SendAsync(request, cts.Token);
                if (!response.IsSuccessStatusCode) continue;

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cts.Token));
                var status = doc.RootElement.GetProperty("Status").GetInt32();
                if (status == 3) return new List<string>(); // NXDOMAIN: name doesn't exist
                if (status != 0) continue;                  // SERVFAIL etc.: try the next resolver

                var results = new List<string>();
                if (doc.RootElement.TryGetProperty("Answer", out var answers))
                {
                    foreach (var answer in answers.EnumerateArray())
                    {
                        // Answers can include the CNAME chain; keep only the type we asked for
                        if (answer.GetProperty("type").GetInt32() != typeCode) continue;
                        var data = answer.GetProperty("data").GetString() ?? "";
                        results.Add(type == "TXT" ? UnquoteTxt(data) : data.TrimEnd('.').ToLowerInvariant());
                    }
                }
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "DNS-over-HTTPS lookup failed for {Name} {Type}", name, type);
            }
        }
        return null;
    }

    /// <summary>TXT data arrives quoted, long values split into chunks: "\"abc\" \"def\"" → "abcdef".</summary>
    private static string UnquoteTxt(string data)
    {
        var chunks = data.Split('"').Where((_, i) => i % 2 == 1).ToArray();
        return chunks.Length > 0 ? string.Concat(chunks) : data;
    }
}
