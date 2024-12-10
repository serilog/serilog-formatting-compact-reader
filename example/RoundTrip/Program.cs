using Serilog.Formatting.Compact.Reader;
using Serilog;
using System;
using System.IO;
using Serilog.Formatting.Compact;


await using (var fileLog = new LoggerConfiguration()
    .WriteTo.File(new CompactJsonFormatter(), "log.clef")
    .CreateLogger())
{
    fileLog.Information("Hello, {@User}", new { Name = "nblumhardt", Id = 101 });
    fileLog.Information("Number {N:x8}", 42);
    fileLog.Information("String {S}", "Yes");
    fileLog.Warning("Tags are {Tags}", new[] { "test", "orange" });

    try
    {
        throw new DivideByZeroException();
    }
    catch(Exception ex)
    {
        fileLog.Error(ex, "Something failed");
    }
}

await using (var console = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger())
{
    using (var clef = File.OpenText("log.clef"))
    using (var reader = new LogEventReader(clef))
    {
        while (await reader.TryReadAsync() is { } evt)
            console.Write(evt);
    }
}

File.Delete("log.clef");
