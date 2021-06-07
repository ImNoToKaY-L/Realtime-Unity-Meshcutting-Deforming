using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Obi
{

    [AddComponentMenu("Physics/Obi/Obi Emitter", 850)]
    [ExecuteInEditMode]
    public class ObiEmitter : ObiActor
    {

        public delegate void EmitterParticleCallback(ObiEmitter emitter, int particleIndex);

        public event EmitterParticleCallback OnEmitParticle;
        public event EmitterParticleCallback OnKillParticle;

        public enum EmissionMethod
        {
            STREAM,     /**< continously emits particles until there are no particles left to emit.*/
            BURST       /**< distributes particles in the surface of the object. Burst emission.*/
        }

        public ObiEmitterBlueprintBase emitterBlueprint;

        public override ObiActorBlueprint blueprint
        {
            get { return emitterBlueprint; }
        }

        [SerializeProperty("FluidPhase")]
        [SerializeField] private int fluidPhase = 1;

        [Tooltip("Changes how the emitter behaves. Available modes are Stream and Burst.")]
        public EmissionMethod emissionMethod = EmissionMethod.STREAM;

        [Range(0,1)]
        public float minPoolSize = 0.5f;

        [Tooltip("Speed (in units/second) of emitted particles. Setting it to zero will stop emission. Large values will cause more particles to be emitted.")]
        public float speed = 0.25f;

        [Tooltip("Lifespan of each particle.")]
        public float lifespan = 4;

        [Range(0, 1)]
        [Tooltip("Amount of randomization applied to particles.")]
        public float randomVelocity = 0;

        [Tooltip("Spawned particles are tinted by the corresponding emitter shape's color.")]
        public bool useShapeColor = true;

        [HideInInspector] [SerializeField] private List<ObiEmitterShape> emitterShapes = new List<ObiEmitterShape>();
        private IEnumerator<ObiEmitterShape.DistributionPoint> distEnumerator;

        [HideInInspector] public float[] life;          /**< per particle remaining life in seconds.*/

        private float unemittedBursts = 0;
        private bool m_IsEmitting = false;

        public int FluidPhase
        {
            set
            {
                if (fluidPhase != value)
                {
                    fluidPhase = value;
                    SetSelfCollisions(true);
                }
            }
            get { return fluidPhase; }
        }

        public bool isEmitting
        {
            get { return m_IsEmitting; }
        }

        public override bool usesCustomExternalForces
        {
            get { return true; }
        }

        public override bool usesAnisotropicParticles
        {
            get { return true; }
        }

        public override void LoadBlueprint(ObiSolver solver)
        {
            base.LoadBlueprint(solver);

            //Copy local arrays:
            life = new float[particleCount];
            for (int i = 0; i < life.Length; ++i)
                life[i] = lifespan;

            UpdateParticleMaterial();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            UpdateEmitterDistribution();
        }

        public void AddShape(ObiEmitterShape shape)
        {
            if (!emitterShapes.Contains(shape))
            {
                emitterShapes.Add(shape);

                if (solver != null)
                {
                    shape.particleSize = (emitterBlueprint != null) ? emitterBlueprint.GetParticleSize(m_Solver.parameters.mode) : 0.1f;
                    shape.GenerateDistribution();
                    distEnumerator = GetDistributionEnumerator();
                }
            }
        }

        public void RemoveShape(ObiEmitterShape shape)
        {
            emitterShapes.Remove(shape);
            if (solver != null)
            {
                distEnumerator = GetDistributionEnumerator();
            }
        }

        public void UpdateEmitterDistribution()
        {
            if (solver != null)
            {
                for (int i = 0; i < emitterShapes.Count; ++i)
                {
                    emitterShapes[i].particleSize = (emitterBlueprint != null) ? emitterBlueprint.GetParticleSize(m_Solver.parameters.mode) : 0.1f;
                    emitterShapes[i].GenerateDistribution();
                }
                distEnumerator = GetDistributionEnumerator();
            }
        }

        private IEnumerator<ObiEmitterShape.DistributionPoint> GetDistributionEnumerator()
        {

            // In case there are no shapes, emit using the emitter itself as a single-point shape.
            if (emitterShapes.Count == 0)
            {
                while (true)
                {
                    Matrix4x4 l2sTransform = actorLocalToSolverMatrix;
                    yield return new ObiEmitterShape.DistributionPoint(l2sTransform.GetColumn(3), l2sTransform.GetColumn(2), Color.white);
                }
            }

            // Emit distributing emission among all shapes:
            while (true)
            {
                for (int j = 0; j < emitterShapes.Count; ++j)
                {
                    ObiEmitterShape shape = emitterShapes[j];

                    if (shape.distribution.Count == 0)
                        yield return new ObiEmitterShape.DistributionPoint(shape.ShapeLocalToSolverMatrix.GetColumn(3), shape.ShapeLocalToSolverMatrix.GetColumn(2), Color.white);

                    for (int i = 0; i < shape.distribution.Count; ++i)
                        yield return shape.distribution[i].GetTransformed(shape.ShapeLocalToSolverMatrix, shape.color);

                }
            }

        }

        public void UpdateParticleMaterial()
        {
            for (int i = 0; i < activeParticleCount; ++i)
            {
                UpdateParticleMaterial(i);
            }

            UpdateEmitterDistribution();
        }

        public override void SetSelfCollisions(bool selfCollisions)
        {

            if (solver != null && isLoaded)
            {
                Oni.ParticleFlags particlePhase = Oni.ParticleFlags.Fluid;
                if (emitterBlueprint != null && !(emitterBlueprint is ObiFluidEmitterBlueprint))
                    particlePhase = 0;

                for (int i = 0; i < solverIndices.Length; i++)
                {
                    m_Solver.phases[solverIndices[i]] = Oni.MakePhase(fluidPhase, (selfCollisions ? Oni.ParticleFlags.SelfCollide : 0) | particlePhase);
                }
            }
        }

        private void UpdateParticleResolution(int index)
        {

            if (m_Solver == null) return;

            ObiFluidEmitterBlueprint fluidMaterial = emitterBlueprint as ObiFluidEmitterBlueprint;

            int solverIndex = solverIndices[index];

            float restDistance = (emitterBlueprint != null) ? emitterBlueprint.GetParticleSize(m_Solver.parameters.mode) : 0.1f;
            float pmass = (emitterBlueprint != null) ? emitterBlueprint.GetParticleMass(m_Solver.parameters.mode) : 0.1f;

            if (emitterBlueprint != null && fluidMaterial == null)
            {
                float randomRadius = UnityEngine.Random.Range(0, restDistance / 100.0f * (emitterBlueprint as ObiGranularEmitterBlueprint).randomness);
                m_Solver.principalRadii[solverIndex] = Vector3.one * Mathf.Max(0.001f + restDistance * 0.5f - randomRadius);
            }
            else
                m_Solver.principalRadii[solverIndex] = Vector3.one * restDistance * 0.5f;

            m_Solver.invRotationalMasses[solverIndex] = m_Solver.invMasses[solverIndex] = 1 / pmass;
            m_Solver.smoothingRadii[solverIndex] = fluidMaterial != null ? fluidMaterial.GetSmoothingRadius(m_Solver.parameters.mode) : 1f / (10 * Mathf.Pow(1, 1 / (m_Solver.parameters.mode == Oni.SolverParameters.Mode.Mode3D ? 3.0f : 2.0f)));

        }

        public void UpdateParticleMaterial(int index)
        {

            if (m_Solver == null) return;

            UpdateParticleResolution(index);

            ObiFluidEmitterBlueprint fluidMaterial = emitterBlueprint as ObiFluidEmitterBlueprint;

            int solverIndex = solverIndices[index];

            m_Solver.restDensities[solverIndex] = fluidMaterial != null ? fluidMaterial.restDensity : 0;
            m_Solver.viscosities[solverIndex] = fluidMaterial != null ? fluidMaterial.viscosity : 0;
            m_Solver.vortConfinement[solverIndex] = fluidMaterial != null ? fluidMaterial.vorticity : 0;
            m_Solver.surfaceTension[solverIndex] = fluidMaterial != null ? fluidMaterial.surfaceTension : 0;
            m_Solver.buoyancies[solverIndex] = fluidMaterial != null ? fluidMaterial.buoyancy : -1;
            m_Solver.atmosphericDrag[solverIndex] = fluidMaterial != null ? fluidMaterial.atmosphericDrag : 0;
            m_Solver.atmosphericPressure[solverIndex] = fluidMaterial != null ? fluidMaterial.atmosphericPressure : 0;
            m_Solver.diffusion[solverIndex] = fluidMaterial != null ? fluidMaterial.diffusion : 0;
            m_Solver.userData[solverIndex] = fluidMaterial != null ? fluidMaterial.diffusionData : Vector4.zero;

            Oni.ParticleFlags particlePhase = Oni.ParticleFlags.Fluid;
            if (emitterBlueprint != null && fluidMaterial == null)
                particlePhase = 0;

            m_Solver.phases[solverIndex] = Oni.MakePhase(fluidPhase, Oni.ParticleFlags.SelfCollide | particlePhase);
        }

        protected override void SwapWithFirstInactiveParticle(int index)
        {
            base.SwapWithFirstInactiveParticle(index);
            life.Swap(index, activeParticleCount);
        }

        public void ResetParticle(int index, float offset, float deltaTime)
        {

            distEnumerator.MoveNext();
            ObiEmitterShape.DistributionPoint distributionPoint = distEnumerator.Current;

            Vector3 spawnVelocity = Vector3.Lerp(distributionPoint.velocity, UnityEngine.Random.onUnitSphere, randomVelocity);
            Vector3 positionOffset = spawnVelocity * (speed * deltaTime) * offset;

            int solverIndex = solverIndices[index];

            m_Solver.startPositions[solverIndex] = m_Solver.positions[solverIndex] = distributionPoint.position + positionOffset;
            m_Solver.velocities[solverIndex] = spawnVelocity * speed;

            UpdateParticleMaterial(index);

            if (useShapeColor)
                m_Solver.colors[solverIndex] = distributionPoint.color;
        }

        /**
		 * Asks the emitter to emit a new particle. Returns whether the emission was succesful.
		 */
        public bool EmitParticle(float offset, float deltaTime)
        {

            if (activeParticleCount == particleCount) return false;

            life[activeParticleCount] = lifespan;

            // move particle to its spawn position:
            ResetParticle(activeParticleCount, offset, deltaTime);

            // now there's one active particle more:
            if (!ActivateParticle(activeParticleCount))
                return false;

            if (OnEmitParticle != null)
                OnEmitParticle(this, activeParticleCount - 1);

            m_IsEmitting = true;

            return true;
        }

        /**
		 * Asks the emiter to kill a particle. Returns whether it was succesful.
		 */
        private bool KillParticle(int index)
        {

            // reduce amount of active particles:
            if (!DeactivateParticle(index))
                return false;

            if (OnKillParticle != null)
                OnKillParticle(this, activeParticleCount);

            return true;

        }

        public void KillAll()
        {
            for (int i = activeParticleCount - 1; i >= 0; --i)
            {
                KillParticle(i);
            }
        }

        private int GetDistributionPointsCount()
        {
            int size = 0;
            for (int i = 0; i < emitterShapes.Count; ++i)
                size += emitterShapes[i].distribution.Count;
            return Mathf.Max(1, size);
        }

        public override void BeginStep(float stepTime)
        {
            base.BeginStep(stepTime);

            // cache a per-shape matrix that transforms from shape local space to solver space.
            for (int j = 0; j < emitterShapes.Count; ++j)
            {
                emitterShapes[j].UpdateLocalToSolverMatrix();
            }

            // Update lifetime and kill dead particles:
            for (int i = activeParticleCount - 1; i >= 0; --i)
            {
                life[i] -= stepTime;

                if (life[i] <= 0)
                {
                    KillParticle(i);
                }
            }

            int emissionPoints = GetDistributionPointsCount();

            int pooledParticles = particleCount - activeParticleCount;

            if (pooledParticles == 0)
                m_IsEmitting = false;

            if (m_IsEmitting || pooledParticles > Mathf.FloorToInt(minPoolSize * particleCount))
            {

                // stream emission:
                if (emissionMethod == EmissionMethod.STREAM)
                {
                    // number of bursts per simulation step:
                    float burstCount = (speed * stepTime) / ((emitterBlueprint != null) ? emitterBlueprint.GetParticleSize(m_Solver.parameters.mode) : 0.1f);

                    // Emit new particles:
                    unemittedBursts += burstCount;
                    int burst = 0;
                    while (unemittedBursts > 0)
                    {
                        for (int i = 0; i < emissionPoints; ++i)
                        {
                            EmitParticle(burst / burstCount, stepTime);
                        }
                        unemittedBursts -= 1;
                        burst++;
                    }
                }
                else
                { // burst emission:

                    if (activeParticleCount == 0)
                    {
                        for (int i = 0; i < emissionPoints; ++i)
                        {
                            EmitParticle(0,stepTime);
                        }
                    }
                }
            }

        }
    }
}
