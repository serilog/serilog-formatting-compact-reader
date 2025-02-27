using Serilog.Events;
using System.Text.Json;
using Xunit;

namespace Serilog.Formatting.Compact.Reader.Tests;

public class PropertyFactoryTests
{
    [Fact]
    public void PropertiesAreConstructed()
    {
        const string name = "Test";
        const string value = "Value";
        using var jd = JsonDocument.Parse($"{{\"{name}\": \"{value}\"}}");
        var p = PropertyFactory.CreateProperty(name, jd.RootElement.GetProperty(name), null);
        Assert.Equal(name, p.Name);
        var s = Assert.IsType<ScalarValue>(p.Value);
        Assert.Equal(value, s.Value);
    }

    [Fact]
    public void InvalidPropertyNamesAreSubstituted()
    {
        const string name = "";
        using var jd = JsonDocument.Parse("null");
        var p = PropertyFactory.CreateProperty(name, jd.RootElement, null);
        Assert.NotEqual(name, p.Name);
    }
}