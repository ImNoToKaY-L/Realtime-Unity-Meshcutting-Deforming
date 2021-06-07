using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{

    /**
	 * Add this component to any Collider that you want to be considered by Obi.
	 */
    [ExecuteInEditMode]
    [RequireComponent(typeof(Collider))]
    public class ObiCollider : ObiColliderBase
    {

        [SerializeProperty("SourceCollider")]
        [SerializeField] private Collider sourceCollider;

        public Collider SourceCollider
        {
            set
            {
                if (value != null && value.gameObject != this.gameObject)
                {
                    Debug.LogError("The Collider component must reside in the same GameObject as ObiCollider.");
                    return;
                }

                sourceCollider = value;
                RemoveCollider();
                AddCollider();

            }
            get { return sourceCollider; }
        }

        [SerializeProperty("AccurateContacts")]
        [SerializeField] private bool accurateContacts;

        public bool AccurateContacts
        {
            set
            {
                if (accurateContacts != value)
                {
                    accurateContacts = value;
                    CreateTracker();
                }
            }
            get { return accurateContacts; }
        }


        [SerializeProperty("UseDistanceFields")]
        [SerializeField] private bool useDistanceFields = false;

        public bool UseDistanceFields
        {
            set
            {
                if (useDistanceFields != value)
                {

                    useDistanceFields = value;
                    CreateTracker();

                }
            }
            get { return useDistanceFields; }
        }

        [Indent]
        [VisibleIf("useDistanceFields")]
        public ObiDistanceField distanceField; /**< Distance field used by this collider.*/

        /**
		 * Creates an OniColliderTracker of the appropiate type.
   		 */
        protected override void CreateTracker()
        {

            if (tracker != null)
            {
                Oni.SetColliderShape(oniCollider, IntPtr.Zero);
                tracker.Destroy();
                tracker = null;
            }

            if (useDistanceFields)
                tracker = new ObiDistanceFieldShapeTracker(distanceField);
            else
            {

                if (sourceCollider is SphereCollider)
                    tracker = new ObiSphereShapeTracker((SphereCollider)sourceCollider);
                else if (sourceCollider is BoxCollider)
                    tracker = new ObiBoxShapeTracker((BoxCollider)sourceCollider);
                else if (sourceCollider is CapsuleCollider)
                    tracker = new ObiCapsuleShapeTracker((CapsuleCollider)sourceCollider);
                else if (sourceCollider is CharacterController)
                    tracker = new ObiCapsuleShapeTracker((CharacterController)sourceCollider);
                else if (sourceCollider is TerrainCollider)
                    tracker = new ObiTerrainShapeTracker((TerrainCollider)sourceCollider, accurateContacts);
                else if (sourceCollider is MeshCollider)
                {
                    tracker = new ObiMeshShapeTracker((MeshCollider)sourceCollider);
                }
                else
                    Debug.LogWarning("Collider type not supported by Obi.");

            }

            if (tracker != null)
                Oni.SetColliderShape(oniCollider, tracker.OniShape);

        }

        protected override Component GetUnityCollider(ref bool enabled)
        {

            if (sourceCollider != null)
                enabled = sourceCollider.enabled;

            return sourceCollider;
        }

        protected override void UpdateAdaptor()
        {
            adaptor.Set(sourceCollider, Phase, Thickness);
            Oni.UpdateCollider(oniCollider, ref adaptor);
        }

        protected override void FindSourceCollider()
        {
            if (SourceCollider == null)
                SourceCollider = GetComponent<Collider>();
            else
                AddCollider();
        }

    }
}

