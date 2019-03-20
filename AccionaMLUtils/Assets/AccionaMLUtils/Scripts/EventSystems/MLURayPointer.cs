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

namespace Acciona.MLUtils
{
	/// <summary>
	/// Abstract class used by MLURayPointerInputModule to obtain basic input from a ray pointer (for example: VR/AR 6DOF controllers with any button that can act as left/right mouse buttons).
	/// Ray pointers are considered to be world positioned pointers with orientation that raycast into a canvas.
	/// </summary>
	public abstract class MLURayPointer : MonoBehaviour
	{
		/// <summary>
		/// Returns pointer's current absolute world position.
		/// </summary>
		public abstract Vector3 Position { get; }
		/// <summary>
		/// Returns pointer's current absolute world rotation.
		/// </summary>
		public abstract Quaternion Rotation { get; }
		/// <summary>
		/// Returns current scroll delta input from this pointer.
		/// </summary>
		public abstract Vector2 ScrollDelta { get; }

		/// <summary>
		/// Returns true if given button ID (mainly 0 for left mouse click or 1 for right click) is currently pressed.
		/// </summary>
		public abstract bool GetButtonDown (int buttonID);

		/// <summary>
		/// Returns true if given button ID (mainly 0 for left mouse click or 1 for right click) is not currently pressed.
		/// </summary>
		public abstract bool GetButtonUp (int buttonID);

		private MLURaycastResult? currentRaycast;
		
		/// <summary>
		/// Ray representing current pointer.
		/// </summary>
		public Ray Ray { get { return new Ray(Position, Forward); } }
		/// <summary>
		/// Current absolute forward vector for this pointer.
		/// </summary>
		public Vector3 Forward { get { return Rotation * Vector3.forward; } }

		/// <summary>
		/// Returns true if the pointer is currently hitting on a raycast target (Canvas graphic).
		/// </summary>
		public bool IsPointing { get { return currentRaycast != null;} }

		/// <summary>
		/// Returns current raycast result. Default MLURaycastResult struct will be returned if pointer is not over any raycast target (IsPointing == false).
		/// </summary>
		public MLURaycastResult CurrentRaycast { get { return currentRaycast ?? default(MLURaycastResult); } }

		/// <summary>
		/// Current raycast hit absolute world position (zero if not pointing to anything).
		/// </summary>
		public Vector3 HitPosition { get { return CurrentRaycast.worldPosition; } }

		/// <summary>
		/// Used by MLURayPointerInputModule to set the current raycast result when hitting something (should not be used for other purpose).
		/// </summary>
		public void SetPointing (MLURaycastResult? currentRaycast)
		{
			this.currentRaycast = currentRaycast;
		}
	}
}
