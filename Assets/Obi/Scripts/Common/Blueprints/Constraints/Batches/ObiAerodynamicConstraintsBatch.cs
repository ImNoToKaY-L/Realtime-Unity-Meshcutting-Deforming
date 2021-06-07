using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    [System.Serializable]
    public class ObiAerodynamicConstraintsBatch : ObiConstraintsBatch
    {
        [HideInInspector] public ObiNativeFloatList aerodynamicCoeffs = new ObiNativeFloatList();  /**< Per-constraint aerodynamic coeffs, 3 per constraint*/

        public override Oni.ConstraintType constraintType 
        {
            get { return Oni.ConstraintType.Aerodynamics; }
        }

        public ObiAerodynamicConstraintsBatch(ObiAerodynamicConstraintsBatch source = null) : base(source) { }

        public override IObiConstraintsBatch Clone()
        {
            var clone = new ObiAerodynamicConstraintsBatch(this);

            clone.particleIndices.ResizeUninitialized(particleIndices.count);
            clone.aerodynamicCoeffs.ResizeUninitialized(aerodynamicCoeffs.count);

            clone.particleIndices.CopyFrom(particleIndices);
            clone.aerodynamicCoeffs.CopyFrom(aerodynamicCoeffs);

            return clone;
        }

        public void AddConstraint(int index, float area, float drag, float lift)
        {
            RegisterConstraint();

            particleIndices.Add(index);
            aerodynamicCoeffs.Add(area);
            aerodynamicCoeffs.Add(drag);
            aerodynamicCoeffs.Add(lift);
        }

        public override void GetParticlesInvolved(int index, List<int> particles)
        {
            particles.Add(particleIndices[index]);
        }

        public override void Clear()
        {
            base.Clear();
            particleIndices.Clear();
            aerodynamicCoeffs.Clear();
        }

        protected override void SwapConstraints(int sourceIndex, int destIndex)
        {
            particleIndices.Swap(sourceIndex, destIndex);
            aerodynamicCoeffs.Swap(sourceIndex * 3, destIndex * 3);
            aerodynamicCoeffs.Swap(sourceIndex * 3 + 1, destIndex * 3 + 1);
            aerodynamicCoeffs.Swap(sourceIndex * 3 + 2, destIndex * 3 + 2);
        }

        protected override void OnAddToSolver(IObiConstraints constraints)
        {
            for (int i = 0; i < particleIndices.count; i++)
            {
                particleIndices[i] = constraints.GetActor().solverIndices[source.particleIndices[i]];
            }

            // pass constraint data arrays to the solver:
            Oni.SetAerodynamicConstraints(batch, particleIndices.GetIntPtr(), aerodynamicCoeffs.GetIntPtr(), m_ConstraintCount);
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);
        }

        public void SetParameters(float drag, float lift)
        {
            for (int i = 0; i < particleIndices.count; i++)
            {
                aerodynamicCoeffs[i * 3 + 1] = drag;
                aerodynamicCoeffs[i * 3 + 2] = lift;
            }
        }
    }
}
