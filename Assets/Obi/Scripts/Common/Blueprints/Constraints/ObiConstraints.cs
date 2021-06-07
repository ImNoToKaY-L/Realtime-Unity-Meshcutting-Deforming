using UnityEngine;
using System.Collections.Generic;
using System;

namespace Obi
{
    public interface IObiConstraints
    {
        ObiActor GetActor();
        Oni.ConstraintType? GetConstraintType();

        IList<IObiConstraintsBatch> GetBatchInterfaces();
        bool AddBatch(IObiConstraintsBatch batch);
        bool RemoveBatch(IObiConstraintsBatch batch);
        int GetBatchCount();
        void Clear();

        bool AddToSolver();
        bool RemoveFromSolver();
        void SetEnabled(bool enabled);

        int GetConstraintCount();
        int GetActiveConstraintCount();
        void DeactivateAllConstraints();

        IObiConstraints Clone(ObiActor actor);
    }

    [Serializable]
    public class ObiConstraints<T> : IObiConstraints where T : class, IObiConstraintsBatch
    {
        protected ObiActor actor;
        [NonSerialized] protected ObiConstraints<T> source;

        protected bool inSolver;
        [HideInInspector] public List<T> batches = new List<T>();

        public ObiConstraints(ObiActor actor = null, ObiConstraints<T> source = null)
        {
            this.actor = actor;
            this.source = source;

            if (source != null)
            {
                foreach (T batch in source.batches)
                    AddBatch(batch.Clone());
            }
        }

        public IObiConstraints Clone(ObiActor actor)
        {
            return new ObiConstraints<T>(actor,this);
        }

        public ObiActor GetActor()
        {
            return actor;
        }

        public IList<IObiConstraintsBatch> GetBatchInterfaces()
        {
            return batches.CastList<T, IObiConstraintsBatch>();
        }

        public int GetBatchCount()
        {
            return batches.Count;
        }

        public int GetConstraintCount()
        {
            int count = 0;
            foreach (T batch in batches)
                count += batch.constraintCount;
            return count;
        }

        public int GetActiveConstraintCount()
        {
            int count = 0;
            foreach (T batch in batches)
                count += batch.activeConstraintCount;
            return count;
        }

        public void DeactivateAllConstraints()
        {
            foreach (T batch in batches)
                batch.DeactivateAllConstraints();
        }

        public T GetFirstBatch()
        {
            return batches.Count > 0 ? batches[0] : null;
        }

        public Oni.ConstraintType? GetConstraintType()
        {
            if (batches.Count > 0)
                return batches[0].constraintType;
            else return null;
        }

        public void Clear()
        {
            RemoveFromSolver();
            batches.Clear();
        }

        public bool AddBatch(IObiConstraintsBatch batch)
        {
            T dataBatch = batch as T;
            if (dataBatch != null)
            {
                batches.Add(dataBatch);
                return true;
            }
            return false;
        }

        public bool RemoveBatch(IObiConstraintsBatch batch)
        {
            return batches.Remove(batch as T);
        }

        public bool AddToSolver()
        {

            if (inSolver || actor == null || actor.solver == null)
                return false;

            inSolver = true;

            foreach (T batch in batches)
                batch.AddToSolver(this);

            GenerateBatchDependencies();

            // enable/disable all batches:
            SetEnabled(actor.isActiveAndEnabled);

            return true;

        }

        public bool RemoveFromSolver()
        {

            if (!inSolver || actor == null || actor.solver == null)
                return false;

            foreach (T batch in batches)
                batch.RemoveFromSolver(this);

            inSolver = false;

            return true;

        }

        private void GenerateBatchDependencies()
        {
            if (inSolver)
            {
                T prevBatch = null;
                foreach (T batch in batches)
                {
                    // each batch depends on the previous one:
                    if (prevBatch != null)
                        Oni.SetDependency(batch.oniBatch, prevBatch.oniBatch);
                    prevBatch = batch;
                }
            }
        }

        public void SetEnabled(bool enabled)
        {
            foreach (T batch in batches)
                batch.SetEnabled(enabled);
        }
    }
}
