/*
MIT License

Copyright (c) 2019 ACCIONA S.A.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using UnityEngine;
using UnityEngine.XR.MagicLeap;

namespace Acciona.MLUtils
{
	public enum ButtonState
	{
		Up, // default state
		Pressed, // just pressed this frame
		Down, // maintained pressed
		Released // just released this frame (then it goes to up state)
	}
	
	/// <summary>
	/// Component that exposes Magic Leap controller input in a more convenient way with extra information like frame button state (up, down, pressed...) and touch pad interactions.
	/// Doesn't require any other component to work. It will automatically stay connected to the Magic Leap controller everytime it is active.
	/// If no ControllerTransform component (from Magic Leap package) is present on the GameObject, this script will manually apply the 6DOF transformation.
	/// </summary>
	public class MLUController : MonoBehaviour
	{
		private const int ML_MAX_CONTROLLER_ID_SEARCH = 8;
		private const int ML_BUTTONS_COUNT = 16;

		[Tooltip("When trigger value is equal or greater than this value it will be considered pressed.")]
		[Range(0.0F, 1.0F)]
		public float triggerDownThreshold = 0.8F;
		[Tooltip("When interpreting touchpad as a D-Pad this defines the radius of the center button.")]
		[Range(0.0F, 1.0F)]
		public float touchpadDirCenterRadius = 0.3F;

		/// <summary>
		/// Reference to the current MLInputController being used to expose the input.
		/// </summary>
		public MLInputController InputController { get { return controller; } }
		public bool IsConnected { get { return controller != null; } }

		/// <summary>
		/// Current value of the trigger.
		/// </summary>
		public float TriggerValue { get { return IsConnected ? controller.TriggerValue : 0.0F; } }

		/// <summary>
		/// Current 6DOF world position of the controller (requires ControllerPose privilege).
		/// </summary>
		public Vector3 Position { get { return IsConnected ? controller.Position : Vector3.zero; } }
		/// <summary>
		/// Current 6DOF world orientation of the controller (requires ControllerPose privilege).
		/// </summary>
		public Quaternion Orientation { get { return IsConnected ? controller.Orientation : Quaternion.identity; } }

		/// <summary>
		/// Intepreting trigger as a button (based on specified triggerDownThreshold), returns true if the trigger is currently not "pressed" on this frame.
		/// </summary>
		public bool TriggerUp { get { return TriggerValue < triggerDownThreshold; } }
		/// <summary>
		/// Intepreting trigger as a button (based on specified triggerDownThreshold), returns true if the trigger was pressed down on this frame (just one frame).
		/// </summary>
		public bool TriggerPressed { get { return TriggerValue >= triggerDownThreshold && !triggerDown; } }
		/// <summary>
		/// Intepreting trigger as a button (based on specified triggerDownThreshold), returns true if the trigger is currently pressed on this frame.
		/// </summary>
		public bool TriggerDown { get { return (TriggerValue >= triggerDownThreshold && triggerDown) || TriggerPressed; } }
		/// <summary>
		/// Intepreting trigger as a button (based on specified triggerDownThreshold), returns true if the trigger was released on this frame (just one frame).
		/// </summary>
		public bool TriggerReleased { get { return TriggerValue < triggerDownThreshold && triggerDown; } }

		/// <summary>
		/// Returns Touch1PosAndForce vector from MLInputController, x and y are ranged from -1 to 1 being 0 the center of the touchpad and z is ranged from 0 to 1 being 1 the max pressing force.
		/// </summary>
		public Vector3 Touch1 { get { return IsConnected ? controller.Touch1PosAndForce : Vector3.zero; } }
		/// <summary>
		/// Returns true if the touchpad is not currently touched.
		/// </summary>
		public bool Touch1Up { get { return IsConnected && !controller.Touch1Active; } }
		/// <summary>
		/// Returns true if a touch started on this frame (just one frame).
		/// </summary>
		public bool Touch1Pressed { get { return IsConnected && controller.Touch1Active && !touch1Active; } }
		/// <summary>
		/// Returns true if the touchpad is currently being touched.
		/// </summary>
		public bool Touch1Down { get { return IsConnected && ((controller.Touch1Active && touch1Active) || Touch1Pressed); } }
		/// <summary>
		/// Returns true if touch has been released this frame (just one frame)
		/// </summary>
		public bool Touch1Released { get { return IsConnected && !controller.Touch1Active && touch1Active; } }

		/// <summary>
		/// Returns current touch position angle (connecting touch pos with center). Ranges from 0 to 360 being 0 right, 90 up, 180 left and 270 down.
		/// </summary>
		public float Touch1Angle { get { return GetTouchAngle(Touch1);} }
		/// <summary>
		/// Interpreting the touchpad as a D-Pad, returns true if touch is currently on the center (based on touchpadDirCenterRadius).
		/// </summary>
		public bool Touch1DirCenter { get { return ((Vector2) Touch1).magnitude <= touchpadDirCenterRadius; } }
		/// <summary>
		/// Interpreting the touchpad as a D-Pad (dividing each side by 90 degrees), returns true if touch is currently right.
		/// </summary>
		public bool Touch1DirRight { get { if (Touch1DirCenter) return false; float angle = GetTouchAngle(Touch1); return angle >= 315 || angle < 45; } }
		/// <summary>
		/// Interpreting the touchpad as a D-Pad (dividing each side by 90 degrees),returns true if touch is currently up.
		/// </summary>
		public bool Touch1DirUp { get { if (Touch1DirCenter) return false; float angle = GetTouchAngle(Touch1); return angle >= 45 && angle < 135; } }
		/// <summary>
		/// Interpreting the touchpad as a D-Pad (dividing each side by 90 degrees), returns true if touch is currently left.
		/// </summary>
		public bool Touch1DirLeft { get { if (Touch1DirCenter) return false; float angle = GetTouchAngle(Touch1); return angle >= 135 && angle < 225; } }
		/// <summary>
		/// Interpreting the touchpad as a D-Pad (dividing each side by 90 degrees), returns true if touch is currently right.
		/// </summary>
		public bool Touch1DirDown { get { if (Touch1DirCenter) return false; float angle = GetTouchAngle(Touch1); return angle >= 225 && angle < 315; } }

		// I currently don't know why MLInputController has "Touch2", but I still implemented the same functionality as with touch1 input
		public Vector3 Touch2 { get { return IsConnected ? controller.Touch2PosAndForce : Vector3.zero; } }
		public bool Touch2Up { get { return IsConnected && !controller.Touch2Active; } }
		public bool Touch2Pressed { get { return IsConnected && controller.Touch2Active && !touch2Active; } }
		public bool Touch2Down { get { return IsConnected && (controller.Touch2Active && touch2Active) || Touch2Pressed; } }
		public bool Touch2Released { get { return IsConnected && !controller.Touch2Active && touch2Active; } }

		public float Touch2Angle { get { return GetTouchAngle(Touch2);} }
		public bool Touch2DirCenter { get { return ((Vector2) Touch2).magnitude <= touchpadDirCenterRadius; } }
		public bool Touch2DirRight { get { if (Touch2DirCenter) return false; float angle = GetTouchAngle(Touch2); return angle >= 315 || angle < 45; } }
		public bool Touch2DirUp { get { if (Touch2DirCenter) return false; float angle = GetTouchAngle(Touch2); return angle >= 45 && angle < 135; } }
		public bool Touch2DirLeft { get { if (Touch2DirCenter) return false; float angle = GetTouchAngle(Touch2); return angle >= 135 && angle < 225; } }
		public bool Touch2DirDown { get { if (Touch2DirCenter) return false; float angle = GetTouchAngle(Touch2); return angle >= 225 && angle < 315; } }

		/// <summary>
		/// Returns true if bumper button is currently not pressed.
		/// </summary>
		public bool BumperUp { get { return IsButtonUp(MLInputControllerButton.Bumper); } }
		/// <summary>
		/// Returns true if bumper button was pressed down on this frame (just one frame).
		/// </summary>
		public bool BumperPressed { get { return IsButtonPressed(MLInputControllerButton.Bumper); } }
		/// <summary>
		/// Returns true if bumper button is currently pressed on this frame.
		/// </summary>
		public bool BumperDown { get { return IsButtonDown(MLInputControllerButton.Bumper); } }
		/// <summary>
		/// Returns true if bumper button was released during this frame (just one frame).
		/// </summary>
		public bool BumperReleased { get { return IsButtonReleased(MLInputControllerButton.Bumper); } }
		
		/// <summary>
		/// Returns true if home button is currently not pressed. WARNING: this is currently not working as espected, I think it's because LuminOS takes control of the home press to check application quit.
		/// </summary>
		public bool HomeUp { get { return IsButtonUp(MLInputControllerButton.HomeTap); } }
		/// <summary>
		/// Returns true if home button was pressed down on this frame (just one frame). WARNING: this is currently not working as espected, I think it's because LuminOS takes control of the home press to check application quit.
		/// </summary>
		public bool HomePressed { get { return IsButtonPressed(MLInputControllerButton.HomeTap); } }
		/// <summary>
		/// Returns true if home button is currently pressed on this frame. WARNING: this is currently not working as espected, I think it's because LuminOS takes control of the home press to check application quit.
		/// </summary>
		public bool HomeDown { get { return IsButtonDown(MLInputControllerButton.HomeTap); } }
		/// <summary>
		/// Returns true if home button was released during this frame (just one frame). WARNING: this is currently not working as espected, I think it's because LuminOS takes control of the home press to check application quit.
		/// </summary>
		public bool HomeReleased { get { return IsButtonReleased(MLInputControllerButton.HomeTap); } }

		private MLInputController controller;
		private readonly ButtonState[] buttons = new ButtonState[ML_BUTTONS_COUNT];
		private bool touch1Active, touch2Active, triggerDown;
		private MagicLeap.ControllerTransform controllerTransformComponent;
		
		/// <summary>
		/// Gets current button state (up, pressed, down or released) for the given MLInputControllerButton
		/// </summary>
		public ButtonState GetButtonState (MLInputControllerButton button)
		{
			return GetButtonState((int) button);
		}

		/// <summary>
		/// Gets current button state (up, pressed, down or released) for the given MLInputControllerButton as integer.
		/// </summary>
		public ButtonState GetButtonState (int button)
		{
			return buttons[(int) button];
		}

		/// <summary>
		/// Returns true if given MLInputControllerButton is currently not pressed on this frame.
		/// </summary>
		public bool IsButtonUp (MLInputControllerButton button)
		{
			return buttons[(int) button] == ButtonState.Up || buttons[(int) button] == ButtonState.Released;
		}

		/// <summary>
		/// Returns true if given MLInputControllerButton has been pressed during this frame (just one frame).
		/// </summary>
		public bool IsButtonPressed (MLInputControllerButton button)
		{
			return buttons[(int) button] == ButtonState.Pressed;
		}

		/// <summary>
		/// Returns true if given MLInputControllerButton is currently pressed on this frame.
		/// </summary>
		public bool IsButtonDown (MLInputControllerButton button)
		{
			return buttons[(int) button] == ButtonState.Down || buttons[(int) button] == ButtonState.Pressed;
		}

		/// <summary>
		/// Returns true if given MLInputControllerButton has been released during this frame (just one frame).
		/// </summary>
		public bool IsButtonReleased (MLInputControllerButton button)
		{
			return buttons[(int) button] == ButtonState.Released;
		}

		private void Awake ()
		{
			if (!MLInput.IsStarted)
			{
				MLInputConfiguration config = new MLInputConfiguration
				(
					MLInputConfiguration.DEFAULT_TRIGGER_DOWN_THRESHOLD,
					MLInputConfiguration.DEFAULT_TRIGGER_UP_THRESHOLD,
					true
				);

				MLResult result = MLInput.Start(config);

				if (!result.IsOk)
				{
					Debug.LogError("Error: MLController failed starting MLInput, disabling script. Reason: " + result);
					enabled = false;
					return;
				}
			}

			for (int i = 0; i < buttons.Length; i++)
				buttons[i] = ButtonState.Up;
			
			controllerTransformComponent = GetComponent<MagicLeap.ControllerTransform>();
		}

		private void OnEnable ()
		{
			FindCurrentController();
			RegisterHandlers();
		}

		private void OnDisable ()
		{
			controller = null;
			UnregisterHandlers();
		}

		private void OnDestroy ()
		{
			if (MLInput.IsStarted)
            {
                UnregisterHandlers();
				MLInput.Stop();
            }
		}

		private void Update ()
		{
			// if the GameObject does not have the Magic Leap's ControllerTransform component apply manually the controller's 6DOF transform.
			if (controllerTransformComponent == null && IsConnected && controller.Type == MLInputControllerType.Control)
			{
				transform.position = controller.Position;
				transform.rotation = controller.Orientation;
			}
		}

		// takes care of button state updates per frame
		private void LateUpdate ()
		{
			for (int i = 0; i < buttons.Length; i++)
			{
				if (IsConnected)
				{
					switch (buttons[i])
					{
						case ButtonState.Pressed: buttons[i] = ButtonState.Down; break;
						case ButtonState.Released: buttons[i] = ButtonState.Up; break;
					}
				}
				else
					buttons[i] = ButtonState.Up;
			}

			touch1Active = IsConnected && controller.Touch1Active;
			touch2Active = IsConnected && controller.Touch2Active;
			triggerDown = IsConnected && controller.TriggerValue >= triggerDownThreshold;
		}

		private void FindCurrentController ()
		{
			for (int i = 0; i < ML_MAX_CONTROLLER_ID_SEARCH; i++)
				if ((controller = MLInput.GetController(i)) != null)
					break;
			
			if (controller == null)
				Debug.Log("No connected ML controller was found");
			else
				Debug.Log("ML controller with ID: " + controller.Id + " connected!");
		}

		private void RegisterHandlers ()
		{
			MLInput.OnControllerConnected += HandleControllerConnected;
			MLInput.OnControllerDisconnected += HandleControllerDisconnected;
			MLInput.OnControllerButtonDown += HandleButtonDown;
			MLInput.OnControllerButtonUp += HandleButtonUp;
		}

		private void UnregisterHandlers ()
		{
			MLInput.OnControllerConnected -= HandleControllerConnected;
			MLInput.OnControllerDisconnected -= HandleControllerDisconnected;
			MLInput.OnControllerButtonDown -= HandleButtonDown;
			MLInput.OnControllerButtonUp -= HandleButtonUp;
		}

		private void HandleControllerConnected (byte controllerID)
		{
			if (controller == null)
			{
				controller = MLInput.GetController(controllerID);
				Debug.Log("ML controller with ID: " + controller.Id + " connected!");
			}
		}

		private void HandleControllerDisconnected (byte controllerID)
		{
			if (controller.Id == controllerID)
			{
				Debug.Log("ML controller with ID: " + controller.Id + " disconnected!");
				controller = null;
			}
		}

		private void HandleButtonDown (byte controllerID, MLInputControllerButton button)
		{
			if (IsConnected && controller.Id == controllerID)
				buttons[(int) button] = ButtonState.Pressed;
		}

		private void HandleButtonUp (byte controllerID, MLInputControllerButton button)
		{
			if (IsConnected && controller.Id == controllerID)
					buttons[(int) button] = ButtonState.Released;
		}

		private float GetTouchAngle (Vector3 touch)
		{
			float angle = Vector2.SignedAngle(Vector2.right, touch);
			angle = angle < 0 ? 360 + angle : angle;

			return angle;
		}
	}
}