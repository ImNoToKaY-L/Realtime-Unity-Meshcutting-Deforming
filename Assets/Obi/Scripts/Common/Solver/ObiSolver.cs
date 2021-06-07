/**
\mainpage Obi documentation
 
Introduction:
------------- 

Obi is a position-based dynamics framework for unity. It enables the simulation of cloth, ropes and fluid in realtime, complete with two-way
rigidbody interaction.
 
Features:
-------------------

- Particles can be pinned both in local space and to rigidbodies (kinematic or not).
- Realistic wind forces.
- Rigidbodies react to particle dynamics, and particles reach to each other and to rigidbodies too.
- Easy prefab instantiation, particle-based actors can be translated, scaled and rotated.
- Simulation can be warm-started in the editor, then all simulation state gets serialized with the object. This means
  your prefabs can be stored at any point in the simulation, and they will resume it when instantiated.
- Custom editor tools.

*/

using UnityEngine;
using Unity.Profiling;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.InteropServices;
using System.Linq;

namespace Obi
{

    /**
     * An ObiSolver component simulates particles and their interactions using the Oni unified physics library.
     */
    [AddComponentMenu("Physics/Obi/Obi Solver", 800)]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public sealed class ObiSolver : MonoBehaviour
    {
        static ProfilerMarker m_StateInterpolationPerfMarker = new ProfilerMarker("ApplyStateInterpolation");
        static ProfilerMarker m_UpdateVisibilityPerfMarker = new ProfilerMarker("UpdateVisibility");
        static ProfilerMarker m_GetSolverBoundsPerfMarker = new ProfilerMarker("GetSolverBounds");
        static ProfilerMarker m_TestBoundsPerfMarker = new ProfilerMarker("TestBoundsAgainstCameras");
        static ProfilerMarker m_GetAllCamerasPerfMarker = new ProfilerMarker("GetAllCameras");

        private static int initialCapacity = 256;

        public enum UpdateMode
        {
            FixedUpdate,
            AfterFixedUpdate,
            LateUpdate
        }

        public class ObiCollisionEventArgs : System.EventArgs
        {
            public ObiList<Oni.Contact> contacts = new ObiList<Oni.Contact>();  /**< collision contacts.*/
        }

        [Serializable]
        public class ParticleInActor
        {
            public ObiActor actor;
            public int indexInActor;

            public ParticleInActor()
            {
                this.actor = null;
                this.indexInActor = -1;
            }

            public ParticleInActor(ObiActor actor, int indexInActor)
            {
                this.actor = actor;
                this.indexInActor = indexInActor;
            }
        }

        public delegate void SolverCallback(ObiSolver solver);
        public delegate void SolverStepCallback(ObiSolver solver, float stepTime);
        public delegate void CollisionCallback(ObiSolver solver, ObiCollisionEventArgs contacts);

        public event CollisionCallback OnCollision;
        public event CollisionCallback OnParticleCollision;
        public event SolverCallback OnUpdateParameters;

        public event SolverStepCallback OnBeginStep;
        public event SolverStepCallback OnSubstep;
        public event SolverCallback OnEndStep;
        public event SolverCallback OnInterpolate;

        [Tooltip("If enabled, will force the solver to keep simulating even when not visible from any camera.")]
        public bool simulateWhenInvisible = true;           /**< Whether to keep simulating the cloth when its not visible by any camera.*/

        [ChildrenOnly]
        public Oni.SolverParameters parameters = new Oni.SolverParameters(Oni.SolverParameters.Interpolation.None,
                                                                          new Vector4(0, -9.81f, 0, 0));

        [Range(0, 1)]
        public float worldLinearInertiaScale = 0;           /**< how much does world-space linear inertia affect the actor. This only applies when the solver has "simulateInLocalSpace" enabled.*/

        [Range(0, 1)]
        public float worldAngularInertiaScale = 0;          /**< how much does world-space angular inertia affect the actor. This only applies when the solver has "simulateInLocalSpace" enabled.*/

        [HideInInspector] [NonSerialized] public List<ObiActor> actors = new List<ObiActor>();
        [HideInInspector] [NonSerialized] public ParticleInActor[] m_ParticleToActor;
        private int[] activeParticles;
        private int activeParticleCount = 0;
        [HideInInspector] [NonSerialized] public bool activeParticleCountChanged = true;

        private IntPtr oniSolver;
        private ObiCollisionEventArgs collisionArgs = new ObiCollisionEventArgs();
        private ObiCollisionEventArgs particleCollisionArgs = new ObiCollisionEventArgs();

        private float m_MaxScale = 1;
        private UnityEngine.Bounds bounds = new UnityEngine.Bounds();
        private Plane[] planes = new Plane[6];
        private Camera[] sceneCameras = new Camera[1];
        private bool isVisible = true;

        // constraint parameters:
        public Oni.ConstraintParameters distanceConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Sequential, 3);
        public Oni.ConstraintParameters bendingConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 3);
        public Oni.ConstraintParameters particleCollisionConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Sequential, 2);
        public Oni.ConstraintParameters particleFrictionConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 1);
        public Oni.ConstraintParameters collisionConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Sequential, 1);
        public Oni.ConstraintParameters frictionConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 1);
        public Oni.ConstraintParameters skinConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Sequential, 1);
        public Oni.ConstraintParameters volumeConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 2);
        public Oni.ConstraintParameters shapeMatchingConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 3);
        public Oni.ConstraintParameters tetherConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 1);
        public Oni.ConstraintParameters pinConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 3);
        public Oni.ConstraintParameters stitchConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 2);
        public Oni.ConstraintParameters densityConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 2);
        public Oni.ConstraintParameters stretchShearConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Sequential, 3);
        public Oni.ConstraintParameters bendTwistConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Sequential, 3);
        public Oni.ConstraintParameters chainConstraintParameters = new Oni.ConstraintParameters(false, Oni.ConstraintParameters.EvaluationOrder.Sequential, 3);

        // colors
        public Color[] m_Colors;

        // positions
        [NonSerialized] private ObiNativeVector4List m_Positions;
        [NonSerialized] private ObiNativeVector4List m_RestPositions;
        [NonSerialized] private ObiNativeVector4List m_PrevPositions;
        [NonSerialized] private ObiNativeVector4List m_StartPositions;
        [NonSerialized] private ObiNativeVector4List m_RenderablePositions;

        // orientations
        [NonSerialized] private ObiNativeQuaternionList m_Orientations;
        [NonSerialized] private ObiNativeQuaternionList m_RestOrientations;
        [NonSerialized] private ObiNativeQuaternionList m_PrevOrientations;
        [NonSerialized] private ObiNativeQuaternionList m_StartOrientations;
        [NonSerialized] private ObiNativeQuaternionList m_RenderableOrientations; /**< renderable particle orientations.*/

        // velocities
        [NonSerialized] private ObiNativeVector4List m_Velocities;
        [NonSerialized] private ObiNativeVector4List m_AngularVelocities;

        // masses/inertia tensors
        [NonSerialized] private ObiNativeFloatList m_InvMasses;
        [NonSerialized] private ObiNativeFloatList m_InvRotationalMasses;
        [NonSerialized] private ObiNativeVector4List m_InvInertiaTensors;

        // external forces
        [NonSerialized] private ObiNativeVector4List m_ExternalForces;
        [NonSerialized] private ObiNativeVector4List m_ExternalTorques;
        [NonSerialized] private ObiNativeVector4List m_Wind;

        // deltas
        [NonSerialized] private ObiNativeVector4List m_PositionDeltas;
        [NonSerialized] private ObiNativeQuaternionList m_OrientationDeltas;
        [NonSerialized] private ObiNativeIntList m_PositionConstraintCounts;
        [NonSerialized] private ObiNativeIntList m_OrientationConstraintCounts;

        // particle phase / shape
        [NonSerialized] private ObiNativeIntList m_Phases;
        [NonSerialized] private ObiNativeVector4List m_Anisotropies;
        [NonSerialized] private ObiNativeVector4List m_PrincipalRadii;
        [NonSerialized] private ObiNativeVector4List m_Normals;

        // fluids
        [NonSerialized] private ObiNativeVector4List m_Vorticities;
        [NonSerialized] private ObiNativeVector4List m_FluidData;
        [NonSerialized] private ObiNativeVector4List m_UserData;
        [NonSerialized] private ObiNativeFloatList m_SmoothingRadii;
        [NonSerialized] private ObiNativeFloatList m_Buoyancies;
        [NonSerialized] private ObiNativeFloatList m_RestDensities;
        [NonSerialized] private ObiNativeFloatList m_Viscosities;
        [NonSerialized] private ObiNativeFloatList m_SurfaceTension;
        [NonSerialized] private ObiNativeFloatList m_VortConfinement;
        [NonSerialized] private ObiNativeFloatList m_AtmosphericDrag;
        [NonSerialized] private ObiNativeFloatList m_AtmosphericPressure;
        [NonSerialized] private ObiNativeFloatList m_Diffusion;

        public IntPtr OniSolver
        {
            get { return oniSolver; }
        }

        public UnityEngine.Bounds Bounds
        {
            get { return bounds; }
        }

        public bool IsVisible
        {
            get { return isVisible; }
        }

        public float maxScale
        {
            get { return m_MaxScale; }
        }

        public int AllocParticleCount
        {
            get { return particleToActor.Count(s => s != null && s.actor != null); }
        }

        public ParticleInActor[] particleToActor
        {
            get
            {
                if (m_ParticleToActor == null)
                {
                    m_ParticleToActor = new ParticleInActor[initialCapacity];
                    for (int i = 0; i < initialCapacity; ++i)
                        m_ParticleToActor[i] = new ParticleInActor();
                }
                return m_ParticleToActor;
            }
        }

        public Color[] colors
        {
            get
            {
                if (m_Colors == null)
                {
                    m_Colors = new Color[initialCapacity];
                    for (int i = 0; i < initialCapacity; ++i)
                        m_Colors[i] = Color.white;
                }
                return m_Colors;
            }
        }

        #region Position arrays

        public ObiNativeVector4List positions
        {
            get
            {
                if (m_Positions == null)
                {
                    m_Positions = new ObiNativeVector4List(initialCapacity);
                    m_Positions.count = initialCapacity;
                }
                return m_Positions;
            }
        }

        public ObiNativeVector4List restPositions
        {
            get
            {
                if (m_RestPositions == null)
                {
                    m_RestPositions = new ObiNativeVector4List(initialCapacity);
                    m_RestPositions.count = initialCapacity;
                }
                return m_RestPositions;
            }
        }

        public ObiNativeVector4List prevPositions
        {
            get
            {
                if (m_PrevPositions == null)
                {
                    m_PrevPositions = new ObiNativeVector4List(initialCapacity);
                    m_PrevPositions.count = initialCapacity;
                }
                return m_PrevPositions;
            }
        }

        public ObiNativeVector4List startPositions
        {
            get
            {
                if (m_StartPositions == null)
                {
                    m_StartPositions = new ObiNativeVector4List(initialCapacity);
                    m_StartPositions.count = initialCapacity;
                }
                return m_StartPositions;
            }
        }

        public ObiNativeVector4List renderablePositions
        {
            get
            {
                if (m_RenderablePositions == null)
                {
                    m_RenderablePositions = new ObiNativeVector4List(initialCapacity);
                    m_RenderablePositions.count = initialCapacity;
                }
                return m_RenderablePositions;
            }
        }

        #endregion

        #region Orientation arrays

        public ObiNativeQuaternionList orientations
        {
            get
            {
                if (m_Orientations == null)
                {
                    m_Orientations = new ObiNativeQuaternionList(initialCapacity);
                    m_Orientations.count = initialCapacity;
                }
                return m_Orientations;
            }
        }

        public ObiNativeQuaternionList restOrientations
        {
            get
            {
                if (m_RestOrientations == null)
                {
                    m_RestOrientations = new ObiNativeQuaternionList(initialCapacity);
                    m_RestOrientations.count = initialCapacity;
                }
                return m_RestOrientations;
            }
        }

        public ObiNativeQuaternionList prevOrientations
        {
            get
            {
                if (m_PrevOrientations == null)
                {
                    m_PrevOrientations = new ObiNativeQuaternionList(initialCapacity);
                    m_PrevOrientations.count = initialCapacity;
                }
                return m_PrevOrientations;
            }
        }

        public ObiNativeQuaternionList startOrientations
        {
            get
            {
                if (m_StartOrientations == null)
                {
                    m_StartOrientations = new ObiNativeQuaternionList(initialCapacity);
                    m_StartOrientations.count = initialCapacity;
                }
                return m_StartOrientations;
            }
        }

        public ObiNativeQuaternionList renderableOrientations
        {
            get
            {
                if (m_RenderableOrientations == null)
                {
                    m_RenderableOrientations = new ObiNativeQuaternionList(initialCapacity);
                    m_RenderableOrientations.count = initialCapacity; 
                }
                return m_RenderableOrientations;
            }
        }

        #endregion

        #region Velocity arrays

        public ObiNativeVector4List velocities
        {
            get
            {
                if (m_Velocities == null)
                {
                    m_Velocities = new ObiNativeVector4List(initialCapacity);
                    m_Velocities.count = initialCapacity;
                }
                return m_Velocities;
            }
        }

        public ObiNativeVector4List angularVelocities
        {
            get
            {
                if (m_AngularVelocities == null)
                {
                    m_AngularVelocities = new ObiNativeVector4List(initialCapacity);
                    m_AngularVelocities.count = initialCapacity;
                }
                return m_AngularVelocities;
            }
        }

        #endregion

        #region Mass arrays

        public ObiNativeFloatList invMasses
        {
            get
            {
                if (m_InvMasses == null)
                {
                    m_InvMasses = new ObiNativeFloatList(initialCapacity);
                    m_InvMasses.count = initialCapacity;
                }
                return m_InvMasses;
            }
        }

        public ObiNativeFloatList invRotationalMasses
        {
            get
            {
                if (m_InvRotationalMasses == null)
                {
                    m_InvRotationalMasses = new ObiNativeFloatList(initialCapacity);
                    m_InvRotationalMasses.count = initialCapacity;
                }
                return m_InvRotationalMasses;
            }
        }

        public ObiNativeVector4List invInertiaTensors
        {
            get
            {
                if (m_InvInertiaTensors == null)
                {
                    m_InvInertiaTensors = new ObiNativeVector4List(initialCapacity);
                    m_InvInertiaTensors.count = initialCapacity;
                }
                return m_InvInertiaTensors;
            }
        }

        #endregion

        #region External forces

        public ObiNativeVector4List externalForces
        {
            get
            {
                if (m_ExternalForces == null)
                {
                    m_ExternalForces = new ObiNativeVector4List(initialCapacity);
                    m_ExternalForces.count = initialCapacity;
                }
                return m_ExternalForces;
            }
        }

        public ObiNativeVector4List externalTorques
        {
            get
            {
                if (m_ExternalTorques == null)
                {
                    m_ExternalTorques = new ObiNativeVector4List(initialCapacity);
                    m_ExternalTorques.count = initialCapacity;
                }
                return m_ExternalTorques;
            }
        }

        public ObiNativeVector4List wind
        {
            get
            {
                if (m_Wind == null)
                {
                    m_Wind = new ObiNativeVector4List(initialCapacity);
                    m_Wind.count = initialCapacity;
                }
                return m_Wind;
            }
        }

        #endregion

        #region Deltas

        public ObiNativeVector4List positionDeltas
        {
            get
            {
                if (m_PositionDeltas == null)
                {
                    m_PositionDeltas = new ObiNativeVector4List(initialCapacity);
                    m_PositionDeltas.count = initialCapacity;
                }
                return m_PositionDeltas;
            }
        }

        public ObiNativeQuaternionList orientationDeltas
        {
            get
            {
                if (m_OrientationDeltas == null)
                {
                    m_OrientationDeltas = new ObiNativeQuaternionList(initialCapacity, 16, new Quaternion(0, 0, 0, 0));
                    m_OrientationDeltas.count = initialCapacity;
                }
                return m_OrientationDeltas;
            }
        }

        public ObiNativeIntList positionConstraintCounts
        {
            get
            {
                if (m_PositionConstraintCounts == null)
                {
                    m_PositionConstraintCounts = new ObiNativeIntList(initialCapacity);
                    m_PositionConstraintCounts.count = initialCapacity;
                }
                return m_PositionConstraintCounts;
            }
        }

        public ObiNativeIntList orientationConstraintCounts
        {
            get
            {
                if (m_OrientationConstraintCounts == null)
                {
                    m_OrientationConstraintCounts = new ObiNativeIntList(initialCapacity);
                    m_OrientationConstraintCounts.count = initialCapacity;
                }
                return m_OrientationConstraintCounts;
            }
        }

        #endregion

        #region Shape and phase

        public ObiNativeIntList phases
        {
            get
            {
                if (m_Phases == null)
                {
                    m_Phases = new ObiNativeIntList(initialCapacity);
                    m_Phases.count = initialCapacity;
                }
                return m_Phases;
            }
        }

        public ObiNativeVector4List anisotropies
        {
            get
            {
                if (m_Anisotropies == null)
                {
                    m_Anisotropies = new ObiNativeVector4List(initialCapacity * 3);
                    m_Anisotropies.count = initialCapacity * 3;
                }
                return m_Anisotropies;
            }
        }

        public ObiNativeVector4List principalRadii
        {
            get
            {
                if (m_PrincipalRadii == null)
                {
                    m_PrincipalRadii = new ObiNativeVector4List(initialCapacity);
                    m_PrincipalRadii.count = initialCapacity;
                }
                return m_PrincipalRadii;
            }
        }

        public ObiNativeVector4List normals
        {
            get
            {
                if (m_Normals == null)
                {
                    m_Normals = new ObiNativeVector4List(initialCapacity);
                    m_Normals.count = initialCapacity;
                }
                return m_Normals;
            }
        }

        #endregion

        #region Fluid properties

        public ObiNativeVector4List vorticities
        {
            get
            {
                if (m_Vorticities == null)
                {
                    m_Vorticities = new ObiNativeVector4List(initialCapacity);
                    m_Vorticities.count = initialCapacity;
                }
                return m_Vorticities;
            }
        }

        public ObiNativeVector4List fluidData
        {
            get
            {
                if (m_FluidData == null)
                {
                    m_FluidData = new ObiNativeVector4List(initialCapacity);
                    m_FluidData.count = initialCapacity;
                }
                return m_FluidData;
            }
        }

        public ObiNativeVector4List userData
        {
            get
            {
                if (m_UserData == null)
                {
                    m_UserData = new ObiNativeVector4List(initialCapacity);
                    m_UserData.count = initialCapacity;
                }
                return m_UserData;
            }
        }

        public ObiNativeFloatList smoothingRadii
        {
            get
            {
                if (m_SmoothingRadii == null)
                {
                    m_SmoothingRadii = new ObiNativeFloatList(initialCapacity);
                    m_SmoothingRadii.count = initialCapacity;
                }
                return m_SmoothingRadii;
            }
        }

        public ObiNativeFloatList buoyancies
        {
            get
            {
                if (m_Buoyancies == null)
                {
                    m_Buoyancies = new ObiNativeFloatList(initialCapacity);
                    m_Buoyancies.count = initialCapacity;
                }
                return m_Buoyancies;
            }
        }

        public ObiNativeFloatList restDensities
        {
            get
            {
                if (m_RestDensities == null)
                {
                    m_RestDensities = new ObiNativeFloatList(initialCapacity);
                    m_RestDensities.count = initialCapacity;
                }
                return m_RestDensities;
            }
        }

        public ObiNativeFloatList viscosities
        {
            get
            {
                if (m_Viscosities == null)
                {
                    m_Viscosities = new ObiNativeFloatList(initialCapacity);
                    m_Viscosities.count = initialCapacity;
                }
                return m_Viscosities;
            }
        }

        public ObiNativeFloatList surfaceTension
        {
            get
            {
                if (m_SurfaceTension == null)
                {
                    m_SurfaceTension = new ObiNativeFloatList(initialCapacity);
                    m_SurfaceTension.count = initialCapacity;
                }
                return m_SurfaceTension;
            }
        }

        public ObiNativeFloatList vortConfinement
        {
            get
            {
                if (m_VortConfinement == null)
                {
                    m_VortConfinement = new ObiNativeFloatList(initialCapacity);
                    m_VortConfinement.count = initialCapacity;
                }
                return m_VortConfinement;
            }
        }

        public ObiNativeFloatList atmosphericDrag
        {
            get
            {
                if (m_AtmosphericDrag == null)
                {
                    m_AtmosphericDrag = new ObiNativeFloatList(initialCapacity);
                    m_AtmosphericDrag.count = initialCapacity;
                }
                return m_AtmosphericDrag;
            }
        }

        public ObiNativeFloatList atmosphericPressure
        {
            get
            {
                if (m_AtmosphericPressure == null)
                {
                    m_AtmosphericPressure = new ObiNativeFloatList(initialCapacity);
                    m_AtmosphericPressure.count = initialCapacity;
                }
                return m_AtmosphericPressure;
            }
        }

        public ObiNativeFloatList diffusion
        {
            get
            {
                if (m_Diffusion == null)
                {
                    m_Diffusion = new ObiNativeFloatList(initialCapacity);
                    m_Diffusion.count = initialCapacity;
                }
                return m_Diffusion;
            }
        }

        #endregion

		private void OnEnable() 
        {
            Initialize();
        }

        void Update()
        {
            m_MaxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        }

        private void OnDestroy()
        {
            // Remove all actors from the solver:
            while (actors.Count > 0)
                RemoveActor(actors[actors.Count - 1]);
            
            Teardown();
        }

		public void Initialize()
		{
            if (oniSolver == IntPtr.Zero)
            {
                actors = new List<ObiActor>();
                activeParticles = new int[initialCapacity];
                m_ParticleToActor = new ParticleInActor[initialCapacity];
                m_Colors = new Color[initialCapacity];

                // Create the Oni solver:
                oniSolver = Oni.CreateSolver(initialCapacity);

                // Send particle arrays to Oni:
                PushParticleArrays();

                // Initialize moving transform:
                InitializeTransformFrame();

                // Set initial parameter values:
                UpdateParameters();
            }
		}

        public void Teardown()
        {
            // Free particle memory:
            FreeParticleArrays(); 

            // Destroy the Oni solver:
            Oni.DestroySolver(oniSolver);
            oniSolver = IntPtr.Zero;
        }

		private void PushParticleArrays()
        {
            Oni.SetParticlePositions(oniSolver, positions.GetIntPtr());
            Oni.SetParticlePreviousPositions(oniSolver, prevPositions.GetIntPtr());
            Oni.SetRestPositions(oniSolver, restPositions.GetIntPtr());
            Oni.SetParticleOrientations(oniSolver, orientations.GetIntPtr());
            Oni.SetParticlePreviousOrientations(oniSolver, prevOrientations.GetIntPtr());
            Oni.SetRestOrientations(oniSolver, restOrientations.GetIntPtr());
            Oni.SetParticleVelocities(oniSolver, velocities.GetIntPtr());
            Oni.SetParticleAngularVelocities(oniSolver, angularVelocities.GetIntPtr());
            Oni.SetParticleInverseMasses(oniSolver, invMasses.GetIntPtr());
            Oni.SetParticleInverseRotationalMasses(oniSolver, invRotationalMasses.GetIntPtr());
            Oni.SetParticlePrincipalRadii(oniSolver, principalRadii.GetIntPtr());
            Oni.SetParticlePhases(oniSolver, phases.GetIntPtr());
            Oni.SetRenderableParticlePositions(oniSolver, renderablePositions.GetIntPtr());
            Oni.SetRenderableParticleOrientations(oniSolver, renderableOrientations.GetIntPtr());
            Oni.SetParticleAnisotropies(oniSolver, anisotropies.GetIntPtr());
            Oni.SetParticleSmoothingRadii(oniSolver, smoothingRadii.GetIntPtr());
            Oni.SetParticleBuoyancy(oniSolver, buoyancies.GetIntPtr());
            Oni.SetParticleRestDensities(oniSolver, restDensities.GetIntPtr());
            Oni.SetParticleViscosities(oniSolver, viscosities.GetIntPtr());
            Oni.SetParticleSurfaceTension(oniSolver, surfaceTension.GetIntPtr());
            Oni.SetParticleVorticityConfinement(oniSolver, vortConfinement.GetIntPtr());
            Oni.SetParticleAtmosphericDragPressure(oniSolver, atmosphericDrag.GetIntPtr(), atmosphericPressure.GetIntPtr());
            Oni.SetParticleDiffusion(oniSolver, diffusion.GetIntPtr());
            Oni.SetParticleVorticities(oniSolver, vorticities.GetIntPtr());
            Oni.SetParticleFluidData(oniSolver, fluidData.GetIntPtr());
            Oni.SetParticleUserData(oniSolver, userData.GetIntPtr());
            Oni.SetParticleExternalForces(oniSolver, externalForces.GetIntPtr());
            Oni.SetParticleExternalTorques(oniSolver, externalTorques.GetIntPtr());
            Oni.SetParticleWinds(oniSolver, wind.GetIntPtr());
            Oni.SetParticlePositionDeltas(oniSolver, positionDeltas.GetIntPtr());
            Oni.SetParticleOrientationDeltas(oniSolver, orientationDeltas.GetIntPtr());
            Oni.SetParticlePositionConstraintCounts(oniSolver, positionConstraintCounts.GetIntPtr());
            Oni.SetParticleOrientationConstraintCounts(oniSolver, orientationConstraintCounts.GetIntPtr());
            Oni.SetParticleNormals(oniSolver, normals.GetIntPtr());
            Oni.SetParticleInverseInertiaTensors(oniSolver, invInertiaTensors.GetIntPtr());
        }

        private void FreeParticleArrays()
        {
            startPositions.Dispose();
            startOrientations.Dispose();
            positions.Dispose();
            prevPositions.Dispose();
            restPositions.Dispose();
            velocities.Dispose();
            orientations.Dispose();
            prevOrientations.Dispose();
            restOrientations.Dispose();
            angularVelocities.Dispose();
            invMasses.Dispose();
            invRotationalMasses.Dispose();
            principalRadii.Dispose();
            phases.Dispose();
            renderablePositions.Dispose();
            renderableOrientations.Dispose();
            anisotropies.Dispose();
            smoothingRadii.Dispose();
            buoyancies.Dispose();
            restDensities.Dispose();
            viscosities.Dispose();
            surfaceTension.Dispose();
            vortConfinement.Dispose();
            atmosphericDrag.Dispose();
            atmosphericPressure.Dispose();
            diffusion.Dispose();
            vorticities.Dispose();
            fluidData.Dispose();
            userData.Dispose();
            externalForces.Dispose();
            externalTorques.Dispose();
            wind.Dispose();
            positionDeltas.Dispose();
            orientationDeltas.Dispose();
            positionConstraintCounts.Dispose();
            orientationConstraintCounts.Dispose();
            normals.Dispose();
            invInertiaTensors.Dispose();

            m_Colors = null;
            m_Positions = null;
            m_RestPositions = null;
            m_PrevPositions = null;
            m_StartPositions = null;
            m_RenderablePositions = null;
            m_Orientations = null;
            m_RestOrientations = null;
            m_PrevOrientations = null;
            m_StartOrientations = null;
            m_RenderableOrientations = null;
            m_Velocities = null;
            m_AngularVelocities = null;
            m_InvMasses = null;
            m_InvRotationalMasses = null;
            m_InvInertiaTensors = null;
            m_ExternalForces = null;
            m_ExternalTorques = null;
            m_Wind = null;
            m_PositionDeltas = null;
            m_OrientationDeltas = null;
            m_PositionConstraintCounts = null;
            m_OrientationConstraintCounts = null;
            m_Phases = null;
            m_Anisotropies = null;
            m_PrincipalRadii = null;
            m_Normals = null;
            m_Vorticities = null;
            m_FluidData = null;
            m_UserData = null;
            m_SmoothingRadii = null;
            m_Buoyancies = null;
            m_RestDensities = null;
            m_Viscosities = null;
            m_SurfaceTension = null;
            m_VortConfinement = null;
            m_AtmosphericDrag = null;
            m_AtmosphericPressure = null;
            m_Diffusion = null;
        }

        private void EnsureParticleArraysCapacity(int count)
        {
            if (count >= positions.count)
            {
                startPositions.ResizeInitialized(count);
                positions.ResizeInitialized(count);
                prevPositions.ResizeInitialized(count);
                restPositions.ResizeInitialized(count);
                startOrientations.ResizeInitialized(count);
                orientations.ResizeInitialized(count);
                prevOrientations.ResizeInitialized(count);
                restOrientations.ResizeInitialized(count);
                renderablePositions.ResizeInitialized(count);
                renderableOrientations.ResizeInitialized(count);
                velocities.ResizeInitialized(count);
                angularVelocities.ResizeInitialized(count);
                invMasses.ResizeInitialized(count);
                invRotationalMasses.ResizeInitialized(count);
                principalRadii.ResizeInitialized(count);
                phases.ResizeInitialized(count);
                anisotropies.ResizeInitialized(count * 3);
                smoothingRadii.ResizeInitialized(count);
                buoyancies.ResizeInitialized(count);
                restDensities.ResizeInitialized(count);
                viscosities.ResizeInitialized(count);
                surfaceTension.ResizeInitialized(count);
                vortConfinement.ResizeInitialized(count);
                atmosphericDrag.ResizeInitialized(count);
                atmosphericPressure.ResizeInitialized(count);
                diffusion.ResizeInitialized(count);
                vorticities.ResizeInitialized(count);
                fluidData.ResizeInitialized(count);
                userData.ResizeInitialized(count);
                externalForces.ResizeInitialized(count);
                externalTorques.ResizeInitialized(count);
                wind.ResizeInitialized(count);
                positionDeltas.ResizeInitialized(count);
                orientationDeltas.ResizeInitialized(count, new Quaternion(0, 0, 0, 0));
                positionConstraintCounts.ResizeInitialized(count);
                orientationConstraintCounts.ResizeInitialized(count);
                normals.ResizeInitialized(count);
                invInertiaTensors.ResizeInitialized(count);
            }

            if (count >= activeParticles.Length)
            {
                Array.Resize(ref activeParticles, count * 2);
                Array.Resize(ref m_ParticleToActor, count * 2);
                Array.Resize(ref m_Colors, count * 2);

                PushParticleArrays();
                Oni.SetCapacity(oniSolver, positions.capacity);
            }
        }

        private void AllocateParticles(ObiActor actor, int[] particleIndices)
        {
            int allocatedCount = 0;
            int current = 0;

            while (allocatedCount < particleIndices.Length)
            {
                EnsureParticleArraysCapacity(current + 1);

                if (particleToActor[current] == null || particleToActor[current].actor == null)
                {
                    particleToActor[current] = new ObiSolver.ParticleInActor(actor, allocatedCount);
                    particleIndices[allocatedCount] = current;
                    allocatedCount++;
                }

                current++;
            }
        }

        /**
    	 * Adds the actor to this solver. Will return whether the allocation was sucessful or not. While in play mode,
    	 * if the actor is sucessfully added tp the solver, will also call actor.LoadBlueprint().
    	 */
        public bool AddActor(ObiActor actor)
        {

            if (actor == null)
                return false;

            // Find actor index in our actors array:
            int index = actors.IndexOf(actor);

            if (index < 0 && actor.blueprint != null)
            {
                int[] allocated = new int[actor.blueprint.particleCount];
                AllocateParticles(actor, allocated);

                actor.solverIndices = allocated;

                actors.Add(actor);

                actor.LoadBlueprint(this);
            }

            return true;

        }

        /// <summary>  
        /// Removes an actor from this solver. Will only complete successfully if the actor had been previously added to this solver. 
        /// If the actor is sucessfully removed from the solver, will also call actor.UnloadBlueprint().
        /// </summary>  
        /// <param name="actor"> An actor.</param>  
        /// <returns>
        /// Whether the actor was sucessfully removed.
        /// </returns> 
    	public bool RemoveActor(ObiActor actor)
        {

            if (actor == null)
                return false;

            // Find actor index in our actors array:
            int index = actors.IndexOf(actor);

            // If we are in charge of this actor indeed, perform all steps necessary to release it.
            if (index >= 0)
            {
                actor.UnloadBlueprint(this);

                for (int i = 0; i < actor.solverIndices.Length; ++i)
                    particleToActor[actor.solverIndices[i]] = null;

                actors.RemoveAt(index);

                actor.solverIndices = null;

                return true;
            }

            return false;
        }

        /**
    	 * Updates solver parameters, sending them to the Oni library.
    	 */
        public void UpdateParameters()
        {

            Oni.SetSolverParameters(oniSolver, ref parameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.Distance, ref distanceConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.Bending, ref bendingConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.ParticleCollision, ref particleCollisionConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.ParticleFriction, ref particleFrictionConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.Collision, ref collisionConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.Friction, ref frictionConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.Density, ref densityConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.Skin, ref skinConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.Volume, ref volumeConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.ShapeMatching, ref shapeMatchingConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.Tether, ref tetherConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.Pin, ref pinConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.Stitch, ref stitchConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.StretchShear, ref stretchShearConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.BendTwist, ref bendTwistConstraintParameters);

            Oni.SetConstraintGroupParameters(oniSolver, (int)Oni.ConstraintType.Chain, ref chainConstraintParameters);

            if (OnUpdateParameters != null)
                OnUpdateParameters(this);

        }

        /**
    	 * Updates the active particles array.
    	 */
        public void PushActiveParticles()
        {
            if (activeParticleCountChanged)
            {
                activeParticleCount = 0;

                for (int i = 0; i < actors.Count; ++i)
                {

                    ObiActor currentActor = actors[i];

                    if (currentActor.isActiveAndEnabled)
                    {
                        for (int j = 0; j < currentActor.activeParticleCount; ++j)
                        {
                            activeParticles[activeParticleCount] = currentActor.solverIndices[j];
                            activeParticleCount++;
                        }
                    }
                }

                Oni.SetActiveParticles(oniSolver, activeParticles, activeParticleCount);
                activeParticleCountChanged = false;  
            }

        }

        /**
         * Checks if any particle in the solver is visible from at least one camera. If so, sets isVisible to true, false otherwise.
         */
        private void UpdateVisibility()
        {

            using (m_UpdateVisibilityPerfMarker.Auto())
            {

                using (m_GetSolverBoundsPerfMarker.Auto())
                {
                    // get bounds in solver space:
                    Vector3 min = Vector3.zero, max = Vector3.zero;
                    Oni.GetBounds(oniSolver, ref min, ref max);
                    bounds.SetMinMax(min, max);
                }

                if (bounds.AreValid())
                {
                    using (m_TestBoundsPerfMarker.Auto())
                    {
                        // transform bounds to world space:
                        bounds = bounds.Transform(transform.localToWorldMatrix);

                        using (m_GetAllCamerasPerfMarker.Auto())
                        {
                            Array.Resize(ref sceneCameras, Camera.allCamerasCount);
                            Camera.GetAllCameras(sceneCameras);
                        }

                        foreach (Camera cam in sceneCameras)
                        {
                            GeometryUtility.CalculateFrustumPlanes(cam, planes);
                            if (GeometryUtility.TestPlanesAABB(planes, bounds))
                            {
                                if (!isVisible)
                                {
                                    isVisible = true;
                                    foreach (ObiActor actor in actors)
                                        actor.OnSolverVisibilityChanged(isVisible);
                                }
                                return;
                            }
                        }
                    }
                }

                if (isVisible)
                {
                    isVisible = false;
                    foreach (ObiActor actor in actors)
                        actor.OnSolverVisibilityChanged(isVisible);
                }
            }
        }

        private void InitializeTransformFrame()
        {
            Vector4 translation = transform.position;
            Vector4 scale = transform.lossyScale;
            Quaternion rotation = transform.rotation;
            Oni.InitializeFrame(this.oniSolver, ref translation, ref scale, ref rotation);
        }

        public void UpdateTransformFrame(float dt)
        {
            Vector4 translation = transform.position;
            Vector4 scale = transform.lossyScale;
            Quaternion rotation = transform.rotation;
            Oni.UpdateFrame(this.oniSolver, ref translation, ref scale, ref rotation, dt);
            Oni.ApplyFrame(this.oniSolver, 0, 0, worldLinearInertiaScale, worldAngularInertiaScale, dt);
        }

        public IntPtr BeginStep(float stepTime)
        {
            if (!isActiveAndEnabled || oniSolver == IntPtr.Zero)
                return IntPtr.Zero;

            // Update inertial frame:
            UpdateTransformFrame(stepTime);

            // Copy positions / orientations at the start of the step, for interpolation:
            startPositions.CopyFrom(positions);
            startOrientations.CopyFrom(orientations);

            // Update the active particles array:
            PushActiveParticles();

            // Recalculate inertia tensors:
            Oni.RecalculateInertiaTensors(OniSolver);

            if (OnBeginStep != null)
                OnBeginStep(this,stepTime);

            foreach (ObiActor actor in actors)
                actor.BeginStep(stepTime);

            // Perform collision detection:
            if (simulateWhenInvisible || isVisible)
                return Oni.CollisionDetection(oniSolver, stepTime);

            return IntPtr.Zero;
        }

        public IntPtr Substep(float substepTime)
        {

            // Only update the solver if it is visible, or if we must simulate even when invisible.
            if (isActiveAndEnabled && (simulateWhenInvisible || isVisible) && oniSolver != IntPtr.Zero)
            {
                if (OnSubstep != null)
                    OnSubstep(this,substepTime);

                foreach (ObiActor actor in actors)
                    actor.Substep(substepTime);

                // Update the solver (this is internally split in tasks so multiple solvers can be updated in parallel)
                return Oni.Step(oniSolver, substepTime);
            }

            return IntPtr.Zero;
        }

        public void EndStep()
        {
            if (oniSolver == IntPtr.Zero)
                return;

            if (OnCollision != null)
            {

                int numCollisions = Oni.GetConstraintCount(oniSolver, (int)Oni.ConstraintType.Collision);
                collisionArgs.contacts.SetCount(numCollisions);

                if (numCollisions > 0)
                    Oni.GetCollisionContacts(oniSolver, collisionArgs.contacts.Data, numCollisions);

                OnCollision(this, collisionArgs);

            }

            if (OnParticleCollision != null)
            {

                int numCollisions = Oni.GetConstraintCount(oniSolver, (int)Oni.ConstraintType.ParticleCollision);
                particleCollisionArgs.contacts.SetCount(numCollisions);

                if (numCollisions > 0)
                    Oni.GetParticleCollisionContacts(oniSolver, particleCollisionArgs.contacts.Data, numCollisions);

                OnParticleCollision(this, particleCollisionArgs);

            }

            Oni.ResetForces(oniSolver);

            if (OnEndStep != null)
                OnEndStep(this);

            foreach (ObiActor actor in actors)
                actor.EndStep();
        }

        // Finalizes the frame by performing physics state interpolation, and triggers PostSolverInterpolate for all actors.
        // This is usually used for mesh generation, rendering setup and other tasks that must take place after all physics steps for this frame are done.
        public void Interpolate(float stepTime, float unsimulatedTime) // TODO: give a better name, as "Interpolate" is too specific.
        {
            if (!isActiveAndEnabled || oniSolver == IntPtr.Zero)
                return;

            // Only perform interpolation if the solver is visible, or if we must simulate even when invisible.
            if (simulateWhenInvisible || isVisible)
            {
                using (m_StateInterpolationPerfMarker.Auto())
                {
                    // interpolate physics state:
                    Oni.ApplyPositionInterpolation(oniSolver, startPositions.GetIntPtr(), startOrientations.GetIntPtr(), stepTime, unsimulatedTime);
                }
            }

            UpdateVisibility();

            if (OnInterpolate != null)
                OnInterpolate(this);

            foreach (ObiActor actor in actors)
                actor.Interpolate();

        }


    }

}
