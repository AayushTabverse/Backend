using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;

namespace menu_backend.Helpers;

/// <summary>
/// Checks whether a restaurant website address actually works: does the host resolve, and does it
/// serve the Tabverse app over the given URL (including a valid HTTPS certificate)?
/// </summary>
public class SiteProbe
{
    public record Result(bool Ok, bool Resolves, string Reason);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SiteProbe> _logger;

    public SiteProbe(IHttpClientFactory httpClientFactory, ILogger<SiteProbe> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<Result> CheckAsync(string url)
    {
        var host = new Uri(url).Host;

        try
        {
            using var dnsCts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            var addresses = await Dns.GetHostAddressesAsync(host, dnsCts.Token);
            if (addresses.Length == 0) return new(false, false, "the address doesn't resolve yet");
        }
        catch (Exception)
        {
            return new(false, false, "the address doesn't resolve yet");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("site-check");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var response = await client.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode)
                return new(false, true, $"it responded with HTTP {(int)response.StatusCode}");

            var body = await response.Content.ReadAsStringAsync(cts.Token);
            return body.Contains("<app-root", StringComparison.OrdinalIgnoreCase)
                ? new(true, true, "")
                : new(false, true, "it's showing a page that isn't your Tabverse website");
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
        {
            return new(false, true, "the secure (https) certificate isn't ready yet");
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException)
        {
            return new(false, true, "the server isn't accepting connections yet");
        }
        catch (OperationCanceledException)
        {
            return new(false, true, "it took too long to respond");
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Site check failed for {Url}", url);
            return new(false, true, "it couldn't be reached");
        }
    }
}
