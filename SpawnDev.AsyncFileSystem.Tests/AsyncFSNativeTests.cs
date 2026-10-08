using SpawnDev.AsyncFileSystem;
using SpawnDev.AsyncFileSystem.Native;
using System.Text;

namespace SpawnDev.AsyncFileSystem.Tests;

/// <summary>
/// AsyncFSNative against a real folder on disk. Each test gets its own temp folder.
/// </summary>
public class AsyncFSNativeTests
{
    string _root = "";
    AsyncFSNative _fs = null!;
    List<(FileSystemChangeType Type, string Path)> _events = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "asyncfs-tests-" + Guid.NewGuid().ToString("N"));
        _fs = new AsyncFSNative(_root, createIfNotExists: true);
        _events = new();
        _fs.FileSystemChanged += (_, e) => _events.Add((e.ChangeType, e.Path));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    /// <summary>
    /// IAsyncFS.FileSystemChanged is raised by the browser implementation on every mutation. The native one
    /// declared the event and never raised it (CS0067), so a desktop subscriber heard nothing. Same change
    /// types as the browser: Created for a directory, Changed for every write/append, Deleted for a removal.
    /// </summary>
    [Test]
    public async Task EveryMutationRaisesFileSystemChanged()
    {
        await _fs.CreateDirectory("dir");
        await _fs.Write("dir/a.txt", "hello");
        await _fs.Write("dir/b.bin", new byte[] { 1, 2, 3 });
        await _fs.Write("dir/c.bin", new MemoryStream(new byte[] { 4, 5 }));
        await _fs.WriteJSON("dir/d.json", new { x = 1 });
        await _fs.Append("dir/a.txt", " world");
        await _fs.Append("dir/b.bin", new byte[] { 9 });
        await _fs.Remove("dir/c.bin");
        await _fs.Remove("dir", recursive: true);

        Assert.That(_events, Is.EqualTo(new List<(FileSystemChangeType, string)>
        {
            (FileSystemChangeType.Created, "dir"),
            (FileSystemChangeType.Changed, "dir/a.txt"),
            (FileSystemChangeType.Changed, "dir/b.bin"),
            (FileSystemChangeType.Changed, "dir/c.bin"),
            (FileSystemChangeType.Changed, "dir/d.json"),
            (FileSystemChangeType.Changed, "dir/a.txt"),
            (FileSystemChangeType.Changed, "dir/b.bin"), // ONCE: Append(byte[]) goes through Append(Stream)
            (FileSystemChangeType.Deleted, "dir/c.bin"),
            (FileSystemChangeType.Deleted, "dir"),
        }));
    }

    /// <summary>A subscriber that reads the file in its handler must see the whole write.</summary>
    [Test]
    public async Task StreamWriteIsCompleteWhenChangedFires()
    {
        var payload = new byte[1 << 20];
        new Random(1).NextBytes(payload);
        byte[]? seen = null;
        _fs.FileSystemChanged += (_, e) => seen = File.ReadAllBytes(Path.Combine(_root, e.Path));
        await _fs.Write("big.bin", new MemoryStream(payload));
        Assert.That(seen, Is.EqualTo(payload));
        seen = null;
        await _fs.Append("big.bin", new MemoryStream(payload));
        Assert.That(seen!.Length, Is.EqualTo(payload.Length * 2));
    }

    /// <summary>Removing something that is not there changes nothing, so it raises nothing.</summary>
    [Test]
    public async Task RemovingAMissingEntryRaisesNothing()
    {
        await _fs.Remove("not-there.txt");
        Assert.That(_events, Is.Empty);
    }

    [Test]
    public async Task WriteReadRoundTrip()
    {
        await _fs.Write("t.txt", "héllo");
        Assert.That(await _fs.ReadText("t.txt"), Is.EqualTo("héllo"));
        await _fs.Write("b.bin", Encoding.UTF8.GetBytes("xyz"));
        Assert.That(await _fs.ReadBytes("b.bin"), Is.EqualTo(Encoding.UTF8.GetBytes("xyz")));
        var info = await _fs.GetInfo("b.bin");
        Assert.That(info!.Size, Is.EqualTo(3));
        Assert.That(info.IsDirectory, Is.False);
    }

    /// <summary>The parameterless constructor (JSON) used to leave the three strings null.</summary>
    [Test]
    public void EntryInfoDefaultsAreNotNull()
    {
        var e = new ASyncFSEntryInfo();
        Assert.That(e.Name, Is.Not.Null);
        Assert.That(e.Directory, Is.Not.Null);
        Assert.That(e.FullPath, Is.Not.Null);
    }
}
