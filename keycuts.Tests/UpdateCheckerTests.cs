#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The update check (#171). The release source and the state are fakes: nothing touches the network or HKCU.</summary>
    public class UpdateCheckerTests
    {
        #region Versions

        [Theory]
        [InlineData("v1.2.0", "1.2.0")]
        [InlineData("1.2.0", "1.2.0")]
        [InlineData("V1.2.0", "1.2.0")]
        [InlineData(" v1.2.0 ", "1.2.0")]
        [InlineData("v1.2", "1.2.0")]
        [InlineData("v1.2.0.7", "1.2.0")]
        public void TryParseTag_ReadsThreeParts(string tag, string expected)
        {
            Assert.True(UpdateChecker.TryParseTag(tag, out var version));
            Assert.Equal(Version.Parse(expected), version);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("v")]
        [InlineData("1")]
        [InlineData("v1.2.0-beta")]
        [InlineData("release-1.2.0")]
        public void TryParseTag_RefusesWhatIsNotAVersion(string? tag)
        {
            Assert.False(UpdateChecker.TryParseTag(tag!, out var version));
            Assert.Null(version);
        }

        // In .NET, 1.2.0 < 1.2.0.0 (a missing part is -1), so comparing raw versions would get these wrong
        [Theory]
        [InlineData("1.2.0", "1.2.0.0", false)]
        [InlineData("1.2.0.0", "1.2.0", false)]
        [InlineData("1.2.0.9", "1.2.0", false)]
        [InlineData("1.2.1", "1.2.0.0", true)]
        [InlineData("1.2.0", "1.1.9.0", true)]
        [InlineData("1.10.0", "1.9.0", true)]
        [InlineData("1.1.0", "1.2.0", false)]
        public void IsNewer_ComparesTheFirstThreeParts(string candidate, string current, bool expected)
        {
            Assert.Equal(expected, UpdateChecker.IsNewer(Version.Parse(candidate), Version.Parse(current)));
        }

        [Fact]
        public void CurrentVersion_DropsTheFourthPart()
        {
            var checker = UpdateFakes.Checker(new FakeReleaseSource(), new FakeUpdateState(), "1.1.1.0");
            Assert.Equal(new Version(1, 1, 1), checker.CurrentVersion);
        }

        // The registry holds this text; a test can't touch HKCU, so the format and parse are checked here
        [Fact]
        public void UpdateCheckTime_RoundTripsAsUtc_OnAnyCulture()
        {
            var previous = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
                var local = new DateTimeOffset(2026, 11, 12, 4, 30, 15, 123, TimeSpan.FromHours(-5));

                var text = RegistryStuff.FormatUpdateCheckTime(local);

                Assert.Equal("2026-11-12T09:30:15.1230000+00:00", text);
                Assert.Equal(local, RegistryStuff.ParseUpdateCheckTime(text));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = previous;
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("yesterday")]
        [InlineData("11/12/2026")]
        public void UpdateCheckTime_AnUnreadableValue_IsNeverChecked(string? value)
        {
            Assert.Null(RegistryStuff.ParseUpdateCheckTime(value!));
        }

        // LatestReleaseSeen is one REG_MULTI_SZ value (#191), so a write can't pair one release's version with another's URL.
        // A test can't touch HKCU, so the stored form and its reading are checked here
        const string SeenUrl = "https://github.com/andrewralon/keycuts/releases/tag/v1.2.0";

        [Fact]
        public void LatestReleaseSeen_IsTheVersionThenTheUrl_AndRoundTrips()
        {
            var lines = RegistryStuff.FormatLatestReleaseSeen("1.2.0", SeenUrl);

            Assert.Equal(new[] { "1.2.0", SeenUrl }, lines);
            Assert.Equal(("1.2.0", SeenUrl), RegistryStuff.ParseLatestReleaseSeen(lines));
        }

        public static TheoryData<object?> UnreadableLatestReleaseSeen => new TheoryData<object?>
        {
            null,
            // Another kind of value, such as one typed into regedit as REG_SZ
            "1.2.0",
            "1.2.0\n" + SeenUrl,
            42,
            new byte[] { 1, 2 },
            new object[] { "1.2.0", SeenUrl },
            // Not exactly two entries
            new string[0],
            new[] { "1.2.0" },
            new[] { "1.2.0", SeenUrl, "extra" },
            // An empty entry
            new[] { "", SeenUrl },
            new[] { "1.2.0", " " }
        };

        [Theory]
        [MemberData(nameof(UnreadableLatestReleaseSeen))]
        public void LatestReleaseSeen_AnythingButTwoEntries_IsNeverChecked(object? value)
        {
            Assert.Null(RegistryStuff.ParseLatestReleaseSeen(value!));
        }

        // An empty entry would end the multi-string early and lose the URL
        [Theory]
        [InlineData(null, SeenUrl)]
        [InlineData("", SeenUrl)]
        [InlineData("1.2.0", null)]
        [InlineData("1.2.0", " ")]
        public void LatestReleaseSeen_RefusesToStoreAnEmptyEntry(string? version, string? url)
        {
            Assert.Throws<ArgumentException>(() => RegistryStuff.FormatLatestReleaseSeen(version!, url!));
        }

        // The first write of a new value deletes the legacy names, so none may be a name that is still in use
        [Fact]
        public void TheLegacyValueNames_AreNotInUse_AndTheCurrentOnesAreAsDocumented()
        {
            var inUse = new[]
            {
                RegistryStuff.LatestReleaseSeenKeyName, RegistryStuff.DismissedReleaseKeyName,
                RegistryStuff.LastUpdateCheckKeyName, RegistryStuff.CheckForUpdatesKeyName
            };

            // Registry value names ignore case
            Assert.Empty(RegistryStuff.LegacyUpdateKeyNames.Intersect(inUse, StringComparer.OrdinalIgnoreCase));
            Assert.Equal(new[] { "LatestVersion", "LatestReleaseUrl", "DismissedUpdateVersion" }, RegistryStuff.LegacyUpdateKeyNames);

            // These are what regedit shows, so renaming one is a change to stored data
            Assert.Equal("LatestReleaseSeen", RegistryStuff.LatestReleaseSeenKeyName);
            Assert.Equal("DismissedRelease", RegistryStuff.DismissedReleaseKeyName);
            Assert.Equal("LastUpdateCheck", RegistryStuff.LastUpdateCheckKeyName);
        }

        #endregion

        #region Check now

        [Fact]
        public async Task CheckNow_NewerRelease_IsAvailable_AndIsStored()
        {
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release("v1.2.0") };
            var state = new FakeUpdateState();

            var result = await UpdateFakes.Checker(source, state).CheckNowAsync();

            Assert.Equal(UpdateStatus.Available, result.Status);
            Assert.Equal(new Version(1, 2, 0), result.Latest);
            Assert.Equal("https://github.com/andrewralon/keycuts/releases/tag/v1.2.0", result.ReleaseUrl);
            Assert.Equal(("1.2.0", result.ReleaseUrl), state.LatestReleaseSeen);
            Assert.Equal(UpdateFakes.Now, state.LastCheck);
        }

        [Theory]
        [InlineData("v1.1.1")]
        [InlineData("v1.1.0")]
        [InlineData("v1.1.1.5")]
        public async Task CheckNow_SameOrOlderRelease_IsUpToDate(string tag)
        {
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release(tag) };

            var result = await UpdateFakes.Checker(source, new FakeUpdateState(), "1.1.1.0").CheckNowAsync();

            Assert.Equal(UpdateStatus.UpToDate, result.Status);
        }

        public static TheoryData<Exception> Failures => new TheoryData<Exception>
        {
            new HttpRequestException("offline"),
            new HttpRequestException("rate limited", null, HttpStatusCode.Forbidden),
            new TaskCanceledException("timeout"),
            new JsonException("bad json"),
            new InvalidDataException("no tag"),
            new IOException("connection reset")
        };

        [Theory]
        [MemberData(nameof(Failures))]
        public async Task CheckNow_AFailure_IsReported_AndStoresNothing(Exception failure)
        {
            var source = new FakeReleaseSource { Next = () => throw failure };
            var state = new FakeUpdateState();

            var result = await UpdateFakes.Checker(source, state).CheckNowAsync();

            Assert.Equal(UpdateStatus.Failed, result.Status);
            Assert.Null(result.Latest);
            Assert.Equal(0, state.Writes);
            Assert.Null(state.LastCheck);
        }

        [Fact]
        public async Task CheckNow_AnUnparseableTag_IsAFailure_AndStoresNothing()
        {
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release("v2.0.0-beta") };
            var state = new FakeUpdateState();

            var result = await UpdateFakes.Checker(source, state).CheckNowAsync();

            Assert.Equal(UpdateStatus.Failed, result.Status);
            Assert.Equal(0, state.Writes);
        }

        [Theory]
        [InlineData("https://evil.example/andrewralon/keycuts/releases")]
        [InlineData("http://github.com/andrewralon/keycuts/releases/tag/v1.2.0")]
        [InlineData("https://github.com/someone/else/releases/tag/v1.2.0")]
        [InlineData("file:///C:/Windows/System32/calc.exe")]
        [InlineData("not a url")]
        public async Task CheckNow_AReleaseUrlOutsideThisRepository_IsReplacedWithTheReleasesPage(string url)
        {
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release("v1.2.0", url) };
            var state = new FakeUpdateState();

            var result = await UpdateFakes.Checker(source, state).CheckNowAsync();

            Assert.Equal(GitHubReleaseSource.ReleasesPageUrl, result.ReleaseUrl);
            Assert.Equal(("1.2.0", GitHubReleaseSource.ReleasesPageUrl), state.LatestReleaseSeen);
        }

        [Fact]
        public async Task CheckNow_WhenTheStateCantBeWritten_StillReportsTheResult()
        {
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release("v1.2.0") };
            var state = new FakeUpdateState { WriteFailure = new UnauthorizedAccessException() };

            var result = await UpdateFakes.Checker(source, state).CheckNowAsync();

            Assert.Equal(UpdateStatus.Available, result.Status);
        }

        // The startup check and Settings' Check now share the checker: an older answer must not overwrite a newer one
        [Fact]
        public async Task CheckNow_OverlappingChecks_RunOneAtATime_AndTheLaterOneIsStored()
        {
            var firstAnswer = new TaskCompletionSource<LatestRelease>(TaskCreationOptions.RunContinuationsAsynchronously);
            var answers = 0;
            var source = new FakeReleaseSource
            {
                NextAsync = () => ++answers == 1 ? firstAnswer.Task : Task.FromResult(FakeReleaseSource.Release("v1.3.0"))
            };
            var state = new FakeUpdateState();
            var checker = UpdateFakes.Checker(source, state);

            var first = checker.CheckNowAsync();
            var second = checker.CheckNowAsync();
            // The second check waits for the first; without the gate it would ask (and store) straight away
            await Task.Delay(50);
            Assert.Equal(1, source.Calls);

            firstAnswer.SetResult(FakeReleaseSource.Release("v1.2.0"));
            var results = await Task.WhenAll(first, second);

            Assert.Equal(1, source.MaxConcurrent);
            Assert.Equal(new Version(1, 2, 0), results[0].Latest);
            Assert.Equal(new Version(1, 3, 0), results[1].Latest);
            // The pair is one value, so the later check's version can only come with the later check's URL
            Assert.Equal(("1.3.0", "https://github.com/andrewralon/keycuts/releases/tag/v1.3.0"), state.LatestReleaseSeen);
        }

        [Fact]
        public async Task CheckNow_CancelledWhileWaitingForAnotherCheck_ReportsAFailure()
        {
            var firstAnswer = new TaskCompletionSource<LatestRelease>(TaskCreationOptions.RunContinuationsAsynchronously);
            var source = new FakeReleaseSource { NextAsync = () => firstAnswer.Task };
            var checker = UpdateFakes.Checker(source, new FakeUpdateState());
            var first = checker.CheckNowAsync();
            using var cancel = new CancellationTokenSource();

            var waiting = checker.CheckNowAsync(cancel.Token);
            cancel.Cancel();

            Assert.Equal(UpdateStatus.Failed, (await waiting).Status);
            firstAnswer.SetResult(FakeReleaseSource.Release("v1.2.0"));
            Assert.Equal(UpdateStatus.Available, (await first).Status);
            Assert.Equal(1, source.Calls);
        }

        [Fact]
        public async Task CheckNow_IgnoresTheToggleAndTheOnceADayLimit()
        {
            var source = new FakeReleaseSource();
            var state = new FakeUpdateState { Enabled = false, LastCheck = UpdateFakes.Now.AddMinutes(-5) };

            await UpdateFakes.Checker(source, state).CheckNowAsync();

            Assert.Equal(1, source.Calls);
        }

        #endregion

        #region Startup

        [Fact]
        public async Task Startup_WhenTurnedOff_MakesNoRequest()
        {
            var source = new FakeReleaseSource();
            var checker = UpdateFakes.Checker(source, new FakeUpdateState { Enabled = false });

            Assert.False(await checker.CheckOnStartupAsync());
            Assert.Equal(0, source.Calls);
        }

        [Fact]
        public async Task Startup_TheFirstTime_Asks()
        {
            var source = new FakeReleaseSource();

            Assert.True(await UpdateFakes.Checker(source, new FakeUpdateState()).CheckOnStartupAsync());
            Assert.Equal(1, source.Calls);
        }

        [Theory]
        [InlineData(-1, false)]
        [InlineData(-23 * 60, false)]
        [InlineData(-24 * 60, true)]
        [InlineData(-3 * 24 * 60, true)]
        // A last check in the future (the clock was set back) doesn't hold the next one off
        [InlineData(60, true)]
        public async Task Startup_AsksAtMostOnceADay(int lastCheckMinutesAgo, bool asks)
        {
            var source = new FakeReleaseSource();
            var state = new FakeUpdateState { LastCheck = UpdateFakes.Now.AddMinutes(lastCheckMinutesAgo) };

            Assert.Equal(asks, await UpdateFakes.Checker(source, state).CheckOnStartupAsync());
            Assert.Equal(asks ? 1 : 0, source.Calls);
        }

        [Fact]
        public async Task Startup_ASecondStart_WithinADayOfASuccessfulCheck_MakesNoRequest()
        {
            var source = new FakeReleaseSource();
            var state = new FakeUpdateState();
            var now = UpdateFakes.Now;
            var checker = UpdateFakes.Checker(source, state, now: () => now);

            await checker.CheckOnStartupAsync();
            now = now.AddHours(6);
            await checker.CheckOnStartupAsync();

            Assert.Equal(1, source.Calls);
        }

        [Fact]
        public async Task Startup_AfterAFailedCheck_AsksAgainNextTime()
        {
            var source = new FakeReleaseSource { Next = () => throw new HttpRequestException() };
            var state = new FakeUpdateState();
            var checker = UpdateFakes.Checker(source, state);

            await checker.CheckOnStartupAsync();
            await checker.CheckOnStartupAsync();

            Assert.Equal(2, source.Calls);
        }

        [Fact]
        public async Task Startup_WhenTheStateCantBeRead_StillChecks_AndNeverThrows()
        {
            var source = new FakeReleaseSource();
            var state = new FakeUpdateState { ReadFailure = new IOException() };
            var checker = UpdateFakes.Checker(source, state);

            Assert.True(await checker.CheckOnStartupAsync());
            Assert.Null(checker.GetNotice());
            Assert.Null(checker.GetLatestKnown());
        }

        #endregion

        #region Notice and dismissal

        static FakeUpdateState Known(string latest, string? dismissed = null)
        {
            var state = new FakeUpdateState
            {
                LatestReleaseSeen = (latest, "https://github.com/andrewralon/keycuts/releases/tag/v" + latest)
            };

            if (dismissed != null)
            {
                state.DismissedRelease = dismissed;
            }

            return state;
        }

        [Fact]
        public void GetNotice_BeforeAnyCheck_IsNull()
        {
            Assert.Null(UpdateFakes.Checker(new FakeReleaseSource(), new FakeUpdateState()).GetNotice());
        }

        [Fact]
        public void GetNotice_ANewerKnownRelease_IsShown()
        {
            var notice = UpdateFakes.Checker(new FakeReleaseSource(), Known("1.2.0")).GetNotice();

            Assert.NotNull(notice);
            Assert.Equal(new Version(1, 2, 0), notice!.Latest);
            Assert.Equal("https://github.com/andrewralon/keycuts/releases/tag/v1.2.0", notice.ReleaseUrl);
        }

        // After an upgrade the stored release is the running one
        [Theory]
        [InlineData("1.1.1")]
        [InlineData("1.0.0")]
        public void GetNotice_TheSameOrAnOlderRelease_IsNull(string latest)
        {
            Assert.Null(UpdateFakes.Checker(new FakeReleaseSource(), Known(latest), "1.1.1.0").GetNotice());
        }

        [Fact]
        public void GetNotice_WhenTurnedOff_IsNull()
        {
            var state = Known("1.2.0");
            state.Enabled = false;

            Assert.Null(UpdateFakes.Checker(new FakeReleaseSource(), state).GetNotice());
        }

        [Fact]
        public void GetNotice_ForTheDismissedRelease_IsNull()
        {
            Assert.Null(UpdateFakes.Checker(new FakeReleaseSource(), Known("1.2.0", dismissed: "1.2.0")).GetNotice());
        }

        [Fact]
        public void GetNotice_ForAReleaseNewerThanTheDismissedOne_IsShownAgain()
        {
            Assert.NotNull(UpdateFakes.Checker(new FakeReleaseSource(), Known("1.3.0", dismissed: "1.2.0")).GetNotice());
        }

        [Fact]
        public void GetNotice_AStoredUrlOutsideThisRepository_IsReplacedWithTheReleasesPage()
        {
            var state = Known("1.2.0");
            state.LatestReleaseSeen = ("1.2.0", "https://evil.example/");

            Assert.Equal(GitHubReleaseSource.ReleasesPageUrl, UpdateFakes.Checker(new FakeReleaseSource(), state).GetNotice()!.ReleaseUrl);
        }

        [Fact]
        public void Dismiss_StoresThreeParts_AndHidesTheNotice()
        {
            var state = Known("1.2.0");
            var checker = UpdateFakes.Checker(new FakeReleaseSource(), state);

            checker.Dismiss(new Version(1, 2, 0, 0));

            Assert.Equal("1.2.0", state.DismissedRelease);
            Assert.Null(checker.GetNotice());
        }

        [Fact]
        public void Dismiss_WhenTheStateCantBeWritten_DoesNotThrow()
        {
            var state = Known("1.2.0");
            state.WriteFailure = new UnauthorizedAccessException();

            UpdateFakes.Checker(new FakeReleaseSource(), state).Dismiss(new Version(1, 2, 0));
        }

        // A stored pair whose version isn't a version behaves like a first run, not a crash
        [Theory]
        [InlineData("")]
        [InlineData("soon")]
        [InlineData("v1.2.0-beta")]
        public void GetLatestKnown_AStoredVersionThatIsNotAVersion_IsNeverChecked(string version)
        {
            var state = new FakeUpdateState { LatestReleaseSeen = (version, "https://github.com/andrewralon/keycuts/releases/tag/v1.2.0") };
            var checker = UpdateFakes.Checker(new FakeReleaseSource(), state);

            Assert.Null(checker.GetLatestKnown());
            Assert.Null(checker.GetNotice());
        }

        [Fact]
        public void GetLatestKnown_ReportsUpToDate_WhenTheStoredReleaseIsThisOne()
        {
            var known = UpdateFakes.Checker(new FakeReleaseSource(), Known("1.1.1")).GetLatestKnown();

            Assert.Equal(UpdateStatus.UpToDate, known!.Status);
        }

        #endregion
    }

    /// <summary>The GitHub request and response handling, over a fake HTTP handler.</summary>
    public class GitHubReleaseSourceTests
    {
        sealed class FakeHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
                (request, ct) => Task.FromResult(Json(HttpStatusCode.OK, ReleaseJson("v1.2.0")));

            public HttpRequestMessage? LastRequest { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Respond(request, cancellationToken);
            }
        }

        static string ReleaseJson(string tag, string url = "https://github.com/andrewralon/keycuts/releases/tag/v1.2.0")
        {
            return "{\"tag_name\":\"" + tag + "\",\"html_url\":\"" + url + "\",\"draft\":false,\"prerelease\":false,\"assets\":[]}";
        }

        static HttpResponseMessage Json(HttpStatusCode status, string body)
        {
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }

        [Fact]
        public async Task GetLatest_SendsAUserAgent_AndTheGitHubAcceptHeader_AndNothingElse()
        {
            var handler = new FakeHandler();

            await new GitHubReleaseSource(handler, "1.1.1").GetLatestAsync(CancellationToken.None);

            var request = handler.LastRequest!;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(GitHubReleaseSource.LatestReleaseApiUrl, request.RequestUri!.ToString());
            Assert.Equal("keycuts/1.1.1", request.Headers.UserAgent.ToString());
            Assert.Equal(GitHubReleaseSource.AcceptHeader, Assert.Single(request.Headers.Accept).MediaType);
            Assert.Null(request.Headers.Authorization);
            Assert.Equal(new[] { "Accept", "User-Agent" }, request.Headers.Select(h => h.Key).OrderBy(k => k, StringComparer.Ordinal));
            Assert.Null(request.Content);
        }

        [Fact]
        public void UserAgent_DefaultsToTheRunningVersionInThreeParts()
        {
            var expected = "keycuts/" + UpdateChecker.Normalize(Version.Parse(Runner.Version)).ToString(3);

            Assert.Equal(expected, new GitHubReleaseSource(new FakeHandler()).UserAgent);
        }

        [Fact]
        public async Task GetLatest_ReadsTheTagAndThePage()
        {
            var release = await new GitHubReleaseSource(new FakeHandler(), "1.1.1").GetLatestAsync(CancellationToken.None);

            Assert.Equal("v1.2.0", release.Tag);
            Assert.Equal("https://github.com/andrewralon/keycuts/releases/tag/v1.2.0", release.Url);
        }

        [Theory]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.TooManyRequests)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task GetLatest_ANonSuccessStatus_Throws(HttpStatusCode status)
        {
            var handler = new FakeHandler { Respond = (request, ct) => Task.FromResult(Json(status, "{\"message\":\"no\"}")) };

            var e = await Assert.ThrowsAsync<HttpRequestException>(() => new GitHubReleaseSource(handler, "1.1.1").GetLatestAsync(CancellationToken.None));
            Assert.Equal(status, e.StatusCode);
        }

        // ReadAsStringAsync throws InvalidOperationException for a charset .NET doesn't know; that must read as a failed check
        [Fact]
        public async Task GetLatest_ABogusCharset_StillReadsTheRelease()
        {
            var handler = new FakeHandler
            {
                Respond = (request, ct) =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(ReleaseJson("v1.2.0"))) };
                    response.Content.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=not-a-charset");
                    return Task.FromResult(response);
                }
            };

            var release = await new GitHubReleaseSource(handler, "1.1.1").GetLatestAsync(CancellationToken.None);

            Assert.Equal("v1.2.0", release.Tag);
        }

        [Fact]
        public async Task GetLatest_ASlowAnswer_TimesOut()
        {
            var handler = new FakeHandler
            {
                Respond = async (request, ct) =>
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    return Json(HttpStatusCode.OK, ReleaseJson("v1.2.0"));
                }
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => new GitHubReleaseSource(handler, "1.1.1", TimeSpan.FromMilliseconds(50)).GetLatestAsync(CancellationToken.None));
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("{\"tag_name\":")]
        public void Parse_MalformedJson_Throws(string json)
        {
            Assert.ThrowsAny<JsonException>(() => GitHubReleaseSource.Parse(json));
        }

        [Fact]
        public void Parse_AUtf8Bom_IsSkipped()
        {
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ReleaseJson("v1.2.0"))).ToArray();

            Assert.Equal("v1.2.0", GitHubReleaseSource.Parse(bytes).Tag);
        }

        // JsonDocument throws InvalidOperationException from GetString for this, which the checker wouldn't catch; it must
        // be an InvalidDataException (a failed check), not an exception that escapes Check now
        [Fact]
        public void Parse_InvalidUtf8InTheTag_IsAnUnreadableResponse()
        {
            var bytes = Encoding.UTF8.GetBytes("{\"tag_name\":\"v1.2.0").Concat(new byte[] { 0xC3, 0x28 }).Concat(Encoding.UTF8.GetBytes("\"}")).ToArray();

            Assert.Throws<InvalidDataException>(() => GitHubReleaseSource.Parse(bytes));
        }

        [Fact]
        public async Task CheckNow_InvalidUtf8FromGitHub_ReportsAFailure()
        {
            var handler = new FakeHandler
            {
                Respond = (request, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("{\"tag_name\":\"v").Concat(new byte[] { 0xFF }).Concat(Encoding.UTF8.GetBytes("\"}")).ToArray())
                })
            };
            var checker = new UpdateChecker(new GitHubReleaseSource(handler, "1.1.1"), new FakeUpdateState(), new Version(1, 1, 1));

            Assert.Equal(UpdateStatus.Failed, (await checker.CheckNowAsync()).Status);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("[]")]
        [InlineData("{\"tag_name\":null}")]
        [InlineData("{\"tag_name\":12}")]
        [InlineData("{\"tag_name\":\"  \"}")]
        public void Parse_NoTag_Throws(string json)
        {
            Assert.Throws<InvalidDataException>(() => GitHubReleaseSource.Parse(json));
        }

        [Theory]
        [InlineData("{\"tag_name\":\"v1.2.0\"}")]
        [InlineData("{\"tag_name\":\"v1.2.0\",\"html_url\":\"https://evil.example/\"}")]
        [InlineData("{\"tag_name\":\"v1.2.0\",\"html_url\":\"javascript:alert(1)\"}")]
        public void Parse_AMissingOrForeignPage_FallsBackToTheReleasesPage(string json)
        {
            Assert.Equal(GitHubReleaseSource.ReleasesPageUrl, GitHubReleaseSource.Parse(json).Url);
        }

        [Theory]
        [InlineData("https://github.com/andrewralon/keycuts/releases/tag/v1.2.0", true)]
        [InlineData("https://GitHub.com/andrewralon/keycuts/releases/latest", true)]
        [InlineData("https://github.com/andrewralon/keycuts-fork/releases", false)]
        [InlineData("https://github.com/andrewralon/keycuts", false)]
        [InlineData("https://github.com:8443/andrewralon/keycuts/releases", false)]
        [InlineData("https://user@github.com/andrewralon/keycuts/releases", false)]
        [InlineData("https://github.com.evil.example/andrewralon/keycuts/releases", false)]
        [InlineData("http://github.com/andrewralon/keycuts/releases", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsReleasePage_AcceptsOnlyThisRepositoryOnGitHub(string? url, bool expected)
        {
            Assert.Equal(expected, GitHubReleaseSource.IsReleasePage(url!));
        }
    }
}
