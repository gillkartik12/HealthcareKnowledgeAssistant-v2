using System.Text.Json;

namespace HealthcareKnowledgeAssistant.Services;

/// <summary>
/// Minimal JSON file persistence used by the in-memory stores so data
/// survives app restarts / app-pool recycles on shared hosting.
/// </summary>
public static class JsonFileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false
    };

    public static List<T> Load<T>(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new List<T>();

            var json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<List<T>>(json, Options)
                ?? new List<T>();
        }
        catch
        {
            // Corrupt or unreadable file: start fresh rather than crash the app.
            return new List<T>();
        }
    }

    public static void Save<T>(string path, List<T> items)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, JsonSerializer.Serialize(items, Options));
        }
        catch
        {
            // Persistence is best-effort; never fail a request because of it.
        }
    }

    public static string GetDataPath(IHostEnvironment env, string fileName)
    {
        return Path.Combine(env.ContentRootPath, "App_Data", fileName);
    }
}
