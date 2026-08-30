using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Circle.Desktop.Models;

namespace Circle.Desktop.Services;

/// <summary>
/// Manages saved wheel item lists. Each list is a folder under LocalAppData/Circle/wheels/
/// containing items.json and a media/ subfolder for copied images.
/// </summary>
public sealed class WheelListService
{
    private readonly string _wheelsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Circle", "wheels");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public IReadOnlyList<string> GetSavedListNames()
    {
        EnsureRootDirectory();
        return Directory.GetDirectories(_wheelsDirectory)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList()!;
    }

    public ObservableCollection<WheelItem> LoadList(string name)
    {
        var itemsPath = GetItemsPath(name);
        if (!File.Exists(itemsPath))
            return new ObservableCollection<WheelItem>();

        try
        {
            var items = JsonSerializer.Deserialize<List<WheelItem>>(File.ReadAllText(itemsPath));
            return new ObservableCollection<WheelItem>(items ?? new List<WheelItem>());
        }
        catch (JsonException)
        {
            return new ObservableCollection<WheelItem>();
        }
    }

    public void SaveList(string name, IEnumerable<WheelItem> items)
    {
        var listDir = GetListDirectory(name);
        Directory.CreateDirectory(listDir);
        Directory.CreateDirectory(Path.Combine(listDir, "media"));
        File.WriteAllText(GetItemsPath(name), JsonSerializer.Serialize(items.ToList(), JsonOptions));
    }

    public void DeleteList(string name)
    {
        var listDir = GetListDirectory(name);
        if (Directory.Exists(listDir))
            Directory.Delete(listDir, recursive: true);
    }

    public void RenameList(string oldName, string newName)
    {
        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            return;

        var oldDir = GetListDirectory(oldName);
        var newDir = GetListDirectory(newName);

        if (!Directory.Exists(oldDir) || Directory.Exists(newDir))
            return;

        Directory.Move(oldDir, newDir);

        // Update image paths in items.json to point to the new directory
        var itemsPath = Path.Combine(newDir, "items.json");
        if (!File.Exists(itemsPath))
            return;

        var oldMediaDir = Path.Combine(oldDir, "media");
        var newMediaDir = Path.Combine(newDir, "media");

        try
        {
            var items = JsonSerializer.Deserialize<List<WheelItem>>(File.ReadAllText(itemsPath));
            if (items is null)
                return;

            var updated = false;
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.ImagePath))
                    continue;

                var fullOld = Path.GetFullPath(item.ImagePath).TrimEnd(Path.DirectorySeparatorChar);
                var fullOldMedia = Path.GetFullPath(oldMediaDir).TrimEnd(Path.DirectorySeparatorChar);

                if (fullOld.StartsWith(fullOldMedia, StringComparison.OrdinalIgnoreCase))
                {
                    var relative = fullOld[fullOldMedia.Length..].TrimStart(Path.DirectorySeparatorChar);
                    item.ImagePath = Path.Combine(newMediaDir, relative);
                    updated = true;
                }
            }

            if (updated)
                File.WriteAllText(itemsPath, JsonSerializer.Serialize(items, JsonOptions));
        }
        catch (JsonException)
        {
            // Ignore if items.json is corrupted
        }
    }

    /// <summary>
    /// Copies an image file into the list's media folder and returns the new path.
    /// If the file is already inside the list's media folder, returns it as-is.
    /// </summary>
    public string CopyImageToList(string listName, string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            return sourcePath;

        var mediaDir = Path.Combine(GetListDirectory(listName), "media");
        var fullPath = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar);
        var fullMediaDir = Path.GetFullPath(mediaDir).TrimEnd(Path.DirectorySeparatorChar);

        if (fullPath.StartsWith(fullMediaDir, StringComparison.OrdinalIgnoreCase))
            return sourcePath;

        Directory.CreateDirectory(mediaDir);
        var ext = Path.GetExtension(sourcePath);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var destPath = Path.Combine(mediaDir, fileName);
        File.Copy(sourcePath, destPath, overwrite: true);
        return destPath;
    }

    private string GetListDirectory(string name)
    {
        var safeName = new string(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
        return Path.Combine(_wheelsDirectory, safeName);
    }

    private string GetItemsPath(string name) => Path.Combine(GetListDirectory(name), "items.json");

    private void EnsureRootDirectory()
    {
        if (!Directory.Exists(_wheelsDirectory))
            Directory.CreateDirectory(_wheelsDirectory);
    }
}
