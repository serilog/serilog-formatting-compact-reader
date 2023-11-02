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

using System.Text.Json;
using Serilog.Events;
using System.Linq;

namespace Serilog.Formatting.Compact.Reader
{
    static class PropertyFactory
    {
        const string TypeTagPropertyName = "$type";
        const string InvalidPropertyNameSubstitute = "(unnamed)";

        public static LogEventProperty CreateProperty(string name, in JsonElement value, Rendering[] renderings)
        {
            // The format allows (does not disallow) empty/null property names, but Serilog cannot represent them.
            if (!LogEventProperty.IsValidName(name))
                name = InvalidPropertyNameSubstitute;
            
            return new LogEventProperty(name, CreatePropertyValue(value, renderings));
        }

        static LogEventPropertyValue CreatePropertyValue(in JsonElement value, Rendering[] renderings)
        {
            if (value.ValueKind == JsonValueKind.Null)
                return new ScalarValue(null);

            if (value.ValueKind == JsonValueKind.Object)
            {
                string tts = null;
                if(value.TryGetProperty(TypeTagPropertyName, out var tt))
                    tts = tt.GetString();
                return new StructureValue(
                    value.EnumerateObject().Where(kvp => kvp.Name != TypeTagPropertyName).Select(kvp => CreateProperty(kvp.Name, kvp.Value, null)),
                    tts);
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                return new SequenceValue(value.EnumerateArray().Select(v => CreatePropertyValue(v, null)));
            }

            object raw = null;
            switch(value.ValueKind)
            {
                case JsonValueKind.String:
                    raw = value.GetString();break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    raw = value.GetBoolean();break;
                case JsonValueKind.Number:
                    {
                        if (value.TryGetInt64(out var x))
                        {
                            raw = x; break;
                        }
                    }
                    {
                        if (value.TryGetUInt64(out var x))
                        {
                            raw = x; break;
                        }
                    }
                    {
                        if (value.TryGetDouble(out var x))
                        {
                            raw = x; break;
                        }
                    }
                    {
                        if (value.TryGetDecimal(out var x))
                        {
                            raw = x; break;
                        }
                    }
                    break;
            }
            if(raw == null)
                raw = value.GetRawText();

            return renderings != null && renderings.Length != 0 ? 
                new RenderableScalarValue(raw, renderings) :
                new ScalarValue(raw);
        }
    }
}
