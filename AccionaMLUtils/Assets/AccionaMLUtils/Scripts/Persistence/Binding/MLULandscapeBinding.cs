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


namespace Acciona.MLUtils
{
	/// <summary>
	/// Struct that represents binding data between a Magic Leap Landscape and a basic position/rotation transform.
	/// It stores multiple PCF bindings so there is more binding data when not all PCFs are resotred correctly.
	/// </summary>
	[Serializable]
	public struct MLULandscapeBinding
	{
		/// <summary>
		/// Default binding radius for new landscape bindings where binding radius is not specified.
		/// </summary>
		public static float defaultBindingRadius = 5.0F;
		
		private readonly MLUPCFBinding[] bindings;
		private float bindingRadius;

		/// <summary>
		/// Checks if the binding is valid (has at least one PCF binding).
		/// </summary>
		public bool IsValid { get { return bindings != null && bindings.Length > 0; } }

		/// <summary>
		/// All tracked PCFs that were out of this radius from the binding position were discarded when the binding was created.
		/// </summary>
		public float BindingRadius { get { return bindingRadius; } }

		public MLULandscapeBinding (Transform transform, float? bindingRadius = null)
			: this(MLULandscape.AllTrackedPCFs, transform, bindingRadius) { }
		public MLULandscapeBinding (Vector3 position, Quaternion rotation, Vector3? scale = null, float? bindingRadius = null)
			: this(MLULandscape.AllTrackedPCFs, position, rotation, scale, bindingRadius) { }
		public MLULandscapeBinding (IEnumerable<MLUTrackedPCF> trackedPCFs, Transform transform, float? bindingRadius = null)
			: this(trackedPCFs, transform.position, transform.rotation, transform.localScale, bindingRadius) { }
		public MLULandscapeBinding (IEnumerable<MLUTrackedPCF> trackedPCFs, Vector3 position, Quaternion rotation, Vector3? scale = null, float? bindingRadius = null)
		{
			this.bindingRadius =  bindingRadius ?? defaultBindingRadius;
			// create individual PCF/transform bindings
			List<MLUPCFBinding> bindingsList = new List<MLUPCFBinding>();

			foreach (MLUTrackedPCF trackedPCF in trackedPCFs)
			{
				if (trackedPCF.PCF.CurrentResult == MLResultCode.Ok && Vector3.Distance(trackedPCF.transform.position, position) <= this.bindingRadius)
					bindingsList.Add(new MLUPCFBinding(trackedPCF.PCF, position, rotation, scale));
			}

			// sort bindings by PCF to transform distance (closest first)
			bindingsList.Sort((x, y) => x.Position.sqrMagnitude - y.Position.sqrMagnitude < 0 ? -1 : 1);
			bindings = bindingsList.ToArray();
		}

		/// <summary>
		/// Checks if the binding is valid (has at least one PCF binding).
		/// </summary>
		public static implicit operator bool (MLULandscapeBinding binding)
		{
			return binding.IsValid;
		}

		/// <summary>
		/// Looks on the current landscape tracked PCFs and returns the best PCF binding for this landscape binding (if any exist). Returns false if failed to find a valid PCF binding on the current landscape.
		/// </summary>
		public bool TryGetPCFBinding (out MLUPCFBinding bindingData) { return TryGetPCFBinding(MLULandscape.AllPCFs, out bindingData); }
		
		/// <summary>
		/// Looks on the given tracked PCFs and returns the best PCF binding for this landscape binding (if any exist). Returns false if failed to find a valid PCF binding on the given tracked PCFs.
		/// </summary>
		public bool TryGetPCFBinding (IEnumerable<MLPCF> PCFs, out MLUPCFBinding bindingData)
		{
			foreach (MLUPCFBinding binding in bindings)
			{
				foreach (MLPCF PCF in PCFs)
				{
					if (PCF.CurrentResult == MLResultCode.Ok && PCF.CFUID.Equals(binding.CFUID))
					{
						bindingData = binding;
						return true;
					}
				}
			}

			bindingData = default(MLUPCFBinding);
			return false;
		}

		/// <summary>
		/// Tries to get the information required by a persistent object to bind to the current landscape using this landscape binding. Retrieved information includes a reference to the best tracked PCF
		/// and its binding information. Returns false if failed to find a valid PCF binding for the current landscape.
		/// </summary>
		public bool TryGetBindingInfo (out MLUPersistentObject.BindingInfo bindingInfo) { return TryGetBindingInfo(MLULandscape.AllTrackedPCFs, out bindingInfo); }

		/// <summary>
		/// Tries to get the information required by a persistent object to bind to the given tracked PCFs using this landscape binding. Retrieved information includes a reference to the best tracked PCF
		/// and its binding information. Returns false if failed to find a valid PCF binding for the given tracked PCFs.
		/// </summary>
		public bool TryGetBindingInfo (IEnumerable<MLUTrackedPCF> trackedPCFs, out MLUPersistentObject.BindingInfo bindingInfo)
		{
			if (trackedPCFs != null)
			{
				foreach (MLUPCFBinding binding in bindings)
				{
					foreach (MLUTrackedPCF trackedPCF in trackedPCFs)
					{
						if (trackedPCF.PCF.CurrentResult == MLResultCode.Ok && trackedPCF.PCF.CFUID.Equals(binding.CFUID))
						{
							bindingInfo = new MLUPersistentObject.BindingInfo(this, binding, trackedPCF);
							return true;
						}
					}
				}
			}

			bindingInfo = default(MLUPersistentObject.BindingInfo);
			return false;
		}

		public static bool operator == (MLULandscapeBinding x, MLULandscapeBinding y)
		{
			if (x.bindingRadius == y.bindingRadius && x.IsValid && y.IsValid && x.bindings.Length == y.bindings.Length)
			{
				for (int i = 0; i < x.bindings.Length; i++)
					if (x.bindings[i] != y.bindings[i])
						return false;

				return true;
			}
			else
				return false;
		}

		public static bool operator != (MLULandscapeBinding x, MLULandscapeBinding y)
		{
			return !(x == y);
		}
	}
}