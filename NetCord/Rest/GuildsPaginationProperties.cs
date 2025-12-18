using System.Runtime.CompilerServices;

namespace NetCord.Rest;

[GenerateMethodsForProperties]
public partial record GuildsPaginationProperties : PaginationProperties<ulong>, IPaginationProperties<ulong, GuildsPaginationProperties>
{
    public bool WithCounts { get; set; }

    public static GuildsPaginationProperties Create() => new();
    public static GuildsPaginationProperties Create(GuildsPaginationProperties properties) => new(properties);
}

sealed file class Initializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        PaginationPropertiesStatic.Register(GuildsPaginationProperties.Create, GuildsPaginationProperties.Create);
    }
}
