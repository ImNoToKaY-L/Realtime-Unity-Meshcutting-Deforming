using System;
using UnityEngine;

namespace Obi{

	public class ObiCircleShapeTracker2D : ObiShapeTracker
	{
		private float radius;
		private Vector2 center;

		public ObiCircleShapeTracker2D(CircleCollider2D collider){
			this.collider = collider;
			adaptor.is2D = true;
			oniShape = Oni.CreateShape(Oni.ShapeType.Sphere);
		}	

		public override bool UpdateIfNeeded (){

			CircleCollider2D sphere = collider as CircleCollider2D;
	
			if (sphere != null && (sphere.radius != radius || sphere.offset != center)){
				radius = sphere.radius;
				center = sphere.offset;
				adaptor.Set(center, radius);
				Oni.UpdateShape(oniShape,ref adaptor);
				return true;
			}
			return false;
		}

	}
}

