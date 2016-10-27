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

using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog.Events;
using Serilog.Parsing;
using System.Linq;

namespace Serilog.Formatting.Compact.Reader
{
    /// <summary>
    /// Reads files produced by <em>Serilog.Formatting.Compact.CompactJsonFormatter</em>. Events
    /// are expected to be encoded as newline-separated JSON documents.
    /// </summary>
    public class LogEventReader : IDisposable
    {
        readonly MessageTemplateParser _parser = new MessageTemplateParser();
        readonly TextReader _text;
        readonly JsonSerializer _serializer;

        int _lineNumber = 0;

        /// <summary>
        /// Construct a <see cref="LogEventReader"/>.
        /// </summary>
        /// <param name="text">Text to read from.</param>
        /// <param name="serializerSettings">If specified, JSON serializer settings to customize how
        /// properties are deserialized into objects.</param>
        public LogEventReader(TextReader text, JsonSerializerSettings serializerSettings = null)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            _text = text;
            _serializer = JsonSerializer.Create(serializerSettings ?? new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.None,
                Culture = CultureInfo.InvariantCulture
            });
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
        public bool TryRead(out LogEvent evt)
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

            var data = _serializer.Deserialize(new JsonTextReader(new StringReader(line)));
            var fields = data as JObject;
            if (fields == null)
                throw new InvalidDataException($"The data on line {_lineNumber} is not a complete JSON object.");

            var timestamp = DateTimeOffset.Parse(GetRequiredField(_lineNumber, fields, ClefFields.Timestamp));

            string messageTemplate;
            if (!TryGetOptionalField(_lineNumber, fields, ClefFields.MessageTemplate, out messageTemplate))
            {
                string message;
                if (!TryGetOptionalField(_lineNumber, fields, ClefFields.Message, out message))
                    throw new InvalidDataException($"The data on line {_lineNumber} does not include the required `{ClefFields.MessageTemplate}` or `{ClefFields.Message}` field.");

                messageTemplate = MessageTemplateEscape(message);
            }

            var level = LogEventLevel.Information;
            string l;
            if (TryGetOptionalField(_lineNumber, fields, ClefFields.Level, out l))
                level = (LogEventLevel)Enum.Parse(typeof(LogEventLevel), l);
            Exception exception = null;
            string ex;
            if (TryGetOptionalField(_lineNumber, fields, ClefFields.Exception, out ex))
                exception = new TextException(ex);

            var unrecognized = fields.Properties().Where(p => ClefFields.IsUnrecognized(p.Name));
            if (unrecognized.Any())
            {
                var names = string.Join(", ", unrecognized.Select(p => $"`{p.Name}`"));
                throw new InvalidDataException($"{names} on line {_lineNumber} are unrecognized.");
            }

            var parsedTemplate = _parser.Parse(messageTemplate);
            var renderings = Enumerable.Empty<Rendering>();

            JToken r;
            if (fields.TryGetValue(ClefFields.Renderings, out r))
            {
                var renderedByIndex = r as JArray;
                if (renderedByIndex == null)
                    throw new InvalidDataException($"The `{ClefFields.Renderings}` value on line {_lineNumber} is not an array as expected.");

                renderings = parsedTemplate.Tokens
                    .OfType<PropertyToken>()
                    .Where(t => t.Format != null)
                    .Zip(renderedByIndex, (t, rd) => new Rendering(t.PropertyName, t.Format, rd.Value<string>()))
                    .ToArray();
            }

            var properties = fields
                .Properties()
                .Where(f => !ClefFields.All.Contains(f.Name))
                .Select(f =>
                {
                    var name = ClefFields.Unescape(f.Name);
                    var renderingsByFormat = renderings.Where(rd => rd.Name == name);
                    return PropertyFactory.CreateProperty(name, f.Value, renderingsByFormat);
                })
                .ToList();

            string eventId;
            if (TryGetOptionalField(_lineNumber, fields, ClefFields.EventId, out eventId))
            {
                properties.Add(new LogEventProperty("@i", new ScalarValue(eventId)));
            }

            evt = new LogEvent(timestamp, level, exception, parsedTemplate, properties);
            return true;
        }

        static string MessageTemplateEscape(string message)
        {
            return message.Replace("{", "{{").Replace("}", "}}");
        }

        static string GetRequiredField(int lineNumber, JObject data, string field)
        {
            string value;
            if (!TryGetOptionalField(lineNumber, data, field, out value))
                throw new InvalidDataException($"The data on line {lineNumber} does not include the required `{field}` field.");

            return value;
        }

        static bool TryGetOptionalField(int lineNumber, JObject data, string field, out string value)
        {
            JToken token;
            if (!data.TryGetValue(field, out token) || token.Type == JTokenType.Null)
            {
                value = null;
                return false;
            }

            if (token.Type != JTokenType.String) // This also excludes nulls
                throw new InvalidDataException($"The value of `{field}` on line {lineNumber} is not in a supported format.");

            value = token.Value<string>();
            return true;
        }
    }
}
