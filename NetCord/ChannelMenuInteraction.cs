using NetCord.Gateway;
using NetCord.Rest;

namespace NetCord;

public class ChannelMenuInteraction(JsonModels.JsonInteraction jsonModel, Guild? guild, InteractionResponseDelegate sendResponseAsync, RestClient client) : EntityMenuInteraction(jsonModel, guild, sendResponseAsync, client)
{
    public ChannelMenuInteractionData Data5 { get; } = new(jsonModel.Data!, jsonModel.GuildId, client);

    public sealed override EntityMenuInteractionData Data4 => Data5;
}

public class ChannelMenuInteractionData : EntityMenuInteractionData
{
    public unsafe ChannelMenuInteractionData(JsonModels.JsonInteractionData jsonModel,
                                             ulong? guildId,
                                             RestClient client) : base(jsonModel,
                                                                       GetSelectedValues(jsonModel, guildId, client, &EntityMenuHelper.GetChannelValues, out var selectedValues, out var resolvedData),
                                                                       resolvedData)
    {
        SelectedValues = selectedValues;
    }

    public new IReadOnlyList<Channel> SelectedValues { get; }
}
