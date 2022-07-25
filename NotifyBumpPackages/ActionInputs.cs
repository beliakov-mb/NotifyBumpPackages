using CommandLine;

namespace NotifyBumpPackages;

public class ActionInputs
{
    [Option('k', "token",
        Required = true,
        HelpText = "Github token")]
    public string Token { get; set; } = null!;

    [Option('t', "team",
        Required = true,
        HelpText = "Team to route warning message")]
    public string Team { get; set; } = null!;

    [Option('a', "authors",
        Required = true,
        HelpText = "Pull request authors")]
    public string Authors { get; set; } = null!;

    [Option('d', "timeout",
        Required = true,
        HelpText = "Timeout")]
    public int Timeout { get; set; }

    [Option('r', "retries",
        Required = true,
        HelpText = "Retries count")]
    public int Retries { get; set; }

    [Option('s', "repositories",
        Required = true,
        HelpText = "Repositories to scan")]
    public string Repositories { get; set; } = null!;
}