using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Obi{

	public class ObiDistanceFieldShapeTracker : ObiShapeTracker
	{
		public ObiDistanceField distanceField;

		private bool fieldDataHasChanged = false;

		public ObiDistanceFieldShapeTracker(ObiDistanceField distanceField){
			this.distanceField = distanceField;
			adaptor.is2D = false;
			oniShape = Oni.CreateShape(Oni.ShapeType.SignedDistanceField);
			fieldDataHasChanged = true;
		}		
	
		public override bool UpdateIfNeeded (){
	
			if (distanceField != null && distanceField.Initialized && fieldDataHasChanged){
				Oni.SetShapeDistanceField(oniShape,distanceField.OniDistanceField);
				fieldDataHasChanged = false;
				return true;
			}
			return false;
		}

	}
}

