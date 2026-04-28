using System.Text.Json;
using EfPilot.Core.Configuration;

namespace EfPilot.Workspace.Configuration;

public class EfPilotConfigStore
{
    private const string ConfigDirectoryName = ".efpilot";
    private const string ConfigFileName = "config.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public string GetConfigPath(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        return Path.Combine(rootDirectory, ConfigDirectoryName, ConfigFileName);
    }

    public async Task SaveAsync(
        string rootDirectory,
        EfPilotConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(config);

        var configDirectory = Path.Combine(rootDirectory, ConfigDirectoryName);
        Directory.CreateDirectory(configDirectory);

        var configPath = GetConfigPath(rootDirectory);
        var json = JsonSerializer.Serialize(config, JsonOptions);

        await File.WriteAllTextAsync(configPath, json, cancellationToken);
    }

    public async Task<EfPilotConfig?> LoadAsync(
        string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        var configPath = GetConfigPath(rootDirectory);

        if (!File.Exists(configPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(configPath, cancellationToken);

        return JsonSerializer.Deserialize<EfPilotConfig>(json, JsonOptions);
    }
}