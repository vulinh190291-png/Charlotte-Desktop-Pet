using Charlotte.Core.Organizer;
using Charlotte.Core.Persistence;
using Charlotte.Windows.Services;
using System.IO;

namespace Charlotte.Tests;

public class PersistenceTests
{
    [Fact]
    public async Task Save_and_load_preserve_unicode_and_position()
    {
        using var files = TestDirectory.Create();
        var task = new DailyTask(Guid.NewGuid(), "喝水🌹", true, DateTimeOffset.Parse("2026-09-13T00:00:00Z"), 0);
        var data = new AppData(1, [task], [], new(2026,9,13), 1);
        var store = new JsonStateStore(files.Path);
        await store.SaveAsync(data, new(1, .75, true), default);
        var loaded = await store.LoadAsync(default);
        Assert.Equal("喝水🌹", loaded.Data.Tasks.Single().Title);
        Assert.Equal(.75, loaded.Settings.XRatio);
        Assert.Empty(loaded.Warnings);
    }

    [Fact]
    public async Task Corrupt_current_file_recovers_last_backup()
    {
        using var files = TestDirectory.Create();
        Directory.CreateDirectory(System.IO.Path.Combine(files.Path,"backups"));
        await File.WriteAllTextAsync(System.IO.Path.Combine(files.Path,"data.json"),"{broken");
        await File.WriteAllTextAsync(System.IO.Path.Combine(files.Path,"settings.json"),"{\"schemaVersion\":1,\"xRatio\":null,\"autoStart\":false}");
        await File.WriteAllTextAsync(System.IO.Path.Combine(files.Path,"backups","data.previous.json"),
            "{\"schemaVersion\":1,\"tasks\":[{\"id\":\"00000000-0000-0000-0000-000000000001\",\"title\":\"恢复\",\"isCompleted\":false,\"createdAt\":\"2026-09-13T00:00:00+00:00\",\"order\":0}],\"schedules\":[],\"lastResetDate\":\"2026-09-13\",\"nextOrder\":1}");
        var loaded = await new JsonStateStore(files.Path).LoadAsync(default);
        Assert.Equal("恢复",loaded.Data.Tasks.Single().Title);
        Assert.Contains(loaded.Warnings,x=>x.Contains("备份"));
    }

    [Fact]
    public async Task Failed_parse_never_deletes_corrupt_input()
    {
        using var files = TestDirectory.Create();
        var current = System.IO.Path.Combine(files.Path,"data.json");
        await File.WriteAllTextAsync(current,"{broken");
        var loaded = await new JsonStateStore(files.Path).LoadAsync(default);
        Assert.Empty(loaded.Data.Tasks);
        Assert.True(File.Exists(current));
        Assert.NotEmpty(Directory.GetFiles(files.Path,"data.corrupt.*.json"));
    }

    private sealed class TestDirectory : IDisposable
    {
        public string Path { get; }
        private TestDirectory(string path) { Path=path; Directory.CreateDirectory(path); }
        public static TestDirectory Create() => new(System.IO.Path.Combine(AppContext.BaseDirectory,"artifacts","persistence",Guid.NewGuid().ToString("N")));
        public void Dispose()
        {
            var full=System.IO.Path.GetFullPath(Path);
            if (!full.Contains(System.IO.Path.Combine("artifacts","persistence"),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            if (Directory.Exists(full)) Directory.Delete(full,true);
        }
    }
}
