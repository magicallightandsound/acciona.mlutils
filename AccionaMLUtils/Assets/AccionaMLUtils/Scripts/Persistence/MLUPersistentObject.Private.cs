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

using System;
using System.Collections;
using System.Collections.Generic;

namespace Acciona.MLUtils
{
	/// <summary>
	/// Private part of MLUPersistentObject (for convenience since the class's code is big).
	/// </summary>
	public partial class MLUPersistentObject : MonoBehaviour
	{
		private MLUBindingLibrary bindingLibrary;
		private TransformStatus transformStatus;
		private Coroutine autoBindAndSaveCoroutine;
		private Coroutine restoringCoroutine;
		private MLULandscapeBinding restoringBinding;
		private Transform originalParent;
		private bool handlersRegistered = false;
		private bool isRestoring = false;

		private string LogHeader { get { return "|| MLUPersistentObject: " + UniqueID + " || "; } }

		void Awake ()
		{
			transformStatus = new TransformStatus(transform);
			originalParent = transform.parent;
			CurrentBindingInfo = null;
		}

		// check if the object is moved so it unbinds himself (library binding will not be deleted)
		void LateUpdate ()
		{
			if (CurrentBindingInfo != null && transformStatus.HasChanged)
				Unbind(false); // unbind the object without deleting saved data from the library
		}

		void Start () { MLULandscape.Start(OnLandscapeInitialized); }
		void OnEnable () { RestartObject(); }
		void OnDisable () { UnregisterHandlers(); }
		void OnDestroy () { UnregisterHandlers(); }

		/// <summary>
		/// Initializes the persistent object. It's suppossed to be fired when MLULandscape is initialized.
		/// </summary>
		private void OnLandscapeInitialized ()
		{
			MLULandscape.OnInitialized -= OnLandscapeInitialized;

			// check if MLULandscape initialization succeded, in that case we register PCF creation handlers and try to restore the object binding from the library
			if (MLULandscape.IsReady)
			{
				Debug.Log(LogHeader + "Initialization... Binding library: " + BindingLibrary.libraryID);
				transformStatus.RegisterState(); // we don't want the auto save coroutine to save anything until we move or restore the object
				RestartObject();
				TryRestore();
			}
			else
			{
				enabled = false;
				Debug.LogError(LogHeader + "ERROR: MLULandscape could not start, this script will be disabled...");
			}
		}

		/// <summary>
		/// Restarts the object behaviour (auto bind and save coroutine, handlers, restoring coroutine if previously running...)
		/// </summary>
		private void RestartObject ()
		{
			if (!MLULandscape.IsReady) return;

			RegisterHandlers();
			if (autoBindAndSaveCoroutine != null) StopCoroutine(autoBindAndSaveCoroutine);
			autoBindAndSaveCoroutine = StartCoroutine(C_AutoBindAndSave());
			if (restoringCoroutine != null) TryRestore(restoringBinding);
		}

		/// <summary>
		/// Restores the original parent (if still exists).
		/// </summary>
		private void UnparentFromTrackedPCF ()
		{
			if (originalParent != null && originalParent.gameObject && transform.parent != originalParent)
				transform.parent = originalParent;
			else
				transform.parent = null;
		}

		/// <summary>
		/// Tries to create a new binding with the current landscape and apply it to the object. Returns true if succeded.
		/// </summary>
		private bool TryCreateAndApplyBinding ()
		{
			MLULandscapeBinding binding = new MLULandscapeBinding(transform, bindingRadius);
			return TryApplyBinding(binding);
		}

		/// <summary>
		/// Tries to apply the given binding to the transform (parenting it with the corresponding Tracked PCF if specified and setting its local position, rotation and scale).
		/// Returns true if applied succesfully.
		/// </summary>
		private bool TryApplyBinding (MLULandscapeBinding binding)
		{
			if (!binding.IsValid) return false;

			BindingInfo bindingInfo;

			// try to retrieve binding information (at least one current PCF must be binded to successfully restore the object binding)
			if (binding.TryGetBindingInfo(out bindingInfo))
			{
				// only apply binding if its actually different from current one
				if (bindingInfo != CurrentBindingInfo)
				{
					Transform currentParent = transform.parent;
					// apply binding transformation
					transform.parent = bindingInfo.trackedPCF.transform;
					transform.localPosition = bindingInfo.binding.Position;
					transform.localRotation = bindingInfo.binding.Rotation;
					transform.localScale = bindingInfo.binding.Scale;
					// restore previus parent if not parenting with tracked PCF is desired
					if (!parentToBindedPCF) transform.parent = currentParent;

					CurrentBindingInfo = bindingInfo; // only succesfully applied bindings are saved
					transformStatus.RegisterState();
					isRestoring = false; // we are for sure not restoring any more if we succesfully apply a binding
					if (OnBindingStateChanged != null) OnBindingStateChanged(this);
				}

				return true;
			}
			else
				return false;
		}
		
		/// <summary>
		/// Permantent coroutine that will try to bind and save the object in a fixed interval if autoBindAndSave is enabled.
		/// </summary>
		private IEnumerator C_AutoBindAndSave ()
		{
			while (true)
			{
				if (autoBindAndSave && !isRestoring && (!IsBinded || transformStatus.HasChanged))
					TryBind(true);

				yield return new WaitForSecondsRealtime(autoBindAndSaveInterval);
			}
		}

		/// <summary>
		/// TryRestore coroutine that tries to restore the object with the given binding (if it's valid) until it succeeds or the object is moved.
		/// </summary>
		private IEnumerator C_TryRestore (MLULandscapeBinding binding)
		{
			bool lastRestorationFailed = true;

			if (binding.IsValid)
			{
				restoringBinding = binding;
				transformStatus.RegisterState();

				while (isRestoring = (!transformStatus.HasChanged && (lastRestorationFailed = !TryRestoreOnce(binding))))
				{
					float time = 0.0F;

					while (time < tryRestoreInterval)
					{
						yield return null;
						time += Time.deltaTime;
						if (transformStatus.HasChanged) break;
					}
				}
			}

			if (lastRestorationFailed)
				Debug.Log(LogHeader + "Restoration coroutine terminated. No binding data was restored!");
			else
				Debug.Log(LogHeader + "Restoration coroutine terminated. Binding restored succesfully!");

			restoringCoroutine = null;
		}

		private void RegisterHandlers ()
		{
			if (handlersRegistered || !MLULandscape.IsReady) return;
			MLULandscape.OnTrackedPCFCreate += OnTrackedPCFCreateHandler;
			handlersRegistered = true;
		}

		private void UnregisterHandlers ()
		{
			if (!handlersRegistered) return;
			MLULandscape.OnTrackedPCFCreate -= OnTrackedPCFCreateHandler;
			handlersRegistered = false;
		}

		private void OnTrackedPCFCreateHandler (MLUTrackedPCF trackedPCF)
		{
			// if the object is binded we try to reaply the binding (since new tracked PCF may be binded and better than previous ones)
			if (IsBinded)
			{
				BindingInfo bindingInfo = CurrentBindingInfo ?? default(BindingInfo);
				TryApplyBinding(bindingInfo.landscapeBinding); 
			}
		}

		/// <summary>
		/// Private struct to check if a transform has changed from frame to frame.
		/// </summary>
		private struct TransformStatus
		{
			public Transform transform;
			private Vector3 position;
			private Quaternion rotation;
			private Vector3 localScale;

			public bool HasChanged { get { return position != transform.position || rotation != transform.rotation || localScale != transform.localScale; } }

			public TransformStatus (Transform transform)
			{
				this.transform = transform;
				this.position = transform.position;
				this.rotation = transform.rotation;
				this.localScale = transform.localScale;
			}

			public void RegisterState ()
			{
				position = transform.position;
				rotation = transform.rotation;
				localScale = transform.localScale;
			}
		}
	}
}