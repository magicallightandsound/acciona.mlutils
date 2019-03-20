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
using UnityEngine.XR.MagicLeap;

using System.Collections;

namespace Acciona.MLUtils.Examples
{
	/// <summary>
	/// Example class to manage the persistent boxes in the persistence scene.
	/// </summary>
	[RequireComponent(typeof(MLUController))]
	[RequireComponent(typeof(LineRenderer))]
	public class BoxManager : MonoBehaviour
	{
		private const float RESET_PRESS_TIME = 2.0F; // time in seconds to maintain bumper pressed to reset boxes

		public GameObject boxPrefab;
		public GameObject cursor;
		public Text respawnStatus;
		public int boxCount = 5;

		private GameObject[] boxes;
		private MLUController controller;
		private LineRenderer lineRenderer;
		private Coroutine moveCoroutine;
		private Coroutine resetCoroutine;
		private RaycastHit hit;

		void Awake ()
		{
			controller = GetComponent<MLUController>();
			lineRenderer = GetComponent<LineRenderer>();
			lineRenderer.positionCount = 2;
			respawnStatus.text = "";
			cursor.SetActive(false);

			int savedBoxCount = PlayerPrefs.GetInt("boxCount", 0);
			boxCount = Mathf.Max(boxCount, savedBoxCount);

			if (savedBoxCount > 0)
			{
				boxes = new GameObject[boxCount];
				
				for (int i = 0; i < boxCount; i++)
				{
					boxPrefab.name = GetBoxName(i);
					boxes[i] = GameObject.Instantiate(boxPrefab, controller.Position + controller.Orientation * Vector3.forward, controller.Orientation);
				}
			}
			else
			{
				resetCoroutine = StartCoroutine(C_ResetCoroutine(false));
			}
		}

		void Update ()
		{
			cursor.SetActive(false);

			if (resetCoroutine == null)
			{
				lineRenderer.SetPositions(new Vector3[] { controller.Position, controller.Position + controller.Orientation * (1000.0F * Vector3.forward) });

				if (controller.BumperPressed)
				{
					resetCoroutine = StartCoroutine(C_ResetCoroutine());
				}
				else if (Physics.Raycast(controller.Position, controller.Orientation * Vector3.forward, out hit) && hit.collider.GetComponent<MLUPersistentObject>() != null)
				{
					lineRenderer.SetPositions(new Vector3[] { controller.Position, hit.point });
					cursor.transform.position = hit.point;
					cursor.SetActive(true);

					if (controller.TriggerPressed)
					{
						if (moveCoroutine != null) StopCoroutine(moveCoroutine);
						moveCoroutine = StartCoroutine(C_Move(hit.collider.transform, hit.point));
					}
				}
			}
		}

		private IEnumerator C_Move (Transform target, Vector3 hitPoint)
		{
			// register target and cursor transformation matrices parented to the controller's transform
			ParentedMatrixTR targetMatrix = new ParentedMatrixTR(target.position, target.rotation, controller.Position, controller.Orientation);
			ParentedMatrixTR cursorMatrix = new ParentedMatrixTR(cursor.transform.position, cursor.transform.rotation, controller.Position, controller.Orientation);

			yield return null;

			while (controller.TriggerDown)
			{
				// update the parented matrices with the new controller's transform on this frame
				targetMatrix.UpdateParentTR(controller.Position, controller.Orientation);
				cursorMatrix.UpdateParentTR(controller.Position, controller.Orientation);
				// set Unit's transforms for target and cursor objects with the updated parented matrices's transform
				targetMatrix.SetTransform(target);
				cursorMatrix.SetTransform(cursor.transform);

				// update linerenderer and cursor visibility
				lineRenderer.SetPositions(new Vector3[] { controller.Position, cursor.transform.position });
				cursor.SetActive(true);

				yield return null;
			}

			moveCoroutine = null;
		}

		private IEnumerator C_ResetCoroutine (bool waitForHold = true)
		{	
			float time = 0.0F;

			if (waitForHold)
			{
				respawnStatus.text = "Bumper press detected!\nHold it for " + RESET_PRESS_TIME + " seconds to respawn all boxes";

				// check how much time the bumper is holded pressed
				while (controller.BumperDown && time < RESET_PRESS_TIME)
				{
					yield return null;
					time += Time.deltaTime;
				}
			}

			// wait for trigger release (if it was down)

			// if bumper has been pressed for the reset time then respawn all boxes
			if (!waitForHold || time >= RESET_PRESS_TIME)
			{
				if (boxes != null)
					foreach (GameObject box in boxes)
						if (box != null)
							Destroy(box);
				
				if (boxes == null || boxes.Length != boxCount)
					boxes = new GameObject[boxCount];
				
				controller.InputController.StartFeedbackPatternVibe(MLInputControllerFeedbackPatternVibe.DoubleClick, MLInputControllerFeedbackIntensity.High);
				
				for (int i = 0; i < boxCount; i++)
				{
					respawnStatus.text =	"Respawning...\n";
					respawnStatus.text +=	"Press trigger to spawn next box (" + (boxCount - i) + " left).\n"; 
					respawnStatus.text +=	"Maintain trigger pressed to move it."; 
					
					// if trigger were down we wait till its release
					while (controller.TriggerDown)
					{
						lineRenderer.SetPositions(new Vector3[] { controller.Position, controller.Orientation * Vector3.forward * 1000.0F } );
						yield return null;
					}

					// wait for trigger press
					while (controller.TriggerUp)
					{
						lineRenderer.SetPositions(new Vector3[] { controller.Position, controller.Position + controller.Orientation * Vector3.forward } );
						cursor.transform.position = lineRenderer.GetPosition(1);
						cursor.SetActive(true);
						yield return null;
					}

					boxPrefab.name = GetBoxName(i);
					boxes[i] = GameObject.Instantiate(boxPrefab, controller.Position + controller.Orientation * Vector3.forward, controller.Orientation);
					respawnStatus.text = "Release trigger to place the box";
					cursor.transform.position = boxes[i].transform.position;
					yield return C_Move(boxes[i].transform, boxes[i].transform.position);
				}

				controller.InputController.StartFeedbackPatternVibe(MLInputControllerFeedbackPatternVibe.DoubleClick, MLInputControllerFeedbackIntensity.High);
				PlayerPrefs.SetInt("boxCount", boxCount);
			}

			respawnStatus.text = "";
			resetCoroutine = null;
		}

		private string GetBoxName (int index)
		{
			return "PersistentBox_" + index;
		}

		/// <summary>
		/// Transform/Rotation parented matrix to help track a parented position/orientation movement without depending on Unity's Transform class.
		/// </summary>
		private struct ParentedMatrixTR
		{
			private Matrix4x4 matrix, parent;
			private readonly Matrix4x4 local;

			public Vector3 Position { get { return matrix.GetColumn(3); } }
			public Quaternion Rotation { get { return matrix.rotation; } }

			public ParentedMatrixTR (Vector3 position, Quaternion rotation, Vector3 parentPosition, Quaternion parentRotation)
			{
				this.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
				this.parent = Matrix4x4.TRS(parentPosition, parentRotation, Vector3.one);
				this.local = parent.inverse * matrix;
			}

			public void UpdateParentTR (Vector3 position, Quaternion rotation)
			{
				parent.SetTRS(position, rotation, Vector3.one);
				matrix = parent * local;
			}

			public void SetTransform (Transform transform)
			{
				transform.position = Position;
				transform.rotation = Rotation;
			}
		}
	}
}