using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scry.Probes.Internal;

internal static class YamlConfig
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static T Deserialize<T>(string yaml) => Deserializer.Deserialize<T>(yaml);
}
