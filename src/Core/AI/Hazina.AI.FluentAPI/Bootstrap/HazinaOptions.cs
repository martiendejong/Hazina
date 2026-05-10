namespace Hazina.AI.FluentAPI.Bootstrap;

/// <summary>
/// Configuration options for the Hazina framework bootstrap.
/// Used with <see cref="HazinaBootstrap.AddHazina"/> to configure all core services
/// in a single call.
/// </summary>
public sealed class HazinaOptions
{
    // -------------------------------------------------------------------------
    // OpenAI Provider
    // -------------------------------------------------------------------------

    /// <summary>
    /// OpenAI API key. Set to enable the OpenAI provider.
    /// </summary>
    public string? OpenAiApiKey { get; set; }

    /// <summary>
    /// OpenAI model to use. Defaults to "gpt-4o-mini".
    /// </summary>
    public string OpenAiModel { get; set; } = "gpt-4o-mini";

    // -------------------------------------------------------------------------
    // Anthropic Provider
    // -------------------------------------------------------------------------

    /// <summary>
    /// Anthropic API key. Set to enable the Anthropic (Claude) provider.
    /// </summary>
    public string? AnthropicApiKey { get; set; }

    /// <summary>
    /// Anthropic model to use. Defaults to "claude-3-5-haiku-latest".
    /// </summary>
    public string AnthropicModel { get; set; } = "claude-3-5-haiku-latest";

    // -------------------------------------------------------------------------
    // AgentManager
    // -------------------------------------------------------------------------

    /// <summary>
    /// Path to agents.json configuration file.
    /// Mutually exclusive with <see cref="AgentsJson"/> — file path takes priority.
    /// </summary>
    public string? AgentsJsonPath { get; set; }

    /// <summary>
    /// Inline agents JSON configuration (array of agent objects).
    /// Used when you don't want a config file. Ignored if <see cref="AgentsJsonPath"/> is set.
    /// </summary>
    public string AgentsJson { get; set; } = "[]";

    /// <summary>
    /// Path to stores.json configuration file.
    /// </summary>
    public string? StoresJsonPath { get; set; }

    /// <summary>
    /// Inline stores JSON configuration. Ignored if <see cref="StoresJsonPath"/> is set.
    /// </summary>
    public string StoresJson { get; set; } = "[]";

    /// <summary>
    /// Path to flows.json configuration file.
    /// </summary>
    public string? FlowsJsonPath { get; set; }

    /// <summary>
    /// Inline flows JSON configuration. Ignored if <see cref="FlowsJsonPath"/> is set.
    /// </summary>
    public string FlowsJson { get; set; } = "[]";

    /// <summary>
    /// Path where the agent activity log is written.
    /// Defaults to "hazina-agent.log" in the application base directory.
    /// </summary>
    public string AgentLogPath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "hazina-agent.log");

    // -------------------------------------------------------------------------
    // Provider selection
    // -------------------------------------------------------------------------

    /// <summary>
    /// When multiple providers are configured, this determines which one is used
    /// by default for new requests. Defaults to <see cref="ProviderPreference.FirstAvailable"/>.
    /// </summary>
    public ProviderPreference DefaultProvider { get; set; } = ProviderPreference.FirstAvailable;
}

/// <summary>
/// Controls which configured provider is selected as the default.
/// </summary>
public enum ProviderPreference
{
    /// <summary>
    /// Use the first provider that has a valid API key (OpenAI checked first).
    /// </summary>
    FirstAvailable,

    /// <summary>
    /// Prefer OpenAI when available, fall back to Anthropic.
    /// </summary>
    OpenAiFirst,

    /// <summary>
    /// Prefer Anthropic when available, fall back to OpenAI.
    /// </summary>
    AnthropicFirst,
}
