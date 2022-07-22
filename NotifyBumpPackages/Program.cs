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
var _repositoryEnvironmentVariable = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ??
                                     throw new NullReferenceException("GITHUB_REPOSITORY environment variable");

var _repositoryOwner = _repositoryEnvironmentVariable.Split('/')[0];
var _repositoryName = _repositoryEnvironmentVariable.Split('/')[1];
var _pullRequests = await _githubClient.PullRequest.GetAllForRepository(_repositoryOwner, _repositoryName);
var _authorList = _options.Authors.Split("/");
var _filteredPullRequests = _pullRequests
    .Where(pr => _authorList.Any(a => a == pr.User.Login))
    .Where(pr => (DateTime.UtcNow - pr.CreatedAt.UtcDateTime).TotalHours > _options.Timeout);

var _httpClient = new HttpClient();
var _retryPolicy = Policy
    .Handle<HttpRequestException>()
    .OrResult<HttpResponseMessage>(r => r.StatusCode != HttpStatusCode.OK)
    .WaitAndRetryAsync(_options.Retries, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

foreach (var pr in _filteredPullRequests)
{
    var _forgottenPullRequest = new ForgottenPullRequestDto()
    {
        Author = pr.User.Login,
        Title = pr.Title,
        Url = pr.HtmlUrl,
        RepositoryName = _repositoryName,
        Timeout = (int)(DateTime.UtcNow - pr.CreatedAt.UtcDateTime).TotalHours,
        Team = _options.Team
    };

    _ = _retryPolicy.ExecuteAsync(() =>
        _httpClient.PostAsync(
            grafanaUri,
            new StringContent(JsonSerializer.Serialize(_forgottenPullRequest), Encoding.Default,
                "application/json"))
        );
}