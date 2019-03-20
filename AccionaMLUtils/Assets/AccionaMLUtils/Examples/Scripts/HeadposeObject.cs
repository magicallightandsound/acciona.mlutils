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

namespace Acciona.MLUtils.Examples
{
	/// <summary>
	/// Fast modification of HeadposeCanvas script from MagicLeap package to not depend on a canvas.
	/// </summary>
    public class HeadposeObject : MonoBehaviour
    {
        public Camera _camera;

        [Tooltip("The distance from the camera that this object should be placed.")]
        public float CanvasDistance = 1.5f;
        [Tooltip("The speed at which this object changes it's position.")]
        public float PositionLerpSpeed = 5f;
        [Tooltip("The speed at which this object changes it's rotation.")]
        public float RotationLerpSpeed = 5f;

        void Update()
        {
            // Move the object CanvasDistance units in front of the camera.
            float posSpeed = Time.deltaTime * PositionLerpSpeed;
            Vector3 posTo = _camera.transform.position + (_camera.transform.forward * CanvasDistance);
            transform.position = Vector3.SlerpUnclamped(transform.position, posTo, posSpeed);

            // Rotate the object to face the camera.
            float rotSpeed = Time.deltaTime * RotationLerpSpeed;
            Quaternion rotTo = Quaternion.LookRotation(transform.position - _camera.transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotTo, rotSpeed);
        }
    }
}
