using Newtonsoft.Json.Linq;
using Serilog.Events;
using Xunit;

namespace Serilog.Formatting.Compact.Reader.Tests;

public class PropertyFactoryTests
{
    [Fact]
    public void PropertiesAreConstructed()
    {
        const string name = "Test";
        const string value = "Value";
        var p = PropertyFactory.CreateProperty(name, new JValue(value), null);
        Assert.Equal(name, p.Name);
        var s = Assert.IsType<ScalarValue>(p.Value);
        Assert.Equal(value, s.Value);
    }

    [Fact]
    public void InvalidPropertyNamesAreSubstituted()
    {
        const string name = "";
        var p = PropertyFactory.CreateProperty(name, new JValue((object)null), null);
        Assert.NotEqual(name, p.Name);
    }
}