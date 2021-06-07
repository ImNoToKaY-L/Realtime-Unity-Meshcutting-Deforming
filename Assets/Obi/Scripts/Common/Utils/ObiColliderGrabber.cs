using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Obi;

/**
 * Sample component that makes a collider "grab" any particle it touches (regardless of which Actor it belongs to).
 */ 
[RequireComponent(typeof(ObiCollider))]
public class ObiColliderGrabber : MonoBehaviour
{

    public ObiSolver solver;

    /**
     * Helper class that stores the index of a particle in the solver, its position in the grabber's local space, and its inverse mass previous to being grabbed.
     * This makes it easy to tell if a particle has been grabbed, update its position while grabbing, and restore its mass after being released.
     */
    private class GrabbedParticle : IEqualityComparer<GrabbedParticle>
    {
        public int index;
        public float invMass;
        public Vector3 localPosition;

        public GrabbedParticle(int index, float invMass)
        {
            this.index = index;
            this.invMass = invMass;
        }

        public bool Equals(GrabbedParticle x, GrabbedParticle y)
        {
            return x.index == y.index;
        }

        public int GetHashCode(GrabbedParticle obj)
        {
            return index;
        }
    }

    private Obi.ObiSolver.ObiCollisionEventArgs collisionEvent;                                  /**< store the current collision event*/
    private ObiCollider localCollider;                                                           /**< the collider on this gameObject.*/
    private HashSet<GrabbedParticle> grabbedParticles = new HashSet<GrabbedParticle>();          /**< set to store all currently grabbed particles.*/
    private Matrix4x4 grabber2Solver;
    private Matrix4x4 solver2Grabber;

    private void Awake()
    {
        localCollider = GetComponent<ObiCollider>();
    }

    private void OnEnable()
    {
        solver.OnCollision += Solver_OnCollision;
    }

    private void OnDisable()
    {
        solver.OnCollision -= Solver_OnCollision;
    }

    private void Solver_OnCollision(object sender, Obi.ObiSolver.ObiCollisionEventArgs e)
    {
        // Calculate transform matrix from grabber to world space (Note: if using local space simulation, postmultiply with solver.transform.localToWorldMatrix)
        solver2Grabber = transform.worldToLocalMatrix * solver.transform.localToWorldMatrix;

        // and its inverse:
        grabber2Solver = solver2Grabber.inverse;

        collisionEvent = e;
    }

    private void UpdateRestShapeMatching()
    {
    }

    /**
     * Creates and stores a GrabbedParticle from the particle at the given index.
     * Returns true if we sucessfully grabbed a particle, false if the particle was already grabbed.
     */
    private bool GrabParticle(int index)
    {
        GrabbedParticle p = new GrabbedParticle(index, solver.invMasses[index]);

        // in case this particle has not been grabbed yet:
        if (!grabbedParticles.Contains(p))
        {
            // record the particle's position relative to the grabber, and store it.
            p.localPosition = solver2Grabber.MultiplyPoint3x4(solver.positions[index]);
            grabbedParticles.Add(p);

            // Set inv mass and velocity to zero:
            solver.invMasses[index] = 0;
            solver.velocities[index] = Vector4.zero;

            return true;
        }

        return false;
    }

    /**
     * Grabs all particles currently touching the grabber.
     */
    public void Grab()
    {

        foreach (Oni.Contact contact in collisionEvent.contacts)
        {
            // this one is an actual collision:
            if (contact.distance < 0.01f)
            {
                Component contactCollider;
                if (ObiCollider.idToCollider.TryGetValue(contact.other, out contactCollider))
                {
                    // if the current contact references our collider, proceed to grab the particle.
                    if (contactCollider == localCollider.SourceCollider)
                    {
                        // try to grab the particle, if not already grabbed.
                        GrabParticle(contact.particle);
                    }
                }
            }
        }

        UpdateRestShapeMatching();
    }

    /**
     * Releases all currently grabbed particles. This boils down to simply resetting their invMass.
     */
    public void Release()
    {
        // Restore the inverse mass of all grabbed particles, so dynamics affect them.
        foreach (GrabbedParticle p in grabbedParticles)
            solver.invMasses[p.index] = p.invMass;

        grabbedParticles.Clear();
    }

    /**
     * Updates the position of the grabbed particles.
     */
    private void FixedUpdate()
    {
        foreach (GrabbedParticle p in grabbedParticles)
            solver.positions[p.index] = grabber2Solver.MultiplyPoint3x4(p.localPosition);
    }

    /**
     * Just for convenience. Ideally, this should not be part of this component.
     * You're expected to control the Grabber from outside.
     */
    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            Grab();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            Release();
        }
            
    }
}
