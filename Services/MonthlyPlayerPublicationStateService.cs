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
            throw new FileNotFoundException(
                "Månadsstate saknas. Återställ den versionshanterade filen " +
                "history/monthly-player-state.json innan boten startas.",
                StateFile);
        }

        try
        {
            var json =
                await File.ReadAllTextAsync(
                    StateFile,
                    cancellationToken);

            var state = JsonSerializer.Deserialize<MonthlyPlayerPublicationState>(
                json, _jsonOptions)
                ?? throw new JsonException("Månadsstate är tom eller ogiltig.");
            if (string.IsNullOrWhiteSpace(state.TrackingStartedMonth) ||
                state.PublishedMonths is null)
                throw new JsonException("TrackingStartedMonth/PublishedMonths saknas.");
            return state;
        }
        catch (Exception ex) when (ex is IOException ||
                                   ex is UnauthorizedAccessException ||
                                   ex is JsonException)
        {
            throw new InvalidOperationException(
                $"Månadsstate ({StateFile}) kunde inte läsas. " +
                "Stoppar körningen för att inte förlora publiceringshistorik.", ex);
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
