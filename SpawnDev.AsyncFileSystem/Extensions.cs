using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SpawnDev.AsyncFileSystem.BrowserWASM;
using SpawnDev.AsyncFileSystem.Native;

namespace SpawnDev.AsyncFileSystem
{
    /// <summary>
    /// Extension methods
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Registers an implementation of the IAsyncFS interface with the dependency injection container, selecting the
        /// appropriate file system for the current platform.
        /// </summary>
        /// <remarks>On browser platforms, this method registers an implementation that uses the File
        /// System Access API (OPFS). On other platforms, it registers a native file system implementation
        /// using the specified base path (defaults to LocalApplicationData/SpawnDev).</remarks>
        /// <param name="services">The IServiceCollection to add the file system service to.</param>
        /// <param name="basePath">Base directory for native file system (desktop only). Ignored on browser.
        /// Defaults to LocalApplicationData/SpawnDev. Created automatically if it doesn't exist.</param>
        /// <returns>The IServiceCollection instance with the file system service registered.</returns>
        public static IServiceCollection AddAsyncFileSystem(this IServiceCollection services, string? basePath = null)
        {
            if (OperatingSystem.IsBrowser())
            {
                services.TryAddSingleton<IAsyncFS, AsyncFSFileSystemDirectoryHandle>();
            }
            else
            {
                var path = basePath ?? AsyncFSNative.DefaultBasePath;
                services.TryAddSingleton<IAsyncFS>(sp => new AsyncFSNative(path, createIfNotExists: true));
            }
            return services;
        }
    }
}
