using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    [System.Serializable]
    public class ObiPinConstraintsBatch : ObiConstraintsBatch
    {
        [HideInInspector] public ObiNativeIntPtrList pinBodies = new ObiNativeIntPtrList();                         /**< Pin bodies.*/
        [HideInInspector] public ObiNativeVector4List offsets = new ObiNativeVector4List();                         /**< Offset expressed in the attachment's local space.*/
        [HideInInspector] public ObiNativeQuaternionList restDarbouxVectors = new ObiNativeQuaternionList();        /**< Rest Darboux vectors.*/
        [HideInInspector] public ObiNativeFloatList stiffnesses = new ObiNativeFloatList();                         /**< Stiffnesses of pin constraits. 2 float per constraint (positional and rotational stiffness).*/
        [HideInInspector] public ObiNativeFloatList breakThresholds = new ObiNativeFloatList();

        public float[] constraintForces;

        public override Oni.ConstraintType constraintType
        {
            get { return Oni.ConstraintType.Pin; }
        }

        public ObiPinConstraintsBatch(ObiPinConstraintsBatch source = null) : base(source) { }

        public override IObiConstraintsBatch Clone()
        {
            var clone = new ObiPinConstraintsBatch(this);

            // careful here: since IntPtr is not serializable and the pinBodies array can be null, use offsets count instead.
            clone.pinBodies.ResizeUninitialized(offsets.count);

            clone.particleIndices.ResizeUninitialized(particleIndices.count);
            clone.offsets.ResizeUninitialized(offsets.count);
            clone.restDarbouxVectors.ResizeUninitialized(restDarbouxVectors.count);
            clone.stiffnesses.ResizeUninitialized(stiffnesses.count);
            clone.breakThresholds.ResizeUninitialized(breakThresholds.count);

            if (pinBodies != null)
                clone.pinBodies.CopyFrom(pinBodies);

            clone.particleIndices.CopyFrom(particleIndices);
            clone.offsets.CopyFrom(offsets);
            clone.restDarbouxVectors.CopyFrom(restDarbouxVectors);
            clone.stiffnesses.CopyFrom(stiffnesses);
            clone.breakThresholds.CopyFrom(breakThresholds);

            return clone;
        }

        public void AddConstraint(int index, ObiColliderBase body, Vector3 offset, Quaternion restDarboux)
        {
            RegisterConstraint();

            particleIndices.Add(index);
            pinBodies.Add(body != null ? body.OniCollider : IntPtr.Zero);
            offsets.Add(offset);
            restDarbouxVectors.Add(restDarboux);
            stiffnesses.Add(0);
            stiffnesses.Add(0);
            breakThresholds.Add(float.PositiveInfinity);
        }

        public override void Clear()
        {
            base.Clear();
            particleIndices.Clear();
            pinBodies.Clear();
            offsets.Clear();
            restDarbouxVectors.Clear();
            stiffnesses.Clear();
        }

        public override void GetParticlesInvolved(int index, List<int> particles)
        {
            particles.Add(particleIndices[index]);
        }

        protected override void SwapConstraints(int sourceIndex, int destIndex)
        {
            particleIndices.Swap(sourceIndex, destIndex);
            pinBodies.Swap(sourceIndex, destIndex);
            offsets.Swap(sourceIndex, destIndex);
            restDarbouxVectors.Swap(sourceIndex, destIndex);
            stiffnesses.Swap(sourceIndex * 2, destIndex * 2);
            stiffnesses.Swap(sourceIndex * 2 + 1, destIndex * 2 + 1);
        }

        protected override void OnAddToSolver(IObiConstraints constraints)
        {
            if (source != null)
            {
                for (int i = 0; i < particleIndices.count; i++)
                {
                    particleIndices[i] = constraints.GetActor().solverIndices[source.particleIndices[i]];
                    stiffnesses[i * 2] = 0;
                    stiffnesses[i * 2 + 1] = 0;
                }
            }

            // pass constraint data arrays to the solver:
            Oni.SetPinConstraints(batch, particleIndices.GetIntPtr(), offsets.GetIntPtr(), restDarbouxVectors.GetIntPtr(), pinBodies.GetIntPtr(), stiffnesses.GetIntPtr(), m_ConstraintCount);
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);
        }

        public void BreakConstraints()
        {

            if (constraintForces == null || constraintForces.Length != constraintCount * 4)
                constraintForces = new float[constraintCount * 4];

            Oni.GetBatchConstraintForces(batch, constraintForces, constraintCount, 0);

            for (int i = 0; i < constraintCount; i++)
            {
                if (-constraintForces[i * 4 + 3] > breakThresholds[i])// units are newtons.
                    DeactivateConstraint(i);
            }

        }
    }
}
