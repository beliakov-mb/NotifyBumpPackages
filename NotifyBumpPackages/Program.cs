using System.Net;
using CommandLine;
using NotifyBumpPackages;
using Octokit;
using System.Text;
using System.Text.Json;
using Polly;

const string grafanaUri =
    @"https://a-prod-us-central-0.grafana.net/integrations/v1/formatted_webhook/4kI3HQb0Tg0G7pt25YXURTvBP/";

var _options = Parser.Default.ParseArguments<ActionInputs>(args).Value;
var _githubClient = new GitHubClient(new ProductHeaderValue("notify-bump-packages"))
{
    Credentials = new Credentials(_options.Token)
};

var _messageToGrafana = new MessageToGrafanaDto()
{
    Team = _options.Team,
    PullRequests = new List<ForgottenPullRequestDto>()
};

foreach (var _repository in _options.Repositories)
{
    var _repositoryOwner = _repository.Split('/')[0];
    var _repositoryName = _repository.Split('/')[1];

    var _pullRequests =
        await _githubClient.PullRequest.GetAllForRepository(_repositoryOwner, _repositoryName);

    var _filteredPullRequests = _pullRequests
        .Where(pr => _options.Authors.Any(a => a == pr.User.Login))
        .Where(pr => (DateTime.UtcNow - pr.CreatedAt.UtcDateTime).TotalHours > _options.Timeout);

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

var _httpClient = new HttpClient();
var _retryPolicy = Policy
    .Handle<HttpRequestException>()
    .OrResult<HttpResponseMessage>(r => r.StatusCode != HttpStatusCode.OK)
    .WaitAndRetryAsync(_options.Retries, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

_ = await _retryPolicy.ExecuteAsync(async () =>
    await _httpClient.PostAsync(
        grafanaUri,
        new StringContent(JsonSerializer.Serialize(_messageToGrafana), Encoding.Default,
            "application/json")));