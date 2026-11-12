using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace keycuts.Common
{
    /// <summary>The newest published release as GitHub reports it: its tag (<c>v1.2.0</c>) and its page.</summary>
    public sealed class LatestRelease
    {
        public LatestRelease(string tag, string url)
        {
            Tag = tag ?? throw new ArgumentNullException(nameof(tag));
            Url = url ?? throw new ArgumentNullException(nameof(url));
        }

        public string Tag { get; }

        /// <summary>The release page. Always a page of this repository (see <see cref="GitHubReleaseSource.IsReleasePage"/>).</summary>
        public string Url { get; }
    }

    /// <summary>Where the update check asks for the latest release (#171). Tests use a fake so they never touch the network.</summary>
    public interface IReleaseSource
    {
        /// <summary>
        /// Throws on any failure: <see cref="HttpRequestException"/> (network, non-success status), <see cref="OperationCanceledException"/>
        /// (timeout), <see cref="JsonException"/> or <see cref="InvalidDataException"/> (a response it can't read).
        /// </summary>
        Task<LatestRelease> GetLatestAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Reads the latest release from the GitHub API. Unauthenticated (60 requests an hour per IP), and the request carries nothing
    /// beyond any API call: the URL, a <c>User-Agent</c> of the app name and version (GitHub rejects requests without one), and <c>Accept</c>.
    /// <c>/releases/latest</c> skips drafts and prereleases, so a prerelease never shows as an update.
    /// </summary>
    public class GitHubReleaseSource : IReleaseSource
    {
        public const string LatestReleaseApiUrl = "https://api.github.com/repos/andrewralon/keycuts/releases/latest";

        /// <summary>Opened instead of the API's <c>html_url</c> when that isn't a page of this repository.</summary>
        public const string ReleasesPageUrl = "https://github.com/andrewralon/keycuts/releases/latest";

        public const string AcceptHeader = "application/vnd.github+json";

        private const string RepositoryPathPrefix = "/andrewralon/keycuts/";

        // A release response is a few kilobytes; anything far bigger isn't one
        private const long MaxResponseBytes = 1024 * 1024;

        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        private static readonly Lazy<HttpClient> SharedClient = new Lazy<HttpClient>(() => CreateClient(new HttpClientHandler
        {
            // Behind a proxy that wants Windows sign-in, use the user's own
            DefaultProxyCredentials = CredentialCache.DefaultCredentials
        }));

        // Null for the shared client, which is created on the first request rather than at startup
        private readonly HttpClient ownClient;

        private readonly string userAgentVersion;

        private readonly TimeSpan timeout;

        /// <param name="handler">Tests pass a fake handler. Null uses one shared client with the system proxy.</param>
        /// <param name="userAgentVersion">The version in the <c>User-Agent</c>; null uses <see cref="Runner.Version"/> in three parts.</param>
        public GitHubReleaseSource(HttpMessageHandler handler = null, string userAgentVersion = null, TimeSpan? timeout = null)
        {
            ownClient = handler == null ? null : CreateClient(handler);
            this.userAgentVersion = userAgentVersion ?? UpdateChecker.Normalize(Version.Parse(Runner.Version)).ToString(3);
            this.timeout = timeout ?? DefaultTimeout;
        }

        public string UserAgent
        {
            get { return Runner.AppName + "/" + userAgentVersion; }
        }

        public async Task<LatestRelease> GetLatestAsync(CancellationToken cancellationToken)
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(Runner.AppName, userAgentVersion));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(AcceptHeader));

            var client = ownClient ?? SharedClient.Value;
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutSource.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"GitHub answered {(int)response.StatusCode}.", null, response.StatusCode);
            }

            // Decode the bytes as UTF-8 (JSON's encoding) ourselves: ReadAsStringAsync throws InvalidOperationException
            // for a Content-Type with a bogus charset, which isn't a failure the checker would expect
            var bytes = await response.Content.ReadAsByteArrayAsync(timeoutSource.Token).ConfigureAwait(false);
            return Parse(Encoding.UTF8.GetString(bytes));
        }

        /// <summary>Reads <c>tag_name</c> and <c>html_url</c>. Throws <see cref="JsonException"/> or <see cref="InvalidDataException"/>.</summary>
        public static LatestRelease Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("tag_name", out var tagElement) || tagElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(tagElement.GetString()))
            {
                throw new InvalidDataException("The release has no tag_name.");
            }

            var url = root.TryGetProperty("html_url", out var urlElement) && urlElement.ValueKind == JsonValueKind.String
                ? urlElement.GetString()
                : null;

            return new LatestRelease(tagElement.GetString().Trim(), IsReleasePage(url) ? url : ReleasesPageUrl);
        }

        /// <summary>
        /// Whether the URL is an https page of this repository on github.com. Download opens it in the browser, so anything
        /// else (another host, another repository, a file: or javascript: URL) is replaced by <see cref="ReleasesPageUrl"/>.
        /// </summary>
        public static bool IsReleasePage(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps &&
                string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
                uri.IsDefaultPort &&
                string.IsNullOrEmpty(uri.UserInfo) &&
                uri.AbsolutePath.StartsWith(RepositoryPathPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static HttpClient CreateClient(HttpMessageHandler handler)
        {
            // The per-request timeout above is the real one; this only bounds a request that somehow outlives it
            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30),
                MaxResponseContentBufferSize = MaxResponseBytes
            };
        }
    }
}
