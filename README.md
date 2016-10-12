# Serilog.Formatting.Compact.Reader [![Build status](https://ci.appveyor.com/api/projects/status/wb24026kijmhynls?svg=true)](https://ci.appveyor.com/project/NicholasBlumhardt/serilog-formatting-compact-reader) [![NuGet Pre Release](https://img.shields.io/nuget/vpre/Serilog.Formatting.Compact.svg)](https://www.nuget.org/packages/serilog.formatting.compact.reader)

Reads (deserializes) log files in JSON format created by [Serilog.Formatting.Compact](https://github.com/serilog/serilog-formatting-compact).

### Example

Log events are written to a file using `CompactJsonFormatter`:

```csharp
using (var fileLog = new LoggerConfiguration()
    .WriteTo.File(new CompactJsonFormatter(), "log.clef")
    .CreateLogger())
{
    fileLog.Information("Hello, {@User}", new { Name = "nblumhardt", Id = 101 });
    fileLog.Information("Number {N:x8}", 42);
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
```

This creates a log file with content similar to:

```json
{"@t":"2016-10-12T04:46:58.0554314Z","@mt":"Hello, {@User}","User":{"Name":"nblumhardt","Id":101}}
{"@t":"2016-10-12T04:46:58.0684369Z","@mt":"Number {N:x8}","@r":["0000002a"],"N":42}
{"@t":"2016-10-12T04:46:58.0724384Z","@mt":"Tags are {Tags}","@l":"Warning","Tags":["test","orange"]}
{"@t":"2016-10-12T04:46:58.0904378Z","@mt":"Something failed","@l":"Error", "@x":"System.DivideByZer...<snip>"}
```

An instance of `LogEventReader` converts each line of the log file back into a `LogEvent`, which can be manipulated, rendered, or written through another Serilog sink:

```csharp
using (var console = new LoggerConfiguration()
    .WriteTo.LiterateConsole()
    .CreateLogger())
{
    using (var clef = File.OpenText("log.clef"))
    {
        var reader = new LogEventReader(clef);
        LogEvent evt;
        while (reader.TryRead(out evt))
            console.Write(evt);
    }
}
```

Output from the logger:

![Screenshot](https://raw.githubusercontent.com/nblumhardt/serilog-formatting-compact-reader/dev/asset/Screenshot.png)

### Limitations

Events deserialized from JSON are for typical purposes just like the original log events. There are two main things to keep in mind:

 1. JSON doesn't carry all of the type information necessary to determine if, for example, a number is an `int` or a `float`. JSON.NET does a good job of deserializing anything that it encounters, but you can't rely on the types here being identical.
 2. Exceptions deserialized this way aren't instances of the original exception type - all you can do with them is call `ToString()` to get the formatted message and stack trace, which is what 99% of Serilog sinks will do.
