using UnityEngine;
using System;
using System.Collections;

namespace Obi{

/**
 * Holds information about the physics properties of a particle or collider, and how it should react to collisions.
 */
[CreateAssetMenu(fileName = "collision material", menuName = "Obi/Collision Material", order = 180)]
public class ObiCollisionMaterial : ScriptableObject
{
	private IntPtr oniCollisionMaterial = IntPtr.Zero;
	Oni.CollisionMaterial adaptor = new Oni.CollisionMaterial();

	public float dynamicFriction;
	public float staticFriction;
	public float stickiness;
	public float stickDistance;
	
	public Oni.MaterialCombineMode frictionCombine;
	public Oni.MaterialCombineMode stickinessCombine;

	[Space]
	public bool rollingContacts = false;

	[Indent()]
	[VisibleIf("rollingContacts")]
	public float rollingFriction;

	public IntPtr OniCollisionMaterial{
		get{return oniCollisionMaterial;}
	}

	public void OnEnable(){
		oniCollisionMaterial = Oni.CreateCollisionMaterial();
		OnValidate();
	}

	public void OnDisable(){
		Oni.DestroyCollisionMaterial(oniCollisionMaterial);
		oniCollisionMaterial = IntPtr.Zero;
	}

	public void OnValidate()
	{
		adaptor.dynamicFriction = dynamicFriction;
		adaptor.staticFriction = staticFriction;
		adaptor.rollingFriction = rollingFriction;
		adaptor.stickiness = stickiness;
		adaptor.stickDistance = stickDistance;
		adaptor.frictionCombine = frictionCombine;
		adaptor.stickinessCombine = stickinessCombine;
		adaptor.rollingContacts = rollingContacts;
		
		Oni.UpdateCollisionMaterial(oniCollisionMaterial,ref adaptor);
	}
}
}