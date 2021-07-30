using UnityEngine;
using System.Collections;

public class Probe : MonoBehaviour {

	ReflectionProbe probe;

	// Use this for initialization
	void Awake() {
		probe = GetComponent<ReflectionProbe>();
	}

	// Update is called once per frame
	void Update () {
		probe.RenderProbe();
	}
}
