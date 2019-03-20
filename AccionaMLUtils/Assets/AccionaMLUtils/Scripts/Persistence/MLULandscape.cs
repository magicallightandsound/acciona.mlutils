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
using System.Collections.ObjectModel;

using MLCoordinateFrameUID = MagicLeapInternal.MagicLeapNativeBindings.MLCoordinateFrameUID;

namespace Acciona.MLUtils
{
	/// <summary>
	/// Represents current Magic Leap Landscape (as all the stored PCFs and MLUTrackedPCF GameObjects).
	/// Start() must be called before using this class.
	/// </summary>
	public static class MLULandscape
	{
		private const int LISTS_CAPACITY = 100;

		public static event Action OnInitialized;
		public static event Action<MLUTrackedPCF> OnTrackedPCFCreate;

		private static List<MLPCF> PCFs;
		private static List<MLUTrackedPCF> trackedPCFs;
		private static float updatePCFsInterval = 2.0F;
		private static bool disableTrackedPCFsOnLost = false;
		private static bool starting = false;
		private static bool subscribed = false;

		public static ReadOnlyCollection<MLPCF> AllPCFs { get { return PCFs.AsReadOnly(); } }
		public static List<MLPCF> OkPCFs { get { return PCFs.FindAll((PCF) => PCF.CurrentResult == MLResultCode.Ok); } }
		public static ReadOnlyCollection<MLUTrackedPCF> AllTrackedPCFs { get { return trackedPCFs.AsReadOnly(); } }
		public static List<MLUTrackedPCF> OkTrackedPCFs { get { return trackedPCFs.FindAll((trackedPCF) => trackedPCF.PCF.CurrentResult == MLResultCode.Ok); } }
		public static float UpdatePCFsInterval { get { return updatePCFsInterval; } set { if (value < 0) value = 0; updatePCFsInterval = value; } }
		public static bool IsReady { get; private set; }
		public static int PCFCount { get { return PCFs.Count; } }

		/// <summary>
		/// Set all tracked PCFs DisableOnLost parameter. Also next created tracked PCFs will be set the same.
		/// If any tracked PCF DisableOnLost parameter is set individually the return value of DisableTrackedPCFsOnLost may diverge from those.
		/// </summary>
		public static bool DisableTrackedPCFsOnLost
		{
			get { return disableTrackedPCFsOnLost; }
			set { disableTrackedPCFsOnLost = value; foreach (MLUTrackedPCF trackedPCF in trackedPCFs) trackedPCF.InactiveOnLost = value; }
		}

		/// <summary>
		/// Starts tracking the landscape and generating tracked PCF GameObjets (Uses MLPersistentCoordinateFrames).
		/// </summary>
		public static void Start (Action onInitializedCallback = null)
		{
			// check if it's already ready or in starting process
			if (IsReady || starting)
			{
				// check what to do with the callback depending on ready or starting status
				if (onInitializedCallback != null)
				{
					if (IsReady)
						onInitializedCallback();
					else
						OnInitialized += onInitializedCallback;
				}

				return;
			}

			// initialize MLPersistentCoordinateFrames
			Debug.Log("======== MLULandscape: Initializing landscape... ========");
			if (onInitializedCallback != null) OnInitialized += onInitializedCallback;
			MLResult result = MLPersistentCoordinateFrames.Start();

			// check if MLPersistentCoordinateFrames started successfully so we can initialize the landscape
			if (result.IsOk)
			{
				if (MLPersistentCoordinateFrames.IsReady)
					OnAPIInitialized(result);
				else
				{
					MLPersistentCoordinateFrames.OnInitialized += OnAPIInitialized;
					starting = subscribed = true;
				}
			}
			else
			{
				Debug.LogError("MLULandscape error: couldn't start MLPersistentCoordinateFrames!");
				if (OnInitialized != null) OnInitialized();
			}
		}

		public static MLPCF GetPCF (MLCoordinateFrameUID CFUID)
		{
			foreach (MLPCF PCF in PCFs)
				if (PCF.CFUID.Equals(CFUID))
					return PCF;
			
			return null;
		}

		public static MLUTrackedPCF GetTrackedPCF (MLPCF PCF)
		{
			for (int i = 0; i < PCFs.Count; i++)
				if (PCF.CFUID.Equals(PCFs[i].CFUID))
					return trackedPCFs[i];
			
			return null;
		}

		public static MLUTrackedPCF GetTrackedPCF (MLCoordinateFrameUID CFUID)
		{
			foreach (MLUTrackedPCF trackedPCF in trackedPCFs)
				if (CFUID.Equals(trackedPCF.PCF.CFUID))
					return trackedPCF;
			
			return null;
		}

		private static void OnPCFFoundHandler (MLPCF PCF)
		{
			if (PCFs.Contains(PCF)) return;

			MLPersistentCoordinateFrames.QueueForUpdates(PCF); // this doesn't work as spected for now
			MLUTrackedPCF trackedPCF = MLUTrackedPCF.CreateTrackedPCF(PCF);
			trackedPCF.InactiveOnLost = disableTrackedPCFsOnLost;
			PCFs.Add(PCF);
			trackedPCFs.Add(trackedPCF);
			if (OnTrackedPCFCreate != null)	OnTrackedPCFCreate(trackedPCF);

			Debug.Log("MLULandscape: started tracking PCF [CFUID: " + PCF.CFUID + " | result: " + PCF.CurrentResult + "]");
		}

		private static void OnAPIInitialized (MLResult result)
		{
			if (subscribed)
			{
				MLPersistentCoordinateFrames.OnInitialized -= OnAPIInitialized;
				subscribed = false;
			}

			if (result.IsOk)
			{
				// initialize lists
				PCFs = new List<MLPCF>(LISTS_CAPACITY);
				trackedPCFs = new List<MLUTrackedPCF>(LISTS_CAPACITY);
				// start permanente PCF updating coroutine
				StaticCoroutine.StartCoroutine(C_UpdatePCFs());
				IsReady = true;

				Debug.Log("MLULandscape initalization successull. " + PCFs.Count + " PCFs were registered.");
			}
			else
				Debug.LogErrorFormat("MLULandscape error: couldn't start MLPersistentCoordinateFrames. Reason: {0}", result);
			
			starting = false;
			if (OnInitialized != null) OnInitialized();
		}

		private static IEnumerator C_UpdatePCFs ()
		{
			List<MLPCF> allPCFs;

			while (true)
			{
				MLResult result = MLPersistentCoordinateFrames.GetAllPCFs(out allPCFs);

				if (result.IsOk)
				{
					foreach (MLPCF PCF in allPCFs)
						if (PCFs.Find((element) => element.CFUID.Equals(PCF.CFUID)) == null)
							OnPCFFoundHandler(PCF);
				}
				else
					Debug.Log("MLULandscape: PCFs update failed. result: " + result);

				yield return new WaitForSecondsRealtime(updatePCFsInterval);
			}
		}
	}
}