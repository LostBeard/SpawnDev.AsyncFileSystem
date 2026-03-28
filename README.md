# SpawnDev.AsyncFileSystem

[![NuGet](https://img.shields.io/nuget/v/SpawnDev.AsyncFileSystem.svg)](https://www.nuget.org/packages/SpawnDev.AsyncFileSystem)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Cross-platform async file system for .NET. Same API on desktop (native filesystem) and browser (OPFS — Origin Private File System).

## Features

- **Unified `IAsyncFS` interface** — one API for both platforms
- **Browser**: OPFS via `FileSystemDirectoryHandle` (persistent, survives page reloads)
- **Desktop**: Native filesystem via `System.IO`
- **Full file operations**: Read, Write, Append, Delete, Directory management
- **Stream support**: `GetReadStream`, `GetWriteStream` for large files
- **JSON serialization**: `ReadJSON<T>`, `WriteJSON` built-in
- **File system change events**: `FileSystemChanged` event
- **Path utilities**: Cross-platform path manipulation via `IOPath`

## Quick Start

### Browser (Blazor WASM) — OPFS

```csharp
using SpawnDev.AsyncFileSystem;
using SpawnDev.AsyncFileSystem.BrowserWASM;

// Get the OPFS root directory
var fs = new AsyncFSFileSystemDirectoryHandle();

// Write and read files — persists across page reloads
await fs.Write("data/config.json", """{"setting": true}""");
var config = await fs.ReadText("data/config.json");

// Binary data
await fs.Write("data/model.bin", modelBytes);
var bytes = await fs.ReadBytes("data/model.bin");
```

### Desktop (.NET) — Native Filesystem

```csharp
using SpawnDev.AsyncFileSystem;
using SpawnDev.AsyncFileSystem.Native;

var fs = new AsyncFSNative("/path/to/data");

await fs.Write("cache/piece_0", pieceData);
var data = await fs.ReadBytes("cache/piece_0");
```

## API Reference

### IAsyncFS

| Method | Description |
|--------|-------------|
| `Write(path, byte[])` | Write binary data |
| `Write(path, string)` | Write text |
| `Write(path, Stream)` | Write from stream |
| `ReadBytes(path)` | Read as byte array |
| `ReadText(path)` | Read as string |
| `ReadStream(path)` | Get read stream |
| `Append(path, data)` | Append to file |
| `ReadJSON<T>(path)` | Deserialize JSON file |
| `WriteJSON(path, obj)` | Serialize to JSON file |
| `FileExists(path)` | Check if file exists |
| `DirectoryExists(path)` | Check if directory exists |
| `Exists(path)` | Check if path exists |
| `CreateDirectory(path)` | Create directory |
| `Remove(path, recursive)` | Delete file or directory |
| `GetFiles(path)` | List files in directory |
| `GetDirectories(path)` | List subdirectories |
| `GetEntries(path)` | List all entries |
| `GetInfo(path)` | Get file/directory info |
| `GetReadStream(path)` | Get read-only stream |
| `GetWriteStream(path)` | Get writable stream |

## Use Cases

- **Torrent piece storage** — [SpawnDev.WebTorrent](https://github.com/LostBeard/SpawnDev.WebTorrent) uses this for persistent piece caching in the browser
- **ML model caching** — Cache downloaded model weights in OPFS so they load instantly on return visits
- **Application data** — Config files, user data, cached assets
- **Offline-first apps** — Store data locally in the browser, sync when online

## Credits

Built by Todd Tanner ([@LostBeard](https://github.com/LostBeard)) and the SpawnDev team.

## License

MIT
