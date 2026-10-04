namespace FinanceApp.UnitTests;

using System.Net;
using System.Text;
using FinanceApp.Application.Interfaces;
using FinanceApp.Infrastructure.Configuration;
using FinanceApp.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public class GitHubAppUpdateServiceTests
{
    [Fact]
    public async Task ReturnsUpdate_WhenPublishedVersionIsNewer()
    {
        var service = CreateService(HttpStatusCode.OK, Release("v1.1.0", "Finora-v1.1.0.apk"));

        var update = await service.GetUpdateAsync("1.0.0");

        Assert.NotNull(update);
        Assert.Equal("v1.1.0", update!.TagName);
        Assert.Equal("1.1.0", update.Version);
        Assert.Equal("https://example.test/Finora-v1.1.0.apk", update.DownloadUrl);
    }

    [Fact]
    public async Task ReturnsNull_WhenInstalledIsCurrent()
    {
        var service = CreateService(HttpStatusCode.OK, Release("v1.0.0", "Finora-v1.0.0.apk"));

        Assert.Null(await service.GetUpdateAsync("1.0.0"));
    }

    [Fact]
    public async Task ReturnsNull_WhenInstalledIsNewerThanPublished()
    {
        // A user on a newer build than the newest release - for example a tester -
        // must never be pushed backwards.
        var service = CreateService(HttpStatusCode.OK, Release("v1.0.0", "Finora-v1.0.0.apk"));

        Assert.Null(await service.GetUpdateAsync("1.2.0"));
    }

    [Fact]
    public async Task ReturnsNull_WhenNoReleasePublishedYet()
    {
        // GitHub answers 404 until the first release is published, which is the
        // state this repository starts in.
        var service = CreateService(HttpStatusCode.NotFound, "{}");

        Assert.Null(await service.GetUpdateAsync("1.0.0"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]   // unauthenticated rate limit
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ReturnsNull_OnFailureStatus(HttpStatusCode status)
    {
        var service = CreateService(status, "{}");

        Assert.Null(await service.GetUpdateAsync("1.0.0"));
    }

    [Fact]
    public async Task ReturnsNull_WhenNetworkThrows()
    {
        var service = CreateService(HttpStatusCode.OK, body: "", throwOnSend: true);

        Assert.Null(await service.GetUpdateAsync("1.0.0"));
    }

    [Fact]
    public async Task ReturnsNull_WhenDisabled()
    {
        var service = CreateService(HttpStatusCode.OK, Release("v9.9.9", "Finora-v9.9.9.apk"), enabled: false);

        Assert.Null(await service.GetUpdateAsync("1.0.0"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-version")]
    [InlineData("v")]
    public async Task ReturnsNull_WhenInstalledVersionIsUnparseable(string installed)
    {
        var service = CreateService(HttpStatusCode.OK, Release("v1.1.0", "Finora-v1.1.0.apk"));

        Assert.Null(await service.GetUpdateAsync(installed));
    }

    [Fact]
    public async Task ReturnsNull_WhenTagIsNotSemver()
    {
        // A release tagged "nightly" must never be pushed at a user as an update.
        var service = CreateService(HttpStatusCode.OK, Release("nightly", "Finora-nightly.apk"));

        Assert.Null(await service.GetUpdateAsync("1.0.0"));
    }

    [Fact]
    public async Task FallsBackToReleasePage_WhenNoApkAssetAttached()
    {
        const string json = """
        {
          "tag_name": "v1.1.0",
          "html_url": "https://example.test/release/v1.1.0",
          "assets": []
        }
        """;

        var service = CreateService(HttpStatusCode.OK, json);

        var update = await service.GetUpdateAsync("1.0.0");

        Assert.NotNull(update);
        Assert.Equal("https://example.test/release/v1.1.0", update!.DownloadUrl);
    }

    [Fact]
    public async Task ReturnsNull_WhenPayloadIsNotJson()
    {
        var service = CreateService(HttpStatusCode.OK, "<html>rate limited</html>");

        Assert.Null(await service.GetUpdateAsync("1.0.0"));
    }

    private static string Release(string tag, string assetName) => $$"""
    {
      "tag_name": "{{tag}}",
      "html_url": "https://example.test/release/{{tag}}",
      "assets": [
        {
          "name": "checksums.txt",
          "browser_download_url": "https://example.test/checksums.txt"
        },
        {
          "name": "{{assetName}}",
          "browser_download_url": "https://example.test/{{assetName}}"
        }
      ]
    }
    """;

    private static GitHubAppUpdateService CreateService(
        HttpStatusCode status,
        string body,
        bool enabled = true,
        bool throwOnSend = false) =>
        new(
            new StubHttpClientFactory(new StubHandler(status, body, throwOnSend)),
            Options.Create(new AppUpdateOptions
            {
                Enabled = enabled,
                Owner = "CyKean",
                Repository = "FinanceApp"
            }),
            NullLogger<GitHubAppUpdateService>.Instance);

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly bool _throwOnSend;

        public StubHandler(HttpStatusCode status, string body, bool throwOnSend)
        {
            _status = status;
            _body = body;
            _throwOnSend = throwOnSend;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_throwOnSend)
                throw new HttpRequestException("Simulated network failure");

            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };

            // Mirror the real handler so cancellation still works in tests.
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response);
        }
    }
}