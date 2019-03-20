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
using UnityEngine.UI;

namespace Acciona.MLUtils.Examples
{
	[RequireComponent(typeof(MLUPersistentObject))]
	public class PersistentObjectStatus : MonoBehaviour
	{
		public Text text;

		private MLUPersistentObject persistentObject;

		void Awake ()
		{
			persistentObject = GetComponent<MLUPersistentObject>();
		}

		void Update ()
		{
			if (persistentObject.IsBinded)
			{
				text.text = "Binded";
				text.color = Color.green;
			}
			else if (persistentObject.IsRestoring)
			{
				text.text = "Restoring...";
				text.color = Color.yellow;
			}
			else
			{
				text.text = "Not binded";
				text.color = Color.red;
			}
		}
	}
}
