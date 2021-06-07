using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Obi
{
    /**
     * ObiStitcher will create stitch constraints between 2 actors. All actors must be assigned to the same solver.
     * - Add the constraint batch to the solver once all actors have been added.
     * - If any of the actors is removed from the solver, remove the stitcher too.
     * - Stitch constraints can keep n particles together, respecting their masses.
     * - In edit mode, select the actors to be stitched and then select groups of particles and click "stitch". Or, create stitches by closeness.
     */
    [ExecuteInEditMode]
    //[AddComponentMenu("Physics/Obi/Obi Stitcher")]
    public class ObiStitcher : MonoBehaviour
    {
        [Serializable]
        public class Stitch
        {
            public int particleIndex1;
            public int particleIndex2;

            public Stitch(int particleIndex1, int particleIndex2)
            {
                this.particleIndex1 = particleIndex1;
                this.particleIndex2 = particleIndex2;
            }
        }

        [SerializeField] [HideInInspector] private List<Stitch> stitches = new List<Stitch>();

        [SerializeField] [HideInInspector] private ObiActor actor1 = null;          /**< one of the actors used by the stitcher.*/
        [SerializeField] [HideInInspector] private ObiActor actor2 = null;           /**< the second actor used by the stitcher.*/

        [HideInInspector] public ObiNativeIntList particleIndices = new ObiNativeIntList();
        [HideInInspector] public ObiNativeFloatList stiffnesses = new ObiNativeFloatList();

        private IntPtr batch;
        private bool inSolver = false;

        public ObiActor Actor1
        {
            set
            {
                if (actor1 != value)
                {
                    if (actor1 != null)
                    {
                        actor1.OnBlueprintLoaded -= Actor_OnBlueprintLoaded;
                        actor1.OnBlueprintUnloaded -= Actor_OnBlueprintUnloaded;

                        if (actor1.solver != null)
                            Actor_OnBlueprintUnloaded(actor1, actor1.blueprint);

                    }

                    actor1 = value;

                    if (actor1 != null)
                    {
                        actor1.OnBlueprintLoaded += Actor_OnBlueprintLoaded;
                        actor1.OnBlueprintUnloaded += Actor_OnBlueprintUnloaded;

                        if (actor1.solver != null)
                            Actor_OnBlueprintLoaded(actor1, actor1.blueprint);

                    }
                }
            }
            get { return actor1; }
        }

        public ObiActor Actor2
        {
            set
            {
                if (actor2 != value)
                {
                    if (actor2 != null)
                    {
                        actor2.OnBlueprintLoaded -= Actor_OnBlueprintLoaded;
                        actor2.OnBlueprintUnloaded -= Actor_OnBlueprintUnloaded;

                        if (actor2.solver != null)
                            Actor_OnBlueprintUnloaded(actor2, actor2.blueprint);

                    }

                    actor2 = value;

                    if (actor2 != null)
                    {
                        actor2.OnBlueprintLoaded += Actor_OnBlueprintLoaded;
                        actor2.OnBlueprintUnloaded += Actor_OnBlueprintUnloaded;

                        if (actor2.solver != null)
                            Actor_OnBlueprintLoaded(actor2, actor2.blueprint);

                    }
                }
            }
            get { return actor2; }
        }

        public int StitchCount
        {
            get { return stitches.Count; }
        }

        public IEnumerable<Stitch> Stitches
        {
            get { return stitches.AsReadOnly(); }
        }

        public void OnEnable()
        {

            if (actor1 != null)
            {
                actor1.OnBlueprintLoaded += Actor_OnBlueprintLoaded;
                actor1.OnBlueprintUnloaded += Actor_OnBlueprintUnloaded;
            }
            if (actor2 != null)
            {
                actor2.OnBlueprintLoaded += Actor_OnBlueprintLoaded;
                actor2.OnBlueprintUnloaded += Actor_OnBlueprintUnloaded;
            }

            if (actor1 != null && actor2 != null)
                Oni.EnableBatch(batch, true);
        }

        public void OnDisable()
        {
            Oni.EnableBatch(batch, false);
        }

        /**
         * Adds a new stitch to the stitcher. Note that unlike calling Clear(), AddStitch does not automatically perform a
         * PushDataToSolver(). You should manually call it once you're done adding multiple stitches.
         * @param index of a particle in the first actor. 
         * @param index of a particle in the second actor.
         * @return constrant index, that can be used to remove it with a call to RemoveStitch.
         */
        public int AddStitch(int particle1, int particle2)
        {
            stitches.Add(new Stitch(particle1, particle2));
            return stitches.Count - 1;
        }

        /**
         * Removes. Note that unlike calling Clear(), AddStitch does not automatically perform a
         * PushDataToSolver(). You should manually call it once you're done adding multiple stitches.
         * @param constraint index.
         */
        public void RemoveStitch(int index)
        {
            if (index >= 0 && index < stitches.Count)
                stitches.RemoveAt(index);
        }

        public void Clear()
        {
            stitches.Clear();
            PushDataToSolver();
        }

        void Actor_OnBlueprintUnloaded(ObiActor actor, ObiActorBlueprint blueprint)
        {
            // when any actor is removed from solver, remove stitches.
            this.RemoveFromSolver(null);
        }

        void Actor_OnBlueprintLoaded(ObiActor actor, ObiActorBlueprint blueprint)
        {
            // when both actors are in the same solver, add stitches.
            if (actor1 != null && actor2 != null && actor1.isLoaded && actor2.isLoaded)
            {

                if (actor1.solver != actor2.solver)
                {
                    Debug.LogError("ObiStitcher cannot handle actors in different solvers.");
                    return;
                }

                AddToSolver();
            }
        }

        private void AddToSolver()
        {

            // create a constraint batch:
            batch = Oni.CreateBatch((int)Oni.ConstraintType.Stitch);
            Oni.AddBatch(actor1.solver.OniSolver, batch);

            inSolver = true;

            // push current data to solver:
            PushDataToSolver();

            // enable/disable the batch:
            if (isActiveAndEnabled)
                OnEnable();
            else
                OnDisable();

        }

        private void RemoveFromSolver(object info)
        {

            // remove the constraint batch from the solver 
            // (no need to destroy it as its destruction is managed by the solver)
            Oni.RemoveBatch(actor1.solver.OniSolver, batch);

            // important: set the batch pointer to null, as it could be destroyed by the solver.
            batch = IntPtr.Zero;

            inSolver = false;
        }

        public void PushDataToSolver()
        {

            if (!inSolver)
                return;

            // set solver constraint data:
            particleIndices.ResizeUninitialized(stitches.Count * 2);
            stiffnesses.ResizeUninitialized(stitches.Count);

            for (int i = 0; i < stitches.Count; i++)
            {
                particleIndices[i * 2] = actor1.solverIndices[stitches[i].particleIndex1];
                particleIndices[i * 2 + 1] = actor2.solverIndices[stitches[i].particleIndex2];
                stiffnesses[i] = 0;
            }

            Oni.SetStitchConstraints(batch, particleIndices.GetIntPtr(), stiffnesses.GetIntPtr(), stitches.Count);
            Oni.SetActiveConstraints(batch, stitches.Count);

        }

    }
}

