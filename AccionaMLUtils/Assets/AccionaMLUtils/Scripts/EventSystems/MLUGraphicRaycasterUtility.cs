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

using System.Reflection;
using System.Collections.Generic;

using BlockingObjects = UnityEngine.UI.GraphicRaycaster.BlockingObjects;

namespace Acciona.MLUtils
{
	/// <summary>
	/// Contains modified versions of Unity's GraphicRaycaster raycasting methods to support physical ray pointer for Input Modules.
	/// It's static and needs reference to an actual GraphicRaycaster instance to work.
	/// </summary>
	public static class MLUGraphicRaycasterUtility
	{
		private static List<Graphic> s_RaycastResults = new List<Graphic>();
		
		private static FieldInfo blockingMaskInfo;
		private static FieldInfo BlockingMaskInfo { get { return blockingMaskInfo ?? (blockingMaskInfo = typeof(GraphicRaycaster).GetField("m_BlockingMask", BindingFlags.NonPublic | BindingFlags.Instance)); } }

		/// <summary>
		/// Performs the given GraphicRaycaster raycast taking the given ray as if it were a world pointer instead of screen mouse input. Event camera must be viewing the whole ray for this modification to work.
		/// </summary>
		/// <param name="rayPointer">ray represeting a physical pointer that may point into the raycaster's canvas</param>
		/// <param name="raycaster">the configuration of this raycaster will be used to perform the raycast</param>
		/// <param name="eventData">Current event data</param>
		/// <param name="resultAppendList">List of hit objects to append new results to.</param>
		public static void RaycastRayPointer (Ray rayPointer, GraphicRaycaster raycaster, Camera eventCamera, PointerEventData eventData, List<MLURaycastResult> resultAppendList)
		{
			Canvas canvas = raycaster.GetComponent<Canvas>();

			if (canvas == null || eventCamera == null) return;
			canvas.worldCamera = eventCamera; // this kind of raycasting requires all canvases to use the same eventCamera
			
			if (canvas.renderMode != RenderMode.WorldSpace)
			{
				Debug.LogError("GraphicRaycaster's canvas must be in world space render mode when using pointer ray raycasting. Disabling GraphicRaycaster...");
				raycaster.enabled = false;
				return;
			}

			var canvasGraphics = GraphicRegistry.GetGraphicsForCanvas(canvas);
			if (canvasGraphics == null || canvasGraphics.Count == 0)
				return;

			Plane canvasPlane = new Plane(-canvas.transform.forward, canvas.transform.position);
			float dist;
			if (!canvasPlane.Raycast(rayPointer, out dist))	return;
			Vector2 screenPointerPos = eventCamera.WorldToScreenPoint(rayPointer.GetPoint(dist));

			Vector2 previousPosition = eventData.position;
			Vector2 previousDelta = eventData.delta;
			eventData.delta = screenPointerPos - eventData.position;
			eventData.position = screenPointerPos;

			float hitDistance = float.MaxValue;

			// obtain m_BlockingMask field from through reflection
			LayerMask m_BlockingMask = (LayerMask) BlockingMaskInfo.GetValue(raycaster);

			if (raycaster.blockingObjects != BlockingObjects.None)
			{
				float distanceToClipPlane;
				canvasPlane.Raycast(rayPointer, out distanceToClipPlane);

				if (raycaster.blockingObjects == BlockingObjects.ThreeD || raycaster.blockingObjects == BlockingObjects.All)
				{
					var hits = Physics.RaycastAll(rayPointer, distanceToClipPlane, (int)m_BlockingMask);
					if (hits.Length > 0)
						hitDistance = hits[0].distance;
				}

				if (raycaster.blockingObjects == BlockingObjects.TwoD || raycaster.blockingObjects == BlockingObjects.All)
				{
					var hits = Physics2D.GetRayIntersectionAll(rayPointer, distanceToClipPlane, (int)m_BlockingMask);
					if (hits.Length > 0)
						hitDistance = hits[0].distance;
				}
			}

			s_RaycastResults.Clear();
			Raycast(canvas, eventCamera, eventData.position, canvasGraphics, s_RaycastResults);

			int totalCount = s_RaycastResults.Count;
			for (var index = 0; index < totalCount; index++)
			{
				var go = s_RaycastResults[index].gameObject;
				bool appendGraphic = true;

				if (raycaster.ignoreReversedGraphics)
				{
					var dir = go.transform.rotation * Vector3.forward;
					appendGraphic = Vector3.Dot(rayPointer.direction, dir) > 0;
				}

				if (appendGraphic)
				{
					float distance = 0;

					Transform trans = go.transform;
					Vector3 transForward = trans.forward;
					// http://geomalgorithms.com/a06-_intersect-2.html
					distance = (Vector3.Dot(transForward, trans.position - rayPointer.origin) / Vector3.Dot(transForward, rayPointer.direction));

					// Check to see if the go is behind the camera or its blocked by another object
					if (distance < 0 || distance >= hitDistance)
						continue;

					var castResult = new MLURaycastResult
					{
						gameObject = go,
						module = raycaster,
						distance = distance,
						screenPosition = eventData.position,
						screenDelta = eventData.delta,
						index = resultAppendList.Count,
						depth = s_RaycastResults[index].depth,
						sortingLayer = canvas.sortingLayerID,
						sortingOrder = canvas.sortingOrder,
						canvas = canvas,
						rootCanvas = canvas.rootCanvas,
						worldPosition = rayPointer.GetPoint(distance),
						worldNormal = -trans.forward
					};

					resultAppendList.Add(castResult);
				}
			}

			// in this kind of raycast event position and delta are supposed to be assigned on the Input Module, the fact that they are changed at the start of this method its only
			// to prevent more code rewritting from original Unity code
			eventData.position = previousPosition;
			eventData.delta = previousDelta;
		}

		/// <summary>
		/// Perform a raycast into the canvas and collect all graphics underneath it.
		/// </summary>
		private static readonly List<Graphic> s_SortedGraphics = new List<Graphic>();
		private static void Raycast(Canvas canvas, Camera eventCamera, Vector2 pointerPosition, IList<Graphic> foundGraphics, List<Graphic> results)
		{
			// Necessary for the event system
			int totalCount = foundGraphics.Count;
			for (int i = 0; i < totalCount; ++i)
			{
				Graphic graphic = foundGraphics[i];

				// -1 means it hasn't been processed by the canvas, which means it isn't actually drawn
				if (graphic.depth == -1 || !graphic.raycastTarget || graphic.canvasRenderer.cull)
					continue;

				if (!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, pointerPosition, eventCamera))
					continue;

				if (graphic.Raycast(pointerPosition, eventCamera))
				{
					s_SortedGraphics.Add(graphic);
				}
			}

			s_SortedGraphics.Sort((g1, g2) => g2.depth.CompareTo(g1.depth));
			totalCount = s_SortedGraphics.Count;
			for (int i = 0; i < totalCount; ++i)
				results.Add(s_SortedGraphics[i]);

			s_SortedGraphics.Clear();
		}
	}
}