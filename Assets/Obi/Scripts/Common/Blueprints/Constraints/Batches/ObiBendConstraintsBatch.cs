using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    [System.Serializable]
    public class ObiBendConstraintsBatch : ObiConstraintsBatch
    {
        [HideInInspector] public ObiNativeFloatList restBends = new ObiNativeFloatList();                 /**< Rest distances.*/
        [HideInInspector] public ObiNativeVector2List bendingStiffnesses = new ObiNativeVector2List();

        public override Oni.ConstraintType constraintType
        {
            get { return Oni.ConstraintType.Bending; }
        }

        public ObiBendConstraintsBatch(ObiBendConstraintsBatch source = null) : base(source) { }

        public override IObiConstraintsBatch Clone()
        {
            var clone = new ObiBendConstraintsBatch(this);

            clone.particleIndices.ResizeUninitialized(particleIndices.count);
            clone.restBends.ResizeUninitialized(restBends.count);
            clone.bendingStiffnesses.ResizeUninitialized(bendingStiffnesses.count);

            clone.particleIndices.CopyFrom(particleIndices);
            clone.restBends.CopyFrom(restBends);
            clone.bendingStiffnesses.CopyFrom(bendingStiffnesses);

            return clone;
        }

        public void AddConstraint(Vector3Int indices, float restBend)
        {
            RegisterConstraint();

            particleIndices.Add(indices[0]);
            particleIndices.Add(indices[1]);
            particleIndices.Add(indices[2]);
            restBends.Add(restBend);
            bendingStiffnesses.Add(Vector2.zero);
        }

        public override void Clear()
        {
            base.Clear();
            particleIndices.Clear();
            restBends.Clear();
            bendingStiffnesses.Clear();
        }

        public override void GetParticlesInvolved(int index, List<int> particles)
        {
            particles.Add(particleIndices[index * 3]);
            particles.Add(particleIndices[index * 3 + 1]);
            particles.Add(particleIndices[index * 3 + 2]);
        }

        protected override void SwapConstraints(int sourceIndex, int destIndex)
        {
            particleIndices.Swap(sourceIndex * 3, destIndex * 3);
            particleIndices.Swap(sourceIndex * 3 + 1 , destIndex * 3 + 1);
            particleIndices.Swap(sourceIndex * 3 + 2, destIndex * 3 + 2);
            restBends.Swap(sourceIndex, destIndex);
            bendingStiffnesses.Swap(sourceIndex, destIndex);
        }

        protected override void OnAddToSolver(IObiConstraints constraints)
        {
            for (int i = 0; i < restBends.count; i++)
            {
                particleIndices[i * 3] = constraints.GetActor().solverIndices[source.particleIndices[i * 3]];
                particleIndices[i * 3 + 1] = constraints.GetActor().solverIndices[source.particleIndices[i * 3 + 1]];
                particleIndices[i * 3 + 2] = constraints.GetActor().solverIndices[source.particleIndices[i * 3 + 2]];
            }

            // pass constraint data arrays to the solver:
            Oni.SetBendingConstraints(batch, particleIndices.GetIntPtr(), restBends.GetIntPtr(), bendingStiffnesses.GetIntPtr(), m_ConstraintCount);
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);
        }

        public void SetParameters(float compliance, float maxBending)
        {
            for (int i = 0; i < bendingStiffnesses.count; i++)
                bendingStiffnesses[i] = new Vector2(maxBending, compliance);
        }
    }
}
