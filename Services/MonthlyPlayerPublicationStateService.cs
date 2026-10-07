using System.Text.Json;

namespace FaceitPolice.Services;

public sealed class MonthlyPlayerPublicationStateService
{
    private const string StateFile =
        "history/monthly-player-state.json";

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            WriteIndented = true
        };

    public async Task<MonthlyPlayerPublicationState> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(StateFile))
        {
            return new MonthlyPlayerPublicationState();
        }

        try
        {
            var json =
                await File.ReadAllTextAsync(
                    StateFile,
                    cancellationToken);

            return JsonSerializer.Deserialize<MonthlyPlayerPublicationState>(
                       json,
                       _jsonOptions)
                   ?? new MonthlyPlayerPublicationState();
        }
        catch
        {
            return new MonthlyPlayerPublicationState();
        }
    }

    public async Task SaveAsync(
        MonthlyPlayerPublicationState state,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory("history");

        var json =
            JsonSerializer.Serialize(
                state,
                _jsonOptions);

        await File.WriteAllTextAsync(
            StateFile,
            json,
            cancellationToken);
    }
}

public sealed class MonthlyPlayerPublicationState
{
    public string? TrackingStartedMonth { get; set; }

    public List<string> PublishedMonths { get; set; } = [];
}
