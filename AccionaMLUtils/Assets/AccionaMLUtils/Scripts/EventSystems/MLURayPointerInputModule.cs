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
using UnityEngine.UI;
using UnityEngine.EventSystems;

using System;
using System.Reflection;
using System.Collections.Generic;

namespace Acciona.MLUtils
{
	/// <summary>
	/// Modification of Unity's StandaloneInputModule to support physical ray pointer input (input from VR/AR 6DOF controllers for example). Because the actual Unity EventSystems implementation is really poor, C# reflection will be used in some points to
	/// gain more control over the internal Unity logic.
	/// 
	/// IMPORTANT: since it implements StandaloneInputModule it seems that EventSystem won't activate the module in some platforms. Please check Force Module Active to ensure it works.
	/// </summary>
	public class MLURayPointerInputModule : StandaloneInputModule
	{
		private static List<BaseRaycaster> instancedRaycasters;
		private static Comparison<MLURaycastResult> s_RaycastComparer = RaycastComparer;
		
		/// <summary>
		/// The pointer to be used by this input module.
		/// </summary>
		[Space(10)]
		[Header("MLUtils Ray Pointer")]
		public MLURayPointer pointer;

		private Camera eventCamera;
		private readonly MouseState m_MouseState = new MouseState();
		private new readonly List<MLURaycastResult> m_RaycastResultCache = new List<MLURaycastResult>();

		public override void ActivateModule ()
		{
			if (pointer == null)
				Debug.LogWarning("MLURayPointerInputModule warning: pointer is not set, mouse input will be used instead...");

			// obtain a reference to instanced Raycasters list that gets automatically updated when new raycasters are added to the scene.
			// The static reference is contained in RaycasterManager internal class.
			if (instancedRaycasters == null)
			{
				Assembly assembly = typeof(BaseRaycaster).Assembly;
				Type type = assembly.GetType("UnityEngine.EventSystems.RaycasterManager");
				FieldInfo field = type.GetField("s_Raycasters", BindingFlags.NonPublic | BindingFlags.Static);
				instancedRaycasters = field.GetValue(null) as List<BaseRaycaster>;
			}

			if (eventCamera == null)
			{
				eventCamera = (new GameObject("EventCamera")).AddComponent<Camera>();
				eventCamera.enabled = false;
				eventCamera.orthographic = true;
				eventCamera.clearFlags = CameraClearFlags.SolidColor;
				eventCamera.backgroundColor = Color.clear;
				eventCamera.cullingMask = 0;
				eventCamera.orthographicSize = 1.0F;
				eventCamera.farClipPlane = 1.0F;

				eventCamera.transform.parent = transform;
				eventCamera.transform.localScale = Vector3.one;

				if (pointer != null)
				{
					eventCamera.transform.position = pointer.Position - pointer.Forward;
					eventCamera.transform.rotation = pointer.Rotation;
				}
			}

			base.ActivateModule();
		}

		public override void DeactivateModule ()
		{
			if (pointer != null)
			{
				pointer.SetPointing(null);
			}

			base.DeactivateModule();
		}

		void LateUpdate()
		{
			if (pointer != null && eventCamera != null)
			{
				// check event camera orientation and position are suitable for proyecting the ray hit on the imaginary screen
				float forwardAngle = Mathf.Abs(Vector3.Angle(eventCamera.transform.forward, pointer.Forward));
				float positionAngle = Mathf.Abs(Vector3.Angle(eventCamera.transform.forward, pointer.Position - eventCamera.transform.position));

				// seems that doing this instead of just following the ray on every frame solves some glitches on sliders
				if (forwardAngle > 60.0F || positionAngle > 60.0F)
				{
					// make the camera follow pointer
					eventCamera.transform.position = pointer.Position - pointer.Forward;
					eventCamera.transform.rotation = pointer.Rotation;
				}
			}
		}

		public override void Process()
		{
			if (pointer == null && Camera.main == null)
				return;
			if (pointer != null && !pointer.enabled)
				return;
			if (!eventSystem.isFocused && ShouldIgnoreEventsOnNoFocus())
				return;

			bool usedEvent = SendUpdateEventToSelectedObject();

			if (eventSystem.sendNavigationEvents)
			{
				if (!usedEvent)
					usedEvent |= SendMoveEventToSelectedObject();

				if (!usedEvent)
					SendSubmitEventToSelectedObject();
			}

			ProcessMouseEvent();
		}

		protected override MouseState GetMousePointerEventData(int id)
		{
			// Populate the left button...
			PointerEventData leftData;
			GetPointerData(kMouseLeftId, out leftData, true);

			leftData.Reset();

			if (pointer == null)
				leftData.scrollDelta = input.mouseScrollDelta;
			else
				leftData.scrollDelta = pointer.ScrollDelta;

			leftData.button = PointerEventData.InputButton.Left;

			Ray rayPointer;

			if (pointer == null)
				rayPointer = Camera.main.ScreenPointToRay(input.mousePosition); // emulate pointer with mouse if on PC and pointer is null
			else
				rayPointer = pointer.Ray;
			
			RayPointerRaycastAll(rayPointer, pointer == null ? Camera.main : eventCamera, leftData, m_RaycastResultCache);

			var raycast = FindFirstMLURaycast(m_RaycastResultCache);
			leftData.pointerCurrentRaycast = raycast.ToRaycastResult();

			if (!leftData.dragging)
			{
				leftData.position = raycast.screenPosition;
				leftData.delta = raycast.screenDelta;
			}
			else
			{
				Plane plane = new Plane(-leftData.pointerDrag.transform.forward, leftData.pointerDrag.transform.position);
				float dist;

				if (plane.Raycast(rayPointer, out dist))
				{
					Vector2 position = leftData.pressEventCamera.WorldToScreenPoint(rayPointer.GetPoint(dist));
					leftData.delta = position - leftData.position;
					leftData.position = position;
				}
			}

			if (pointer != null)
			{
				if (m_RaycastResultCache.Count > 0)
					pointer.SetPointing(raycast);
				else
					pointer.SetPointing(null);
			}

			m_RaycastResultCache.Clear();

			// copy the apropriate data into right and middle slots
			PointerEventData rightData;
			GetPointerData(kMouseRightId, out rightData, true);
			CopyFromTo(leftData, rightData);
			rightData.button = PointerEventData.InputButton.Right;

			PointerEventData middleData;
			GetPointerData(kMouseMiddleId, out middleData, true);
			CopyFromTo(leftData, middleData);
			middleData.button = PointerEventData.InputButton.Middle;

			m_MouseState.SetButtonState(PointerEventData.InputButton.Left, StateForMouseButton(0), leftData);
			m_MouseState.SetButtonState(PointerEventData.InputButton.Right, StateForMouseButton(1), rightData);
			m_MouseState.SetButtonState(PointerEventData.InputButton.Middle, StateForMouseButton(2), middleData);

			return m_MouseState;
		}

		protected new PointerEventData.FramePressState StateForMouseButton(int buttonId)
		{
			bool pressed, released;

			if (pointer == null)
			{
				pressed = input.GetMouseButtonDown(buttonId);
				released = input.GetMouseButtonUp(buttonId);
			}
			else
			{
				pressed = pointer.GetButtonDown(buttonId);
				released = pointer.GetButtonUp(buttonId);
			}

			if (pressed && released)
				return PointerEventData.FramePressState.PressedAndReleased;
			if (pressed)
				return PointerEventData.FramePressState.Pressed;
			if (released)
				return PointerEventData.FramePressState.Released;

			return PointerEventData.FramePressState.NotChanged;
		}

		private bool ShouldIgnoreEventsOnNoFocus()
		{
			switch (SystemInfo.operatingSystemFamily)
			{
				case OperatingSystemFamily.Windows:
				case OperatingSystemFamily.Linux:
				case OperatingSystemFamily.MacOSX:
#if UNITY_EDITOR
					if (UnityEditor.EditorApplication.isRemoteConnected)
						return false;
#endif
					return true;
				default:
					return false;
			}
		}

		protected static MLURaycastResult FindFirstMLURaycast(List<MLURaycastResult> candidates)
        {
            for (var i = 0; i < candidates.Count; ++i)
            {
                if (candidates[i].gameObject == null)
                    continue;

                return candidates[i];
            }
            return new MLURaycastResult();
        }
		
		// same as eventSystem.RaycastAll but uses RayPointerRaycast implementation and only performs with GraphicRaycaster instances
		public static void RayPointerRaycastAll(Ray rayPointer, Camera eventCamera, PointerEventData eventData, List<MLURaycastResult> raycastResults)
		{
			raycastResults.Clear();

			foreach (BaseRaycaster raycaster in instancedRaycasters)
				if (raycaster != null && raycaster is GraphicRaycaster && raycaster.IsActive() && raycaster.GetComponent<Canvas>().rootCanvas.renderMode == RenderMode.WorldSpace)
					MLUGraphicRaycasterUtility.RaycastRayPointer(rayPointer, raycaster as GraphicRaycaster, eventCamera, eventData, raycastResults);

			raycastResults.Sort(s_RaycastComparer);
		}

		// copy paste from EventSystem.cs original code since its private and i need it
		private static int RaycastComparer(MLURaycastResult lhs, MLURaycastResult rhs)
        {
            if (lhs.module != rhs.module && lhs.rootCanvas != rhs.rootCanvas)
            {
				if (lhs.distance < rhs.distance)
					return -1;
				if (lhs.distance > rhs.distance)
					return 1;
				
				return 0;
            }

            if (lhs.sortingLayer != rhs.sortingLayer)
            {
                // Uses the layer value to properly compare the relative order of the layers.
                var rid = SortingLayer.GetLayerValueFromID(rhs.sortingLayer);
                var lid = SortingLayer.GetLayerValueFromID(lhs.sortingLayer);
                return rid.CompareTo(lid);
            }

            if (lhs.sortingOrder != rhs.sortingOrder)
                return rhs.sortingOrder.CompareTo(lhs.sortingOrder);

            if (lhs.depth != rhs.depth)
                return rhs.depth.CompareTo(lhs.depth);

            if (lhs.distance != rhs.distance)
                return lhs.distance.CompareTo(rhs.distance);

            return lhs.index.CompareTo(rhs.index);
        }
	}
}