using System.IO;
using System.Text.Json;
using DailyToDoList.Models;

namespace DailyToDoList.Services;

public class TaskStorageService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _storagePath;

    public TaskStorageService()
    {
        var appDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DailyToDoList");
        Directory.CreateDirectory(appDirectory);
        _storagePath = Path.Combine(appDirectory, "tasks.json");
    }

    public async Task<IList<TaskItem>> LoadAsync()
    {
        if (!File.Exists(_storagePath))
        {
            return new List<TaskItem>();
        }

        await using var stream = File.OpenRead(_storagePath);
        var items = await JsonSerializer.DeserializeAsync<List<TaskItem>>(stream, SerializerOptions);
        return items ?? new List<TaskItem>();
    }

    public async Task SaveAsync(IEnumerable<TaskItem> tasks)
    {
        await using var stream = File.Create(_storagePath);
        await JsonSerializer.SerializeAsync(stream, tasks, SerializerOptions);
    }
}
