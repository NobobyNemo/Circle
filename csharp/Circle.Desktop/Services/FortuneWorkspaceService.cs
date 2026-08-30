using System.Text.Json;
using Circle.Desktop.Models;
using Circle.Desktop.ViewModels;

namespace Circle.Desktop.Services;

public sealed class FortuneWorkspaceState
{
    public int SelectedTabIndex { get; set; }
    public FortuneGameType GameType { get; set; }
    public WheelGameMode GameMode { get; set; }
    public string? CurrentListName { get; set; }
    public List<WheelItem> WheelItems { get; set; } = [];
    public List<WheelItem> SavedItems { get; set; } = [];
    public List<WheelItem> Team1 { get; set; } = [];
    public List<WheelItem> Team2 { get; set; } = [];
    public bool UseTwoTeams { get; set; }
    public int TeamPickTargetCount { get; set; }
    public int Team2TargetCount { get; set; }
}

/// <summary>Persists the current fortune workspace between application launches.</summary>
public sealed class FortuneWorkspaceService
{
    private readonly string _workspacePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Circle", "fortune-workspace.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public FortuneWorkspaceState? Load()
    {
        if (!File.Exists(_workspacePath))
            return null;

        try
        {
            return JsonSerializer.Deserialize<FortuneWorkspaceState>(
                File.ReadAllText(_workspacePath), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Save(FortuneWorkspaceState state)
    {
        var directory = Path.GetDirectoryName(_workspacePath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(_workspacePath, JsonSerializer.Serialize(state, JsonOptions));
    }
}
