using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Serilog.Formatting.Compact.Reader.Tests;

public class LogEventReaderTests
{
    [Fact]
    public void AllEventsAreRead()
    {
        var all = new List<LogEvent>();

        using (var clef = File.OpenText("LogEventReaderTests.clef"))
        using (var reader = new LogEventReader(clef))
        {
            while (reader.TryRead(out var evt))
                all.Add(evt);
        }

        Assert.Equal(6, all.Count);
    }

    [Fact]
    public async Task AllEventsAreReadAsynchronously()
    {
        var all = new List<LogEvent>();

        using (var clef = File.OpenText("LogEventReaderTests.clef"))
        using (var reader = new LogEventReader(clef))
        {
            while (await reader.TryReadAsync() is { } evt)
                all.Add(evt);
        }

        Assert.Equal(6, all.Count);
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

    [Fact]
    public void MissingMessagesAreAcceptedAsEmpty()
    {
        const string document = "{\"@t\":\"2016-10-12T04:20:58.0554314Z\"}";
        var evt = LogEventReader.ReadFromString(document);

        Assert.Empty(evt.MessageTemplate.Tokens);
    }

    [Fact]
    public void EventIdIntegersAreAccepted()
    {
        const string document = "{\"@t\":\"2016-10-12T04:20:58.0554314Z\",\"@i\":42,\"@m\":\"Hello\"}";
        var evt = LogEventReader.ReadFromString(document);

        Assert.Equal((uint)42, ((ScalarValue)evt.Properties["@i"]).Value);
    }

    [Fact]
    public void ReadsTraceAndSpanIds()
    {
        const string document = "{\"@t\":\"2016-10-12T04:20:58.0554314Z\",\"@tr\":\"1befc31e94b01d1a473f63a7905f6c9b\",\"@sp\":\"bb1111820570b80e\"}";
        var evt = LogEventReader.ReadFromString(document);

        Assert.Equal("1befc31e94b01d1a473f63a7905f6c9b", evt.TraceId.ToString());
        Assert.Equal("bb1111820570b80e", evt.SpanId.ToString());
    }

    [Fact]
    public void EmptyDocumentThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() => LogEventReader.ReadFromString(string.Empty));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("#$%")]
    [InlineData("{\"@t\":0}")]
    [InlineData("{\"@t\":\"2016-02-30\"}")]
    [InlineData("{\"@t\":\"2016-02-12\"")]
    [InlineData("{\"@t\":\"2016-02-12\",\"@l\":\"Trace\"}")]
    [InlineData("{\"@t\":\"2016-02-12\",\"@r\":\"[]\"}")]
    [InlineData("{\"@t\":\"2016-02-12\",\"@m\":0}")]
    [InlineData("{\"@t\":\"2016-02-12\",\"@mt\":[]}")]
    [InlineData("{\"@t\":\"2016-02-12\",\"@x\":[\"\"]}")]
    [InlineData("{\"@t\":\"2016-02-12\",\"@tr\":{}}")]
    [InlineData("{\"@t\":\"2016-02-12\",\"@sp\":true}")]
    [InlineData("{\"@t\":\"2016-02-12\",\"@i\":true}")]
    public async Task InvalidDataThrowsInvalidDataException(string document)
    {
        using var reader = new LogEventReader(new StringReader(document));
        Assert.Throws<InvalidDataException>(() => reader.TryRead(out _));

        using var asyncReader = new LogEventReader(new StringReader(document));
        await Assert.ThrowsAsync<InvalidDataException>(asyncReader.TryReadAsync);

        Assert.Throws<InvalidDataException>(() => LogEventReader.ReadFromString(document));
    }
}