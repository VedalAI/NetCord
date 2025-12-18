using NetCord.Gateway;
using NetCord.JsonModels;
using NetCord.Rest;

namespace NetCord;

public abstract class ComponentInteraction : Interaction
{
    private protected ComponentInteraction(JsonInteraction jsonModel, Guild? guild, InteractionResponseDelegate sendResponseAsync, RestClient client) : base(jsonModel, guild, sendResponseAsync, client)
    {
    }

    public abstract ComponentInteractionData Data2 { get; }

    public sealed override InteractionData Data => Data2;
}

public class ComponentInteractionData : InteractionData
{
    private protected ComponentInteractionData(JsonInteractionData jsonModel) : base(jsonModel)
    {
    }

    public string CustomId => _jsonModel.CustomId!;
}
