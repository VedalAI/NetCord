using NetCord.Gateway;
using NetCord.Rest;

namespace NetCord;

public class AutocompleteInteraction(JsonModels.JsonInteraction jsonModel, Guild? guild, InteractionResponseDelegate sendResponseAsync, RestClient client) : Interaction(jsonModel, guild, sendResponseAsync, client)
{
    public AutocompleteInteractionData Data2 { get; } = new(jsonModel.Data!, jsonModel.GuildId, client);

    public sealed override InteractionData Data => Data2;
}

public class AutocompleteInteractionData(JsonModels.JsonInteractionData jsonModel, ulong? guildId, RestClient client) : SlashCommandInteractionData(jsonModel, guildId, client)
{
}
