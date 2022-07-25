using System.Collections;
using System.Net;
using System.Text;
using System.Text.Json;
using Octokit;
using System.Net.Http;
using Polly;

namespace NotifyBumpPackages;

public class BumpPackagesNotifier
{
    private readonly IEnumerable<string> _repositories;
    private readonly IGitHubClient _githubClient;
    private readonly IEnumerable<string> _authors;
    private readonly int _timeout;
    private readonly string _team;
    private readonly int _retries;
    private readonly string _grafanaUri;
    private HttpClient _httpClient;

    public BumpPackagesNotifier(
        IGitHubClient githubClient,
        HttpClient httpClient,
        IEnumerable<string> repositories,
        IEnumerable<string> authors,
        int timeout,
        string team,
        int retries,
        string grafanaUri)
    {
        _repositories = repositories;
        _authors = authors;
        _timeout = timeout;
        _team = team;
        _retries = retries;
        _grafanaUri = grafanaUri;
        _httpClient = httpClient;
        _githubClient = githubClient;
    }

    public async Task ScanAndNotifyAsync()
    {
        var _messageToGrafana = new MessageToGrafanaDto()
        {
            Team = _team,
            PullRequests = new List<ForgottenPullRequestDto>()
        };

        foreach (var _repository in _repositories)
        {
            var _repositoryOwner = _repository.Split('/')[0].Trim();
            var _repositoryName = _repository.Split('/')[1].Trim();

            var _pullRequests =
                await _githubClient.PullRequest.GetAllForRepository(_repositoryOwner, _repositoryName);

            var _filteredPullRequests = _pullRequests
                .Where(pr => _authors.Any(a => a == pr.User.Login))
                .Where(pr => (DateTime.UtcNow - pr.CreatedAt.UtcDateTime).TotalHours > _timeout);

            foreach (var _pr in _filteredPullRequests)
            {
                var _forgottenPullRequest = new ForgottenPullRequestDto()
                {
                    Author = _pr.User.Login,
                    Title = _pr.Title,
                    Url = _pr.HtmlUrl,
                    RepositoryName = _repositoryName,
                    Timeout = (int)(DateTime.UtcNow - _pr.CreatedAt.UtcDateTime).TotalHours
                };

                _messageToGrafana.PullRequests.Add(_forgottenPullRequest);
            }
        }

        if (_messageToGrafana.PullRequests.Count > 0)
        {
            var _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => r.StatusCode != HttpStatusCode.OK)
                .WaitAndRetryAsync(_retries, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            _ = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.PostAsync(
                    _grafanaUri,
                    new StringContent(JsonSerializer.Serialize(_messageToGrafana), Encoding.Default,
                        "application/json")));
        }
    }
}