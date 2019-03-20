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

namespace Acciona.MLUtils
{
	/// <summary>
	/// Add this to any GameObject on the scene and set a reference to the directional light to support the ML light tracking.
	/// This behaviour was edited from the Magic Leap Unity package example scenes.
	/// </summary>
	public class MLULightTracker : MonoBehaviour
	{
		[SerializeField]
		private Light directionalLight;

		private Color color;
		private float normalizedLuminance;
		private float maxLuminance = 0;

		private void Start ()
		{
			MLResult result = MLLightingTracker.Start();

			if (result.IsOk)
			{
				enabled = true;
				RenderSettings.ambientLight = Color.black;
			}
		}

		private void OnDestroy ()
		{
			if (MLLightingTracker.IsStarted)
				MLLightingTracker.Stop();
		}

		private void Update ()
		{
			if (MLLightingTracker.IsStarted)
			{
				// Set the maximum observed luminance, so we can normalize it.
				if (maxLuminance < (MLLightingTracker.AverageLuminance / 3.0f))
					maxLuminance = (MLLightingTracker.AverageLuminance / 3.0f);

				color = MLLightingTracker.GlobalTemperatureColor;
				normalizedLuminance = (float) (System.Math.Min(System.Math.Max((double) MLLightingTracker.AverageLuminance, 0.0), maxLuminance) / maxLuminance);

				RenderSettings.ambientLight = color;
				RenderSettings.ambientIntensity = normalizedLuminance;

				// Set the light intensity of the scene light.
				if (directionalLight != null)
					directionalLight.intensity = normalizedLuminance;
			}
        }
	}
}