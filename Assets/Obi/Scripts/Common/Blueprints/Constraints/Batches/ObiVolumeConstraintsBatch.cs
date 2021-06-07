using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    [System.Serializable]
    public class ObiVolumeConstraintsBatch : ObiConstraintsBatch
    {
        [HideInInspector] public ObiNativeIntList firstTriangle = new ObiNativeIntList();               /**< index of first triangle for each constraint.*/
        [HideInInspector] public ObiNativeFloatList restVolumes = new ObiNativeFloatList();             /**< rest volume for each constraint.*/
        [HideInInspector] public ObiNativeVector2List pressureStiffness = new ObiNativeVector2List();       /**< pressure and stiffness for each constraint.*/

        public override Oni.ConstraintType constraintType
        {
            get { return Oni.ConstraintType.Volume; }
        }

        public ObiVolumeConstraintsBatch(ObiVolumeConstraintsBatch source = null) : base(source) { }

        public override IObiConstraintsBatch Clone()
        {
            var clone = new ObiVolumeConstraintsBatch(this);

            clone.particleIndices.ResizeUninitialized(particleIndices.count);
            clone.firstTriangle.ResizeUninitialized(firstTriangle.count);
            clone.restVolumes.ResizeUninitialized(restVolumes.count);
            clone.pressureStiffness.ResizeUninitialized(pressureStiffness.count);

            clone.particleIndices.CopyFrom(particleIndices);
            clone.firstTriangle.CopyFrom(firstTriangle);
            clone.restVolumes.CopyFrom(restVolumes);
            clone.pressureStiffness.CopyFrom(pressureStiffness);

            return clone;
        }

        public void AddConstraint(int[] triangles, float restVolume)
        {
            RegisterConstraint();

            firstTriangle.Add((int)particleIndices.count / 3);
            particleIndices.AddRange(triangles);
            restVolumes.Add(restVolume);
            pressureStiffness.Add(new Vector2(1,0));
        }

        public override void Clear()
        {
            base.Clear();
            particleIndices.Clear();
            firstTriangle.Clear();
            restVolumes.Clear();
            pressureStiffness.Clear();
        }

        public override void GetParticlesInvolved(int index, List<int> particles)
        {
            //TODO.
        }

        protected override void SwapConstraints(int sourceIndex, int destIndex)
        {
            firstTriangle.Swap(sourceIndex, destIndex);
            restVolumes.Swap(sourceIndex, destIndex);
            pressureStiffness.Swap(sourceIndex, destIndex);
        }

        protected override void OnAddToSolver(IObiConstraints constraints)
        {
            for (int i = 0; i < particleIndices.count; i++)
                particleIndices[i] = constraints.GetActor().solverIndices[source.particleIndices[i]];

            // pass constraint data arrays to the solver:
            Oni.SetVolumeConstraints(batch, particleIndices.GetIntPtr(), firstTriangle.GetIntPtr(),
                                     restVolumes.GetIntPtr(), pressureStiffness.GetIntPtr(), m_ConstraintCount);
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);
        }

        public void SetParameters(float compliance, float pressure)
        {
            Vector2 p = new Vector2(pressure, compliance);
            for (int i = 0; i < pressureStiffness.count; i++)
                pressureStiffness[i] = p;
        }
    }
}
