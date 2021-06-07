using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    [AddComponentMenu("Physics/Obi/Obi Particle Attachment", 820)]
    [RequireComponent(typeof(ObiActor))]
    public class ObiParticleAttachment : MonoBehaviour
    {
        public enum AttachmentType
        {
            Static,
            Dynamic
        }

        [SerializeField] [HideInInspector] private ObiActor m_Actor;

        [SerializeField] [HideInInspector] private Transform m_Target;
        [SerializeField] [HideInInspector] private ObiParticleGroup m_ParticleGroup;
        [SerializeField] [HideInInspector] private AttachmentType m_AttachmentType = AttachmentType.Static;
        [SerializeField] [HideInInspector] private bool m_ConstrainOrientation = false;
        [SerializeField] [HideInInspector] private float m_Compliance = 0;

        [Delayed]
        [SerializeField] [HideInInspector] private float m_BreakThreshold = float.PositiveInfinity;

        [SerializeField] [HideInInspector] private int[] m_SolverIndices;
        [SerializeField] [HideInInspector] private Vector3[] m_PositionOffsets;
        [SerializeField] [HideInInspector] private Quaternion[] m_OrientationOffsets;

        private ObiPinConstraintsBatch pinBatch;

        public ObiActor actor
        {
            get { return m_Actor; }
        }

        public Transform target
        {
            get { return m_Target; }
            set
            {
                if (value != m_Target)
                {
                    Disable(m_AttachmentType);
                    m_Target = value;
                    Bind();
                    UpdateEnabledState();
                }
            }
        }

        public ObiParticleGroup particleGroup
        {
            get
            {
                return m_ParticleGroup;
            }

            set
            {
                if (value != m_ParticleGroup)
                {
                    Disable(m_AttachmentType);
                    m_ParticleGroup = value;
                    Bind();
                    UpdateEnabledState();
                }
            }
        }

        public bool isBound
        {
            get { return m_Target != null && m_SolverIndices != null && m_PositionOffsets != null; }
        }

        public AttachmentType attachmentType
        {
            get { return m_AttachmentType; }
            set
            {
                if (value != m_AttachmentType)
                {
                    Disable(m_AttachmentType);
                    m_AttachmentType = value;
                    UpdateEnabledState();
                }
            }
        }

        public bool constrainOrientation
        {
            get{return m_ConstrainOrientation;}
            set{
                if (value != m_ConstrainOrientation)
                {
                    Disable(m_AttachmentType);
                    m_ConstrainOrientation = value;
                    UpdateEnabledState();
                }
            }
        }

        public float compliance
        {
            get { return m_Compliance; }
            set {
                if (!Mathf.Approximately(value, m_Compliance))
                {
                    m_Compliance = value;
                    if (m_AttachmentType == AttachmentType.Dynamic && pinBatch != null)
                    {
                        for (int i = 0; i < m_SolverIndices.Length; ++i)
                            pinBatch.stiffnesses[i*2] = m_Compliance;
                    }
                }
            }
        }

        public float breakThreshold
        {
            get { return m_BreakThreshold; }
            set
            {
                if (!Mathf.Approximately(value, m_BreakThreshold))
                {
                    m_BreakThreshold = value;
                    if (m_AttachmentType == AttachmentType.Dynamic && pinBatch != null)
                    {
                        for (int i = 0; i < m_SolverIndices.Length; ++i)
                            pinBatch.breakThresholds[i] = m_BreakThreshold;
                    }
                }
            }
        }

        private void Awake()
        {
            m_Actor = GetComponent<ObiActor>();
            m_Actor.OnBlueprintLoaded += Actor_OnBlueprintLoaded;
            m_Actor.OnSubstep += Actor_OnSolverStep;

            if (m_Actor.solver != null)
                Actor_OnBlueprintLoaded(m_Actor, m_Actor.blueprint);
        }

        private void OnDestroy()
        {
            m_Actor.OnBlueprintLoaded -= Actor_OnBlueprintLoaded;
            m_Actor.OnSubstep -= Actor_OnSolverStep;
        }

        private void OnEnable()
        {
            Enable(m_AttachmentType);
        }

        private void OnDisable()
        {
            Disable(m_AttachmentType);
        }

		private void OnValidate()
		{
            m_Actor = GetComponent<ObiActor>();
            Disable(AttachmentType.Static);
            Disable(AttachmentType.Dynamic);
            UpdateEnabledState();
            UpdateAttachment();
		}

		private bool ActorUsesOrientedParticles()
        {
            return GetComponent<ObiActor>().usesOrientedParticles;
        }

        void Actor_OnBlueprintLoaded(ObiActor act, ObiActorBlueprint blueprint)
        {
            Bind(); 
            UpdateEnabledState();
            UpdateAttachment();
        }

        void Actor_OnSolverStep(ObiActor act, float stepTime)
        {
            UpdateAttachment();
        }

        private void Bind()
        {
            var group = particleGroup;

            if (group != null && m_Actor.solver != null)
            {
                Matrix4x4 bindMatrix = m_Target != null ? m_Target.worldToLocalMatrix * m_Actor.solver.transform.localToWorldMatrix : Matrix4x4.identity;

                m_SolverIndices = new int[group.Count];
                m_PositionOffsets = new Vector3[group.Count];
                m_OrientationOffsets = new Quaternion[group.Count];

                var blueprint = m_Actor.blueprint;

                for (int i = 0; i < group.Count; ++i)
                {
                    int particleIndex = group.particleIndices[i];
                    if (particleIndex < m_Actor.solverIndices.Length)
                    {
                        m_SolverIndices[i] = m_Actor.solverIndices[particleIndex];
                        m_PositionOffsets[i] = bindMatrix.MultiplyPoint3x4(m_Actor.solver.positions[m_SolverIndices[i]]);
                    }
                    else
                    {
                        Debug.LogError("The particle group \'"+ group.name + "\' references a particle that does not exist in the actor \'"+ m_Actor.name +"\'.");
                        m_SolverIndices = null;
                        m_PositionOffsets = null;
                        m_OrientationOffsets = null;
                        return;
                    }
                }

                if (m_Actor.usesOrientedParticles)
                {
                    Quaternion bindOrientation = bindMatrix.rotation;

                    for (int i = 0; i < group.Count; ++i)
                    {
                        m_OrientationOffsets[i] = bindOrientation * m_Actor.solver.orientations[m_SolverIndices[i]];
                    }
                }
            }
            else
            {
                m_SolverIndices = null;
                m_PositionOffsets = null;
                m_OrientationOffsets = null;
            }
                
        }

        private void UpdateEnabledState()
        {
            if (enabled)
                Enable(m_AttachmentType);
            else
                Disable(m_AttachmentType);
        }

        private void Enable(AttachmentType type)
        {

            var solver = m_Actor.solver;
            var blueprint = m_Actor.blueprint;

            if (isBound && blueprint != null && m_Actor.solver != null)
            {
                switch (type)
                {
                    case AttachmentType.Dynamic:

                        var pins = m_Actor.GetConstraintsByType(Oni.ConstraintType.Pin) as ObiConstraints<ObiPinConstraintsBatch>;
                        ObiColliderBase attachedCollider = m_Target.GetComponent<ObiColliderBase>();

                        if (pins != null && attachedCollider != null)
                        {
                            // create a new data batch with all our pin constraints:
                            pinBatch = new ObiPinConstraintsBatch();
                            for (int i = 0; i < m_PositionOffsets.Length; ++i)
                            {
                                pinBatch.AddConstraint(0, attachedCollider, m_PositionOffsets[i], m_OrientationOffsets[i]);
                                pinBatch.activeConstraintCount++;
                            }

                            // add the batch to the solver:
                            pins.AddBatch(pinBatch);
                            pinBatch.AddToSolver(pins);

                            // override the pin indices with the ones we got at bind time:
                            for (int i = 0; i < m_SolverIndices.Length; ++i)
                            {
                                pinBatch.particleIndices[i] = m_SolverIndices[i];
                                pinBatch.stiffnesses[i * 2] = m_Compliance;
                                pinBatch.stiffnesses[i * 2 + 1] = constrainOrientation?0:10000;
                                pinBatch.breakThresholds[i] = m_BreakThreshold;
                            }

                            // enable the batch:
                            pinBatch.SetEnabled(true);
                        }

                        break;

                    case AttachmentType.Static:

                        for (int i = 0; i < m_SolverIndices.Length; ++i)
                            solver.invMasses[m_SolverIndices[i]] = 0;

                        if (m_Actor.usesOrientedParticles && m_ConstrainOrientation)
                        {
                            for (int i = 0; i < m_SolverIndices.Length; ++i)
                                solver.invRotationalMasses[m_SolverIndices[i]] = 0;
                        }

                        m_Actor.UpdateParticleProperties();

                        break;

                }
            }

        }

        private void Disable(AttachmentType type)
        {
            var solver = m_Actor.solver;
            var blueprint = m_Actor.blueprint;

            if (isBound && blueprint != null && m_Actor.solver != null)
            {
                switch (m_AttachmentType)
                {
                    case AttachmentType.Dynamic:

                        var pins = m_Actor.GetConstraintsByType(Oni.ConstraintType.Pin) as ObiConstraints<ObiPinConstraintsBatch>;
                        if (pins != null && pinBatch != null)
                        {
                            pinBatch.SetEnabled(false);
                            pinBatch.RemoveFromSolver(pins);
                            pins.RemoveBatch(pinBatch);
                        }

                        break;

                    case AttachmentType.Static:

                        for (int i = 0; i < m_SolverIndices.Length; ++i)
                            solver.invMasses[m_SolverIndices[i]] = blueprint.invMasses[i];

                        if (m_Actor.usesOrientedParticles)
                        {
                            for (int i = 0; i < m_SolverIndices.Length; ++i)
                                solver.invRotationalMasses[m_SolverIndices[i]] = blueprint.invRotationalMasses[i];
                        }

                        m_Actor.UpdateParticleProperties();

                        break;

                }
            }
        }

        private void UpdateAttachment()
        {

            if (!enabled)
                return;

            var solver = m_Actor.solver;
            var blueprint = m_Actor.blueprint;

            if (isBound && blueprint != null && m_Actor.solver != null)
            {
                switch (m_AttachmentType)
                {
                    case AttachmentType.Dynamic:

                        var pins = m_Actor.GetConstraintsByType(Oni.ConstraintType.Pin) as ObiConstraints<ObiPinConstraintsBatch>;
                        if (pins != null && pinBatch != null)
                        {
                            pinBatch.BreakConstraints();
                        }

                        break;

                    case AttachmentType.Static:

                        // Build the attachment matrix:
                        Matrix4x4 attachmentMatrix = solver.transform.worldToLocalMatrix * m_Target.localToWorldMatrix;

                        // Fix all particles in the group and update their position:
                        for (int i = 0; i <m_SolverIndices.Length; ++i)
                        {
                            int solverIndex = m_SolverIndices[i];
                            solver.invMasses[solverIndex] = 0;
                            solver.velocities[solverIndex] = Vector3.zero;

                            // Note: skip assignment to startPositions if you want attached particles to be interpolated too.
                            solver.startPositions[solverIndex] = solver.positions[solverIndex] = attachmentMatrix.MultiplyPoint3x4(m_PositionOffsets[i]);
                        }

                        if (m_Actor.usesOrientedParticles && m_ConstrainOrientation)
                        {
                            Quaternion attachmentRotation = attachmentMatrix.rotation;

                            for (int i = 0; i < m_SolverIndices.Length; ++i)
                            {
                                int solverIndex = m_SolverIndices[i];
                                solver.invRotationalMasses[solverIndex] = 0;
                                solver.angularVelocities[solverIndex] = Vector3.zero;

                                // Note: skip assignment to startPositions if you want attached particles to be interpolated too.
                                solver.startOrientations[solverIndex] = solver.orientations[solverIndex] = attachmentRotation * m_OrientationOffsets[i];
                            }
                        }

                        break;
                }
            }
        }
    }
}
