using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Obi
{

    /**
     * Represents a group of related particles. ObiActor does not make
     * any assumptions about the relationship between these particles, except that they get allocated 
     * and released together.
     */
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public abstract class ObiActor : MonoBehaviour, IObiParticleCollection
    {
        public class ObiActorSolverArgs : System.EventArgs
        {

            private ObiSolver m_Solver;
            public ObiSolver solver
            {
                get { return m_Solver; }
            }

            public ObiActorSolverArgs(ObiSolver solver)
            {
                this.m_Solver = solver;
            }
        }

        public delegate void ActorCallback(ObiActor actor);
        public delegate void ActorStepCallback(ObiActor actor,float stepTime);
        public delegate void ActorBlueprintCallback(ObiActor actor,ObiActorBlueprint blueprint);

        public event ActorBlueprintCallback OnBlueprintLoaded;
        public event ActorBlueprintCallback OnBlueprintUnloaded;

        public event ActorStepCallback OnBeginStep;
        public event ActorStepCallback OnSubstep;
        public event ActorCallback OnEndStep;
        public event ActorCallback OnInterpolate;

        protected ObiSolver m_Solver;
        protected IObiConstraints[] m_Constraints = new IObiConstraints[0];               /**< list of constraint components used by this actor. May contain null entries.*/
        [SerializeField] [HideInInspector] protected ObiCollisionMaterial m_CollisionMaterial;

        [HideInInspector] protected int m_ActiveParticleCount = 0;
        [HideInInspector] public int[] solverIndices;                 /**< indices of allocated particles in the solver.*/
        protected bool m_Loaded= false;               /**< True if the blueprint is  oaded in a solver. False otherwise.*/

        private ObiActorBlueprint state;    /**< temporary instance of the blueprint, used to store physics state when moving the actor to a new solver.*/

        public ObiSolver solver
        {
            get { return m_Solver; }
        }

        public bool isLoaded
        {
            get { return m_Loaded; }
        }

        public ObiCollisionMaterial collisionMaterial
        {
            get
            {
                return m_CollisionMaterial;
            }
            set
            {
                if (m_CollisionMaterial != value)
                {
                    m_CollisionMaterial = value;
                    PushCollisionMaterial();
                }
            }
        }

        public int particleCount
        {
            get
            {
                return blueprint != null ? blueprint.particleCount : 0;
            }
        }

        public int activeParticleCount
        {
            get
            {
               return m_ActiveParticleCount;
            }
        }

        public bool usesOrientedParticles
        {
            get
            {
                return blueprint != null &&
                       blueprint.invRotationalMasses != null && blueprint.invRotationalMasses.Length > 0 &&
                       blueprint.orientations != null && blueprint.orientations.Length > 0 &&
                       blueprint.restOrientations != null && blueprint.restOrientations.Length > 0;
            }
        }

        /**
    	 * If true, it means particles may not be completely spherical, but ellipsoidal.
    	 */
        public virtual bool usesAnisotropicParticles
        {
            get
            {
                return false;
            }
        }

        /**
    	 * If true, it means external forces aren't applied to the particles directly. For instance,
    	 * cloth uses aerodynamic constraints to do so, and fluid uses drag.
    	 */
        public virtual bool usesCustomExternalForces
        {
            get { return false; }
        }

        public Matrix4x4 actorLocalToSolverMatrix
        {
            get
            {
                if (m_Solver != null)
                    return m_Solver.transform.worldToLocalMatrix * transform.localToWorldMatrix;
                else
                    return transform.localToWorldMatrix;
            }
        }

        public Matrix4x4 actorSolverToLocalMatrix
        {
            get
            {
                if (m_Solver != null)
                    return transform.worldToLocalMatrix * m_Solver.transform.localToWorldMatrix;
                else
                    return transform.worldToLocalMatrix;
            }
        }

        public abstract ObiActorBlueprint blueprint
        {
            get;
        }

        protected void Awake()
        {
#if UNITY_EDITOR

            // Check if this script's GameObject is in a PrefabStage
            var prefabStage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject);

            if (prefabStage != null)
            {
                // Only create a solver if there's not one up our hierarchy.
                if (GetComponentInParent<ObiSolver>() == null)
                {
                    // Add our own environment root and move it to the PrefabStage scene
                    var newParent = new GameObject("ObiSolver (Environment)", typeof(ObiSolver), typeof(ObiLateFixedUpdater));
                    newParent.GetComponent<ObiLateFixedUpdater>().solvers.Add(newParent.GetComponent<ObiSolver>());
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(newParent, gameObject.scene);
                    transform.root.parent = newParent.transform;
                }
            }
#endif
        }

        protected virtual void OnEnable()
        {
            // when an actor is enabled, grabs the first solver up its hierarchy,
            // initializes it (if not initialized) and gets added to it.
            m_Solver = GetComponentInParent<ObiSolver>();

            AddToSolver();
        }

        protected virtual void OnDisable()
        {
            RemoveFromSolver();
        }

        protected virtual void OnValidate()
        {
            PushCollisionMaterial();
        }

        void OnTransformParentChanged()
        {
            if (isActiveAndEnabled)
                SetSolver(GetComponentInParent<ObiSolver>());
        }

        public void AddToSolver()
        {
            if (m_Solver != null)
            {
                m_Solver.Initialize();
                if (!m_Solver.AddActor(this))
                    m_Solver = null;
                else if (blueprint != null)
                    blueprint.OnBlueprintGenerate += OnBlueprintRegenerate;
            }
        }

        public void RemoveFromSolver()
        {
            if (m_Solver != null)
            {
                m_Solver.RemoveActor(this);
                if (blueprint != null)
                    blueprint.OnBlueprintGenerate -= OnBlueprintRegenerate;
            }
        }

        private void SetSolver(ObiSolver newSolver)
        {
            // In case the first solver up our hierarchy is not the one we're currently in, change solver.
            if (newSolver != m_Solver)
            {
                RemoveFromSolver();

                m_Solver = newSolver;

                AddToSolver();
            }
        }

        // Called when the blueprint is regerenated while loaded.
        protected virtual void OnBlueprintRegenerate(ObiActorBlueprint blueprint)
        {
            // Reload:
            RemoveFromSolver();
            AddToSolver();
        }

        protected void PushDistanceConstraints(bool enabled, float compliance, float maxCompression)
        {
            var dc = GetConstraintsByType(Oni.ConstraintType.Distance) as ObiConstraints<ObiDistanceConstraintsBatch>;
            if (dc != null)
            {
                foreach (ObiDistanceConstraintsBatch batch in dc.batches)
                {
                    batch.SetEnabled(enabled);
                    batch.SetParameters(Mathf.Max(0, compliance), maxCompression, 1);
                }
            }
        }

        protected void PushTetherConstraints(bool enabled, float compliance, float scale)
        {
            var dc = GetConstraintsByType(Oni.ConstraintType.Tether) as ObiConstraints<ObiTetherConstraintsBatch>;
            if (dc != null)
            {
                foreach (ObiTetherConstraintsBatch batch in dc.batches)
                {
                    batch.SetEnabled(enabled);
                    batch.SetParameters(Mathf.Max(0, compliance), scale);
                }
            }
        }

        protected void PushBendConstraints(bool enabled, float compliance, float maxBending)
        {
            var bc = GetConstraintsByType(Oni.ConstraintType.Bending) as ObiConstraints<ObiBendConstraintsBatch>;
            if (bc != null)
            {
                foreach (ObiBendConstraintsBatch batch in bc.batches)
                {
                    batch.SetEnabled(enabled);
                    batch.SetParameters(Mathf.Max(0, compliance), maxBending);
                }
            }
        }

        protected void PushStretchShearConstraints(bool enabled, float stretchCompliance, float shear1Compliance, float shear2Compliance)
        {
            var dc = GetConstraintsByType(Oni.ConstraintType.StretchShear) as ObiConstraints<ObiStretchShearConstraintsBatch>;
            if (dc != null)
            {
                foreach (ObiStretchShearConstraintsBatch batch in dc.batches)
                {
                    batch.SetEnabled(enabled);
                    batch.SetParameters(Mathf.Max(0, stretchCompliance), Mathf.Max(0, shear1Compliance), Mathf.Max(0, shear2Compliance));
                }
            }
        }

        protected void PushBendTwistConstraints(bool enabled, float torsionCompliance, float bend1Compliance, float bend2Compliance)
        {
            var dc = GetConstraintsByType(Oni.ConstraintType.BendTwist) as ObiConstraints<ObiBendTwistConstraintsBatch>;
            if (dc != null)
            {
                foreach (ObiBendTwistConstraintsBatch batch in dc.batches)
                {
                    batch.SetEnabled(enabled);
                    batch.SetParameters(Mathf.Max(0, torsionCompliance), Mathf.Max(0, bend1Compliance), Mathf.Max(0, bend2Compliance));
                }
            }
        }

        protected void PushAerodynamicConstraints(bool enabled, float drag, float lift)
        {
            var ac = GetConstraintsByType(Oni.ConstraintType.Aerodynamics) as ObiConstraints<ObiAerodynamicConstraintsBatch>;
            if (ac != null)
            {
                foreach (ObiAerodynamicConstraintsBatch batch in ac.batches)
                {
                    batch.SetEnabled(enabled);
                    batch.SetParameters(drag, lift);
                }
            }
        }

        protected void PushVolumeConstraints(bool enabled, float compliance, float pressure)
        {
            var ac = GetConstraintsByType(Oni.ConstraintType.Volume) as ObiConstraints<ObiVolumeConstraintsBatch>;
            if (ac != null)
            {
                foreach (ObiVolumeConstraintsBatch batch in ac.batches)
                {
                    batch.SetEnabled(enabled);
                    batch.SetParameters(Mathf.Max(0, compliance), pressure);
                }
            }
        }

        protected void PushShapeMatchingConstraints(bool enabled, float stiffness, float yield, float creep, float recovery, float maxDeformation)
        {
            var ac = GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiConstraints<ObiShapeMatchingConstraintsBatch>;
            if (ac != null)
            {
                foreach (ObiShapeMatchingConstraintsBatch batch in ac.batches)
                {
                    batch.SetEnabled(enabled);
                    batch.SetParameters(stiffness, yield, creep, recovery, maxDeformation);
                }
            }
        }

        protected void PushChainConstraints(bool enabled, float tightness)
        {
            var cc = GetConstraintsByType(Oni.ConstraintType.Chain) as ObiConstraints<ObiChainConstraintsBatch>;
            if (cc != null)
            {
                foreach (ObiChainConstraintsBatch batch in cc.batches)
                {
                    batch.SetEnabled(enabled);
                    batch.SetParameters(tightness);
                }
            }
        }

        protected void PushCollisionMaterial()
        {
            if (m_Solver != null && solverIndices != null)
            {
                IntPtr[] materials = new IntPtr[solverIndices.Length];
                for (int i = 0; i < solverIndices.Length; i++)
                    materials[i] = m_CollisionMaterial != null ? m_CollisionMaterial.OniCollisionMaterial : IntPtr.Zero;
                Oni.SetCollisionMaterials(m_Solver.OniSolver, materials, solverIndices, solverIndices.Length);
            }
        }

        /**
		 * Extend CopyParticleData to implement copying your own custom particle data.
		 */
        public virtual void CopyParticle(int actorSourceIndex, int actorDestIndex)
        {
            if (actorSourceIndex < 0 || actorSourceIndex >= solverIndices.Length ||
                actorDestIndex < 0 || actorDestIndex >= solverIndices.Length)
                return;

            int sourceIndex = solverIndices[actorSourceIndex];
            int destIndex = solverIndices[actorDestIndex];

            // Copy solver data:
            m_Solver.prevPositions[destIndex] = m_Solver.prevPositions[sourceIndex];
            m_Solver.renderablePositions[destIndex] = m_Solver.renderablePositions[sourceIndex];
            m_Solver.startPositions[destIndex] = m_Solver.positions[destIndex] = m_Solver.positions[sourceIndex];
            m_Solver.startOrientations[destIndex] = m_Solver.orientations[destIndex] = m_Solver.orientations[sourceIndex];
            m_Solver.restPositions[destIndex] = m_Solver.restPositions[sourceIndex];
            m_Solver.restOrientations[destIndex] = m_Solver.restOrientations[sourceIndex];
            m_Solver.velocities[destIndex] = m_Solver.velocities[sourceIndex];
            m_Solver.angularVelocities[destIndex] = m_Solver.velocities[sourceIndex];
            m_Solver.invMasses[destIndex] = m_Solver.invMasses[sourceIndex];
            m_Solver.invRotationalMasses[destIndex] = m_Solver.invRotationalMasses[sourceIndex];
            m_Solver.principalRadii[destIndex] = m_Solver.principalRadii[sourceIndex];
            m_Solver.phases[destIndex] = m_Solver.phases[sourceIndex];
            m_Solver.colors[destIndex] = m_Solver.colors[sourceIndex];
        }

        public void TeleportParticle(int actorIndex, Vector3 position)
        {
            if (actorIndex < 0 || actorIndex >= solverIndices.Length)
                return;

            int solverIndex = solverIndices[actorIndex];

            Vector4 delta = (Vector4)position - m_Solver.positions[solverIndex];
            m_Solver.positions[solverIndex] += delta;
            m_Solver.prevPositions[solverIndex] += delta;
            m_Solver.renderablePositions[solverIndex] += delta;
            m_Solver.startPositions[solverIndex] += delta;

        }

        protected virtual void SwapWithFirstInactiveParticle(int actorIndex)
        {
            // update solver indices:
            m_Solver.particleToActor[solverIndices[actorIndex]].indexInActor = m_ActiveParticleCount;
            m_Solver.particleToActor[solverIndices[m_ActiveParticleCount]].indexInActor = actorIndex;

            solverIndices.Swap(actorIndex, m_ActiveParticleCount);
        }

        /** 
         * Activates one particle. This operation preserves the relative order of all particles.
         */
        public bool ActivateParticle(int actorIndex)
        {
            if (IsParticleActive(actorIndex))
                return false;

            SwapWithFirstInactiveParticle(actorIndex);
            m_ActiveParticleCount++;
            m_Solver.activeParticleCountChanged = true;

            return true;
        }

        /** 
         * Deactivates one particle. This operation does not preserve the relative order of other particles, because the last active particle will
         * swap positions with the particle being deactivated.
         */
        public bool DeactivateParticle(int actorIndex)
        {
            if (!IsParticleActive(actorIndex))
                return false;

            m_ActiveParticleCount--;
            SwapWithFirstInactiveParticle(actorIndex);
            m_Solver.activeParticleCountChanged = true;

            return true;
        }

        public bool IsParticleActive(int actorIndex)
        {
            return actorIndex < m_ActiveParticleCount;
        }

        /**
    	 * Updates particle phases in the solver. 
    	 * 
    	 * - At runtime, initialize with blueprint phases.
    	 * - When user changes self collision or phase, set.
    	 */
        public virtual void SetSelfCollisions(bool selfCollisions)
        {
            if (solver != null && Application.isPlaying && isLoaded)
            {
                for (int i = 0; i < particleCount; i++)
                {
                    if (selfCollisions)
                        solver.phases[solverIndices[i]] |= (int)Oni.ParticleFlags.SelfCollide;
                    else
                        solver.phases[solverIndices[i]] &= ~(int)Oni.ParticleFlags.SelfCollide;
                }
            }
        }

        public IObiConstraints GetConstraintsByType(Oni.ConstraintType type)
        {
            int index = (int)type;
            if (m_Constraints != null && index >= 0 && index < m_Constraints.Length)
                return m_Constraints[index];
            return null;
        }

        // Does not do anything by default. Call when manually modifying particle properties in the solver, should the actor need to do some book keeping.
        // For softbodies, updates their rest state.
        public virtual void UpdateParticleProperties()
        {
        }

        public int GetParticleRuntimeIndex(int actorIndex)
        {
            if (isLoaded)
                return solverIndices[actorIndex];
            return actorIndex;
        }

        /**
    	 * Given a solver particle index, returns the position of that particle in world space. 
    	 */
        public Vector3 GetParticlePosition(int solverIndex)
        {
            if (isLoaded)
                return m_Solver.transform.TransformPoint(m_Solver.renderablePositions[solverIndex]);
            return Vector3.zero;
        }

        /**
    	 * Given a solver particle index, returns the orientation of that particle in world space. 
    	 */
        public Quaternion GetParticleOrientation(int solverIndex)
        {
            if (isLoaded)
                return m_Solver.transform.rotation * m_Solver.renderableOrientations[solverIndex];
            return Quaternion.identity;
        }

        /**
         * Given a solver particle index, returns the anisotropic frame of that particle in world space. 
         */
        public void GetParticleAnisotropy(int solverIndex, ref Vector4 b1, ref Vector4 b2, ref Vector4 b3)
        {
            if (isLoaded && usesAnisotropicParticles)
            {
                int baseIndex = solverIndex * 3;

                b1 = m_Solver.transform.TransformDirection(m_Solver.anisotropies[baseIndex]);
                b2 = m_Solver.transform.TransformDirection(m_Solver.anisotropies[baseIndex + 1]);
                b3 = m_Solver.transform.TransformDirection(m_Solver.anisotropies[baseIndex + 2]);

                b1[3] = m_Solver.maxScale * m_Solver.anisotropies[baseIndex][3];
                b2[3] = m_Solver.maxScale * m_Solver.anisotropies[baseIndex + 1][3];
                b3[3] = m_Solver.maxScale * m_Solver.anisotropies[baseIndex + 2][3];

            }
            else
            {
                b1[3] = b2[3] = b3[3] = m_Solver.maxScale * m_Solver.principalRadii[solverIndex][0];
            }
        }

        /**
         * Given a solver particle index, returns the maximum radius of that particle.
         */
        public float GetParticleMaxRadius(int solverIndex)
        {
            if (isLoaded)
                return m_Solver.maxScale * m_Solver.principalRadii[solverIndex][0];
            return 0;
        }

        /**
         * Given a solver particle color, returns the color of that particle.
         */
        public Color GetParticleColor(int solverIndex)
        {
            if (isLoaded)
                return m_Solver.colors[solverIndex];
            return Color.white;
        }

        public void SetPhase(int newPhase)
        {
            newPhase = Mathf.Clamp(newPhase, 0, (1 << 24) - 1);

            for (int i = 0; i < particleCount; ++i)
            {
                int solverIndex = solverIndices[i];
                var flags = (Oni.ParticleFlags)Oni.GetFlagsFromPhase(solver.phases[solverIndex]);
                solver.phases[solverIndex] = Oni.MakePhase(newPhase, flags);
            }
        }

        /**
    	 * Sets the inverse mass of each particle so that the total actor mass matches the one passed by parameter.
         */
        public void SetMass(float mass)
        {
            if (Application.isPlaying && isLoaded && activeParticleCount > 0)
            {
                float invMass = 1.0f / (mass / activeParticleCount);

                for (int i = 0; i < activeParticleCount; ++i)
                {
                    int solverIndex = solverIndices[i];
                    m_Solver.invMasses[solverIndex] = invMass;
                    m_Solver.invRotationalMasses[solverIndex] = invMass;
                }
            }
        }

        /**
    	 * Returns the actor's mass (sum of all particle masses), and the position of its center of mass expressed in solver space. Particles with infinite mass (invMass = 0) are ignored. 
    	 */
        public float GetMass(out Vector3 com)
        {

            float actorMass = 0;
            com = Vector3.zero;

            if (Application.isPlaying && isLoaded && activeParticleCount > 0)
            {
                Vector4 com4 = Vector4.zero;

                for (int i = 0; i < activeParticleCount; ++i)
                {
                    if (solver.invMasses[solverIndices[i]] > 0)
                    {
                        float mass = 1.0f / solver.invMasses[solverIndices[i]];
                        actorMass += mass;
                        com4 += solver.positions[solverIndices[i]] * mass;
                    }
                }

                com = com4;
                if (actorMass > float.Epsilon)
                    com /= actorMass;
            }

            return actorMass;
        }

        /**
    	 * Adds a force to the actor. The force should be expressed in solver space.
         */
        public void AddForce(Vector3 force, ForceMode forceMode)
        {

            Vector3 com;
            float mass = GetMass(out com);

            if (!float.IsInfinity(mass))
            {

                Vector4 bodyForce = force;

                switch (forceMode)
                {
                    case ForceMode.Force:
                        {

                            bodyForce /= mass;

                            for (int i = 0; i < solverIndices.Length; ++i)
                                m_Solver.externalForces[solverIndices[i]] += bodyForce / m_Solver.invMasses[solverIndices[i]];

                        }
                        break;
                    case ForceMode.Acceleration:
                        {

                            for (int i = 0; i < solverIndices.Length; ++i)
                                m_Solver.externalForces[solverIndices[i]] += bodyForce / m_Solver.invMasses[solverIndices[i]];

                        }
                        break;
                    case ForceMode.Impulse:
                        {

                            bodyForce /= mass;

                            for (int i = 0; i < solverIndices.Length; ++i)
                                m_Solver.externalForces[solverIndices[i]] += bodyForce / m_Solver.invMasses[solverIndices[i]] / Time.fixedDeltaTime;

                        }
                        break;
                    case ForceMode.VelocityChange:
                        {

                            for (int i = 0; i < solverIndices.Length; ++i)
                                m_Solver.externalForces[solverIndices[i]] += bodyForce / m_Solver.invMasses[solverIndices[i]] / Time.fixedDeltaTime;

                        }
                        break;
                }
            }
        }

        /**
    	 * Adds a torque to the actor. The torque should be expressed in solver space.
         */
        public void AddTorque(Vector3 force, ForceMode forceMode)
        {

            Vector3 com;
            float mass = GetMass(out com);

            if (!float.IsInfinity(mass))
            {

                Vector3 bodyForce = force;

                switch (forceMode)
                {
                    case ForceMode.Force:
                        {

                            bodyForce /= mass;

                            for (int i = 0; i < solverIndices.Length; ++i)
                            {

                                Vector3 v = Vector3.Cross(bodyForce / m_Solver.invMasses[solverIndices[i]], (Vector3)m_Solver.positions[solverIndices[i]] - com);
                                m_Solver.externalForces[solverIndices[i]] += new Vector4(v.x, v.y, v.z, 0);
                            }

                        }
                        break;
                    case ForceMode.Acceleration:
                        {

                            for (int i = 0; i < solverIndices.Length; ++i)
                            {

                                Vector3 v = Vector3.Cross(bodyForce / m_Solver.invMasses[solverIndices[i]], (Vector3)m_Solver.positions[solverIndices[i]] - com);
                                m_Solver.externalForces[solverIndices[i]] += new Vector4(v.x, v.y, v.z, 0);
                            }

                        }
                        break;
                    case ForceMode.Impulse:
                        {

                            bodyForce /= mass;

                            for (int i = 0; i < solverIndices.Length; ++i)
                            {

                                Vector3 v = Vector3.Cross(bodyForce / m_Solver.invMasses[solverIndices[i]] / Time.fixedDeltaTime, (Vector3)m_Solver.positions[solverIndices[i]] - com);
                                m_Solver.externalForces[solverIndices[i]] += new Vector4(v.x, v.y, v.z, 0);
                            }

                        }
                        break;
                    case ForceMode.VelocityChange:
                        {

                            for (int i = 0; i < solverIndices.Length; ++i)
                            {

                                Vector3 v = Vector3.Cross(bodyForce / m_Solver.invMasses[solverIndices[i]] / Time.fixedDeltaTime, (Vector3)m_Solver.positions[solverIndices[i]] - com);
                                m_Solver.externalForces[solverIndices[i]] += new Vector4(v.x, v.y, v.z, 0);
                            }

                        }
                        break;
                }
            }
        }

        #region Blueprints

        /**
         * Sends blueprint particle data to the solver.
         */
        private void LoadBlueprintParticles(ObiActorBlueprint bp)
        {

            Matrix4x4 l2sTransform = actorLocalToSolverMatrix;
            Quaternion l2sRotation = l2sTransform.rotation;

            for (int i = 0; i < solverIndices.Length; i++)
            {
                int k = solverIndices[i];

                if (bp.positions != null && i < bp.positions.Length)
                {
                    m_Solver.startPositions[k] = m_Solver.prevPositions[k] = m_Solver.positions[k] = l2sTransform.MultiplyPoint3x4(bp.positions[i]);
                    m_Solver.renderablePositions[k] = l2sTransform.MultiplyPoint3x4(bp.positions[i]);
                }

                if (bp.orientations != null && i < bp.orientations.Length)
                {
                    m_Solver.startOrientations[k] = m_Solver.prevOrientations[k] = m_Solver.orientations[k] = l2sRotation * bp.orientations[i];
                    m_Solver.renderableOrientations[k] = l2sRotation * bp.orientations[i];
                }

                if (bp.velocities != null && i < bp.velocities.Length)
                    m_Solver.velocities[k] = l2sTransform.MultiplyVector(bp.velocities[i]);

                if (bp.angularVelocities != null && i < bp.angularVelocities.Length)
                    m_Solver.angularVelocities[k] = l2sTransform.MultiplyVector(bp.angularVelocities[i]);

                if (bp.invMasses != null && i < bp.invMasses.Length)
                    m_Solver.invMasses[k] = bp.invMasses[i];

                if (bp.invRotationalMasses != null && i < bp.invRotationalMasses.Length)
                    m_Solver.invRotationalMasses[k] = bp.invRotationalMasses[i];

                if (bp.principalRadii != null && i < bp.principalRadii.Length)
                    m_Solver.principalRadii[k] = bp.principalRadii[i];

                if (bp.phases != null && i < bp.phases.Length)
                    m_Solver.phases[k] = Oni.MakePhase(bp.phases[i],0);

                if (bp.restPositions != null && i < bp.restPositions.Length)
                    m_Solver.restPositions[k] = bp.restPositions[i];

                if (bp.restOrientations != null && i < bp.restOrientations.Length)
                    m_Solver.restOrientations[k] = bp.restOrientations[i];

                if (bp.colors != null && i < bp.colors.Length)
                    m_Solver.colors[k] = bp.colors[i];

            }

            m_ActiveParticleCount = blueprint.activeParticleCount;
            m_Solver.activeParticleCountChanged = true;

            // Push active particles to the solver:
            m_Solver.PushActiveParticles();

            // Recalculate inertia tensors (shape matching constraints rest shape need up to date inertia tensors, for instance).
            Oni.RecalculateInertiaTensors(m_Solver.OniSolver);

            // Push collision materials:
            PushCollisionMaterial();

        }

        private void LoadBlueprintConstraints(ObiActorBlueprint bp)
        {
            m_Constraints = new IObiConstraints[Oni.ConstraintTypeCount];

            // Iterate trough all non-null constraint types in the blueprint that have at least 1 batch:
            foreach (IObiConstraints constraintData in bp.GetConstraints())
            {
                // Create runtime counterpart
                IObiConstraints runtimeConstraints = constraintData.Clone(this);

                if (runtimeConstraints.GetConstraintType().HasValue)
                {
                    // Store a reference to it in the constraint map, so that they can be accessed by type enum:
                    m_Constraints[(int)runtimeConstraints.GetConstraintType().Value] = runtimeConstraints;

                    // Add it to solver:
                    runtimeConstraints.AddToSolver();
                }
            }
        }

        private void UnloadBlueprintParticles()
        {
            // Update active particles. 
            m_ActiveParticleCount = 0;
            m_Solver.activeParticleCountChanged = true;
            m_Solver.PushActiveParticles();
        }

        private void UnloadBlueprintConstraints()
        {
            for (int i = 0; i < m_Constraints.Length; ++i)
            {
                if (m_Constraints[i] != null)
                {
                    m_Constraints[i].RemoveFromSolver();
                    m_Constraints[i] = null;
                }
            }
        }

        /**
         * Resets the position and velocity (no other property is affected) of all particles, to the values stored in the blueprint. Note however
         * that this does not affect constraints, so if you've torn a cloth/rope or resized a rope, calling ResetParticles won't restore
         * the initial topology of the actor.
         */
        public void ResetParticles()
        {
            if (isLoaded)
            {
                Matrix4x4 l2sTransform = actorLocalToSolverMatrix;
                Quaternion l2sRotation = l2sTransform.rotation;

                for (int i = 0; i < particleCount; ++i)
                {
                    int solverIndex = solverIndices[i];

                    solver.positions[solverIndex] = l2sTransform.MultiplyPoint3x4(blueprint.positions[i]);
                    solver.velocities[solverIndex] = l2sTransform.MultiplyVector(blueprint.velocities[i]);

                    if (usesOrientedParticles)
                    {
                        solver.orientations[solverIndex] = l2sRotation * blueprint.orientations[i];
                        solver.angularVelocities[solverIndex] = l2sTransform.MultiplyVector(blueprint.angularVelocities[i]);
                    }
                }
            }
        }

        #endregion

        #region State

        // Save current particle properties and constraints to a blueprint.
        public void SaveStateToBlueprint(ObiActorBlueprint bp)
        {
            if (bp == null)
                return;

            Matrix4x4 l2sTransform = actorLocalToSolverMatrix.inverse;
            Quaternion l2sRotation = l2sTransform.rotation;

            for (int i = 0; i < solverIndices.Length; i++)
            {
                int k = solverIndices[i];

                if (m_Solver.positions != null && k < m_Solver.positions.count)
                    bp.positions[i] = l2sTransform.MultiplyPoint3x4(m_Solver.positions[k]);

                if (m_Solver.velocities != null && k < m_Solver.velocities.count)
                    bp.velocities[i] = l2sTransform.MultiplyVector(m_Solver.velocities[k]);
            }
        }

        protected void StoreState()
        {
            DestroyImmediate(state);
            state = Instantiate<ObiActorBlueprint>(blueprint);
            SaveStateToBlueprint(state);
        }

        public void ClearState()
        {
            DestroyImmediate(state);
        }

        #endregion

        #region Solver callbacks

        public virtual void LoadBlueprint(ObiSolver solver)
        {
            var bp = blueprint;

            // in case we have temporary state, load that instead of the original blueprint.
            if (Application.isPlaying)
            {
                bp = state != null ? state : blueprint;
            }

            LoadBlueprintParticles(bp);
            LoadBlueprintConstraints(bp);

            m_Loaded = true;

            if (OnBlueprintLoaded != null)
                OnBlueprintLoaded(this, null);
        }

        public virtual void UnloadBlueprint(ObiSolver solver)
        {
            // instantiate blueprint and store current state in the instance:
            if (Application.isPlaying)
            {
                StoreState();
            }

            // unload the blueprint.
            UnloadBlueprintConstraints();
            UnloadBlueprintParticles();

            m_Loaded = false;

            if (OnBlueprintUnloaded != null)
                OnBlueprintUnloaded(this, null);
        }

        public virtual void BeginStep(float stepTime)
        {
            if (OnBeginStep != null)
                OnBeginStep(this,stepTime); 
        }

        public virtual void Substep(float substepTime)
        {
            if (OnSubstep != null)
                OnSubstep(this,substepTime);
        }

        public virtual void EndStep()
        {
            if (OnEndStep != null)
                OnEndStep(this);
        }

        public virtual void Interpolate() 
        {
            // Update particle renderable positions/orientations in the solver:
            if (!Application.isPlaying && isLoaded)
            {
                Matrix4x4 l2sTransform = actorLocalToSolverMatrix;
                Quaternion l2sRotation = l2sTransform.rotation;

                for (int i = 0; i < solverIndices.Length; i++)
                {
                    int k = solverIndices[i];

                    if (blueprint.positions != null && i < blueprint.positions.Length)
                    {
                        m_Solver.renderablePositions[k] = l2sTransform.MultiplyPoint3x4(blueprint.positions[i]);
                    }

                    if (blueprint.orientations != null && i < blueprint.orientations.Length)
                    {
                        m_Solver.renderableOrientations[k] = l2sRotation * blueprint.orientations[i];
                    }
                }
            }

            if (OnInterpolate != null)
                OnInterpolate(this);
        }

        public virtual void OnSolverVisibilityChanged(bool visible)
        {
        }

        #endregion


    }
}

