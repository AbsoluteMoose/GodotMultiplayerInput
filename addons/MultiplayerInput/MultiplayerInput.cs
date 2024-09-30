using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiplayerInputSystem;

/// <summary>
/// <para>A globally accessible manager for device-specific actions.</para>
///
/// <para>This class automatically duplicates relevant events on all actions for new joypads
/// when they connect and disconnect.</para>
/// <para>It also provides a nice API to access all the normal <see cref="Input"/> methods,
/// but using the device integers and the same action names.</para>
/// <para>All methods in this class that have a "device" parameter can accept <c>-1</c>
/// which means the keyboard device.</para>
/// 
/// <para><b>Note:</b> The <c>-1</c> device will not work on Input methods because it is a specific
/// concept to this MultiplayerInput class.</para>
/// 
/// <para>See <seealso cref="DeviceInput"/> for an object-oriented way to get input for a single device.</para>
/// </summary>
public partial class MultiplayerInput : Node
{
	// Gets a static reference to the singleton through the Engine singleton.
	// WARNING: This will fail if the main loop has been changed.
	public static MultiplayerInput Singleton => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<MultiplayerInput>("/root/MultiplayerInput");

    // An array of all the non-duplicated action names
    protected StringName[] CoreActions;

    // A dictionary of all action names
    // The keys are the device numbers
    // The values are a dictionary that maps action name to device action name
    // For example device_actions[device][action_name] is the device-specific action name
    // The purpose of this is to cache all the StringNames of all the actions
    // ... so it doesn't need to generate them every time
    protected readonly Dictionary<int, Dictionary<StringName, StringName>> DeviceActions = new();

	/// <summary>
	/// <value>A list of device GUIDs to be ignored when creating device-specific actions.</value>
	/// </summary>
	public Godot.Collections.Array<StringName> IgnoredGuids = new();

	public MultiplayerInput()
	{
		Reset();

        // Create actions for gamepads that connect in the future
        // Also clean up when gamepads disconnect
        Input.JoyConnectionChanged += OnJoyConnectionChanged;
    }

	/// <summary>
	/// Resets all device-specific actions. Call this if any of the original input actions are changed.
	/// </summary>
	public void Reset() 
	{
        InputMap.LoadFromProjectSettings();

        CoreActions = InputMap.GetActions().ToArray();

		// Disable joypad events on keyboard actions
		// by setting device id to 8 (out of range, so they'll never trigger)
		// I can't just delete them because they're used as blueprints
		// ... when a joypad connects
		// This skips UI actions so it doesn't mess them up.
		foreach (StringName action in CoreActions)
		{
			foreach (InputEvent e in InputMap.ActionGetEvents(action))
            {
                if (IsJoypadEvent(e) && !IsUiAction(action))
                {
                    e.Device = 8;
				}
			}
        }
    }

	private void OnJoyConnectionChanged(long device, bool connected)
	{
		if (connected) CreateActionsForDevice((int)device);
		else DeleteActionsForDevice((int)device);
	}

	private void CreateActionsForDevice(int device)
	{
		GD.Print("Made actions for device " + device);
		// Skip action creation if the device should be ignored
		if (IgnoredGuids.Contains(Input.GetJoyGuid(device))) return;

		Dictionary<StringName, StringName> actions = new();
		foreach (StringName coreAction in CoreActions)
		{
			StringName newAction = device + coreAction;
			float deadzone = InputMap.ActionGetDeadzone(coreAction);

			// Get all joypad events for this action
			IEnumerable<InputEvent> events = InputMap.ActionGetEvents(coreAction).Where(IsJoypadEvent);

			// Only copy this event if it is relevant to joypads
			if (events.Any()) 
			{
				//first add the action with the new name
				InputMap.AddAction(newAction, deadzone);

				actions.Add(coreAction, newAction);
			}

			// Then copy all the events associated with that action
			// This only includes events that are relevant to joypads
			foreach (InputEvent @event in events)
			{
				// Without duplicating, all of them have a reference to the same event object
				// which doesn't work because this has to be unique to this device
				InputEvent newEvent = @event.Duplicate() as InputEvent;
				newEvent.Device = device;

				// Switch the device to be just this joypad
				InputMap.ActionAddEvent(newAction, newEvent);
			}
		}

		DeviceActions.Add(device, actions);
	}

	private void DeleteActionsForDevice(int device)
	{
		DeviceActions.Remove(device);
		List<StringName> actionsToErase = new();
		string deviceNumStr = device.ToString();

		// Figure out which actions should be erased
		foreach (StringName action in InputMap.GetActions())
		{
			string maybeDevice = action.ToString().Substr(0, deviceNumStr.Length);
			if (maybeDevice == deviceNumStr) actionsToErase.Add(action);
		}

		// Now actually erase them
		// This is done separately so I'm not erasing from the collection I'm looping on
		// Not sure if this is necessary but whatever, this is safe
		foreach (StringName action in actionsToErase) InputMap.EraseAction(action);
	}


    // Use these functions to query the action states just like normal Input functions

    /// <summary>
    /// <para>Returns a value between 0 and 1 representing the raw intensity of the given action for this device, ignoring the action's deadzone. In most cases, you should use <see cref="Godot.Input.GetActionStrength(StringName, bool)"/> instead.</para>
    /// <para>If <paramref name="exactMatch"/> is <see langword="false"/>, it ignores additional input modifiers for <see cref="Godot.InputEventKey"/> and <see cref="Godot.InputEventMouseButton"/> events, and the direction for <see cref="Godot.InputEventJoypadMotion"/> events.</para>
    /// </summary>
    public static float GetActionRawStrength(int device, StringName action, bool exactMatch = false)
	{
		if (device >= 0) action = GetActionName(device, action);
        return Input.GetActionRawStrength(action, exactMatch);
    }

    /// <summary>
    /// <para>Returns a value between 0 and 1 representing the intensity of the given action for this device. In a joypad, for example, the further away the axis (analog sticks or L2, R2 triggers) is from the dead zone, the closer the value will be to 1. If the action is mapped to a control that has no axis such as the keyboard, the value returned will be 0 or 1.</para>
    /// <para>If <paramref name="exactMatch"/> is <see langword="false"/>, it ignores additional input modifiers for <see cref="Godot.InputEventKey"/> and <see cref="Godot.InputEventMouseButton"/> events, and the direction for <see cref="Godot.InputEventJoypadMotion"/> events.</para>
    /// </summary>
	public static float GetActionStrength(int device, StringName action, bool exactMatch = false)
	{ 
		if (device >= 0) action = GetActionName(device, action);
		return Input.GetActionStrength(action, exactMatch);
	}

    /// <summary>
    /// <para>Get axis input for this device by specifying two actions, one negative and one positive.</para>
    /// <para>This is a shorthand for writing <c>Input.get_action_strength("positive_action") - Input.get_action_strength("negative_action")</c>.</para>
    /// </summary>
    public static float GetAxis(int device, StringName negativeAction, StringName positiveAction)
	{
		if (device >= 0)
		{
			negativeAction = GetActionName(device, negativeAction);
			positiveAction = GetActionName(device, positiveAction);
        }

		return Input.GetAxis(negativeAction, positiveAction);
	}

    /// <summary>
    /// <para>Gets an input vector for this device by specifying four actions for the positive and negative X and Y axes.</para>
    /// <para>This method is useful when getting vector input, such as from a joystick, directional pad, arrows, or WASD. The vector has its length limited to 1 and has a circular deadzone, which is useful for using vector input as movement.</para>
    /// <para>By default, the deadzone is automatically calculated from the average of the action deadzones. However, you can override the deadzone to be whatever you want (on the range of 0 to 1).</para>
    /// </summary>
    public static Vector2 GetVector(int device, StringName negativeX, StringName positiveX, StringName negativeY, StringName positiveY, float deadzone = -1.0f)
	{
		if (device >= 0)
        {
			negativeX = GetActionName(device, negativeX);
			positiveX = GetActionName(device, positiveX);
			negativeY = GetActionName(device, negativeY);
			positiveY = GetActionName(device, positiveY);
        }

		return Input.GetVector(negativeX, positiveX, negativeY, positiveY, deadzone);
	}

    /// <summary>
    /// <para>Returns <see langword="true"/> when the user has <i>started</i> pressing the action event for this device in the current frame or physics tick. It will only return <see langword="true"/> on the frame or tick that the user pressed down the button.</para>
    /// <para>This is useful for code that needs to run only once when an action is pressed, instead of every frame while it's pressed.</para>
    /// <para>If <paramref name="exactMatch"/> is <see langword="false"/>, it ignores additional input modifiers for <see cref="Godot.InputEventKey"/> and <see cref="Godot.InputEventMouseButton"/> events, and the direction for <see cref="Godot.InputEventJoypadMotion"/> events.</para>
    /// <para><b>Note:</b> Returning <see langword="true"/> does not imply that the action is <i>still</i> pressed. An action can be pressed and released again rapidly, and <see langword="true"/> will still be returned so as not to miss input.</para>
    /// <para><b>Note:</b> Due to keyboard ghosting, <see cref="Godot.Input.IsActionJustPressed(StringName, bool)"/> may return <see langword="false"/> even if one of the action's keys is pressed. See <a href="$DOCS_URL/tutorials/inputs/input_examples.html#keyboard-events">Input examples</a> in the documentation for more information.</para>
    /// <para><b>Note:</b> During input handling (e.g. <see cref="Godot.Node._Input(InputEvent)"/>), use <see cref="Godot.InputEvent.IsActionPressed(StringName, bool, bool)"/> instead to query the action state of the current event.</para>
    /// </summary>
    public static bool IsActionJustPressed(int device, StringName action, bool exactMatch = false)
    {
        if (device >= 0) action = GetActionName(device, action);
		return Input.IsActionJustPressed(action, exactMatch);
    }

    /// <summary>
    /// <para>Returns <see langword="true"/> when the user <i>stops</i> pressing the action event for this device in the current frame or physics tick. It will only return <see langword="true"/> on the frame or tick that the user releases the button.</para>
    /// <para><b>Note:</b> Returning <see langword="true"/> does not imply that the action is <i>still</i> not pressed. An action can be released and pressed again rapidly, and <see langword="true"/> will still be returned so as not to miss input.</para>
    /// <para>If <paramref name="exactMatch"/> is <see langword="false"/>, it ignores additional input modifiers for <see cref="Godot.InputEventKey"/> and <see cref="Godot.InputEventMouseButton"/> events, and the direction for <see cref="Godot.InputEventJoypadMotion"/> events.</para>
    /// <para><b>Note:</b> During input handling (e.g. <see cref="Godot.Node._Input(InputEvent)"/>), use <see cref="Godot.InputEvent.IsActionReleased(StringName, bool)"/> instead to query the action state of the current event.</para>
    /// </summary>
    public static bool IsActionJustReleased(int device, StringName action, bool exactMatch = false)
    {
		if (device >= 0) action = GetActionName(device, action);
		return Input.IsActionJustReleased(action, exactMatch);
    }

    /// <summary>
    /// <para>Returns <see langword="true"/> if you are pressing the action event for this device.</para>
    /// <para>If <paramref name="exactMatch"/> is <see langword="false"/>, it ignores additional input modifiers for <see cref="Godot.InputEventKey"/> and <see cref="Godot.InputEventMouseButton"/> events, and the direction for <see cref="Godot.InputEventJoypadMotion"/> events.</para>
    /// <para><b>Note:</b> Due to keyboard ghosting, <see cref="Godot.Input.IsActionPressed(StringName, bool)"/> may return <see langword="false"/> even if one of the action's keys is pressed. See <a href="$DOCS_URL/tutorials/inputs/input_examples.html#keyboard-events">Input examples</a> in the documentation for more information.</para>
    /// </summary>
    public static bool IsActionPressed(int device, StringName action, bool exactMatch = false)
    {
		if (device >= 0) action = GetActionName(device, action);
		return Input.IsActionPressed(action, exactMatch);
    }

	/// <summary>
	/// Returns the name of a device-specific action.
	/// </summary>
	/// <param name="device"></param>
	/// <param name="action"></param>
	/// <returns></returns>
	/// <exception cref="ApplicationException"></exception>
	public static StringName GetActionName(int device, StringName action)
    {
        if (device >= 0)
        {
			if (!Singleton.DeviceActions.TryGetValue(device, out Dictionary<StringName, StringName> value)) 
			{ 
				throw new ApplicationException("Device " + device + " has no actions. Maybe the joypad is disconnected."); 
			}
			//if it says this dictionary doesn't have the key,
			//that could mean it's an invalid action name.
			//or it could mean that action doesn't have a joypad event assigned
			return value[action];
        }

		//return the normal action name for the keyboard player
		return action;
	}

    /// <summary>
    /// <para> Restricts actions that start with <c>ui_</c> to only work on a single device. </para>
    /// <para> Pass a <c>-2</c> to reset it back to default behavior (allow all devices to trigger UI actions). </para>
    /// <para> For example, pass a <c>-1</c> if you want only the keyboard/mouse device to control menus. </para>
    /// <para> <b>Note:</b> this calls Reset(), so if you make any changes to the InputMap via code, you'll need to make them again. </para>
    /// </summary>
    /// <param name="device"></param>
    public static void SetUIActionDevice(int device)
	{
		// First, totally re-create the InputMap for all devices
		// This is necessary because this function may have messed up the UI Actions
		// ... on a previous call
		Singleton.Reset();

		// We are back to default behavior.
		// So if that's what the caller wants, we're done!
		if (device == -2) return;

		// find all ui actions and erase irrelevant events
		foreach (StringName action in InputMap.GetActions())
		{
			// ignore non-ui-actions
			if (!IsUiAction(action)) continue;

			if (device == -1)
			{
				// in this context, we want to erase all joypad events
				foreach (InputEvent e in InputMap.ActionGetEvents(action))
				{
					if (IsJoypadEvent(e)) InputMap.ActionEraseEvent(action, e);
				}
			}
			else
			{
				// in this context, we want to delete all non-joypad events.
				// and we also want to set the event's device to the given device.
				foreach (InputEvent e in InputMap.ActionGetEvents(action))
				{
					if (IsJoypadEvent(e))
					{
						e.Device = device;
					}
					// this isn't event a joypad event, so erase it entirely
					else InputMap.ActionEraseEvent(action, e);
				}
			}
		}
    }

    /// <summary>
    /// <para> Returns true if the given event is a joypad event.</para>
    /// </summary>
    /// <param name="event"></param>
    /// <returns></returns>
    private static bool IsJoypadEvent(InputEvent @event) => @event is InputEventJoypadButton or InputEventJoypadMotion;

    /// <summary>
    /// <para> Returns true if the given action is a UI action.</para>
    /// <para> By default this means it starts with <c>ui_</c>, but this can be overriden.</para>
	/// </summary>
	/// <param name="actionName"></param>
	/// <returns></returns>
    protected static bool IsUiAction(StringName actionName) => ((string)actionName).StartsWith("ui_");
}
