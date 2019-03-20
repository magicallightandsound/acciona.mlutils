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

using System.Collections;

namespace Acciona.MLUtils
{
	/// <summary>
	/// Implementation of MLURayPointer class to convert a Magic Leap controller into a ray pointer for canvas interaction. It requires MLUController component to process the input and 6DOF transform.
	/// </summary>
	[RequireComponent(typeof(MLUController))]
	public class MLUControllerPointer : MLURayPointer
	{
		[Tooltip("If an object is specified it will be set to the pointer's hit position (inactive when not hitting anything).")]
		public GameObject cursor;
		[Tooltip("If set, the LineRenderer will be used to render the current ray of the pointer.")]
		public LineRenderer lineRenderer;
		[Tooltip("Sensitivity of the touch pad scrolling.")]
		public float scrollDeltaSensitivity = 10.0F;
		[Tooltip("Inverts the scrolling input (grab vs move feedback).")]
		public bool invertedScroll = false;

		public override Vector3 Position { get { return controller.Position; } }
		public override Quaternion Rotation { get { return controller.Orientation; } }
		public override Vector2 ScrollDelta { get { return scrollDelta; } }
		
		private MLUController controller;
		private Vector2 scrollDelta;

		public override bool GetButtonDown (int buttonID)
		{
			switch (buttonID)
			{
				case 0: return controller.BumperPressed;
				case 1: return controller.TriggerPressed;
				default: return false;
			}
		}

		public override bool GetButtonUp (int buttonID)
		{
			switch (buttonID)
			{
				case 0: return controller.BumperReleased;
				case 1: return controller.TriggerReleased;
				default: return false;
			}
		}

		// scrolling coroutine
		private IEnumerator C_TouchScroll ()
		{
			Vector3 lastTouch = controller.Touch1;
			Vector3 currentTouch;

			while (controller.Touch1Down)
			{
				yield return null;

				currentTouch = controller.Touch1;
				scrollDelta = scrollDeltaSensitivity * (currentTouch - lastTouch) / Time.deltaTime;
				scrollDelta *= invertedScroll ? -1.0F : 1.0F;
				lastTouch = currentTouch;
			}

			scrollDelta = Vector2.zero;
		}

		void Awake ()
		{
			if (cursor != null) cursor.SetActive(false);
			
			if (lineRenderer != null)
			{
				lineRenderer.enabled = false;
				lineRenderer.useWorldSpace = true;
			}

			controller = GetComponent<MLUController>();

			if (controller == null)
			{
				Debug.LogError("MLUController component not found in GameObject " + gameObject.name + ". Disabling the script...");
				enabled = false;
			}
		}

		void OnEnable ()
		{
			lineRenderer.enabled = true;
		}

		void OnDisable ()
		{
			if (cursor != null) cursor.SetActive(false);
			
			if (lineRenderer != null)
			{
				lineRenderer.enabled = false;
				lineRenderer.useWorldSpace = true;
			}
		}

		void Update ()
		{
			Vector3 worldPosition = CurrentRaycast.worldPosition;

			if (cursor != null)
			{
				cursor.SetActive(IsPointing);

				if (IsPointing)
				{
					cursor.transform.position = worldPosition;
				}
			}

			if (lineRenderer != null)
			{
				if (IsPointing)
					lineRenderer.SetPositions(new Vector3[] { Position, worldPosition });
				else
					lineRenderer.SetPositions(new Vector3[] { Position, Position + 1000.0F * Forward });
			}

			if (controller.Touch1Pressed)
				StartCoroutine(C_TouchScroll());
		}
	}
}