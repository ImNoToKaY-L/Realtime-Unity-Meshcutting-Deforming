using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    [System.Serializable]
    public class ObiStretchShearConstraintsBatch : ObiConstraintsBatch, IStructuralConstraintBatch
    {
        [HideInInspector] public ObiNativeIntList orientationIndices = new ObiNativeIntList();                 /**< Distance constraint indices.*/
        [HideInInspector] public ObiNativeFloatList restLengths = new ObiNativeFloatList();             /**< Rest distances.*/
        [HideInInspector] public ObiNativeQuaternionList restOrientations = new ObiNativeQuaternionList();          /**< Stiffnesses of distance constraits.*/
        [HideInInspector] public ObiNativeVector3List stiffnesses = new ObiNativeVector3List();      /**< Stiffnesses of distance constraits.*/

        public override Oni.ConstraintType constraintType
        {
            get { return Oni.ConstraintType.StretchShear; }
        }

        public ObiStretchShearConstraintsBatch(ObiStretchShearConstraintsBatch source = null) : base(source) { }

        public override IObiConstraintsBatch Clone()
        {
            var clone = new ObiStretchShearConstraintsBatch(this);

            clone.particleIndices.ResizeUninitialized(particleIndices.count);
            clone.orientationIndices.ResizeUninitialized(orientationIndices.count);
            clone.restLengths.ResizeUninitialized(restLengths.count);
            clone.restOrientations.ResizeUninitialized(restOrientations.count);
            clone.stiffnesses.ResizeUninitialized(stiffnesses.count);

            clone.particleIndices.CopyFrom(particleIndices);
            clone.orientationIndices.CopyFrom(orientationIndices);
            clone.restLengths.CopyFrom(restLengths);
            clone.restOrientations.CopyFrom(restOrientations);
            clone.stiffnesses.CopyFrom(stiffnesses);

            return clone;
        }

        public void AddConstraint(Vector2Int indices, int orientationIndex, float restLength, Quaternion restOrientation)
        {
            RegisterConstraint();

            particleIndices.Add(indices[0]);
            particleIndices.Add(indices[1]);
            orientationIndices.Add(orientationIndex);
            restLengths.Add(restLength);
            restOrientations.Add(restOrientation);
            stiffnesses.Add(Vector3.zero);
        }

        public override void Clear()
        {
            base.Clear();
            particleIndices.Clear();
            orientationIndices.Clear();
            restLengths.Clear();
            restOrientations.Clear();
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
            return new ParticlePair(particleIndices[index * 2], particleIndices[index * 2 + 1]);
        }

        public override void GetParticlesInvolved(int index, List<int> particles)
        {
            particles.Add(particleIndices[index * 2]);
            particles.Add(particleIndices[index * 2 + 1]);
        }

        protected override void SwapConstraints(int sourceIndex, int destIndex)
        {
            particleIndices.Swap(sourceIndex * 2 , destIndex * 2);
            particleIndices.Swap(sourceIndex * 2 + 1, destIndex * 2 + 1);
            orientationIndices.Swap(sourceIndex, destIndex);
            restLengths.Swap(sourceIndex, destIndex);
            restOrientations.Swap(sourceIndex, destIndex);
            stiffnesses.Swap(sourceIndex, destIndex);
        }

        protected override void OnAddToSolver(IObiConstraints constraints)
        {
            for (int i = 0; i < restLengths.count; i++)
            {
                particleIndices[i * 2] = constraints.GetActor().solverIndices[source.particleIndices[i * 2]];
                particleIndices[i * 2 + 1] = constraints.GetActor().solverIndices[source.particleIndices[i * 2 + 1]];
                orientationIndices[i] = constraints.GetActor().solverIndices[((ObiStretchShearConstraintsBatch)source).orientationIndices[i]];
            }

            // pass constraint data arrays to the solver:
            Oni.SetStretchShearConstraints(batch, particleIndices.GetIntPtr(), orientationIndices.GetIntPtr(), restLengths.GetIntPtr(), restOrientations.GetIntPtr(), stiffnesses.GetIntPtr(), m_ConstraintCount);
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);
        }

        public void SetParameters(float stretchCompliance, float shear1Compliance, float shear2Compliance)
        {
            for (int i = 0; i < stiffnesses.count; i++)
            {
                stiffnesses[i] = new Vector3(stretchCompliance, shear1Compliance, shear2Compliance);
            }
        }
    }
}
