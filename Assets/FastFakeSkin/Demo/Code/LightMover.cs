using UnityEngine;
using System.Collections;

public class LightMover : MonoBehaviour {

	private float lerper = 0f;
	private float smoothTime = 0.3f;
	private float velocity = 0f;

	// Use this for initialization
	void Start () {
		StartCoroutine (Rando());
	}
	
	// Update is called once per frame
	void Update () {
	// 0.79 0.55


		float newPosition = Mathf.SmoothDamp(this.transform.position.x, lerper, ref velocity, smoothTime);
		Vector3 newmover = new Vector3 (newPosition, this.transform.position.y, this.transform.position.z);

		this.transform.position = newmover;

	}

	IEnumerator Rando () {
		for (;;) {
			lerper = (float)(Random.Range (50, 85) * 0.01);
			yield return new WaitForSeconds (0.8f);
		}
	}

}
