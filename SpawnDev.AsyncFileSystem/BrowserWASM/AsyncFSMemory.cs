using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.JSObjects;
using SpawnDev.BlazorJS.Toolbox;
using System.Text;
using System.Text.Json;
using BlazorFile = SpawnDev.BlazorJS.JSObjects.File;

namespace SpawnDev.AsyncFileSystem.BrowserWASM
{
    /// <summary>
    /// An in-memory <see cref="IAsyncBrowserFileSystem"/> for the browser. Files are stored as JS
    /// <see cref="Blob"/>s held in JS memory (NOT the .NET/WASM heap, and NOT persisted to OPFS), so it
    /// supports the same zero-copy <see cref="Uint8Array"/> read/write surface as the OPFS implementation
    /// — which means consumers that gate a zero-copy fast-path on <c>fs is IAsyncBrowserFileSystem</c>
    /// (e.g. SpawnDev.WebTorrent's <c>AsyncFSChunkStore.SupportsUint8Array</c>) light up <b>reliably</b>,
    /// with no dependency on OPFS being initialized/available. Storing the data as browser-managed Blobs
    /// (which the engine can spill to disk) also avoids the WASM-heap OOM that a .NET <c>byte[]</c> store
    /// hits on multi-GB payloads. Ephemeral: contents live only for the lifetime of this instance.
    /// <para><b>Disposal/concurrency caveat:</b> a read hands back a <see cref="Blob"/>/<see cref="File"/> that
    /// REFERENCES the stored Blob — it is NOT a deep copy. Disposing that Blob (via <see cref="Remove"/>, an
    /// overwriting <c>Write</c> to the same path, or <see cref="DisposeAsync"/>) while a read of that path is
    /// still in flight invalidates the in-flight read and surfaces as a browser <c>NotReadableError</c>. The
    /// single-threaded WASM model makes non-overlapping access safe, but callers MUST NOT remove/overwrite a
    /// path while an awaited read of it is outstanding — e.g. do not remove a torrent's pieces while a model
    /// load is still streaming them. (This is exactly the trap that produced the intermittent SD-Turbo
    /// NotReadableError: a per-model torrent-removal disposed pieces mid-load.)</para>
    /// </summary>
    public class AsyncFSMemory : IAsyncBrowserFileSystem, IAsyncDisposable
    {
        // path -> Blob (file contents, JS-side). path is normalized: forward slashes, no leading/trailing slash.
        private readonly Dictionary<string, Blob> _files = new(StringComparer.Ordinal);
        // explicit directory set (root "" always present). Files also imply their parent chain.
        private readonly HashSet<string> _dirs = new(StringComparer.Ordinal) { "" };

        public event EventHandler<FileSystemChangeEventArgs> FileSystemChanged = default!;

        public AsyncFSMemory() { }

        // ── path helpers ─────────────────────────────────────────────
        private static string Norm(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            var p = path.Replace('\\', '/').Trim('/');
            // collapse any duplicate slashes
            while (p.Contains("//")) p = p.Replace("//", "/");
            return p;
        }
        private static string Parent(string norm)
        {
            int i = norm.LastIndexOf('/');
            return i < 0 ? "" : norm.Substring(0, i);
        }
        private static string Leaf(string norm)
        {
            int i = norm.LastIndexOf('/');
            return i < 0 ? norm : norm.Substring(i + 1);
        }
        private void EnsureParentDirs(string norm)
        {
            var parent = Parent(norm);
            while (true)
            {
                _dirs.Add(parent);
                if (parent.Length == 0) break;
                parent = Parent(parent);
            }
        }
        private static bool IsUnder(string candidate, string dir)
        {
            if (dir.Length == 0) return candidate.Length > 0; // everything is under root
            return candidate == dir || candidate.StartsWith(dir + "/", StringComparison.Ordinal);
        }
        private static bool IsDirectChild(string candidate, string dir)
        {
            if (!IsUnder(candidate, dir) || candidate == dir) return false;
            var rest = dir.Length == 0 ? candidate : candidate.Substring(dir.Length + 1);
            return !rest.Contains('/');
        }

        // ── core store (replace the blob at path) ────────────────────
        private void Store(string path, Blob blob)
        {
            var n = Norm(path);
            if (_files.TryGetValue(n, out var old)) old.Dispose();
            _files[n] = blob;
            EnsureParentDirs(n);
            FileSystemChanged?.Invoke(this, new FileSystemChangeEventArgs(FileSystemChangeType.Changed, n));
        }
        private Blob Require(string path)
        {
            var n = Norm(path);
            if (!_files.TryGetValue(n, out var blob))
                throw new FileNotFoundException($"File not found: {n}");
            return blob;
        }

        // ── IAsyncBrowserFileSystem: writes (each builds a Blob) ──────
        public Task Write(string path, TypedArray data) { Store(path, new Blob(new[] { data })); return Task.CompletedTask; }
        public Task Write(string path, ArrayBuffer data) { Store(path, new Blob(new[] { data })); return Task.CompletedTask; }
        public Task Write(string path, DataView data) { Store(path, new Blob(new[] { data })); return Task.CompletedTask; }
        public Task Write(string path, Blob data) { Store(path, new Blob(new[] { data })); return Task.CompletedTask; }
        public Task Write(string path, byte[] data) { Store(path, new Blob(new[] { data })); return Task.CompletedTask; }
        public Task Write(string path, string data) { Store(path, new Blob(new[] { Encoding.UTF8.GetBytes(data) })); return Task.CompletedTask; }
        public async Task Write(string path, Stream data)
        {
            using var ms = new MemoryStream();
            await data.CopyToAsync(ms);
            Store(path, new Blob(new[] { ms.ToArray() }));
        }
        public Task Write(string path, FileSystemWriteOptions data)
            => throw new NotSupportedException("FileSystemWriteOptions writes are an OPFS-handle concept; use a byte[]/Uint8Array/Stream overload on the in-memory FS.");

        // ── reads ────────────────────────────────────────────────────
        public async Task<Uint8Array> ReadUint8Array(string path)
        {
            using var ab = await Require(path).ArrayBuffer();
            return new Uint8Array(ab);
        }
        public Task<BlazorFile> ReadFile(string path)
        {
            var blob = Require(path);
            return Task.FromResult(new BlazorFile(new[] { blob }, Leaf(Norm(path))));
        }
        public Task<ArrayBuffer> ReadArrayBuffer(string path) => Require(path).ArrayBuffer();
        public async Task<byte[]> ReadBytes(string path)
        {
            using var ab = await Require(path).ArrayBuffer();
            return ab.ReadBytes();
        }
        public Task<BlobStream> ReadBlobStream(string path) => Task.FromResult(new BlobStream(Require(path)));
        public async Task<string> ReadText(string path) => Encoding.UTF8.GetString(await ReadBytes(path));
        public async Task<T> ReadJSON<T>(string path, JsonSerializerOptions? jsonSerializerOptions = null)
            => JsonSerializer.Deserialize<T>(await ReadText(path), jsonSerializerOptions)!;
        public async Task<Stream> ReadStream(string path) => new MemoryStream(await ReadBytes(path), writable: false);
        public Task<Stream> GetReadStream(string path) => ReadStream(path);
        public Task<T> ReadTypedArray<T>(string path) where T : TypedArray
            => throw new NotSupportedException("ReadTypedArray<T> is not implemented for the in-memory FS; use ReadUint8Array / ReadArrayBuffer.");

        // ── JSON / text convenience writes ───────────────────────────
        public Task WriteJSON(string path, object data, JsonSerializerOptions? jsonSerializerOptions = null)
            => Write(path, JsonSerializer.Serialize(data, jsonSerializerOptions));

        // ── append ───────────────────────────────────────────────────
        private async Task AppendBytes(string path, byte[] tail)
        {
            var n = Norm(path);
            if (_files.TryGetValue(n, out var existing))
            {
                using var ab = await existing.ArrayBuffer();
                var head = ab.ReadBytes();
                var combined = new byte[head.Length + tail.Length];
                Buffer.BlockCopy(head, 0, combined, 0, head.Length);
                Buffer.BlockCopy(tail, 0, combined, head.Length, tail.Length);
                Store(path, new Blob(new[] { combined }));
            }
            else Store(path, new Blob(new[] { tail }));
        }
        public Task Append(string path, byte[] data) => AppendBytes(path, data);
        public Task Append(string path, string data) => AppendBytes(path, Encoding.UTF8.GetBytes(data));
        public async Task Append(string path, Stream data) { using var ms = new MemoryStream(); await data.CopyToAsync(ms); await AppendBytes(path, ms.ToArray()); }
        public async Task Append(string path, ArrayBuffer data) => await AppendBytes(path, data.ReadBytes());
        public async Task Append(string path, TypedArray data) { using var ab = data.Buffer; await AppendBytes(path, ab.ReadBytes()); }
        public async Task Append(string path, DataView data) { using var ab = data.Buffer; await AppendBytes(path, ab.ReadBytes()); }
        public async Task Append(string path, Blob data) { using var ab = await data.ArrayBuffer(); await AppendBytes(path, ab.ReadBytes()); }
        public Task Append(string path, FileSystemWriteOptions data)
            => throw new NotSupportedException("FileSystemWriteOptions appends are an OPFS-handle concept on the in-memory FS.");

        // ── existence ────────────────────────────────────────────────
        public Task<bool> FileExists(string path) => Task.FromResult(_files.ContainsKey(Norm(path)));
        public Task<bool> DirectoryExists(string path) => Task.FromResult(_dirs.Contains(Norm(path)));
        public Task<bool> Exists(string path) { var n = Norm(path); return Task.FromResult(_files.ContainsKey(n) || _dirs.Contains(n)); }

        // ── directories ──────────────────────────────────────────────
        public Task CreateDirectory(string path) { var n = Norm(path); _dirs.Add(n); EnsureParentDirs(n); _dirs.Add(n); return Task.CompletedTask; }
        public Task<List<string>> GetDirectories(string path)
        {
            var dir = Norm(path);
            return Task.FromResult(_dirs.Where(d => d.Length > 0 && IsDirectChild(d, dir)).Select(Leaf).ToList());
        }
        public Task<List<string>> GetFiles(string path)
        {
            var dir = Norm(path);
            return Task.FromResult(_files.Keys.Where(f => IsDirectChild(f, dir)).Select(Leaf).ToList());
        }
        public async Task<List<string>> GetEntries(string path)
        {
            var dirs = await GetDirectories(path);
            var files = await GetFiles(path);
            var ret = dirs.Select(d => d + "/").ToList();
            ret.AddRange(files);
            return ret;
        }
        public Task Remove(string path, bool recursive = false)
        {
            var n = Norm(path);
            if (_files.TryGetValue(n, out var blob)) { blob.Dispose(); _files.Remove(n); FileSystemChanged?.Invoke(this, new FileSystemChangeEventArgs(FileSystemChangeType.Deleted, n)); return Task.CompletedTask; }
            if (_dirs.Contains(n))
            {
                var children = _files.Keys.Where(f => IsUnder(f, n)).Concat(_dirs.Where(d => d.Length > 0 && IsUnder(d, n))).ToList();
                if (!recursive && children.Any(c => c != n))
                    throw new IOException($"Directory not empty: {n}");
                foreach (var f in _files.Keys.Where(f => IsUnder(f, n)).ToList()) { _files[f].Dispose(); _files.Remove(f); }
                foreach (var d in _dirs.Where(d => d.Length > 0 && IsUnder(d, n)).ToList()) _dirs.Remove(d);
                FileSystemChanged?.Invoke(this, new FileSystemChangeEventArgs(FileSystemChangeType.Deleted, n));
            }
            return Task.CompletedTask;
        }

        // ── info ─────────────────────────────────────────────────────
        public Task<ASyncFSEntryInfo?> GetInfo(string path)
        {
            var n = Norm(path);
            if (_files.TryGetValue(n, out var blob))
                return Task.FromResult<ASyncFSEntryInfo?>(new ASyncFSEntryInfo(false, Leaf(n), Parent(n), 0, blob.Size));
            if (_dirs.Contains(n))
                return Task.FromResult<ASyncFSEntryInfo?>(new ASyncFSEntryInfo(true, Leaf(n), Parent(n)));
            return Task.FromResult<ASyncFSEntryInfo?>(null);
        }
        public async Task<List<ASyncFSEntryInfo>> GetInfos(string path, bool recursive = false)
        {
            var ret = new List<ASyncFSEntryInfo>();
            await foreach (var i in EnumerateInfos(path, recursive)) ret.Add(i);
            return ret;
        }
        public async IAsyncEnumerable<ASyncFSEntryInfo> EnumerateInfos(string path, bool recursive = false)
        {
            var dir = Norm(path);
            foreach (var d in _dirs.Where(d => d.Length > 0 && (recursive ? IsUnder(d, dir) : IsDirectChild(d, dir))).ToList())
                yield return new ASyncFSEntryInfo(true, Leaf(d), Parent(d));
            foreach (var f in _files.Keys.Where(f => recursive ? IsUnder(f, dir) : IsDirectChild(f, dir)).ToList())
                yield return new ASyncFSEntryInfo(false, Leaf(f), Parent(f), 0, _files[f].Size);
            await Task.CompletedTask;
        }

        // ── OPFS-handle accessors: not applicable to an in-memory FS ──
        public Task<FileSystemDirectoryHandle?> GetDirectoryHandle(string path) => Task.FromResult<FileSystemDirectoryHandle?>(null);
        public Task<FileSystemFileHandle?> GetFileHandle(string path) => Task.FromResult<FileSystemFileHandle?>(null);
        public Task<FileSystemHandle?> GetHandle(string path) => Task.FromResult<FileSystemHandle?>(null);

        // ── write stream (buffers, stores the whole file on dispose) ──
        public Task<Stream> GetWriteStream(string path) => Task.FromResult<Stream>(new BufferingWriteStream(this, Norm(path)));

        public ValueTask DisposeAsync()
        {
            foreach (var b in _files.Values) b.Dispose();
            _files.Clear();
            return ValueTask.CompletedTask;
        }

        private sealed class BufferingWriteStream : MemoryStream
        {
            private readonly AsyncFSMemory _fs;
            private readonly string _path;
            private bool _flushed;
            public BufferingWriteStream(AsyncFSMemory fs, string path) { _fs = fs; _path = path; }
            public override async ValueTask DisposeAsync() { await CommitAsync(); await base.DisposeAsync(); }
            protected override void Dispose(bool disposing) { if (disposing) CommitAsync().GetAwaiter().GetResult(); base.Dispose(disposing); }
            private Task CommitAsync()
            {
                if (_flushed) return Task.CompletedTask;
                _flushed = true;
                return _fs.Write(_path, ToArray());
            }
        }
    }
}
