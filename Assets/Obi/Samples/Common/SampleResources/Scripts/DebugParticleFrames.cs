using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;

[ExecuteInEditMode]
[RequireComponent(typeof(ObiActor))]
public class DebugParticleFrames : MonoBehaviour {

	ObiActor actor;
	public float size = 1;
	
	public void Awake()
    {
		actor = GetComponent<ObiActor>();
	}
	
	// Update is called once per frame
	void OnDrawGizmos () 
    {
        Vector4 b1 = new Vector4(1, 0, 0, 0);
        Vector4 b2 = new Vector4(0, 1, 0, 0);
        Vector4 b3 = new Vector4(0, 0, 1, 0);
        for (int i = 0; i < actor.activeParticleCount; ++i)
        {

            Vector3 position = actor.GetParticlePosition(actor.solverIndices[i]);
            actor.GetParticleAnisotropy(i,ref b1, ref b2, ref b3);
 
            Gizmos.color = Color.red;
            Gizmos.DrawRay(position, b1 * b1.w * size);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(position, b2 * b2.w * size);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(position, b3 * b3.w * size);
		}
	
	}
}
