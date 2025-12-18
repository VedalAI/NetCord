using System.Runtime.CompilerServices;

namespace NetCord.Rest;

[GenerateMethodsForProperties]
public partial record OptionalGuildUsersPaginationProperties : PaginationProperties<ulong>, IPaginationProperties<ulong, OptionalGuildUsersPaginationProperties>
{
    public bool WithGuildUsers { get; set; }

    public static OptionalGuildUsersPaginationProperties Create() => new();
    public static OptionalGuildUsersPaginationProperties Create(OptionalGuildUsersPaginationProperties properties) => new(properties);
}

sealed file class Initializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        PaginationPropertiesStatic.Register(OptionalGuildUsersPaginationProperties.Create, OptionalGuildUsersPaginationProperties.Create);
    }
}
