using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    [System.Serializable]
    public class ObiBendTwistConstraintsBatch : ObiConstraintsBatch
    {
        [HideInInspector] public ObiNativeQuaternionList restDarbouxVectors = new ObiNativeQuaternionList();                /**< Rest distances.*/
        [HideInInspector] public ObiNativeVector3List stiffnesses = new ObiNativeVector3List(); /**< Stiffnesses of distance constraits.*/

        public override Oni.ConstraintType constraintType
        {
            get { return Oni.ConstraintType.BendTwist; }
        }

        public ObiBendTwistConstraintsBatch(ObiBendTwistConstraintsBatch source = null) : base(source) { }

        public override IObiConstraintsBatch Clone()
        {
            var clone = new ObiBendTwistConstraintsBatch(this);

            clone.particleIndices.ResizeUninitialized(particleIndices.count);
            clone.restDarbouxVectors.ResizeUninitialized(restDarbouxVectors.count);
            clone.stiffnesses.ResizeUninitialized(stiffnesses.count);

            clone.particleIndices.CopyFrom(particleIndices);
            clone.restDarbouxVectors.CopyFrom(restDarbouxVectors);
            clone.stiffnesses.CopyFrom(stiffnesses);

            return clone;
        }

        public void AddConstraint(Vector2Int indices, Quaternion restDarboux)
        {
            RegisterConstraint();

            particleIndices.Add(indices[0]);
            particleIndices.Add(indices[1]);
            restDarbouxVectors.Add(restDarboux);
            stiffnesses.Add(Vector3.zero);
        }

        public override void Clear()
        {
            base.Clear();
            particleIndices.Clear();
            restDarbouxVectors.Clear();
            stiffnesses.Clear();
        }

        public override void GetParticlesInvolved(int index, List<int> particles)
        {
            particles.Add(particleIndices[index * 2]);
            particles.Add(particleIndices[index * 2 + 1]);
        }

        protected override void SwapConstraints(int sourceIndex, int destIndex)
        {
            particleIndices.Swap(sourceIndex * 3, destIndex * 3);
            particleIndices.Swap(sourceIndex * 3 + 1, destIndex * 3 + 1);
            particleIndices.Swap(sourceIndex * 3 + 2, destIndex * 3 + 2);
            restDarbouxVectors.Swap(sourceIndex, destIndex);
            stiffnesses.Swap(sourceIndex, destIndex);
        }

        protected override void OnAddToSolver(IObiConstraints constraints)
        {
            for (int i = 0; i < restDarbouxVectors.count; i++)
            {
                particleIndices[i * 2] = constraints.GetActor().solverIndices[source.particleIndices[i * 2]];
                particleIndices[i * 2 + 1] = constraints.GetActor().solverIndices[source.particleIndices[i * 2 + 1]];
            }

            // pass constraint data arrays to the solver:
            Oni.SetBendTwistConstraints(batch, particleIndices.GetIntPtr(), restDarbouxVectors.GetIntPtr(), stiffnesses.GetIntPtr(), m_ConstraintCount);
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);
        }

        public void SetParameters(float torsionCompliance, float bend1Compliance, float bend2Compliance)
        {
            for (int i = 0; i < stiffnesses.count; i++)
            {
                stiffnesses[i] = new Vector3(torsionCompliance, bend1Compliance, bend2Compliance);
            }
        }
    }
}
