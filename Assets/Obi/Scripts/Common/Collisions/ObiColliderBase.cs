using UnityEngine;
using Unity.Profiling;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{

    /**
	 * Implements common functionality for ObiCollider and ObiCollider2D.
	 */
    public abstract class ObiColliderBase : MonoBehaviour
    {
        static ProfilerMarker m_UpdateCollidersPerfMarker = new ProfilerMarker("UpdateColliders");

        public static Dictionary<int, Component> idToCollider = new Dictionary<int, Component>(); /**< holds pairs of <IntanceID,Collider>. 
																									  Contacts returned by Obi will provide the instance ID of the 
																									  collider involved in the collision, use it to index this dictionary
																									  and find the actual object.*/

        [SerializeProperty("CollisionMaterial")]
        [SerializeField] private ObiCollisionMaterial material;

        [SerializeProperty("Phase")]
        [SerializeField] private int phase = 0;

        [SerializeProperty("Thickness")]
        [SerializeField] private float thickness = 0;

        public ObiCollisionMaterial CollisionMaterial
        {
            set
            {
                material = value;
                UpdateMaterial();
            }
            get { return material; }
        }

        public int Phase
        {
            set
            {
                if (phase != value)
                {
                    phase = value;
                    dirty = true;
                }
            }
            get { return phase; }
        }

        public float Thickness
        {
            set
            {
                if (thickness != value)
                {
                    thickness = value;
                    dirty = true;
                }
            }
            get { return thickness; }
        }

        public ObiShapeTracker Tracker
        {
            get { return tracker; }
        }

        public IntPtr OniCollider
        {
            get
            {
                if (oniCollider == IntPtr.Zero)
                    FindSourceCollider();
                return oniCollider;
            }
        }

        protected IntPtr oniCollider = IntPtr.Zero;
        protected ObiRigidbodyBase obiRigidbody;
        protected bool wasUnityColliderEnabled = true;
        protected bool dirty = false;

        protected ObiShapeTracker tracker;                               /**< tracker object used to determine when to update the collider's shape*/
        protected Oni.Collider adaptor = new Oni.Collider();			 /**< adaptor struct used to transfer collider data to the library.*/

        private delegate void ColliderUpdateCallback();
        private static event ColliderUpdateCallback OnUpdateColliders;
        private static event ColliderUpdateCallback OnResetColliderTransforms;

        public static void UpdateColliders()
        {
            using (m_UpdateCollidersPerfMarker.Auto())
            {
                if (OnUpdateColliders != null)
                    OnUpdateColliders();
            }
        }

        public static void ResetColliderTransforms()
        {
            if (OnResetColliderTransforms != null)
                OnResetColliderTransforms();
        }

        /**
		 * Creates an OniColliderTracker of the appropiate type.
   		 */
        protected abstract void CreateTracker();

        protected abstract Component GetUnityCollider(ref bool enabled);

        protected abstract void UpdateAdaptor();

        protected abstract void FindSourceCollider();

        protected void CreateRigidbody()
        {

            obiRigidbody = null;

            // find the first rigidbody up our hierarchy:
            Rigidbody rb = GetComponentInParent<Rigidbody>();
            Rigidbody2D rb2D = GetComponentInParent<Rigidbody2D>();

            // if we have an rigidbody above us, see if it has a ObiRigidbody component and add one if it doesn't:
            if (rb != null)
            {

                obiRigidbody = rb.GetComponent<ObiRigidbody>();

                if (obiRigidbody == null)
                    obiRigidbody = rb.gameObject.AddComponent<ObiRigidbody>();

                Oni.SetColliderRigidbody(oniCollider, obiRigidbody.OniRigidbody);

            }
            else if (rb2D != null)
            {

                obiRigidbody = rb2D.GetComponent<ObiRigidbody2D>();

                if (obiRigidbody == null)
                    obiRigidbody = rb2D.gameObject.AddComponent<ObiRigidbody2D>();

                Oni.SetColliderRigidbody(oniCollider, obiRigidbody.OniRigidbody);

            }
            else
                Oni.SetColliderRigidbody(oniCollider, IntPtr.Zero);


        }

        private void UpdateMaterial()
        {
            if (material != null)
                Oni.SetColliderMaterial(oniCollider, material.OniCollisionMaterial);
            else
                Oni.SetColliderMaterial(oniCollider, IntPtr.Zero);
        }

        private void OnTransformParentChanged()
        {
            CreateRigidbody();
        }

        protected void AddCollider()
        {

            Component unityCollider = GetUnityCollider(ref wasUnityColliderEnabled);

            if (unityCollider != null && oniCollider == IntPtr.Zero)
            {

                // register the collider:
                idToCollider[unityCollider.GetInstanceID()] = unityCollider;

                oniCollider = Oni.CreateCollider();

                CreateTracker();

                // Send initial collider data:
                UpdateAdaptor();

                // Update collider material:
                UpdateMaterial();

                // Create rigidbody if necessary, and link ourselves to it:
                CreateRigidbody();

                // Subscribe collider callback:
                ObiColliderBase.OnUpdateColliders += UpdateIfNeeded;
                ObiColliderBase.OnResetColliderTransforms += ResetTransformChangeFlag;

            }
        }

        protected void RemoveCollider()
        {
            if (oniCollider != IntPtr.Zero)
            {
                // Unregister collider:
                Component unityCollider = GetUnityCollider(ref wasUnityColliderEnabled);
                if (unityCollider != null)
                    idToCollider.Remove(unityCollider.GetInstanceID());

                // Unsubscribe collider callback:
                ObiColliderBase.OnUpdateColliders -= UpdateIfNeeded;
                ObiColliderBase.OnResetColliderTransforms -= ResetTransformChangeFlag;

                // Remove and destroy collider:
                Oni.RemoveCollider(oniCollider);
                Oni.DestroyCollider(oniCollider);
                oniCollider = IntPtr.Zero;

                // Destroy shape tracker:
                if (tracker != null)
                {
                    tracker.Destroy();
                    tracker = null;
                }
            }
        }

        /** 
		 * Check if the collider transform or its shape have changed any relevant property, and update their Oni counterparts.
		 */
        public void UpdateIfNeeded()
        {

            bool unityColliderEnabled = false;
            Component unityCollider = GetUnityCollider(ref unityColliderEnabled);

            if (unityCollider != null)
            {

                // update the collider:
                if ((tracker != null && tracker.UpdateIfNeeded()) ||
                    transform.hasChanged ||
                    dirty ||
                    unityColliderEnabled != wasUnityColliderEnabled)
                {

                    dirty = false;
                    wasUnityColliderEnabled = unityColliderEnabled;

                    // remove the collider from all solver's spatial partitioning grid:
                    Oni.RemoveCollider(oniCollider);

                    // update the collider:
                    UpdateAdaptor();

                    // re-add the collider to all solver's spatial partitioning grid:
                    if (unityColliderEnabled)
                        Oni.AddCollider(oniCollider);

                }
            }
            // If the unity collider is null but its Oni counterpart isn't, the unity collider has been destroyed.
            else if (oniCollider != IntPtr.Zero)
                RemoveCollider();


        }

        private void ResetTransformChangeFlag()
        {
            //this is done for all colliders at once, when the simulation step has ended.
            transform.hasChanged = false;
        }

        private void Awake()
        {
            // Initialize using the source collider specified by the usee (or find an appropiate one).
            FindSourceCollider();
        }

        private void OnDestroy()
        {

            // Cleanup:
            RemoveCollider();

        }

        private void OnEnable()
        {
            // Add collider to current solvers:
            Oni.AddCollider(oniCollider);
        }

        private void OnDisable()
        {
            // Remove collider from current solvers:
            Oni.RemoveCollider(oniCollider);
        }

    }
}

