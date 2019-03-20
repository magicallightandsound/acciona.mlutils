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
using UnityEngine.EventSystems;

namespace Acciona.MLUtils
{
	/// <summary>
	/// Copy of Unity's RaycastResult with some extra references added: canvas, rootcanvas, pointer delta in event camera screen coordinates and a method to convert to RaycastResult.
	/// </summary>
	public struct MLURaycastResult
	{
		private GameObject m_GameObject; // Game object hit by the raycast

		/// <summary>
		/// The GameObject that was hit by the raycast.
		/// </summary>
		public GameObject gameObject
		{
			get { return m_GameObject; }
			set { m_GameObject = value; }
		}

		/// <summary>
		/// BaseInputModule that raised the hit.
		/// </summary>
		public BaseRaycaster module;

		/// <summary>
		/// Distance to the hit.
		/// </summary>
		public float distance;

		/// <summary>
		/// Hit index
		/// </summary>
		public float index;

		/// <summary>
		/// Used by raycasters where elements may have the same unit distance, but have specific ordering.
		/// </summary>
		public int depth;

		/// <summary>
		/// The SortingLayer of the hit object.
		/// </summary>
		/// <remarks>
		/// For UI.Graphic elements this will be the values from that graphic's Canvas
		/// For 3D objects this will always be 0.
		/// For 2D objects if a SpriteRenderer is attached to the same object as the hit collider that SpriteRenderer sortingLayerID will be used.
		/// </remarks>
		public int sortingLayer;

		/// <summary>
		/// The SortingOrder for the hit object.
		/// </summary>
		/// <remarks>
		/// For Graphic elements this will be the values from that graphics Canvas
		/// For 3D objects this will always be 0.
		/// For 2D objects if a SpriteRenderer is attached to the same object as the hit collider that SpriteRenderer sortingOrder will be used.
		/// </remarks>
		public int sortingOrder;

		public Canvas canvas;
		public Canvas rootCanvas;

		/// <summary>
		/// The world position of the where the raycast has hit.
		/// </summary>
		public Vector3 worldPosition;

		/// <summary>
		/// The normal at the hit location of the raycast.
		/// </summary>
		public Vector3 worldNormal;

		/// <summary>
		/// The screen position from which the raycast was generated.
		/// </summary>
		public Vector2 screenPosition;
		public Vector2 screenDelta;

		/// <summary>
		/// Is there an associated module and a hit GameObject.
		/// </summary>
		public bool isValid
		{
			get { return module != null && gameObject != null; }
		}

		/// <summary>
		/// Reset the result.
		/// </summary>
		public void Clear()
		{
			gameObject = null;
			module = null;
			distance = 0;
			index = 0;
			depth = 0;
			sortingLayer = 0;
			sortingOrder = 0;
			worldNormal = Vector3.up;
			worldPosition = Vector3.zero;
			screenPosition = Vector2.zero;
		}

		public RaycastResult ToRaycastResult ()
		{
			return new RaycastResult()
			{
				gameObject = this.gameObject,
				module = this.module,
				distance = this.distance,
				screenPosition = this.screenPosition,
				index = this.index,
				depth = this.depth,
				sortingLayer = this.sortingLayer,
				sortingOrder = this.sortingOrder,
				worldPosition = this.worldPosition,
				worldNormal = this.worldNormal
			};
		}

		public override string ToString()
		{
			if (!isValid)
				return "";

			return "Name: " + gameObject + "\n" +
				"module: " + module + "\n" +
				"distance: " + distance + "\n" +
				"index: " + index + "\n" +
				"depth: " + depth + "\n" +
				"worldNormal: " + worldNormal + "\n" +
				"worldPosition: " + worldPosition + "\n" +
				"screenPosition: " + screenPosition + "\n" +
				"module.sortOrderPriority: " + module.sortOrderPriority + "\n" +
				"module.renderOrderPriority: " + module.renderOrderPriority + "\n" +
				"sortingLayer: " + sortingLayer + "\n" +
				"sortingOrder: " + sortingOrder +
				"canvas: " + canvas;
		}
	}
}