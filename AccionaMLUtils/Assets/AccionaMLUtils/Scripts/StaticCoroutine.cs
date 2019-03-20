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

using System.Collections;

namespace Acciona.MLUtils
{
	/// <summary>
	/// Simple static class that provides static coroutine tools for MLUtils classes.
	/// </summary>
	public static class StaticCoroutine
	{
		private static CoroutineBehaviour instance;

		private static CoroutineBehaviour Instance 
		{
			get
			{
				if (instance == null)
				{
					GameObject gameObject = new GameObject("StaticCoroutine");
					instance = gameObject.AddComponent<CoroutineBehaviour>();
					GameObject.DontDestroyOnLoad(gameObject);
				}

				return instance;
			}
		}

		public static Coroutine StartCoroutine (IEnumerator routine)				{ return Instance.StartCoroutine(routine); }
		public static Coroutine StartCoroutine (string methodName)					{ return Instance.StartCoroutine(methodName); }
		public static Coroutine StartCoroutine (string methodName, object value)	{ return Instance.StartCoroutine(methodName, value); }
		public static void StopCoroutine (IEnumerator routine)						{ Instance.StopCoroutine(routine); }
		public static void StopCoroutine (Coroutine routine)						{ Instance.StopCoroutine(routine); }
		public static void StopCoroutine (string methodName)						{ Instance.StopCoroutine(methodName); }
		public static void StopAllCoroutines ()										{ Instance.StopAllCoroutines(); }

		[AddComponentMenu("")] // this prevents the component to appear on the add component menu (The only purpose for this script is to be accesed from code)
		private class CoroutineBehaviour : MonoBehaviour { }
	}
}