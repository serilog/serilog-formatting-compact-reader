using Serilog.Formatting.Compact.Reader;
using Serilog;
using Serilog.Events;
using System;
using System.IO;
using Serilog.Formatting.Compact;

namespace RoundTrip
{
    public class Program
    {
        public static void Main(string[] args)
        {
            using (var fileLog = new LoggerConfiguration()
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

            using (var console = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger())
            {
                using (var clef = File.OpenText("log.clef"))
                {
                    var reader = new LogEventReader(clef);
                    while (reader.TryRead(out var evt))
                        console.Write(evt);
                }
            }

            File.Delete("log.clef");
        }
    }
}
