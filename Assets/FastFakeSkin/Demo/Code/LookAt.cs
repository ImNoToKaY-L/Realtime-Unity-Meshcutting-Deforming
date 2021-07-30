using UnityEngine;
using System.Collections;

public class LookAt : MonoBehaviour {

	private Camera maincam;

	// Use this for initialization
	void Start () {
		maincam = Camera.main;
	}

	// Update is called once per frame
	void Update () {

		Vector3 mousePos = Input.mousePosition;
		mousePos.z = 10f;
		Vector3 target = maincam.ScreenToWorldPoint (mousePos);
		target.x += 30f;
		target.y -= 10f;
		target.z = 60f;
		this.transform.LookAt(target);

	}
}
