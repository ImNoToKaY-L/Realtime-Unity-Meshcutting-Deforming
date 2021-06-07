using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    [System.Serializable]
    public class ObiChainConstraintsBatch : ObiConstraintsBatch
    {
        [HideInInspector] public ObiNativeIntList firstParticle = new ObiNativeIntList();           /**< index of first particle for each constraint.*/
        [HideInInspector] public ObiNativeIntList numParticles = new ObiNativeIntList();            /**< num of particles for each constraint.*/
        [HideInInspector] public ObiNativeVector2List lengths = new ObiNativeVector2List();         /**< min/max lenghts for each constraint.*/

        public override Oni.ConstraintType constraintType
        {
            get { return Oni.ConstraintType.Chain; }
        }

        public ObiChainConstraintsBatch(ObiChainConstraintsBatch source = null) : base(source) { }

        public override IObiConstraintsBatch Clone()
        {
            var clone = new ObiChainConstraintsBatch(this);

            clone.particleIndices.ResizeUninitialized(particleIndices.count);
            clone.firstParticle.ResizeUninitialized(firstParticle.count);
            clone.numParticles.ResizeUninitialized(numParticles.count);
            clone.lengths.ResizeUninitialized(lengths.count);

            clone.particleIndices.CopyFrom(particleIndices);
            clone.firstParticle.CopyFrom(firstParticle);
            clone.numParticles.CopyFrom(numParticles);
            clone.lengths.CopyFrom(lengths);

            return clone;
        }

        public void AddConstraint(int[] indices, float restLength, float stretchStiffness, float compressionStiffness)
        {
            RegisterConstraint();

            firstParticle.Add((int)particleIndices.count);
            numParticles.Add((int)indices.Length);
            particleIndices.AddRange(indices);
            lengths.Add(new Vector2(restLength, restLength));
        }

        public override void Clear()
        {
            base.Clear();
            particleIndices.Clear();
            firstParticle.Clear();
            numParticles.Clear();
            lengths.Clear();
        }

        public override void GetParticlesInvolved(int index, List<int> particles)
        {
            //TODO.
        }

        protected override void SwapConstraints(int sourceIndex, int destIndex)
        {
            firstParticle.Swap(sourceIndex, destIndex);
            numParticles.Swap(sourceIndex, destIndex);
            lengths.Swap(sourceIndex, destIndex);
        }

        protected override void OnAddToSolver(IObiConstraints constraints)
        {
            for (int i = 0; i < particleIndices.count; i++)
                particleIndices[i] = constraints.GetActor().solverIndices[source.particleIndices[i]];

            // pass constraint data arrays to the solver:
            Oni.SetChainConstraints(batch, particleIndices.GetIntPtr(), lengths.GetIntPtr(), firstParticle.GetIntPtr(), numParticles.GetIntPtr(), m_ConstraintCount);
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);
        }

        public void SetParameters(float tightness)
        {
            for (int i = 0; i < constraintCount; i++)
                lengths[i] = new Vector2(lengths[i].y * tightness, lengths[i].y);
        }
    }
}
