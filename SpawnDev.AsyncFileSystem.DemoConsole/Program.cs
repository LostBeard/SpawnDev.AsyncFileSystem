using Microsoft.Extensions.DependencyInjection;
using SpawnDev;
using SpawnDev.AsyncFileSystem;
using SpawnDev.SpawnJS;
using System.Text;

var builder = SpawnJSAppBuilder.CreateDefault(args, out var JS);
builder.Services.AddAsyncFileSystem();

var host = builder.Build();
await host.Services.StartBackgroundServices();

var fsService = host.Services.GetRequiredService<IAsyncFS>();
await using (var stream = await fsService.GetWriteStream("myfile.txt"))
{
    var bytes = Encoding.UTF8.GetBytes("Hello world!");
    await stream.WriteAsync(bytes, 0, bytes.Length);
}
await using (var stream = await fsService.GetReadStream("myfile.txt"))
{
    var bytes = new byte[stream.Length];
    await stream.ReadExactlyAsync(bytes);
    var readBack = Encoding.UTF8.GetString(bytes);
    Console.WriteLine($"readBack: {readBack}");
}

await host.RunAsync();