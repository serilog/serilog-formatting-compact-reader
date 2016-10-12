using Serilog.Events;
using Serilog.Formatting.Compact.Reader;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Serilog.Formatting.Compact.Reader.Tests
{
    public class LogEventReaderTests
    {
        [Fact]
        public void AllEventsAreRead()
        {
            var all = new List<LogEvent>();

            using (var clef = File.OpenText("LogEventReaderTests.clef"))
            {
                var reader = new LogEventReader(clef);
                LogEvent evt;
                while (reader.TryRead(out evt))
                    all.Add(evt);
            }

            Assert.Equal(4, all.Count);
        }
    }
}
