using System.Text.Json;
using Circle.Desktop.Models;

namespace Circle.Desktop.Services;

public sealed class ChordLibraryService
{
    private const string SettingsFileName = "settings.json";

    private readonly string _settingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Circle");

    public string? SavedRootPath => ReadSettings()?.ChordLibraryPath;

    public IReadOnlyList<ChordLibraryItem> GetArtists(string rootPath) => GetDirectories(rootPath);

    public IReadOnlyList<ChordLibraryItem> GetSongs(string artistPath) => GetDirectories(artistPath);

    public string GetTabFilePath(ChordLibraryItem song) => Path.Combine(song.DirectoryPath, "song.tab");

    public string? FindSongFile(ChordLibraryItem song)
    {
        var preferred = Path.Combine(song.DirectoryPath, "song.chordpro");
        if (File.Exists(preferred))
            return preferred;

        if (!Directory.Exists(song.DirectoryPath))
            return null;

        var supportedExtensions = new[] { ".chordpro", ".chordpro.txt", ".txt" };
        return Directory.EnumerateFiles(song.DirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .Where(file => supportedExtensions.Any(extension =>
                file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(file => Path.GetFileName(file), StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
    }

    public void SaveRootPath(string rootPath)
    {
        Directory.CreateDirectory(_settingsDirectory);
        var settings = ReadSettings() ?? new AppSettings();
        settings.ChordLibraryPath = rootPath;
        File.WriteAllText(
            Path.Combine(_settingsDirectory, SettingsFileName),
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static IReadOnlyList<ChordLibraryItem> GetDirectories(string path)
    {
        if (!Directory.Exists(path))
            return [];

        return Directory.EnumerateDirectories(path)
            .Select(directory => new ChordLibraryItem(Path.GetFileName(directory), directory))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private AppSettings? ReadSettings()
    {
        var path = Path.Combine(_settingsDirectory, SettingsFileName);
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class AppSettings
    {
        public string? ChordLibraryPath { get; set; }
    }
}
