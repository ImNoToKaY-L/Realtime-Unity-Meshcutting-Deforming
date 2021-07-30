using UnityEngine;
using System.Collections;

public class Breathe : MonoBehaviour {

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
		float pong = Mathf.PingPong (Time.time, 2) - 1f;
		this.transform.RotateAround (this.transform.position, this.transform.right, pong * 0.01f);

	}
}
