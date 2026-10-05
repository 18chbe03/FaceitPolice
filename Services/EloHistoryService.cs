using System.Text.Json;
using FaceitPolice.Models;

namespace FaceitPolice.Services;

public sealed class EloHistoryService
{
    private const string HistoryDirectory = "history";
    private const string HistoryFile = "history/elo-history.json";

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            WriteIndented = true
        };

    private EloHistory _history = new();

    public async Task LoadAsync()
    {
        if (!File.Exists(HistoryFile))
        {
            _history = new EloHistory();
            return;
        }

        var json =
            await File.ReadAllTextAsync(
                HistoryFile);

        _history =
            JsonSerializer.Deserialize<EloHistory>(
                json,
                _jsonOptions)
            ?? new EloHistory();
    }

    public int? GetEloDelta(
        string nickname,
        int currentElo,
        int days = 7)
    {
        if (!_history.Players.TryGetValue(
                nickname,
                out var snapshots))
        {
            return null;
        }

        var targetDate =
            DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(-days));

        var oldSnapshot =
            snapshots
                .Where(x => x.Date <= targetDate)
                .OrderByDescending(x => x.Date)
                .FirstOrDefault();

        if (oldSnapshot is null)
            return null;

        return currentElo - oldSnapshot.Elo;
    }

    public void AddSnapshot(
        string nickname,
        int elo)
    {
        if (!_history.Players.TryGetValue(
                nickname,
                out var snapshots))
        {
            snapshots = [];
            _history.Players[nickname] = snapshots;
        }

        var today =
            DateOnly.FromDateTime(
                DateTime.UtcNow);

        var existing =
            snapshots.FirstOrDefault(
                x => x.Date == today);

        if (existing is not null)
        {
            existing.Elo = elo;
            return;
        }

        snapshots.Add(
            new EloSnapshot
            {
                Date = today,
                Elo = elo
            });

        var cutoff =
            today.AddDays(-60);

        snapshots.RemoveAll(
            x => x.Date < cutoff);
    }

    public async Task SaveAsync()
    {
        Directory.CreateDirectory(
            HistoryDirectory);

        var json =
            JsonSerializer.Serialize(
                _history,
                _jsonOptions);

        await File.WriteAllTextAsync(
            HistoryFile,
            json);
    }
}