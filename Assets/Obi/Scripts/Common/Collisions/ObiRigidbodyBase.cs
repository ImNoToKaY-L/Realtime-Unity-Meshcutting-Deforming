using UnityEngine;
using Unity.Profiling;
using System;
using System.Collections;

namespace Obi{

	/**
	 * Small helper class that lets you specify Obi-only properties for rigidbodies.
	 */

	[ExecuteInEditMode]
	public abstract class ObiRigidbodyBase : MonoBehaviour
	{
        static ProfilerMarker m_UpdateRigidbodiesPerfMarker = new ProfilerMarker("UpdateRigidbodies");
        static ProfilerMarker m_UpdateRigidbodyVelocitiesPerfMarker = new ProfilerMarker("UpdateRigidbodyVelocities");

        public bool kinematicForParticles = false;

        private IntPtr oniRigidbody = IntPtr.Zero;
		protected Oni.Rigidbody adaptor = new Oni.Rigidbody();
		protected Oni.RigidbodyVelocities oniVelocities = new Oni.RigidbodyVelocities();

		protected Vector3 velocity, angularVelocity;

        private delegate void RigidbodyUpdateCallback();
        private static event RigidbodyUpdateCallback OnUpdateRigidbodies;
        private static event RigidbodyUpdateCallback OnUpdateVelocities;

		public IntPtr OniRigidbody {
			get{
                if (oniRigidbody == IntPtr.Zero)
                    oniRigidbody = Oni.CreateRigidbody();
                return oniRigidbody;
            }
		}

		public virtual void Awake()
        {
			UpdateIfNeeded();
            ObiRigidbodyBase.OnUpdateRigidbodies += UpdateIfNeeded;
            ObiRigidbodyBase.OnUpdateVelocities += UpdateVelocities;
		}

		public void OnDestroy()
        {
            ObiRigidbodyBase.OnUpdateRigidbodies -= UpdateIfNeeded;
            ObiRigidbodyBase.OnUpdateVelocities -= UpdateVelocities;
			Oni.DestroyRigidbody(oniRigidbody);
			oniRigidbody = IntPtr.Zero;
		}

        public static void UpdateAllRigidbodies()
        {
            using (m_UpdateRigidbodiesPerfMarker.Auto())
            {
                if (OnUpdateRigidbodies != null)
                    OnUpdateRigidbodies();
            }
        }

        public static void UpdateAllVelocities()
        {
            using (m_UpdateRigidbodyVelocitiesPerfMarker.Auto())
            {
                if (OnUpdateVelocities != null)
                    OnUpdateVelocities();
            }
        }

		public abstract void UpdateIfNeeded();

		/**
		 * Reads velocities back from the solver.
		 */
		public abstract void UpdateVelocities();

	}
}

