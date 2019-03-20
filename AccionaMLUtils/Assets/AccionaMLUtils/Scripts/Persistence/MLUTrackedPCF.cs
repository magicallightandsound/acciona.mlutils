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

using MLCoordinateFrameUID = MagicLeapInternal.MagicLeapNativeBindings.MLCoordinateFrameUID;

namespace Acciona.MLUtils
{
	/// <summary>
	/// Component that makes its GameObject represent a Magic Leap PCF in realtime. It wraps the PCF events with OnUpdate, OnLost and OnRegain and updates
	/// the GameObject depending on the PCF status.
	/// </summary>
	public class MLUTrackedPCF : MonoBehaviour
	{
		public event Action<MLUTrackedPCF> OnUpdate;
		public event Action<MLUTrackedPCF> OnLost;
		public event Action<MLUTrackedPCF> OnRegain;

		[SerializeField]
		private bool inactiveOnLost = false;

		private MLPCF trackedPCF;
		private bool subscribed = false;
		private Coroutine restoringCoroutine;

		public bool IsOk { get { return trackedPCF != null && trackedPCF.CurrentResult == MLResultCode.Ok; } }
		public bool IsRestoring { get { return restoringCoroutine != null; } }

		/// <summary>
		/// The PCF being tracked by this component.
		/// </summary>
		public MLPCF PCF
		{
			get { return trackedPCF; }

			private set
			{
				UnregisterHandlers();
				this.trackedPCF = value;
				RegisterHandlers();
				gameObject.name = "Tracked PCF: " + trackedPCF.CFUID;
				InactiveOnLost = inactiveOnLost; // updates inactive status
				if (!IsOk) Restore();
			}
		}

		/// <summary>
		/// Specifies if the GameObject will be set inactive when the tracked PCF is on lost status.
		/// </summary>
		/// <value></value>
		public bool InactiveOnLost
		{
			get { return inactiveOnLost; }

			set
			{
				inactiveOnLost = value;

				if (inactiveOnLost && trackedPCF != null && trackedPCF.CurrentResult != MLResultCode.Ok)
					gameObject.SetActive(false);
				else
					gameObject.SetActive(true);
			}
		}

		/// <summary>
		/// Instantiates a GameObject with a MLUTrackedPCF component that tracks the given PCF status. PCF event listeners can be specified in the parameters.
		/// </summary>
		public static MLUTrackedPCF CreateTrackedPCF (MLPCF PCF, Action<MLUTrackedPCF> onUpdate = null, Action<MLUTrackedPCF> onLost = null, Action<MLUTrackedPCF> onRegain = null)
		{
			MLUTrackedPCF trackedPCF = (new GameObject()).AddComponent<MLUTrackedPCF>();
			if (onUpdate != null) trackedPCF.OnUpdate += onUpdate;
			if (onLost != null) trackedPCF.OnLost += onLost;
			if (onRegain != null) trackedPCF.OnRegain += onRegain;
			trackedPCF.PCF = PCF;

			if (trackedPCF.inactiveOnLost && PCF.CurrentResult != MLResultCode.Ok)
				trackedPCF.gameObject.SetActive(false);

			return trackedPCF;
		}

		private void RegisterHandlers ()
		{
			if (trackedPCF == null)
				subscribed = false;
			else if (!subscribed)
			{
				trackedPCF.OnUpdate += OnUpdateHandler;
				trackedPCF.OnLost += OnLostHandler;
				trackedPCF.OnRegain += OnRegainHandler;
				subscribed = true;
			}
		}

		private void UnregisterHandlers ()
		{
			if (trackedPCF == null)
				subscribed = false;
			else if (subscribed)
			{
				trackedPCF.OnUpdate -= OnUpdateHandler;
				trackedPCF.OnLost -= OnLostHandler;
				trackedPCF.OnRegain -= OnRegainHandler;
				subscribed = false;
			}
		}

		private void OnUpdateHandler ()
		{
			Debug.Log("MLUTrackedPCF " + trackedPCF.CFUID + " updated!");
			gameObject.SetActive(inactiveOnLost ? trackedPCF.CurrentResult == MLResultCode.Ok : true);
			transform.position = trackedPCF.Position;
			transform.rotation = trackedPCF.Orientation;
			if (OnUpdate != null) OnUpdate(this);
		}

		private void OnLostHandler ()
		{
			if (!IsRestoring)
			{
				Debug.Log("MLUTrackedPCF " + trackedPCF.CFUID + " lost! Trying to restore transform...");
				if (inactiveOnLost) gameObject.SetActive(false);
				Restore();
				if (OnLost != null) OnLost(this);
			}
		}

		private void OnRegainHandler ()
		{
			Debug.Log("MLUTrackedPCF " + trackedPCF.CFUID + " regained!");
			gameObject.SetActive(inactiveOnLost ? trackedPCF.CurrentResult == MLResultCode.Ok : true);
			transform.position = trackedPCF.Position;
			transform.rotation = trackedPCF.Orientation;
			if (OnRegain != null) OnRegain(this);
		}

		private void Restore ()
		{
			if (IsRestoring) StopCoroutine(restoringCoroutine);
			restoringCoroutine = StartCoroutine(C_Restore());
		}

		private IEnumerator C_Restore ()
		{
			// for now this stays disabled since the only way to restore a MLPCF is to use MLPersistentCoordinateFrames.FindClosestPCF (which creates a new MLPCF instance) or calling GetAllPCFs

			// if (!MLPersistentCoordinateFrames.IsReady || !MLPersistentCoordinateFrames.IsStarted) yield break;
			// bool callback = false;

			// while (!IsOk)
			// {
			// 	MLPersistentCoordinateFrames.GetPCFPosition(trackedPCF, (result, PCF) => callback = true);
			// 	yield return new WaitWhile(() => !callback);
			// }

			yield return new WaitWhile(() => !IsOk); // added this to simulate the restoring process until an external proccess succesfully restores de MLPCF position

			restoringCoroutine = null;
		}
	}
}