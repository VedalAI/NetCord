using System.Runtime.CompilerServices;

namespace NetCord.Rest;

[GenerateMethodsForProperties]
public partial record GuildAuditLogPaginationProperties : PaginationProperties<ulong>, IPaginationProperties<ulong, GuildAuditLogPaginationProperties>
{
    public ulong? UserId { get; set; }
    public AuditLogEvent? ActionType { get; set; }

    public static GuildAuditLogPaginationProperties Create() => new();
    public static GuildAuditLogPaginationProperties Create(GuildAuditLogPaginationProperties properties) => new(properties);
}

sealed file class Initializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        PaginationPropertiesStatic.Register(GuildAuditLogPaginationProperties.Create, GuildAuditLogPaginationProperties.Create);
    }
}
