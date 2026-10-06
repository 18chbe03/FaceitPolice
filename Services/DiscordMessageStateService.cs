using System.Text.Json;

namespace FaceitPolice.Services;

public sealed class DiscordMessageStateService
{
    private readonly string _filePath;

    public DiscordMessageStateService(
        string filePath =
            "history/discord-message-ids.json")
    {
        _filePath = filePath;
    }

    public async Task<DiscordMessageState> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new DiscordMessageState();
        }

        try
        {
            var json =
                await File.ReadAllTextAsync(
                    _filePath,
                    cancellationToken);

            return JsonSerializer
                .Deserialize<DiscordMessageState>(
                    json)
                ?? new DiscordMessageState();
        }
        catch
        {
            return new DiscordMessageState();
        }
    }

    public async Task SaveAsync(
        DiscordMessageState state,
        CancellationToken cancellationToken = default)
    {
        var directory =
            Path.GetDirectoryName(
                _filePath);

        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }

        var json =
            JsonSerializer.Serialize(
                state,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        await File.WriteAllTextAsync(
            _filePath,
            json,
            cancellationToken);
    }
}

public sealed class DiscordMessageState
{
    public string? BoardMessageId { get; set; }

    public string? MapStatsMessageId { get; set; }

    public string? AdvancedStatsMessageId { get; set; }

    public string? TiltWatchMessageId { get; set; }
}