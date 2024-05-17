using Godot;

namespace MultiplayerInputSystem;

[Tool]
public partial class MultiplayerInputPlugin : EditorPlugin
{
    public static readonly StringName AutoloadName = "MultiplayerInput";

    public override void _EnterTree()
    {
        AddAutoloadSingleton(AutoloadName, "res://addons/MultiplayerInput/MultiplayerInput.cs");
    }

    public override void _ExitTree()
    {
        RemoveAutoloadSingleton(AutoloadName);
    }
}
