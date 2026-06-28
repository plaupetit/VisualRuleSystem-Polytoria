namespace Vrs.Core.RuntimeEvents;

/// <summary>
/// Owns the Creator hierarchy paths used by VRS runtime relay objects.
/// These labels are intentionally human-readable while keeping the Polytoria
/// event type visible for beginners inspecting World/Hidden.
/// </summary>
public static class VrsRuntimeEventPaths
{
    public const string EventRootPath = "World/Hidden/VRS/Events";
    public const string UserInputNetworkEventsPath = EventRootPath + "/User Input (NetworkEvent)";
    public const string ManagedUserInputNetworkEventsPath = UserInputNetworkEventsPath + "/Input Manager";
    public const string LegacyInputNetworkEventsPath = EventRootPath + "/Input";
    public const string ClientToServerNetworkEventsPath = EventRootPath + "/Client To Server (NetworkEvent)";
    public const string ServerToClientNetworkEventsPath = EventRootPath + "/Server To Client (NetworkEvent)";
    public const string ServerScriptBindableEventsPath = EventRootPath + "/Server Script To Server Script (BindableEvent)";
    public const string ClientScriptBindableEventsPath = EventRootPath + "/Client Script To Client Script (BindableEvent)";
}
