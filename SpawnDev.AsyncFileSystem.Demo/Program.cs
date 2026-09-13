using SpawnDev;
using SpawnDev.AsyncFileSystem;
using SpawnDev.AsyncFileSystem.BrowserWASM;
using SpawnDev.AsyncFileSystem.Demo;
using SpawnDev.AsyncFileSystem.Native;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.RazorRenderer;
using SpawnDev.SpawnJS.RazorUI;
using SpawnDev.SpawnJS.Toolbox;
using SpawnDev.SpawnJS.WebWorkers;
using SpawnDev.SpawnJS.WebWorkers.OPFS;
using System.Text;

var builder = SpawnJSAppBuilder.CreateDefault(args);
builder.Services.AddSpawnJSRuntime(out var JS);
builder.Services.AddWebWorkerService();

//builder.Services.AddAsyncFileSystem();



//builder.Services.AddSingleton(sp =>
//{
//    var js = sp.GetRequiredService<SpawnJSRuntime>();
//    var webWorkerService = sp.GetRequiredService<WebWorkerService>();
//    var service = new OPFSSyncAccessService(js, webWorkerService);

//    return (IAsyncFS)service;
//});

builder.Services.AddRazorRenderer();
builder.Services.AddRazorUI();

if (JS.IsWindow)
{
    builder.RootComponents.Add<App>("#app");
    //builder.RootComponents.Add<HeadOutlet>("head::after");
    //builder.RootComponents.AddSharedStyleSheet("css/app.css");
}

var host = builder.Build();
await host.Services.StartBackgroundServices();

if (JS.IsWindow)
{
    var webWorkerService = host.Services.GetRequiredService<WebWorkerService>();
    //{
    //    try
    //    {
    //        var helloWorldBytes = Encoding.UTF8.GetBytes("Hello world!");
    //        var helloWorldUint8Array = (Uint8Array)helloWorldBytes;
    //        var stream1 = await OPFSStream.Open("some_data.txt").WaitAsync(TimeSpan.FromSeconds(5));
    //        //JS.Log("rbb 1", helloWorldBytes);

    //        await stream1.WriteUint8ArrayAsync(helloWorldUint8Array);

    //        stream1.Position = 0;

    //        var readBackBytes = await stream1.ReadUint8ArrayAsync((int)stream1.Length);
    //        var data1 = readBackBytes.ReadBytes();

    //        var nmt = true;

    //        JS.Log("rbb 1", helloWorldBytes, data1);
    //    }
    //    catch (Exception ex)
    //    {
    //        JS.Log($"Failed: {ex.ToString()}");
    //    }
    //}
    //{
    //    try
    //    {
    //        var helloWorldBytes = Encoding.UTF8.GetBytes("Hello world!");
    //        var helloWorldUint8Array = (Uint8Array)helloWorldBytes;
    //        var stream1 = await OPFSStream.Open("some_data.txt").WaitAsync(TimeSpan.FromSeconds(5));
    //        //JS.Log("rbb 1", helloWorldBytes);

    //        await stream1.WriteAsync(helloWorldBytes, 0, helloWorldBytes.Length);

    //        stream1.SetLength(32);
    //        await stream1.FlushAsync();

    //        stream1.Position = 0;

    //        JS.Log("rbb 2 length", stream1.Length);

    //        var readBackBytes = new byte[stream1.Length];
    //        var btyesRead = await stream1.ReadAsync(readBackBytes);

    //        var nmt = true;

    //        JS.Log("rbb 2", helloWorldBytes, readBackBytes);
    //    }
    //    catch (Exception ex)
    //    {
    //        JS.Log($"Failed: {ex.ToString()}");
    //    }
    //}

    var asyncOPFS = await AsyncFSFileSystemDirectoryHandle.Create();
    await StreamTests.Run(asyncOPFS);

    var asyncNative = new AsyncFSNative();
    await StreamTests.Run(asyncNative);

}

await host.RunAsync();

static class StreamTests
{
    static SpawnJSRuntime JS => SpawnJSRuntime.Instance;
    static string TestString = "Live long and prosper!";
    static byte[] TestStringBytes = Encoding.UTF8.GetBytes(TestString);
    public static async Task Run(IAsyncFS asyncFS)
    {
        var path = "mystream.txt";
        await asyncFS.Remove(path);
        {
            await using var stream = await asyncFS.OpenStream(path, FileMode.Create, FileAccess.Write);
            await stream.WriteAsync(TestStringBytes);
        }
        {
            await using var stream = await asyncFS.OpenStream(path, FileMode.Create, FileAccess.Write);
            await stream.WriteAsync(TestStringBytes);
        }
        {
            await using var stream = await asyncFS.OpenStream(path);
            var readBackBytes = new byte[stream.Length];
            await stream.ReadExactlyAsync(readBackBytes);
            if (!TestStringBytes.SequenceEqual(readBackBytes)) throw new Exception($"{asyncFS.GetType().Name}: Readback failed");
        }
        await asyncFS.Remove(path);

        Console.WriteLine($"{asyncFS.GetType().Name}: Done");
    }
}