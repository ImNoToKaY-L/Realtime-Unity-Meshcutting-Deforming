using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    [System.Serializable]
    public class ObiShapeMatchingConstraintsBatch : ObiConstraintsBatch
    {
        
        public ObiNativeIntList firstIndex = new ObiNativeIntList();
        public ObiNativeIntList numIndices = new ObiNativeIntList();
        public ObiNativeIntList explicitGroup = new ObiNativeIntList(); /**< whether the constraint is implicit (0) or explicit (>0).*/
        public ObiNativeFloatList materialParameters = new ObiNativeFloatList(); /**< 5 floats per constraint: stiffness, plastic yield, creep, recovery and max deformation.*/

        public ObiNativeVector4List restComs = new ObiNativeVector4List();
        public ObiNativeVector4List coms = new ObiNativeVector4List();
        public ObiNativeQuaternionList orientations = new ObiNativeQuaternionList();

        public override Oni.ConstraintType constraintType
        {
            get { return Oni.ConstraintType.ShapeMatching; }
        }

        public ObiShapeMatchingConstraintsBatch(ObiShapeMatchingConstraintsBatch source = null) : base(source) { }

        public override IObiConstraintsBatch Clone()
        {
            var clone = new ObiShapeMatchingConstraintsBatch(this);

            clone.particleIndices.ResizeUninitialized(particleIndices.count);
            clone.firstIndex.ResizeUninitialized(firstIndex.count);
            clone.numIndices.ResizeUninitialized(numIndices.count);
            clone.explicitGroup.ResizeUninitialized(explicitGroup.count);
            clone.materialParameters.ResizeUninitialized(materialParameters.count);

            clone.particleIndices.CopyFrom(particleIndices);
            clone.firstIndex.CopyFrom(firstIndex);
            clone.numIndices.CopyFrom(numIndices);
            clone.explicitGroup.CopyFrom(explicitGroup);
            clone.materialParameters.CopyFrom(materialParameters);

            clone.restComs.ResizeUninitialized(constraintCount);
            clone.coms.ResizeUninitialized(constraintCount);
            clone.orientations.ResizeUninitialized(constraintCount);

            return clone;
        }

        public void AddConstraint(int[] indices, bool isExplicit)
        {
            RegisterConstraint();

            firstIndex.Add((int)particleIndices.count);
            numIndices.Add((int)indices.Length);
            explicitGroup.Add(isExplicit ? 1 : 0);
            particleIndices.AddRange(indices);
            materialParameters.AddRange(new float[] { 1, 1, 1, 1, 1 });
        }

        public override void Clear()
        {
            base.Clear();
            firstIndex.Clear();
            numIndices.Clear();
            explicitGroup.Clear();
            particleIndices.Clear();
            materialParameters.Clear();
        }

        public override void GetParticlesInvolved(int index, List<int> particles)
        {
            int first = firstIndex[index];
            int num = numIndices[index];
            for (int i = first; i < first + num; ++i) 
                particles.Add(particleIndices[i]);
        }

        protected override void SwapConstraints(int sourceIndex, int destIndex)
        {
            firstIndex.Swap(sourceIndex, destIndex);
            numIndices.Swap(sourceIndex, destIndex);
            explicitGroup.Swap(sourceIndex, destIndex);

            for (int i = 0; i < 5; ++i)
                materialParameters.Swap(sourceIndex * 5 + i, destIndex * 5 + i);

            restComs.Swap(sourceIndex, destIndex);
            coms.Swap(sourceIndex, destIndex);
            orientations.Swap(sourceIndex, destIndex);
        }

        protected override void OnAddToSolver(IObiConstraints constraints)
        {
            for (int i = 0; i < particleIndices.count; i++)
            {
                particleIndices[i] = constraints.GetActor().solverIndices[source.particleIndices[i]];
            }

            for (int i = 0; i < orientations.count; i++)
                orientations[i] = constraints.GetActor().actorLocalToSolverMatrix.rotation;

            // pass constraint data arrays to the solver:
            Oni.SetShapeMatchingConstraints(batch, particleIndices.GetIntPtr(), firstIndex.GetIntPtr(), numIndices.GetIntPtr(), explicitGroup.GetIntPtr(),
                                            materialParameters.GetIntPtr(), restComs.GetIntPtr(), coms.GetIntPtr(), orientations.GetIntPtr(), m_ConstraintCount);
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);

            Oni.CalculateRestShapeMatching(constraints.GetActor().solver.OniSolver, batch);
        }

        public void SetParameters(float stiffness, float yield, float creep, float recovery, float maxDeformation)
        {
            for (int i = 0; i < explicitGroup.count; i++)
            {
                materialParameters[i * 5] = stiffness;
                materialParameters[i * 5 + 1] = yield;
                materialParameters[i * 5 + 2] = creep;
                materialParameters[i * 5 + 3] = recovery;
                materialParameters[i * 5 + 4] = maxDeformation;
            }
        }

    }
}
