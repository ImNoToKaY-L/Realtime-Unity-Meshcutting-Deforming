﻿using UnityEngine;
using System.Collections;

public class camrea : MonoBehaviour
{

	//public enum RotationAxes { MouseXAndY = 0, MouseX = 1, MouseY = 2 }
	//public RotationAxes axes = RotationAxes.MouseXAndY;
	//public float sensitivityX = 15F;
	//public float sensitivityY = 15F;

	//public float minimumX = -360F;
	//public float maximumX = 360F;

	//public float minimumY = -60F;
	//public float maximumY = 60F;

	private Vector3 point;

	void Update()
	{

		transform.RotateAround(point, new Vector3(0.0f, 1.0f, 0.0f), 20 * Time.deltaTime);
		//if (axes == RotationAxes.MouseXAndY)
		//{
		//	float rotationX = transform.localEulerAngles.y + Input.GetAxis("Mouse X") * sensitivityX;

		//	rotationY += Input.GetAxis("Mouse Y") * sensitivityY;
		//	rotationY = Mathf.Clamp(rotationY, minimumY, maximumY);

		//	transform.localEulerAngles = new Vector3(-rotationY, rotationX, 0);
		//}
		//else if (axes == RotationAxes.MouseX)
		//{
		//	transform.Rotate(0, Input.GetAxis("Mouse X") * sensitivityX, 0);
		//}
		//else
		//{
		//	rotationY += Input.GetAxis("Mouse Y") * sensitivityY;
		//	rotationY = Mathf.Clamp(rotationY, minimumY, maximumY);

		//	transform.localEulerAngles = new Vector3(-rotationY, transform.localEulerAngles.y, 0);
		//}



	}

	void Start()
	{
		// Make the rigid body not change rotation
		point = GameObject.Find("fetus and pregnant woman 2.8").GetComponent<Transform>().position;
		//transform.LookAt(point);
	}
}
