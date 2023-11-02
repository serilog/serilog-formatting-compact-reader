using Serilog.Events;
using System.Text.Json;
using Xunit;

namespace Serilog.Formatting.Compact.Reader.Tests
{
    public class PropertyFactoryTests
    {
        [Fact]
        public void PropertiesAreConstructed()
        {
            const string name = "Test";
            const string value = "Value";
            using (var jd = JsonDocument.Parse($"{{\"{name}\": \"{value}\"}}"))
            {
                var p = PropertyFactory.CreateProperty(name, jd.RootElement.GetProperty(name), null);
                Assert.Equal(p.Name, name);
                var s = Assert.IsType<ScalarValue>(p.Value);
                Assert.Equal(value, s.Value);
            }
        }

        [Fact]
        public void InvalidPropertyNamesAreSubstituted()
        {
            using (var jd = JsonDocument.Parse("null"))
            {
                const string name = "";
                var p = PropertyFactory.CreateProperty(name, jd.RootElement, null);
                Assert.NotEqual(p.Name, name);
            }
        }
    }
}