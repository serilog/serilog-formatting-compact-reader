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

using Newtonsoft.Json.Linq;
using Serilog.Events;
using System.Collections.Generic;
using System.Linq;

namespace Serilog.Formatting.Compact.Reader
{
    static class PropertyFactory
    {
        const string TypeTagPropertyName = "$type";
        const string InvalidPropertyNameSubstitute = "(unnamed)";

        public static LogEventProperty CreateProperty(string name, JToken value, Rendering[] renderings)
        {
            // The format allows (does not disallow) empty/null property names, but Serilog cannot represent them.
            if (!LogEventProperty.IsValidName(name))
                name = InvalidPropertyNameSubstitute;
            
            return new LogEventProperty(name, CreatePropertyValue(value, renderings));
        }

        static LogEventPropertyValue CreatePropertyValue(JToken value, Rendering[] renderings)
        {
            if (value.Type == JTokenType.Null)
                return new ScalarValue(null);

            if (value is JObject obj)
            {
                obj.TryGetValue(TypeTagPropertyName, out var tt);
                return new StructureValue(
                    obj.Properties().Where(kvp => kvp.Name != TypeTagPropertyName).Select(kvp => CreateProperty(kvp.Name, kvp.Value, null)),
                    tt?.Value<string>());
            }

            if (value is JArray arr)
            {
                return new SequenceValue(arr.Select(v => CreatePropertyValue(v, null)));
            }

            var raw = value.Value<JValue>().Value;

            return renderings != null && renderings.Length != 0 ? 
                new RenderableScalarValue(raw, renderings) :
                new ScalarValue(raw);
        }
    }
}
