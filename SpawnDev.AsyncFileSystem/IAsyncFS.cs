using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.Toolbox;
using System.Text.Json;

namespace SpawnDev.AsyncFileSystem
{
    public interface IAsyncFS
    {
        event EventHandler<FileSystemChangeEventArgs> FileSystemChanged;

        Task<bool> DirectoryExists(string path);
        Task<bool> Exists(string path);
        Task<bool> FileExists(string path);
        Task<List<string>> GetDirectories(string path);
        Task<List<string>> GetEntries(string path);
        Task<List<string>> GetFiles(string path);
        Task<ASyncFSEntryInfo?> GetInfo(string path);
        Task<List<ASyncFSEntryInfo>> GetInfos(string path, bool recursive = false);
        IAsyncEnumerable<ASyncFSEntryInfo> EnumerateInfos(string path, bool recursive = false);

        Task CreateDirectory(string path);
        Task Remove(string path, bool recursive = false);

        Task Write(string path, Stream data);
        Task Write(string path, string data);
        Task Write(string path, byte[] data);
        Task WriteJSON(string path, object data, JsonSerializerOptions? jsonSerializerOptions = null);

        Task Append(string path, Stream data);
        Task Append(string path, string data);
        Task Append(string path, byte[] data);

        Task<byte[]> ReadBytes(string path);
        Task<string> ReadText(string path);
        Task<T> ReadJSON<T>(string path, JsonSerializerOptions? jsonSerializerOptions = null);

        Task<Stream> ReadStream(string path);

        ///// <summary>
        ///// Returns a synchronously readable stream or throws an exception if not supported
        ///// </summary>
        //Task<Stream> GetReadSyncStream(string path, FileMode fileMode = FileMode.Open);
        ///// <summary>
        ///// Returns a synchronously writable stream or throws an exception if not supported
        ///// </summary>
        //Task<Stream> GetWriteSyncStream(string path, FileMode fileMode = FileMode.OpenOrCreate);
        ///// <summary>
        ///// Returns a synchronously accessible stream or throws an exception if not supported
        ///// </summary>
        //Task<Stream> OpenSyncStream(string path, FileMode fileMode = FileMode.Open, FileAccess fileAccess = FileAccess.Read);

        Task<Stream> GetReadStream(string path, FileMode fileMode = FileMode.Open, OPFSFileOptions fileOptions = OPFSFileOptions.Auto);
        Task<Stream> GetWriteStream(string path, FileMode fileMode = FileMode.OpenOrCreate, OPFSFileOptions fileOptions = OPFSFileOptions.Auto);
        Task<Stream> OpenStream(string path, FileMode fileMode = FileMode.Open, FileAccess fileAccess = FileAccess.Read, OPFSFileOptions fileOptions = OPFSFileOptions.Auto);
    }
}
