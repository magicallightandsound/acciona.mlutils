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

#pragma warning disable 0660, 0661

using UnityEngine;
using UnityEngine.XR.MagicLeap;

using System;
using System.Collections.Generic;

using MLCoordinateFrameUID = MagicLeapInternal.MagicLeapNativeBindings.MLCoordinateFrameUID;

namespace Acciona.MLUtils
{
	/// <summary>
	/// Struct that represents binding data between a Magic Leap PCF and a basic position/rotation transform
	/// </summary>
	[Serializable]
	public struct MLUPCFBinding
	{
		public MLCoordinateFrameUID CFUID;

		private SerializableUnityData position;
		private SerializableUnityData rotation;
		private SerializableUnityData scale;

		public Vector3 Position { get { return position.Vector3; } set { position.Vector3 = value; } }
		public Quaternion Rotation { get { return rotation.Quaternion; } set { rotation.Quaternion = value; } }
		public Vector3 Scale { get { return scale.Vector3; } set { scale.Vector3 = value; } }

		public MLUTrackedPCF TrackedPCF { get { return MLULandscape.GetTrackedPCF(CFUID); } }
		public float BindingDistance { get { return Position.magnitude; } }
		public float BindingSqrDistance { get { return Position.sqrMagnitude; } }

		public MLUPCFBinding (MLPCF PCF, Vector3 position, Quaternion rotation, Vector3? scale = null)
		{
			CFUID = PCF.CFUID;
			this.position = this.rotation = this.scale = default(SerializableUnityData);

			Quaternion inverseOrientation = Quaternion.Inverse(PCF.Orientation);
			this.Position = inverseOrientation * (position - PCF.Position);
			this.Rotation = inverseOrientation * rotation;
			this.Scale = scale ?? Vector3.one;
		}

		public MLUTrackedPCF GetTrackedPCF (IEnumerable<MLUTrackedPCF> trackedPCFs)
		{
			if (trackedPCFs != null)
				foreach (MLUTrackedPCF trackedPCF in trackedPCFs)
					if (trackedPCF.PCF.CurrentResult == MLResultCode.Ok && trackedPCF.PCF.CFUID.Equals(CFUID))
						return trackedPCF;
			
			return null;
		}

		/// <summary>
		/// Serializable struct that wraps a Unity Vector3 or Quaternion.
		/// </summary>
		[Serializable]
		private struct SerializableUnityData
		{
			private float x, y, z, w;

			public Vector3 Vector3 { get { return new Vector3(x, y, z); } set { this.x = value.x; this.y = value.y; this.z = value.z; this.w = 0.0F; } }
			public Quaternion Quaternion { get { return new Quaternion(x, y, z, w); } set { this.x = value.x; this.y = value.y; this.z = value.z; this.w = value.w; } }

			public SerializableUnityData (Vector3 vector) { this.x = vector.x; this.y = vector.y; this.z = vector.z; this.w = 0.0F; }
			public SerializableUnityData (Quaternion quaternion) { this.x = quaternion.x; this.y = quaternion.y; this.z = quaternion.z; this.w = quaternion.w; }

			public static bool operator == (SerializableUnityData x, SerializableUnityData y)
			{
				return x.x == y.x && x.y == y.y && x.z == y.z && x.w == y.w;
			}

			public static bool operator != (SerializableUnityData x, SerializableUnityData y)
			{
				return x.x != y.x || x.y != y.y || x.z != y.z || x.w != y.w;
			}
		}

		public static bool operator == (MLUPCFBinding x, MLUPCFBinding y)
		{
			return x.CFUID.Equals(y.CFUID) && x.position == y.position && x.rotation == y.rotation && x.scale == y.scale;
		}

		public static bool operator != (MLUPCFBinding x, MLUPCFBinding y)
		{
			return !x.CFUID.Equals(y.CFUID) || x.position != y.position || x.rotation != y.rotation || x.scale != y.scale;
		}
	}
}