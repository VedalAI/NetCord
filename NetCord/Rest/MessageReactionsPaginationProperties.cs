using System.Runtime.CompilerServices;

namespace NetCord.Rest;

[GenerateMethodsForProperties]
public partial record MessageReactionsPaginationProperties : PaginationProperties<ulong>, IPaginationProperties<ulong, MessageReactionsPaginationProperties>
{
    public ReactionType? Type { get; set; }

    public static MessageReactionsPaginationProperties Create() => new();
    public static MessageReactionsPaginationProperties Create(MessageReactionsPaginationProperties properties) => new(properties);
}

sealed file class Initializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        PaginationPropertiesStatic.Register(MessageReactionsPaginationProperties.Create, MessageReactionsPaginationProperties.Create);
    }
}
