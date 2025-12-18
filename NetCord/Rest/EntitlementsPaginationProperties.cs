using System.Runtime.CompilerServices;

namespace NetCord.Rest;

[GenerateMethodsForProperties]
public partial record EntitlementsPaginationProperties : PaginationProperties<ulong>, IPaginationProperties<ulong, EntitlementsPaginationProperties>
{
    public ulong? UserId { get; set; }
    public IEnumerable<ulong>? SkuIds { get; set; }
    public ulong? GuildId { get; set; }
    public bool? ExcludeEnded { get; set; }

    public static EntitlementsPaginationProperties Create() => new();
    public static EntitlementsPaginationProperties Create(EntitlementsPaginationProperties properties) => new(properties);
}

sealed file class Initializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        PaginationPropertiesStatic.Register(EntitlementsPaginationProperties.Create, EntitlementsPaginationProperties.Create);
    }
}
