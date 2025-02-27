// Copyright 2013-2015 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Serilog.Events;
using Serilog.Parsing;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global

namespace Serilog.Formatting.Compact.Reader;

/// <summary>
/// Reads files produced by <em>Serilog.Formatting.Compact.CompactJsonFormatter</em>. Events
/// are expected to be encoded as newline-separated JSON documents.
/// </summary>
public class LogEventReader : IDisposable
{
    static readonly MessageTemplateParser Parser = new();
    static readonly Rendering[] NoRenderings = [];
    readonly TextReader _text;

    int _lineNumber;

    /// <summary>
    /// Construct a <see cref="LogEventReader"/>.
    /// </summary>
    /// <param name="text">Text to read from.</param>
    public LogEventReader(TextReader text)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _text.Dispose();
    }

    /// <summary>
    /// Read a line from the input. Blank lines are skipped.
    /// </summary>
    /// <param name="evt"></param>
    /// <returns>True if an event could be read; false if the end-of-file was encountered.</returns>
    /// <exception cref="InvalidDataException">The data format is invalid.</exception>
    public bool TryRead([NotNullWhen(true)] out LogEvent? evt)
    {
        var line = _text.ReadLine();
        _lineNumber++;
        while (string.IsNullOrWhiteSpace(line))
        {
            if (line == null)
            {
                evt = null;
                return false;
            }
            line = _text.ReadLine();
            _lineNumber++;
        }

        evt = ParseLine(line);
        return true;
    }

    /// <summary>
    /// Read a line from the input asynchronously. Blank lines are skipped.
    /// </summary>
    /// <returns>The parsed <see cref="LogEvent" /> if one could be read; <see langword="null"/> if the end-of-file was encountered.</returns>
    /// <exception cref="InvalidDataException">The data format is invalid.</exception>
    public async Task<LogEvent?> TryReadAsync()
    {
        var line = await _text.ReadLineAsync().ConfigureAwait(false);
        _lineNumber++;
        while (string.IsNullOrWhiteSpace(line))
        {
            if (line == null)
            {
                return null;
            }
            line = await _text.ReadLineAsync().ConfigureAwait(false);
            _lineNumber++;
        }

        return ParseLine(line);
    }

#if FEATURE_READ_LINE_ASYNC_CANCELLATION
    /// <inheritdoc cref="TryReadAsync()" />
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<LogEvent?> TryReadAsync(CancellationToken cancellationToken)
    {
        var line = await _text.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        _lineNumber++;
        while (string.IsNullOrWhiteSpace(line))
        {
            if (line == null)
            {
                return null;
            }
            line = await _text.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            _lineNumber++;
        }

        return ParseLine(line);
    }
#endif

    /// <summary>
    /// Read a single log event from a JSON-encoded document.
    /// </summary>
    /// <param name="document">The event in compact-JSON.</param>
    /// <returns>The log event.</returns>
    /// <exception cref="InvalidDataException">The data format is invalid.</exception>
    public static LogEvent ReadFromString(string document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        JsonDocument? data = null;
        try
        {
            data = JsonDocument.Parse(document);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The document could not be deserialized.", ex);
        }
        using (data)
        {
            if (data == null || data.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"The document is not a complete JSON object.");
            return ReadFromJObject(data.RootElement);
        }
    }
    /// <summary>
    /// Read a single log event from an already-deserialized JSON object.
    /// </summary>
    /// <param name="jObject">The deserialized compact-JSON event.</param>
    /// <returns>The log event.</returns>
    /// <exception cref="InvalidDataException">The data format is invalid.</exception>
    public static LogEvent ReadFromJObject(in JsonElement jObject)
    {
        if (jObject.ValueKind != JsonValueKind.Object) throw new ArgumentException(nameof(jObject));
        return ReadFromJObject(1, jObject);
    }

    LogEvent ParseLine(string line)
    {
        JsonDocument? data = null;
        try
        {
            data = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
        }
        using (data)
        {
            if (data == null || data.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"The data on line {_lineNumber} is not a complete JSON object.");
            return ReadFromJObject(_lineNumber, data.RootElement);
        }
    }

    static LogEvent ReadFromJObject(int lineNumber, in JsonElement jObject)
    {
        var timestamp = GetRequiredTimestampField(lineNumber, jObject, ClefFields.Timestamp);

        string? messageTemplate;
        if (TryGetOptionalField(lineNumber, jObject, ClefFields.MessageTemplate, out var mt))
            messageTemplate = mt;
        else if (TryGetOptionalField(lineNumber, jObject, ClefFields.Message, out var m))
            messageTemplate = MessageTemplateSyntax.Escape(m);
        else
            messageTemplate = null;

        var level = LogEventLevel.Information;
        if (TryGetOptionalField(lineNumber, jObject, ClefFields.Level, out var l) && !Enum.TryParse(l, true, out level))
            throw new InvalidDataException($"The `{ClefFields.Level}` value on line {lineNumber} is not a valid `{nameof(LogEventLevel)}`.");

        Exception? exception = null;
        if (TryGetOptionalField(lineNumber, jObject, ClefFields.Exception, out var ex))
            exception = new TextException(ex);

        ActivityTraceId traceId = default;
        if (TryGetOptionalField(lineNumber, jObject, ClefFields.TraceId, out var tr))
            traceId = ActivityTraceId.CreateFromString(tr.AsSpan());

        ActivitySpanId spanId = default;
        if (TryGetOptionalField(lineNumber, jObject, ClefFields.SpanId, out var sp))
            spanId = ActivitySpanId.CreateFromString(sp.AsSpan());

        var parsedTemplate = messageTemplate == null ?
            new MessageTemplate([]) :
            Parser.Parse(messageTemplate);

        var renderings = NoRenderings;
        
        if (jObject.TryGetProperty(ClefFields.Renderings, out var r))
        {
            if (!(r.ValueKind == JsonValueKind.Array))
                throw new InvalidDataException($"The `{ClefFields.Renderings}` value on line {lineNumber} is not an array as expected.");

            renderings = parsedTemplate.Tokens
                .OfType<PropertyToken>()
                .Where(t => t.Format != null)
                .Zip(r.EnumerateArray(), (t, rd) => new Rendering(t.PropertyName, t.Format!, rd.GetString()!))
                .ToArray();
        }

        var properties = jObject
            .EnumerateObject()
            .Where(f => !ClefFields.All.Contains(f.Name))
            .Select(f =>
            {
                var name = ClefFields.Unescape(f.Name);
                var renderingsByFormat = renderings.Length != 0 ? renderings.Where(rd => rd.Name == name).ToArray() : NoRenderings;
                return PropertyFactory.CreateProperty(name, f.Value, renderingsByFormat);
            })
            .ToList();

        if (TryGetOptionalEventId(lineNumber, jObject, ClefFields.EventId, out var eventId))
        {
            properties.Add(new LogEventProperty("@i", new ScalarValue(eventId)));
        }

        return new LogEvent(timestamp, level, exception, parsedTemplate, properties, traceId, spanId);
    }

    static bool TryGetOptionalField(int lineNumber, in JsonElement data, string field, [NotNullWhen(true)] out string? value)
    {
        if (!data.TryGetProperty(field, out var prop) || prop.ValueKind == JsonValueKind.Null)
        {
            value = null;
            return false;
        }

        if (prop.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"The value of `{field}` on line {lineNumber} is not in a supported format.");

        value = prop.GetString()!;
        return true;
    }

    static bool TryGetOptionalEventId(int lineNumber, in JsonElement data, string field, out object? eventId)
    {
        if (!data.TryGetProperty(field, out var prop) || prop.ValueKind == JsonValueKind.Null)
        {
            eventId = null;
            return false;
        }

        switch (prop.ValueKind)
        {
            case JsonValueKind.String:
                eventId = prop.GetString();
                return true;
            case JsonValueKind.Number:
                if (prop.TryGetUInt32(out var v))
                {
                    eventId = v;
                    return true;
                }
                break;
        }

        throw new InvalidDataException(
            $"The value of `{field}` on line {lineNumber} is not in a supported format.");
    }

    static DateTimeOffset GetRequiredTimestampField(int lineNumber, in JsonElement data, string field)
    {
        if (!data.TryGetProperty(field, out var prop) || prop.ValueKind == JsonValueKind.Null)
            throw new InvalidDataException($"The data on line {lineNumber} does not include the required `{field}` field.");

        if (prop.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"The value of `{field}` on line {lineNumber} is not in a supported format.");

        var text = prop.GetString()!;
        if (!DateTimeOffset.TryParse(text, out var offset))
            throw new InvalidDataException($"The value of `{field}` on line {lineNumber} is not in a supported timestamp format.");

        return offset;
    }
}
