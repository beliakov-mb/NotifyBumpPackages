using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NotifyBumpPackages;
using Octokit;

namespace NotifyBumpPackagesTest;

public class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    public TestHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request);
    }
}

public class TestUser : User
{
    public TestUser(string login) : base()
    {
        Login = login;
    }
}

public class TestPullRequest : PullRequest
{
    public TestPullRequest(string login, DateTimeOffset createdAt, string prTitle="")
    {
        User = new TestUser(login);
        CreatedAt = createdAt;
        Title = prTitle;
    }
}

[TestClass]
public class BumpPackagesNotifierTests
{
    [TestMethod]
    public async Task ScanAndNotifyAsync_Normal()
    {
        var testAuthor = "testAuthor";

        var _githubClientMock = new Mock<IGitHubClient>();
        _githubClientMock.Setup(c =>
                c.PullRequest.GetAllForRepository(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .ReturnsAsync(new ReadOnlyCollection<PullRequest>(new List<PullRequest>()
            {
                new TestPullRequest(testAuthor, DateTimeOffset.UtcNow - TimeSpan.FromHours(24) ,"t1"),
                new TestPullRequest(testAuthor, DateTimeOffset.UtcNow, "t2"),
                new TestPullRequest(testAuthor+"1", DateTimeOffset.UtcNow - TimeSpan.FromHours(24), "t3")
            }));

        MessageToGrafanaDto? _request = null!;
        var _httpMessageHandler = new TestHttpMessageHandler(req =>
        {
            _request = req.Content?.ReadFromJsonAsync<MessageToGrafanaDto>().Result;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var _httpClient = new HttpClient(_httpMessageHandler);

        var _repositories = new string[] { "testOwner/testRepo" };
        var _authors = new string[] { testAuthor };

        var _notifier = new BumpPackagesNotifier(
            _githubClientMock.Object,
            _httpClient,
            _repositories,
            _authors,
            1,
            "testTeam",
            1,
            "http://localhots");

        await _notifier.ScanAndNotifyAsync();

        Assert.AreEqual("testTeam", _request.Team);
        Assert.AreEqual(testAuthor, _request.PullRequests[0].Author);
        Assert.AreEqual(1, _request.PullRequests.Count);
        Assert.AreEqual("t1", _request.PullRequests[0].Title);
    }

    [TestMethod]
    public async Task ScanAndNotifyAsync_Retries()
    {
        var testAuthor = "testAuthor";

        var _githubClientMock = new Mock<IGitHubClient>();
        _githubClientMock.Setup(c =>
                c.PullRequest.GetAllForRepository(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .ReturnsAsync(new ReadOnlyCollection<PullRequest>(new List<PullRequest>()
            {
                new TestPullRequest(testAuthor, DateTimeOffset.UtcNow - TimeSpan.FromHours(24) ,"t1"),
            }));

        int _counter = 0;
        var _httpMessageHandler = new TestHttpMessageHandler(req =>
        {
            if (_counter++ == 0)
            {
                throw new HttpRequestException();
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var _httpClient = new HttpClient(_httpMessageHandler);

        var _repositories = new string[] { "testOwner/testRepo" };
        var _authors = new string[] { testAuthor };

        var _notifier = new BumpPackagesNotifier(
            _githubClientMock.Object,
            _httpClient,
            _repositories,
            _authors,
            1,
            "testTeam",
            2,
            "http://localhots");

        await _notifier.ScanAndNotifyAsync();
        Assert.AreEqual(2, _counter);
    }

    [TestMethod]
    public async Task ScanAndNotifyAsync_Retries_Exceeded()
    {
        var _testAuthor = "testAuthor";

        var _githubClientMock = new Mock<IGitHubClient>();
        _githubClientMock.Setup(c =>
                c.PullRequest.GetAllForRepository(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .ReturnsAsync(new ReadOnlyCollection<PullRequest>(new List<PullRequest>()
            {
                new TestPullRequest(_testAuthor, DateTimeOffset.UtcNow - TimeSpan.FromHours(24) ,"t1"),
            }));

        int _counter = 0;
        var _httpMessageHandler = new TestHttpMessageHandler(req =>
        {
            _counter++;
            throw new HttpRequestException();
        });

        var _httpClient = new HttpClient(_httpMessageHandler);

        var _repositories = new string[] { "testOwner/testRepo" };
        var _authors = new string[] { _testAuthor };

        var _notifier = new BumpPackagesNotifier(
            _githubClientMock.Object,
            _httpClient,
            _repositories,
            _authors,
            1,
            "testTeam",
            2,
            "http://localhots");

        await Assert.ThrowsExceptionAsync<HttpRequestException>(async () => await _notifier.ScanAndNotifyAsync());
        Assert.AreEqual(3, _counter);
    }
}