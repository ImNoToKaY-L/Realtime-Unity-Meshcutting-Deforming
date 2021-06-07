using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    [System.Serializable]
    public class ObiDistanceConstraintsBatch : ObiConstraintsBatch, IStructuralConstraintBatch
    {
        [HideInInspector] public ObiNativeFloatList restLengths = new ObiNativeFloatList();                /**< Rest distances.*/
        [HideInInspector] public ObiNativeVector2List stiffnesses = new ObiNativeVector2List();              /**< Stiffnesses of distance constraits.*/

        public override Oni.ConstraintType constraintType
        {
            get { return Oni.ConstraintType.Distance; }
        }

        public ObiDistanceConstraintsBatch(ObiDistanceConstraintsBatch source = null):base(source){}

        public void AddConstraint(Vector2Int indices, float restLength)
        {
            RegisterConstraint();

            particleIndices.Add(indices[0]);
            particleIndices.Add(indices[1]);
            restLengths.Add(restLength);
            stiffnesses.Add(Vector2.zero);
        }

        public override void Clear()
        {
            base.Clear();
            particleIndices.Clear();
            restLengths.Clear();
            stiffnesses.Clear();
        }

        public float GetRestLength(int index)
        {
            return restLengths[index];
        }

        public void SetRestLength(int index, float restLength)
        {
            restLengths[index] = restLength;
        }

        public ParticlePair GetParticleIndices(int index)
        {
            return new ParticlePair(particleIndices[index * 2],particleIndices[index * 2 + 1]);
        }

        public override void GetParticlesInvolved(int index, List<int> particles)
        {
            particles.Add(particleIndices[index * 2]);
            particles.Add(particleIndices[index * 2 + 1]);
        }

        protected override void CopyConstraint(ObiConstraintsBatch batch, int constraintIndex)
        {
            if (batch is ObiDistanceConstraintsBatch)
            {
                var db = batch as ObiDistanceConstraintsBatch;
                RegisterConstraint();
                particleIndices.Add(batch.particleIndices[constraintIndex * 2]);
                particleIndices.Add(batch.particleIndices[constraintIndex * 2 + 1]);
                restLengths.Add(db.restLengths[constraintIndex]);
                stiffnesses.Add(db.stiffnesses[constraintIndex]);
                ActivateConstraint(constraintCount - 1);
            }
        }

        protected override void SwapConstraints(int sourceIndex, int destIndex)
        {
            particleIndices.Swap(sourceIndex * 2, destIndex * 2);
            particleIndices.Swap(sourceIndex * 2 + 1, destIndex * 2 + 1);
            restLengths.Swap(sourceIndex, destIndex);
            stiffnesses.Swap(sourceIndex, destIndex);
        }

        public override IObiConstraintsBatch Clone()
        {
            var clone = new ObiDistanceConstraintsBatch(this);

            clone.particleIndices.ResizeUninitialized(particleIndices.count);
            clone.restLengths.ResizeUninitialized(restLengths.count);
            clone.stiffnesses.ResizeUninitialized(stiffnesses.count);

            clone.particleIndices.CopyFrom(particleIndices);
            clone.restLengths.CopyFrom(restLengths);
            clone.stiffnesses.CopyFrom(stiffnesses);

            return clone;
        }

        protected override void OnAddToSolver(IObiConstraints constraints)
        {
            for (int i = 0; i < restLengths.count; i++)
            {
                particleIndices[i * 2] = constraints.GetActor().solverIndices[source.particleIndices[i * 2]];
                particleIndices[i * 2 + 1] = constraints.GetActor().solverIndices[source.particleIndices[i * 2 + 1]];
                stiffnesses[i] = new Vector2(0,restLengths[i]);
            }

            // pass constraint data arrays to the solver:
            Oni.SetDistanceConstraints(batch, particleIndices.GetIntPtr(), restLengths.GetIntPtr(), stiffnesses.GetIntPtr(), m_ConstraintCount);
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);
        }

        public void SetParameters(float compliance, float slack, float stretchingScale)
        {
            for (int i = 0; i < stiffnesses.count; i++)
            {
                restLengths[i] = ((ObiDistanceConstraintsBatch)source).restLengths[i] * stretchingScale;
                stiffnesses[i] = new Vector2(compliance, slack * restLengths[i]);
            }
        }

    }
}
