using Godot;

namespace MultiplayerInputSystem;

/// <summary>
/// <para> An object-oriented replacement of Input that is scoped to a single device.</para>
///
/// <para> To use this class, first instantiate an object like this:</para>
/// <code> <see cref="DeviceInput"/> input =  <see langword="new"/> <see cref="DeviceInput"/>(0); </code>
/// <para> Then you can call any of the methods listed here like this for example:</para>
/// <code> input.<M>IsActionJustPressed</M>("Jump");</code>
///
/// <para> This class gracefully handles joypad disconnection by returning default values for all of the methods with "action" in their name if it's currently disconnected.</para>
/// </summary>
public partial class DeviceInput : RefCounted 
{
    // Emitted when this device disconnects or re-connects
    // This will never be emitted for the keyboard player.
    [Signal] public delegate void ConnectionChangedEventHandler(bool connected);

    /// <summary>
    /// <value>The index of this device. This is <c> -1 </c> for the keyboard.</value>
    /// </summary>
    public int Device;

    /// <summary>
    /// <value>Whether this device is currently connected. This is always true for the keyboard.</value>
    /// </summary>
    public bool IsDeviceConnected = true;

    public DeviceInput(int device)
    {
        Device = device;
        Input.JoyConnectionChanged += OnJoyConnectionChanged;
        if (device >= 0) IsDeviceConnected = Input.GetConnectedJoypads().Contains(device);
    }

    /// <summary>
    /// Returns true if this device is the keyboard/mouse "device"
    /// </summary>
    /// <returns></returns>
    public bool IsKeyboard => Device < 0;

    /// <summary>
    /// Returns true if this device is a joypad.
    /// </summary>
    /// <returns></returns>
    public bool IsJoypad => Device >= 0;

    /// <summary>
    /// <para>Returns <c>"Keyboard"</c> if this device is the keyboard. Returns an SDL2-compatible device GUID on platforms that use gamepad remapping, e.g. <c>030000004c050000c405000000010000</c>. Returns <c>"Default Gamepad"</c> otherwise. Godot uses the <a href="https://github.com/gabomdq/SDL_GameControllerDB">SDL2 game controller database</a> to determine gamepad names and mappings based on this GUID.</para>
    /// </summary>
    public string GetGuid()
    {
        if (IsKeyboard) return "Keyboard";
        else return Input.GetJoyGuid(Device);
    }

    /// <summary>
    /// <para>Returns the name of this device, e.g. <c>Keyboard</c>. Godot uses the <a href="https://github.com/gabomdq/SDL_GameControllerDB">SDL2 game controller database</a> to determine gamepad names.</para>
    /// </summary>
    public string GetName()
    {
        if (IsKeyboard) return "Keyboard";
        else return Input.GetJoyName(Device);
    }

    /// <summary>
    /// <para>Returns the duration of the current vibration effect in seconds.</para>
    /// <para><b>Note:</b> This will always be <c>0</c> for the keyboard device.</para>
    /// </summary>
    public float GetVibrationDuration()
    {
        if (IsKeyboard) return 0;
        else return Input.GetJoyVibrationDuration(Device);
    }

    /// <summary>
    /// <para>Returns the strength of the joypad vibration: x is the strength of the weak motor, and y is the strength of the strong motor.</para>
    /// <para><b>Note:</b> This will always be <see cref="Vector2.Zero"/> for the keyboard device.</para>
    /// </summary>
    public Vector2 GetVibrationStrength()
    {
        if (IsKeyboard) return Vector2.Zero;
        else return Input.GetJoyVibrationStrength(Device);
    }

    /// <summary>
    /// <para>Returns <see langword="true"/> if the system knows the specified device. This means that it sets all button and axis indices. Unknown joypads are not expected to match these constants, but you can still retrieve events from them.</para>
    /// <para><b>Note:</b> This will always return <see langword="true"/> for the keyboard device.</para>
    /// </summary>
    public bool IsKnown() 
    {
        if (IsKeyboard) return true;
        else return Input.IsJoyKnown(Device);
    }

    /// <summary>
    /// <para>Starts to vibrate the joypad. Joypads usually come with two rumble motors, a strong and a weak one. <paramref name="weakMagnitude"/> is the strength of the weak motor (between 0 and 1) and <paramref name="strongMagnitude"/> is the strength of the strong motor (between 0 and 1). <paramref name="duration"/> is the duration of the effect in seconds (a duration of 0 will try to play the vibration indefinitely). The vibration can be stopped early by calling <see cref="Godot.Input.StopJoyVibration(int)"/>.</para>
    /// <para><b>Note:</b> Not every hardware is compatible with long effect durations; it is recommended to restart an effect if it has to be played for more than a few seconds.</para>
    /// <para><b>Note:</b> For macOS, vibration is only supported in macOS 11 and later.</para>
    /// <para><b>Note:</b> This does nothing for the keyboard device.</para>
    /// </summary>
    public void StartVibration(float weakMagnitude, float strongMagnitude, float duration = 0)
    {
        if (IsKeyboard) return;
        Input.StartJoyVibration(Device, weakMagnitude, strongMagnitude, duration);
    }

    /// <summary>
    /// <para>Stops the vibration of the joypad started with <see cref="Godot.Input.StartJoyVibration(int, float, float, float)"/>.</para>
    /// <para><b>Note:</b> This does nothing for the keyboard device.</para>
    /// </summary>
    public void StopVibration()
    {
        if (IsKeyboard) return;
        Input.StopJoyVibration(Device);
    }

    /// <summary>
    /// <para>Returns a value between 0 and 1 representing the raw intensity of the given action for this device, ignoring the action's deadzone. In most cases, you should use <see cref="Godot.Input.GetActionStrength(StringName, bool)"/> instead.</para>
    /// <para>If <paramref name="exactMatch"/> is <see langword="false"/>, it ignores additional input modifiers for <see cref="Godot.InputEventKey"/> and <see cref="Godot.InputEventMouseButton"/> events, and the direction for <see cref="Godot.InputEventJoypadMotion"/> events.</para>
    /// </summary>
    public float GetActionRawStrength(StringName action, bool exactMatch = false)
    {
        if (!IsDeviceConnected) return 0;

        return MultiplayerInput.GetActionRawStrength(Device, action, exactMatch);
    }

    /// <summary>
    /// <para>Returns a value between 0 and 1 representing the intensity of the given action for this device. In a joypad, for example, the further away the axis (analog sticks or L2, R2 triggers) is from the dead zone, the closer the value will be to 1. If the action is mapped to a control that has no axis such as the keyboard, the value returned will be 0 or 1.</para>
    /// <para>If <paramref name="exactMatch"/> is <see langword="false"/>, it ignores additional input modifiers for <see cref="Godot.InputEventKey"/> and <see cref="Godot.InputEventMouseButton"/> events, and the direction for <see cref="Godot.InputEventJoypadMotion"/> events.</para>
    /// </summary>
    public float GetActionStrength(StringName action, bool exactMatch = false)
    {
        if (!IsDeviceConnected) return 0;
        return MultiplayerInput.GetActionStrength(Device, action, exactMatch);
    }

    /// <summary>
    /// <para>Get axis input for this device by specifying two actions, one negative and one positive.</para>
    /// <para>This is a shorthand for writing <c>Input.get_action_strength("positive_action") - Input.get_action_strength("negative_action")</c>.</para>
    /// </summary>
    public float GetAxis(StringName negativeAction, StringName positiveAction)
    {
        if (!IsDeviceConnected) return 0;
        return MultiplayerInput.GetAxis(Device, negativeAction, positiveAction);
    }

    /// <summary>
    /// <para>Gets an input vector for this device by specifying four actions for the positive and negative X and Y axes.</para>
    /// <para>This method is useful when getting vector input, such as from a joystick, directional pad, arrows, or WASD. The vector has its length limited to 1 and has a circular deadzone, which is useful for using vector input as movement.</para>
    /// <para>By default, the deadzone is automatically calculated from the average of the action deadzones. However, you can override the deadzone to be whatever you want (on the range of 0 to 1).</para>
    /// </summary>
    public Vector2 GetVector(StringName negativeX, StringName positiveX, StringName negativeY, StringName positiveY, float deadzone = -1.0f)
    {
        if (!IsDeviceConnected) return Vector2.Zero;
        return MultiplayerInput.GetVector(Device, negativeX, positiveX, negativeY, positiveY, deadzone);
    }

    /// <summary>
    /// <para>Returns <see langword="true"/> when the user has <i>started</i> pressing the action event for this device in the current frame or physics tick. It will only return <see langword="true"/> on the frame or tick that the user pressed down the button.</para>
    /// <para>This is useful for code that needs to run only once when an action is pressed, instead of every frame while it's pressed.</para>
    /// <para>If <paramref name="exactMatch"/> is <see langword="false"/>, it ignores additional input modifiers for <see cref="Godot.InputEventKey"/> and <see cref="Godot.InputEventMouseButton"/> events, and the direction for <see cref="Godot.InputEventJoypadMotion"/> events.</para>
    /// <para><b>Note:</b> Returning <see langword="true"/> does not imply that the action is <i>still</i> pressed. An action can be pressed and released again rapidly, and <see langword="true"/> will still be returned so as not to miss input.</para>
    /// <para><b>Note:</b> Due to keyboard ghosting, <see cref="Godot.Input.IsActionJustPressed(StringName, bool)"/> may return <see langword="false"/> even if one of the action's keys is pressed. See <a href="$DOCS_URL/tutorials/inputs/input_examples.html#keyboard-events">Input examples</a> in the documentation for more information.</para>
    /// <para><b>Note:</b> During input handling (e.g. <see cref="Godot.Node._Input(InputEvent)"/>), use <see cref="Godot.InputEvent.IsActionPressed(StringName, bool, bool)"/> instead to query the action state of the current event.</para>
    /// </summary>
    public bool IsActionJustPressed(StringName action, bool exactMatch = false)
    {
        if (!IsDeviceConnected) return false;
        return MultiplayerInput.IsActionJustPressed(Device, action, exactMatch);
    }

    /// <summary>
    /// <para>Returns <see langword="true"/> when the user <i>stops</i> pressing the action event for this device in the current frame or physics tick. It will only return <see langword="true"/> on the frame or tick that the user releases the button.</para>
    /// <para><b>Note:</b> Returning <see langword="true"/> does not imply that the action is <i>still</i> not pressed. An action can be released and pressed again rapidly, and <see langword="true"/> will still be returned so as not to miss input.</para>
    /// <para>If <paramref name="exactMatch"/> is <see langword="false"/>, it ignores additional input modifiers for <see cref="Godot.InputEventKey"/> and <see cref="Godot.InputEventMouseButton"/> events, and the direction for <see cref="Godot.InputEventJoypadMotion"/> events.</para>
    /// <para><b>Note:</b> During input handling (e.g. <see cref="Godot.Node._Input(InputEvent)"/>), use <see cref="Godot.InputEvent.IsActionReleased(StringName, bool)"/> instead to query the action state of the current event.</para>
    /// </summary>
    public bool IsActionJustReleased(StringName action, bool exactMatch = false)
    {
        if (!IsDeviceConnected) return false;
        return MultiplayerInput.IsActionJustReleased(Device, action, exactMatch);
    }

    /// <summary>
    /// <para>Returns <see langword="true"/> if you are pressing the action event for this device.</para>
    /// <para>If <paramref name="exactMatch"/> is <see langword="false"/>, it ignores additional input modifiers for <see cref="Godot.InputEventKey"/> and <see cref="Godot.InputEventMouseButton"/> events, and the direction for <see cref="Godot.InputEventJoypadMotion"/> events.</para>
    /// <para><b>Note:</b> Due to keyboard ghosting, <see cref="Godot.Input.IsActionPressed(StringName, bool)"/> may return <see langword="false"/> even if one of the action's keys is pressed. See <a href="$DOCS_URL/tutorials/inputs/input_examples.html#keyboard-events">Input examples</a> in the documentation for more information.</para>
    /// </summary>
    public bool IsActionPressed(StringName action, bool exactMatch = false)
    {
        if (!IsDeviceConnected) return false;
        return MultiplayerInput.IsActionPressed(Device, action, exactMatch);
    }

    /// <summary>
    /// <para> Restricts actions that start with <c>ui_</c> to only work on this device. </para>
    /// <para> <b>Note:</b> this calls Reset(), so if you make any changes to the InputMap via code, you'll need to make them again. </para>
    /// </summary>
    /// <param name="device"></param>
    public void TakeUIActions()
    {
        if (!IsDeviceConnected) return;
        MultiplayerInput.SetUIActionDevice(Device);
    }

    // Internal method that is called whenever any device is connected or disconnected.
    // This is how this object keeps its "is_connected" property updated.
    private void OnJoyConnectionChanged(long device, bool connected)
    {
        if (device != Device) return;

        EmitSignal(SignalName.ConnectionChanged, connected);
        IsDeviceConnected = connected;
    }
}
