using System.Text.Json.Serialization;

namespace NotifyBumpPackages;

public record ForgottenPullRequestDto
{
    [JsonPropertyName("prTitle")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("prUrl")]
    public string Url { get; set; } = null!;

    [JsonPropertyName("repositoryName")]
    public string RepositoryName { get; set; } = null!;

    [JsonPropertyName("timeout")]
    public int Timeout { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; } = null!;

    [JsonPropertyName("team")]
    public string Team { get; set; } = null!;
}