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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serilog.Formatting.Compact.Reader
{
    static class PropertyFactory
    {
        public static LogEventProperty CreateProperty(string name, JToken value, IEnumerable<Rendering> renderings)
        {
            return new LogEventProperty(name, CreatePropertyValue(value, renderings));
        }

        static LogEventPropertyValue CreatePropertyValue(JToken value, IEnumerable<Rendering> renderings)
        {
            if (value.Type == JTokenType.Null)
                return new ScalarValue(null);

            var obj = value as JObject;
            if (obj != null)
            {
                JToken tt;
                obj.TryGetValue("$typeTag", out tt);
                return new StructureValue(
                    obj.Properties().Where(kvp => kvp.Name != "$typeTag").Select(kvp => CreateProperty(kvp.Name, kvp.Value, null)),
                    tt?.Value<string>());
            }

            var arr = value as JArray;
            if (arr != null)
            {
                return new SequenceValue(arr.Select(v => CreatePropertyValue(v, null)));
            }

            var raw = value.Value<JValue>().Value;

            if (renderings != null)
                return new RenderableScalarValue(raw, renderings);

            return new ScalarValue(raw);
        }
    }
}
