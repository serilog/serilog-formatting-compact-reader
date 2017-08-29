using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog.Events;
using System;
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

            Assert.Equal(5, all.Count);
        }

        [Fact]
        public void BasicFieldsAreRead()
        {
            const string document = "{\"@t\":\"2016-10-12T04:20:58.0554314Z\",\"@mt\":\"Hello, {@User}\",\"User\":{\"Name\":\"nblumhardt\",\"Id\":101}}";
            var evt = LogEventReader.ReadFromString(document);

            Assert.Equal(DateTimeOffset.Parse("2016-10-12T04:20:58.0554314Z"), evt.Timestamp);
            Assert.Equal(LogEventLevel.Information, evt.Level);
            Assert.Equal("Hello, {@User}", evt.MessageTemplate.Text);

            var user = (StructureValue)evt.Properties["User"];
            Assert.Equal("Name", user.Properties[0].Name);
            Assert.Equal(new ScalarValue("nblumhardt"), (ScalarValue)user.Properties[0].Value);
            Assert.Equal("Id", user.Properties[1].Name);
            Assert.Equal(101L, ((ScalarValue)user.Properties[1].Value).Value);
        }

        [Fact]
        public void MessagesAreEscapedIntoTemplates()
        {
            const string document = "{\"@t\":\"2016-10-12T04:20:58.0554314Z\",\"@m\":\"Hello, {text}\"}";
            var evt = LogEventReader.ReadFromString(document);

            Assert.Equal("Hello, {{text}}", evt.MessageTemplate.Text);
        }

        [Fact]
        public void HandlesDefaultJsonNetSerialization()
        {
            const string document = "{\"@t\":\"2016-10-12T04:20:58.0554314Z\",\"@m\":\"Hello\"}";
            var jObject = JsonConvert.DeserializeObject<JObject>(document);
            var evt = LogEventReader.ReadFromJObject(jObject);

            Assert.Equal(DateTimeOffset.Parse("2016-10-12T04:20:58.0554314Z"), evt.Timestamp);
        }

        [Fact]
        public void RoundTripsTypeTags()
        {
            const string document = "{\"@t\":\"2016-10-12T04:20:58.0554314Z\",\"@m\":\"Hello\",\"User\":{\"$type\":\"TestUser\",\"Name\":\"nblumhardt\"}}";
            var evt = LogEventReader.ReadFromString(document);

            var user = (StructureValue)evt.Properties["User"];
            Assert.Equal("TestUser", user.TypeTag);
        }

        [Fact]
        public void PassesThroughUnrecognizedReifiedProperties()
        {
            const string document = "{\"@t\":\"2016-10-12T04:20:58.0554314Z\",\"@m\":\"Hello\",\"@foo\":42}";
            var evt = LogEventReader.ReadFromString(document);

            var foo = evt.Properties["@foo"];
            Assert.Equal(42L, ((ScalarValue)foo).Value);

            // Ensure we don't just forward everything
            Assert.False(evt.Properties.ContainsKey("@m"));
        }
    }
}
