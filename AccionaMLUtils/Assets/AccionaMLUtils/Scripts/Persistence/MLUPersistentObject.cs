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
using System.Collections;
using System.Collections.Generic;

namespace Acciona.MLUtils
{
	/// <summary>
	/// When added to a GameObject, it will provide the convenient functionallity to make it a persistent object between device shutdowns.
	/// A unique identifier is recommended, if not set, the GameObject's name will be used instead. This component relies on MLULandscape static class functionality (it is not necessary to initialize it manually).
	/// When initialized, it will try to restore itself with the current global binding library (unless a custom one is defined).s
	/// </summary>
	public partial class MLUPersistentObject : MonoBehaviour
	{
		[Tooltip("Unique ID of this persistent object, if not specified the GameObject's name will be used instead.")]
		[SerializeField] private string uniqueID;
		[Tooltip("If enabled the object will be parented with the corresponding tracked PCF GameObject when succesfully binded. If false, binding the object will just modify it's absolute transform (maintaining its parent).")]
		[SerializeField] private bool parentToBindedPCF = true;
		[Tooltip("If enabled the object will try to bind and save itself automatically to the current binding library and landscape (set autoBindAndSaveInterval parameter to decide frequency of this action).")]
		public bool autoBindAndSave = true;
		[Tooltip("If autoBindAndSave is enabled, this defines the interval to perform the auto bind and save action. Set to 0 to perform every frame (not recommended).")]
		public float autoBindAndSaveInterval = 2.0F;
		[Tooltip("When performing a restore (at object's awake or when TryRestoreOnce() or TryRestore() are called) defines the interval between restore tries until the object is succesfully restored.")]
		public float tryRestoreInterval = 2.0F;
		
		/// <summary>
		/// All tracked PCFs that are out of this radius from the object's position will be discarded when binding the object. If set null the MLULandscapeBinding.defaultBindingRadius will be used instead.
		/// </summary>
		public float? bindingRadius = null; 

		/// <summary>
		/// Event fired when the object succesfully restores a binding.
		/// </summary>
		public event Action<MLUPersistentObject> OnRestored;

		/// <summary>
		/// Event fired when the object binding state is updated.
		/// </summary>
		public event Action<MLUPersistentObject> OnBindingStateChanged;

		/// <summary>
		/// Current binding library being used by this persistent object. If user sets a new library and restoring from it is desired, TryRestore() or TryRestoreOnce() must be called.
		/// If TryBind(true) is called after setting a new library previous binding will be overwritten.
		/// </summary>
		public MLUBindingLibrary BindingLibrary { get { return bindingLibrary ?? MLUBindingLibrary.Current; } set { bindingLibrary = value; } }

		/// <summary>
		/// If the object is currently binded this returns the binding information (tracked PCF object, PCF binding and landscape binding used).
		/// It returns null if the object is not currently binded (if a previuos binding is saved on the binding library it can be restored with TryRestore or TryRestoreOnce).
		/// </summary>
		public BindingInfo? CurrentBindingInfo { get; private set; }

		/// <summary>
		/// Returns true if the object is currently binded to the landscape.
		/// </summary>
		public bool IsBinded { get { return CurrentBindingInfo != null && !transformStatus.HasChanged; } }

		/// <summary>
		/// Returns true if the object is currently trying to restore a binding.
		/// </summary>
		public bool IsRestoring { get { return isRestoring; } }
		
		/// <summary>
		/// Persistent object uniqueID that stores the binding information from a binding library.
		/// </summary>
		public string UniqueID
		{
			get
			{
				if (string.IsNullOrEmpty(uniqueID))	uniqueID = gameObject.name; // assign the GameObjet's name as uniqueID if uniqueID was not set
				return uniqueID;
			}
		}

		/// <summary>
		/// If true the object will be parented with the corresponding tracked PCF GameObject when succesfully binded. If false, binding the object will just modify it's absolute transform (maintaining its parent).
		/// </summary>
		public bool ParentToBindedPCF
		{
			get { return parentToBindedPCF; }

			set
			{
				// if we are changing the current value and the object was binded
				if (value != parentToBindedPCF && IsBinded)
				{
					if (!value)
						UnparentFromTrackedPCF();
					else
					{
						BindingInfo bindingInfo = CurrentBindingInfo ?? default(BindingInfo);
						if (bindingInfo.trackedPCF != null) transform.parent = bindingInfo.trackedPCF.transform;
					}
				}

				parentToBindedPCF = value;
			}
		}

		/// <summary>
		/// It will start a coroutine that tries to restore the given binding or look for a saved one in the current binding library (if a custom binding is given
		/// but it's not valid it will look on the library). AutoBindAndSave functionallity will be paused during restoration process. If GameObject transform is changed or
		/// TryBind() is called the restoration coroutine will stop.
		/// </summary>
		public void TryRestore (MLULandscapeBinding? tryBinding = null)
		{
			MLULandscapeBinding binding = tryBinding ?? default(MLULandscapeBinding);
			if (restoringCoroutine != null) StopCoroutine(restoringCoroutine);
			isRestoring = false; // just in case it was true because of the stopped coroutine

			if (binding.IsValid || BindingLibrary.TryGetLandscapeBinding(UniqueID, out binding))
				restoringCoroutine = StartCoroutine(C_TryRestore(binding));
			else
			{
				Debug.Log(LogHeader + "Restore coroutine did not start. Couldn't find valid binding data for this object.");
				restoringCoroutine = null;
			}
		}
		
		/// <summary>
		/// Tries to restore the given binding (It tries once and returns true if succeeded). If no binding is provided it will try to retrieve it from the current binding library.
		/// The binding may be a valid binding but fail to restore the transform if the current landscape is not recognized, in that case false will be returned.
		/// </summary>
		public bool TryRestoreOnce (MLULandscapeBinding? tryBinding = null)
		{
			MLULandscapeBinding binding = tryBinding ?? default(MLULandscapeBinding);

			// either use the given binding if its valid or try to retrieve it from the binding library
			if (binding.IsValid || (BindingLibrary.TryGetLandscapeBinding(UniqueID, out binding) && binding.IsValid))
			{
				// if a saved binding was found try to apply it on the object transform
				if (TryApplyBinding(binding))
				{
					BindingInfo bindingInfo = CurrentBindingInfo ?? default(BindingInfo);
					Debug.Log(LogHeader + "Binding restored succesfully! Binded to PCF: " + bindingInfo.trackedPCF.PCF.CFUID);
					if (OnRestored != null) OnRestored(this);
					return true;
				}
				else
					Debug.Log(LogHeader + "Restore try failed! No binded PCFs were found on current landscape.");
			}
			else
				Debug.Log(LogHeader + "Restore try failed! binding data is not valid.");

			return false;
		}
		
		/// <summary>
		/// Tries to bind the object to the landscape. Returns true if binded successfully.
		/// If bind is succesfully applied and updateLibrary == true it will be saved to the binding library (will overwritte any previous binding).
		/// </summary>
		public bool TryBind (bool updateLibrary = true)
		{
			if (TryCreateAndApplyBinding())
			{
				if (updateLibrary) SaveBindingToLibrary();
				return true;
			}
			else
			{
				Debug.Log(LogHeader + "Binding failed. Cannot create a valid binding with the landscape!");
				return false;
			}
		}

		/// <summary>
		/// If the object is currently binded it will be restored to the original parent transform or just unparent from current tracked PCF (in any case the current absolute object's transform will be preserved).
		/// Deleting the binding from the library can also be specified.
		/// </summary>
		public void Unbind (bool updateLibrary = true)
		{
			bool wasBinded = CurrentBindingInfo != null;
			if (updateLibrary) DeleteBindingFromLibrary();
			if (wasBinded && parentToBindedPCF) UnparentFromTrackedPCF();
			CurrentBindingInfo = null;
			if (wasBinded && OnBindingStateChanged != null) OnBindingStateChanged(this);
		}

		/// <summary>
		/// If the object is binded it will save the binding to the currently used library.
		/// </summary>
		public void SaveBindingToLibrary ()
		{
			if (IsBinded)
			{
				BindingInfo bindingInfo = CurrentBindingInfo ?? default(BindingInfo);
				BindingLibrary.SetLandscapeBinding(UniqueID, bindingInfo.landscapeBinding);
			}
		}

		/// <summary>
		/// If currently used binding library contains a binding for this object it will be deleted (even if the object is not currently binded).
		/// Note that if the object is currently binded it will stay binded (only binding data from the library will be removed).
		/// </summary>
		public void DeleteBindingFromLibrary ()
		{
			BindingLibrary.RemoveLandscapeBinding(UniqueID);
		}

		/// <summary>
		/// Struct that holds a current active binding between the object and the landscape.
		/// </summary>
		public struct BindingInfo
		{
			public MLULandscapeBinding landscapeBinding;
			public MLUPCFBinding binding;
			public MLUTrackedPCF trackedPCF;

			public BindingInfo (MLULandscapeBinding landscapeBinding, MLUPCFBinding binding, MLUTrackedPCF trackedPCF)
			{
				this.landscapeBinding = landscapeBinding;
				this.binding = binding;
				this.trackedPCF = trackedPCF;
			}

			public static bool operator == (BindingInfo x, BindingInfo y)
			{
				return	x.trackedPCF == y.trackedPCF && x.binding == y.binding && x.landscapeBinding == y.landscapeBinding;
			}

			public static bool operator != (BindingInfo x, BindingInfo y)
			{
				return	x.trackedPCF != y.trackedPCF || x.binding != y.binding || x.landscapeBinding != y.landscapeBinding;
			}
		}
	}
}