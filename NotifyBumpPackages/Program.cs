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
using var _httpClient = new HttpClient();

var _repositories = _options.Repositories.Split(",");
var _authors = _options.Authors.Split(",").Select(a => a.Trim());

var _notifier = new BumpPackagesNotifier(
    _githubClient,
    _httpClient,
    _repositories,
    _authors,
    _options.Timeout,
    _options.Team,
    _options.Retries,
    grafanaUri);

await _notifier.ScanAndNotifyAsync();