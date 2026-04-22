using Scry.Probes.Configs;
using Scry.Probes.Internal;

namespace Scry.Probes.Tests;

public class YamlConfigTests
{
    [Fact]
    public void Deserialize_HttpProbeConfig_WithAllFields()
    {
        const string yaml = """
            url: https://example.com/health
            method: POST
            headers:
              Authorization: Bearer token
              X-Custom: value
            timeout: 00:00:15
            expected_status: 200
            body_contains: "ok"
            """;

        var config = YamlConfig.Deserialize<HttpProbeConfig>(yaml);

        Assert.Equal("https://example.com/health", config.Url);
        Assert.Equal("POST", config.Method);
        Assert.Equal("Bearer token", config.Headers["Authorization"]);
        Assert.Equal("value", config.Headers["X-Custom"]);
        Assert.Equal(TimeSpan.FromSeconds(15), config.Timeout);
        Assert.Equal(200, config.ExpectedStatus);
        Assert.Equal("ok", config.BodyContains);
    }

    [Fact]
    public void Deserialize_HttpProbeConfig_Defaults()
    {
        var config = YamlConfig.Deserialize<HttpProbeConfig>("url: https://example.com");

        Assert.Equal("GET", config.Method);
        Assert.Equal(TimeSpan.FromSeconds(30), config.Timeout);
        Assert.Null(config.ExpectedStatus);
        Assert.Null(config.BodyContains);
        Assert.Empty(config.Headers);
    }

    [Fact]
    public void Deserialize_TcpProbeConfig()
    {
        const string yaml = """
            host: db.internal
            port: 5432
            timeout: 00:00:05
            """;

        var config = YamlConfig.Deserialize<TcpProbeConfig>(yaml);

        Assert.Equal("db.internal", config.Host);
        Assert.Equal(5432, config.Port);
        Assert.Equal(TimeSpan.FromSeconds(5), config.Timeout);
    }

    [Fact]
    public void Deserialize_TcpProbeConfig_DefaultTimeout()
    {
        var config = YamlConfig.Deserialize<TcpProbeConfig>("host: example.com\nport: 80");

        Assert.Equal(TimeSpan.FromSeconds(10), config.Timeout);
    }
}
