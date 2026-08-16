using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.Toolbox;
using BlazorFile = SpawnDev.SpawnJS.JSObjects.File;

namespace SpawnDev.AsyncFileSystem
{
    public interface IAsyncBrowserFileSystem : IAsyncFS
    {
        /// <summary>
        /// Append data to the file, the file will be created if it does not exist
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task Append(string path, ArrayBuffer data);
        /// <summary>
        /// Append data to the file, the file will be created if it does not exist
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task Append(string path, Blob data);
        /// <summary>
        /// Append data to the file, the file will be created if it does not exist
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task Append(string path, DataView data);
        /// <summary>
        /// Append data to the file, the file will be created if it does not exist
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task Append(string path, FileSystemWriteOptions data);
        /// <summary>
        /// Append data to the file, the file will be created if it does not exist
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task Append(string path, TypedArray data);
        /// <summary>
        /// Returns the file as a TypedArray
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        Task<T> ReadTypedArray<T>(string path) where T : TypedArray;
        /// <summary>
        /// Read the data from the file as File
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        Task<BlazorFile> ReadFile(string path);
        /// <summary>
        /// Read the data from the file as a BlobStream Stream.<br/>
        /// Note: Reads the file from disk asynchronously. Very useful for large files. The stream ONLY supports asynchronous reading.
        /// </summary>
        Task<BlobStream> ReadBlobStream(string path);
        /// <summary>
        /// Read the data from the file as an ArrayBuffer
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        Task<ArrayBuffer> ReadArrayBuffer(string path);
        /// <summary>
        /// Returns the file as a Uint8Array
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        Task<Uint8Array> ReadUint8Array(string path);
        Task Write(string path, ArrayBuffer data);
        Task Write(string path, Blob data);
        Task Write(string path, DataView data);
        Task Write(string path, FileSystemWriteOptions data);
        Task Write(string path, TypedArray data);
        Task<FileSystemDirectoryHandle?> GetDirectoryHandle(string path);
        Task<FileSystemFileHandle?> GetFileHandle(string path);
        Task<FileSystemHandle?> GetHandle(string path);
    }
}
