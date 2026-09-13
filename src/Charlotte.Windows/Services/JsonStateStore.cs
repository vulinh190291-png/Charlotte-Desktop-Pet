using Charlotte.Core.Persistence;
using System.IO;
using System.Text.Json;

namespace Charlotte.Windows.Services;

public sealed class JsonStateStore(string root) : IStateStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private string DataPath => Path.Combine(root, "data.json");
    private string SettingsPath => Path.Combine(root, "settings.json");
    private string Backups => Path.Combine(root, "backups");

    public async Task<LoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        var warnings = new List<string>();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var data = await LoadOne(DataPath, Path.Combine(Backups,"data.previous.json"), AppData.Empty(today), warnings, cancellationToken);
        var settings = await LoadOne(SettingsPath, Path.Combine(Backups,"settings.previous.json"), AppSettings.Default, warnings, cancellationToken);
        return new(data, settings, warnings);
    }

    public async Task SaveAsync(AppData data, AppSettings settings, CancellationToken cancellationToken)
    {
        ValidateVersion(data.SchemaVersion);
        ValidateVersion(settings.SchemaVersion);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Backups);
        await AtomicWrite(DataPath, Path.Combine(Backups,"data.previous.json"), data, cancellationToken);
        await AtomicWrite(SettingsPath, Path.Combine(Backups,"settings.previous.json"), settings, cancellationToken);
    }

    private async Task<T> LoadOne<T>(string current, string backup, T fallback, List<string> warnings, CancellationToken token) where T : class
    {
        if (!File.Exists(current)) return fallback;
        try { return await Read<T>(current, token); }
        catch (Exception error) when (error is JsonException or IOException or InvalidDataException)
        {
            var quarantined = Path.Combine(root,$"{Path.GetFileNameWithoutExtension(current)}.corrupt.{DateTime.UtcNow:yyyyMMddHHmmssfffffff}.json");
            File.Copy(current,quarantined,false);
            if (File.Exists(backup))
            {
                try
                {
                    var recovered = await Read<T>(backup,token);
                    warnings.Add($"{Path.GetFileName(current)} 已损坏，已从备份恢复。");
                    return recovered;
                }
                catch (Exception backupError) when (backupError is JsonException or IOException or InvalidDataException) { }
            }
            warnings.Add($"{Path.GetFileName(current)} 无法读取，已保留损坏文件并使用安全默认值。");
            return fallback;
        }
    }

    private static async Task<T> Read<T>(string path, CancellationToken token) where T : class
    {
        await using var stream = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,4096,FileOptions.Asynchronous|FileOptions.SequentialScan);
        var value = await JsonSerializer.DeserializeAsync<T>(stream,Json,token) ?? throw new InvalidDataException("JSON root is null");
        var version = value switch { AppData data=>data.SchemaVersion, AppSettings settings=>settings.SchemaVersion, _=>0 };
        ValidateVersion(version);
        return value;
    }

    private static void ValidateVersion(int version)
    {
        if (version != AppData.CurrentSchemaVersion) throw new InvalidDataException($"Unsupported schema version {version}");
    }

    private static async Task AtomicWrite<T>(string target, string backup, T value, CancellationToken token)
    {
        var temp = target+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            await using (var stream = new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.Asynchronous|FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream,value,Json,token);
                await stream.FlushAsync(token);
                stream.Flush(true);
            }
            if (File.Exists(target)) File.Replace(temp,target,backup,true);
            else File.Move(temp,target);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
