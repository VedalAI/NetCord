using NetCord.Gateway;
using NetCord.Rest;

namespace NetCord;

public class ButtonInteraction(JsonModels.JsonInteraction jsonModel, Guild? guild, InteractionResponseDelegate sendResponseAsync, RestClient client) : MessageComponentInteraction(jsonModel, guild, sendResponseAsync, client)
{
    public ButtonInteractionData Data4 { get; } = new(jsonModel.Data!);

    public sealed override MessageComponentInteractionData Data3 => Data4;
}

public class ButtonInteractionData(JsonModels.JsonInteractionData jsonModel) : MessageComponentInteractionData(jsonModel)
{
}
