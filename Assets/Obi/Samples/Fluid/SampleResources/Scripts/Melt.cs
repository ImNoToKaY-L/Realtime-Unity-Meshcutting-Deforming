using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Obi;

[RequireComponent(typeof(ObiSolver))]
public class Melt : MonoBehaviour {

	public float heat = 0.1f;
	public float cooling = 0.1f;

 	ObiSolver solver;
	public Collider hotCollider = null;
	public Collider coldCollider = null;

	void Awake(){
		solver = GetComponent<Obi.ObiSolver>();
	}

	void OnEnable () {
		solver.OnCollision += Solver_OnCollision;
	}

	void OnDisable(){
		solver.OnCollision -= Solver_OnCollision;
	}
	
	void Solver_OnCollision (object sender, Obi.ObiSolver.ObiCollisionEventArgs e)
	{
		for(int i = 0;  i < e.contacts.Count; ++i)
		{
			if (e.contacts.Data[i].distance < 0.001f)
			{

				Component collider;
				if (ObiCollider.idToCollider.TryGetValue(e.contacts.Data[i].other,out collider)){

					int k = e.contacts.Data[i].particle;

					Vector4 userData = solver.userData[k];
					if (collider == hotCollider){
						userData[0] = Mathf.Max(0.05f,userData[0] - heat * Time.fixedDeltaTime);
						userData[1] = Mathf.Max(0.5f,userData[1] - heat * Time.fixedDeltaTime);
					}else if (collider == coldCollider){
						userData[0] = Mathf.Min(10,userData[0] + cooling * Time.fixedDeltaTime);
						userData[1] = Mathf.Min(2,userData[1] + cooling * Time.fixedDeltaTime);
					}
					solver.userData[k] = userData;
				}
			}
		}

	}

}
