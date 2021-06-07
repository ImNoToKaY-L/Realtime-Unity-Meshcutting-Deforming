using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    public interface IObiConstraintsBatch
    {
        int constraintCount
        {
            get;
        }

        int activeConstraintCount
        {
            get;
            set;
        }

        int initialActiveConstraintCount
        {
            get;
            set;
        }

        Oni.ConstraintType constraintType
        {
            get;
        }

        IntPtr oniBatch
        {
            get;
        }

        IObiConstraintsBatch Clone();

        void AddToSolver(IObiConstraints constraints);
        void RemoveFromSolver(IObiConstraints constraints);

        bool DeactivateConstraint(int constraintIndex);
        bool ActivateConstraint(int constraintIndex);
        void DeactivateAllConstraints();

        void SetEnabled(bool enabled);
        void Clear();

        void GetParticlesInvolved(int index, List<int> particles);
        void ParticlesSwapped(int index, int newIndex);
    }

    public abstract class ObiConstraintsBatch : IObiConstraintsBatch
    {
        protected ObiConstraintsBatch source; 
        protected IntPtr batch; /**< pointer to constraint batch in the solver.*/

        [HideInInspector] [SerializeField] protected List<int> m_IDs = new List<int>();
        [HideInInspector] [SerializeField] protected List<int> m_IDToIndex = new List<int>();         /**< maps from constraint ID to constraint index. When activating/deactivating constraints, their order changes. That makes this
                                                         map necessary. All active constraints are at the beginning of the constraint arrays, in the 0, activeConstraintCount index range.*/

        [HideInInspector] [SerializeField] protected int m_ConstraintCount = 0;
        [HideInInspector] [SerializeField] protected int m_ActiveConstraintCount = 0;
        [HideInInspector] [SerializeField] protected int m_InitialActiveConstraintCount = 0;
        [HideInInspector] public ObiNativeIntList particleIndices = new ObiNativeIntList();  /**< particle indices, amount of them per constraint can be variable. */

        public int constraintCount
        {
            get { return m_ConstraintCount; }
        }

        public int activeConstraintCount
        {
            get { return m_ActiveConstraintCount; }
            set { m_ActiveConstraintCount = value; }
        }

        public virtual int initialActiveConstraintCount
        {
            get { return m_InitialActiveConstraintCount; }
            set { m_InitialActiveConstraintCount = value; }
        }

        public abstract Oni.ConstraintType constraintType
        {
            get;
        }

        public IntPtr oniBatch
        {
            get { return batch; }
        }

        public ObiConstraintsBatch(ObiConstraintsBatch source)
        {
            this.source = source;

            if (source != null)
            {
                m_ConstraintCount = source.m_ConstraintCount;
                m_ActiveConstraintCount = source.m_ActiveConstraintCount;
                m_InitialActiveConstraintCount = source.m_InitialActiveConstraintCount;

                m_IDs = new List<int>(source.m_IDs);
                m_IDToIndex = new List<int>(source.m_IDToIndex);
            }
        }

        public abstract IObiConstraintsBatch Clone();
        protected abstract void SwapConstraints(int sourceIndex, int destIndex);
        public abstract void GetParticlesInvolved(int index, List<int> particles);

        protected virtual void CopyConstraint(ObiConstraintsBatch batch, int constraintIndex) { }
        protected virtual void OnAddToSolver(IObiConstraints constraints) { }
        protected virtual void OnRemoveFromSolver(IObiConstraints constraints) { }

        private void InnerSwapConstraints(int sourceIndex, int destIndex)
        {
            m_IDToIndex[m_IDs[sourceIndex]] = destIndex;
            m_IDToIndex[m_IDs[destIndex]] = sourceIndex;
            m_IDs.Swap(sourceIndex, destIndex);
            SwapConstraints(sourceIndex, destIndex);
        }

        /**
         * Registers a new constraint. Call this before adding a new contraint to the batch, so that the constraint is given an ID 
         * and the amount of constraints increased.
         */
        protected void RegisterConstraint()
        {
            m_IDs.Add(m_ConstraintCount);
            m_IDToIndex.Add(m_ConstraintCount);
            m_ConstraintCount++;
        }

        public virtual void Clear()
        {
            m_ConstraintCount = 0;
            m_ActiveConstraintCount = 0;
            m_IDs.Clear();
            m_IDToIndex.Clear();
        }

        /**
         * Given the id of a constraint, return its index in the constraint data arrays. Will return -1 if the constraint does not exist.
         */
        public int GetConstraintIndex(int constraintId)
        {
            if (constraintId < 0 || constraintId >= constraintCount)
                return -1;
            return m_IDToIndex[constraintId];
        }

        public bool IsConstraintActive(int index)
        {
            return index < m_ActiveConstraintCount;
        }

        public bool ActivateConstraint(int constraintIndex)
        {
            if (constraintIndex < m_ActiveConstraintCount)
                return false;

            InnerSwapConstraints(constraintIndex, m_ActiveConstraintCount);
            m_ActiveConstraintCount++;
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);

            return true;
        }

        public bool DeactivateConstraint(int constraintIndex)
        {
            if (constraintIndex >= m_ActiveConstraintCount)
                return false;

            m_ActiveConstraintCount--;
            InnerSwapConstraints(constraintIndex, m_ActiveConstraintCount);
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);

            return true;
        }

        public void DeactivateAllConstraints()
        {
            m_ActiveConstraintCount = 0;
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);
        }

        // Moves a constraint to another batch: First, copies it to the new batch. Then, removes it from this one.
        public void MoveConstraintToBatch(int constraintIndex,ObiConstraintsBatch destBatch)
        {
            destBatch.CopyConstraint(this,constraintIndex);
            RemoveConstraint(constraintIndex);
        }

        // Swaps the constraint with the last one and reduces the amount of constraints by one.
        public void RemoveConstraint(int constraintIndex)
        {
            SwapConstraints(constraintIndex, constraintCount - 1);
            m_IDs.RemoveAt(constraintCount - 1);
            m_IDToIndex.RemoveAt(constraintCount - 1);

            m_ConstraintCount--;
            m_ActiveConstraintCount = Mathf.Min(m_ActiveConstraintCount, m_ConstraintCount);

            Oni.SetConstraintCount(batch, m_ConstraintCount);
            Oni.SetActiveConstraints(batch, m_ActiveConstraintCount);
        }

        public void ParticlesSwapped(int index, int newIndex)
        {
            for (int i = 0; i < particleIndices.count; ++i)
            {
                if (particleIndices[i] == newIndex)
                    particleIndices[i] = index;
                else if (particleIndices[i] == index)
                    particleIndices[i] = newIndex;
            }
        }

        public void AddToSolver(IObiConstraints constraints)
        {
            // create a constraint batch:
            batch = Oni.CreateBatch((int)constraintType);
            Oni.AddBatch(constraints.GetActor().solver.OniSolver, batch);

            OnAddToSolver(constraints);
        }

        public void RemoveFromSolver(IObiConstraints constraints)
        {
            OnRemoveFromSolver(constraints);

            // remove the constraint batch from the solver 
            // (no need to destroy it as its destruction is managed by the solver)
            Oni.RemoveBatch(constraints.GetActor().solver.OniSolver, batch);

            // important: set the batch pointer to null, as it could be destroyed by the solver.
            batch = IntPtr.Zero;
        }

        public void SetEnabled(bool enabled)
        {
            Oni.EnableBatch(batch, enabled);
        }

    }
}
