using NetCord.Gateway;
using NetCord.JsonModels;
using NetCord.Rest;

namespace NetCord;

public class EntryPointCommandInteraction(JsonInteraction jsonModel, Guild? guild, InteractionResponseDelegate sendResponseAsync, RestClient client) : ApplicationCommandInteraction(jsonModel, guild, sendResponseAsync, client)
{
    public EntryPointCommandInteractionData Data3 { get; } = new(jsonModel.Data!);

    public sealed override ApplicationCommandInteractionData Data2 => Data3;
}

public class EntryPointCommandInteractionData(JsonInteractionData jsonModel) : ApplicationCommandInteractionData(jsonModel)
{
}
