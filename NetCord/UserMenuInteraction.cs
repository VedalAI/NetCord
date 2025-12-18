using NetCord.Gateway;
using NetCord.Rest;

namespace NetCord;

public class UserMenuInteraction(JsonModels.JsonInteraction jsonModel, Guild? guild, InteractionResponseDelegate sendResponseAsync, RestClient client) : EntityMenuInteraction(jsonModel, guild, sendResponseAsync, client)
{
    public UserMenuInteractionData Data5 { get; } = new(jsonModel.Data!, jsonModel.GuildId, client);

    public sealed override EntityMenuInteractionData Data4 => Data5;
}

public class UserMenuInteractionData : EntityMenuInteractionData
{
    public unsafe UserMenuInteractionData(JsonModels.JsonInteractionData jsonModel,
                                          ulong? guildId,
                                          RestClient client) : base(jsonModel,
                                                                    GetSelectedValues(jsonModel, guildId, client, &EntityMenuHelper.GetUserValues, out var selectedValues, out var resolvedData),
                                                                    resolvedData)
    {
        SelectedValues = selectedValues;
    }

    public new IReadOnlyList<User> SelectedValues { get; }
}
