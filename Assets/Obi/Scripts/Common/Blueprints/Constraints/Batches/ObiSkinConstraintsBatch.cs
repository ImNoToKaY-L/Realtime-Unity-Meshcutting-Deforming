using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    [System.Serializable]
    public class ObiSkinConstraintsBatch : ObiConstraintsBatch
    {
        [HideInInspector] public ObiNativeVector4List skinPoints = new ObiNativeVector4List();                /**< Skin constraint anchor points, in world space.*/
        [HideInInspector] public ObiNativeVector4List skinNormals = new ObiNativeVector4List();               /**< Rest distances.*/
        [HideInInspector] public ObiNativeFloatList skinRadiiBackstop = new ObiNativeFloatList();             /**< Rest distances.*/
        [HideInInspector] public ObiNativeFloatList skinCompliance = new ObiNativeFloatList();               /**< Stiffnesses of distance constraits.*/

        public override Oni.ConstraintType constraintType
        {
            get { return Oni.ConstraintType.Skin; }
        }

        public ObiSkinConstraintsBatch(ObiSkinConstraintsBatch source = null) : base(source) { }

        public override IObiConstraintsBatch Clone()
        {
            var clone = new ObiSkinConstraintsBatch(this);

            clone.particleIndices.ResizeUninitialized(particleIndices.count);
            clone.skinPoints.ResizeUninitialized(skinPoints.count);
            clone.skinNormals.ResizeUninitialized(skinNormals.count);
            clone.skinRadiiBackstop.ResizeUninitialized(skinRadiiBackstop.count);
            clone.skinCompliance.ResizeUninitialized(skinCompliance.count);

            clone.particleIndices.CopyFrom(particleIndices);
            clone.skinPoints.CopyFrom(skinPoints);
            clone.skinNormals.CopyFrom(skinNormals);
            clone.skinRadiiBackstop.CopyFrom(skinRadiiBackstop);
            clone.skinCompliance.CopyFrom(skinCompliance);

            return clone;
        }

        public void AddConstraint(int index, Vector4 point, Vector4 normal, float radius, float collisionRadius, float backstop, float stiffness)
        {
            RegisterConstraint();

            particleIndices.Add(index);
            skinPoints.Add(point);
            skinNormals.Add(normal);
            skinRadiiBackstop.Add(radius);
            skinRadiiBackstop.Add(collisionRadius);
            skinRadiiBackstop.Add(backstop);
            skinCompliance.Add(stiffness);
        }

        public override void Clear()
        {
            base.Clear();
            particleIndices.Clear();
            skinPoints.Clear();
            skinNormals.Clear();
            skinRadiiBackstop.Clear();
            skinCompliance.Clear();
        }

        public override void GetParticlesInvolved(int index, List<int> particles)
        {
            particles.Add(particleIndices[index]);
        }

        protected override void SwapConstraints(int sourceIndex, int destIndex)
        {
            particleIndices.Swap(sourceIndex, destIndex);
            skinPoints.Swap(sourceIndex, destIndex);
            skinNormals.Swap(sourceIndex, destIndex);
            skinRadiiBackstop.Swap(sourceIndex * 3, destIndex * 3);
            skinRadiiBackstop.Swap(sourceIndex * 3+1, destIndex * 3+1);
            skinRadiiBackstop.Swap(sourceIndex * 3+2, destIndex * 3+2);
            skinCompliance.Swap(sourceIndex, destIndex);
        }

        protected override void OnAddToSolver(IObiConstraints constraints)
        {
            for (int i = 0; i < skinCompliance.count; i++)
            {
                particleIndices[i] = constraints.GetActor().solverIndices[source.particleIndices[i]];
            }

            // pass constraint data arrays to the solver:
            Oni.SetSkinConstraints(batch, particleIndices.GetIntPtr(), skinPoints.GetIntPtr(), skinNormals.GetIntPtr(), skinRadiiBackstop.GetIntPtr(), skinCompliance.GetIntPtr(), m_ConstraintCount);
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);
        }

    }
}
