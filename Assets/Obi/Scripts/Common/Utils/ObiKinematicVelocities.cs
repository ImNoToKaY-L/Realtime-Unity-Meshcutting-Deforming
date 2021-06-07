using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ObiKinematicVelocities : MonoBehaviour {

	private Quaternion prevRotation;
	private Vector3 prevPosition;
	private Rigidbody unityRigidbody;

	void Awake(){
		unityRigidbody = GetComponent<Rigidbody>();
		prevPosition = transform.position;
		prevRotation = transform.rotation;
	}

	void LateUpdate(){

		if (unityRigidbody.isKinematic)
		{
			// differentiate positions to obtain linear velocity:
			unityRigidbody.velocity = (transform.position - prevPosition) / Time.deltaTime;

			// differentiate rotations to obtain angular velocity:
			Quaternion delta = transform.rotation * Quaternion.Inverse(prevRotation);
			unityRigidbody.angularVelocity = new Vector3(delta.x,delta.y,delta.z) * 2.0f / Time.deltaTime;
		}

		prevPosition = transform.position;
		prevRotation = transform.rotation;

	}
}
