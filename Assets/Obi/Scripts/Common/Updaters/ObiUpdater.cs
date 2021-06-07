using UnityEngine;
using Unity.Profiling;
using System.Collections;
using System.Collections.Generic;
using System;

namespace Obi
{
    /// <summary>
    /// Base class for updating multiple solvers in parallel.
    /// Derive from this class to write your onw updater. This grants you precise control over execution order,
    /// as you can choose to update solvers at any point during Unity's update cycle.
    /// </summary>
    [ExecuteInEditMode]
    public abstract class ObiUpdater : MonoBehaviour
    {
        static ProfilerMarker m_BeginStepPerfMarker = new ProfilerMarker("BeginStep");
        static ProfilerMarker m_SubstepPerfMarker = new ProfilerMarker("Substep");
        static ProfilerMarker m_EndStepPerfMarker = new ProfilerMarker("EndStep");
        static ProfilerMarker m_InterpolatePerfMarker = new ProfilerMarker("Interpolate");

        /// <summary>
        /// List of solvers updated by this updater.
        /// </summary>
        public List<ObiSolver> solvers = new List<ObiSolver>();

        /// <summary>
        /// Prepares all solvers to begin simulating a new physics step. This involves
        /// caching some particle data for interpolation, performing collision detection, among other things.
        /// </summary>
        /// <param name="stepDeltaTime"> Duration (in seconds) of the next step.</param>
        protected void BeginStep(float stepDeltaTime)
        {
            using (m_BeginStepPerfMarker.Auto())
            {
                // Update colliders right before collision detection:
                ObiColliderBase.UpdateColliders();

                IntPtr beginStep = Oni.CreateEmpty();

                // Generate a task for each solver's collision detection, and combine them all together:
                foreach (ObiSolver solver in solvers)
                    if (solver != null)
                        Oni.AddChild(beginStep, solver.BeginStep(stepDeltaTime));

                // Schedule the task for execution:
                Oni.Schedule(beginStep);

                // Wait the task to complete:
                Oni.Complete(beginStep);

                // Reset transform change flags:
                ObiColliderBase.ResetColliderTransforms();
            }
        }


        /// <summary>
        /// Advances the simulation a given amount of time. Note that once BeginStep has been called,
        /// Substep can be called multiple times. 
        /// </summary>
        /// <param name="stepDeltaTime"> Duration (in seconds) of the substep.</param>
        protected void Substep(float substepDeltaTime)
        {
            using (m_SubstepPerfMarker.Auto())
            {
                // Necessary when using multiple substeps:
                ObiColliderBase.UpdateColliders();

                // Grab rigidbody info:
                ObiRigidbodyBase.UpdateAllRigidbodies();

                IntPtr stepSimulation = Oni.CreateEmpty();

                // Generate a task for each solver's step, and combine them all together:
                foreach (ObiSolver solver in solvers)
                    if (solver != null)
                        Oni.AddChild(stepSimulation, solver.Substep(substepDeltaTime));

                // Schedule the task for execution:
                Oni.Schedule(stepSimulation);

                // Wait the task to complete:
                Oni.Complete(stepSimulation);

                // Update rigidbody velocities:
                ObiRigidbodyBase.UpdateAllVelocities();

                // Reset transform change flags:
                ObiColliderBase.ResetColliderTransforms();
            }
        }

        /// <summary>
        /// Wraps up the current simulation step. This will trigger contact callbacks.
        /// </summary>
        protected void EndStep()
        {
            using (m_EndStepPerfMarker.Auto())
            {
                // End step: Invokes collision callbacks and notifies actors that the solver step has ended.
                foreach (ObiSolver solver in solvers)
                    if (solver != null)
                        solver.EndStep();
            }
        }

        /// <summary>
        /// Interpolates the previous and current physics states. Should be called right before rendering the current frame.
        /// </summary>
        /// <param name="stepDeltaTime"> Duration (in seconds) of the last step taken.</param>
        /// <param name="stepDeltaTime"> Amount of accumulated (not yet simulated) time.</param>
        protected void Interpolate(float stepDeltaTime, float accumulatedTime)
        {
            using (m_InterpolatePerfMarker.Auto())
            {
                foreach (ObiSolver solver in solvers)
                    if (solver != null)
                        solver.Interpolate(stepDeltaTime, accumulatedTime);
            }
        }
	}
}