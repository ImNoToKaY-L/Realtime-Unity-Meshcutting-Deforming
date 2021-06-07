using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    [System.Serializable]
    public class ObiTetherConstraintsBatch : ObiConstraintsBatch
    {
        [HideInInspector] public ObiNativeVector2List maxLengthsScales = new ObiNativeVector2List();                /**< Rest distances.*/
        [HideInInspector] public ObiNativeFloatList stiffnesses = new ObiNativeFloatList();              /**< Stiffnesses of distance constraits.*/

        public override Oni.ConstraintType constraintType
        {
            get { return Oni.ConstraintType.Tether; }
        }

        public ObiTetherConstraintsBatch(ObiTetherConstraintsBatch source = null) : base(source) { }

        public override IObiConstraintsBatch Clone()
        {
            var clone = new ObiTetherConstraintsBatch(this);

            clone.particleIndices.ResizeUninitialized(particleIndices.count);
            clone.maxLengthsScales.ResizeUninitialized(maxLengthsScales.count);
            clone.stiffnesses.ResizeUninitialized(stiffnesses.count);

            clone.particleIndices.CopyFrom(particleIndices);
            clone.maxLengthsScales.CopyFrom(maxLengthsScales);
            clone.stiffnesses.CopyFrom(stiffnesses);

            return clone;
        }

        public void AddConstraint(Vector2Int indices, float maxLength, float scale)
        {
            RegisterConstraint();

            particleIndices.Add(indices[0]);
            particleIndices.Add(indices[1]);
            maxLengthsScales.Add(new Vector2(maxLength, scale));
            stiffnesses.Add(0);
        }

        public override void Clear()
        {
            base.Clear();
            particleIndices.Clear();
            maxLengthsScales.Clear();
            stiffnesses.Clear();
        }

        public override void GetParticlesInvolved(int index, List<int> particles)
        {
            particles.Add(particleIndices[index * 2]);
            particles.Add(particleIndices[index * 2 + 1]);
        }

        protected override void SwapConstraints(int sourceIndex, int destIndex)
        {
            particleIndices.Swap(sourceIndex * 2, destIndex * 2);
            particleIndices.Swap(sourceIndex * 2 + 1, destIndex * 2 + 1);
            maxLengthsScales.Swap(sourceIndex, destIndex);
            stiffnesses.Swap(sourceIndex, destIndex);
        }

        protected override void OnAddToSolver(IObiConstraints constraints)
        {
            for (int i = 0; i < stiffnesses.count; i++)
            {
                particleIndices[i * 2] = constraints.GetActor().solverIndices[source.particleIndices[i * 2]];
                particleIndices[i * 2 + 1] = constraints.GetActor().solverIndices[source.particleIndices[i * 2 + 1]];
            }

            // pass constraint data arrays to the solver:
            Oni.SetTetherConstraints(batch, particleIndices.GetIntPtr(), maxLengthsScales.GetIntPtr(), stiffnesses.GetIntPtr(), m_ConstraintCount);
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);
        }

        public void SetParameters(float compliance, float scale)
        {
            for (int i = 0; i < stiffnesses.count; i++)
            {
                stiffnesses[i] = compliance;
                maxLengthsScales[i] = new Vector2(maxLengthsScales[i].x, scale);
            }
        }

    }
}
