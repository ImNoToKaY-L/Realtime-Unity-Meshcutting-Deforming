using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using Obi;

/**
 * Interface for the Oni particle physics library.
 */
public static class Oni {

	public const int ConstraintTypeCount = 17;

	public enum ConstraintType
    {
        Tether = 0,
        Volume = 1,
		Chain = 2,
        Bending = 3,
        Distance = 4,
        ShapeMatching = 5,
		BendTwist = 6,
		StretchShear = 7,
        Pin = 8,
        ParticleCollision = 9,
        Density = 10,
        Collision = 11,
        Skin = 12,
        Aerodynamics = 13,
		Stitch = 14,
		ParticleFriction = 15,
		Friction = 16
    };

    [Flags]
	public enum ParticleFlags
    {
		SelfCollide = 1 << 24,
		Fluid = 1 << 25,
		OneSided = 1 << 26
	}

	public enum ShapeType{
		Sphere = 0,
		Box = 1,
		Capsule = 2,
		Heightmap = 3,
		TriangleMesh = 4,
		EdgeMesh = 5,
		SignedDistanceField = 6
	}

	public enum MaterialCombineMode{
		Average = 0,
		Minimium = 1,
		Multiply = 2,
        Maximum = 3
    }

	public enum NormalsUpdate{
		Recalculate = 0,
		Skin = 1
	}

	public enum ProfileMask : uint{
		ThreadIdMask = 0xffff0000,
		TypeMask = 0x000000ff,
		StackLevelMask = 0x0000ff00
	}

	public struct ProfileInfo
	{
		public double start;
		public double end;
		public uint info;
		public int pad;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
		public string name;
	}

	public struct GridCell
	{
		public Vector3 center;
		public Vector3 size;
		public int count;
	}

	[Serializable]
	public struct SolverParameters{

		public enum Interpolation
		{
			None,
			Interpolate,
		};

		public enum Mode
		{
			Mode3D,
			Mode2D,
		};

		[Tooltip("In 2D mode, particles are simulated on the XY plane only. For use in conjunction with Unity's 2D mode.")]
		public Mode mode;

		[Tooltip("Same as Rigidbody.interpolation. Set to INTERPOLATE for cloth that is applied on a main character or closely followed by a camera. NONE for everything else.")]
		public Interpolation interpolation;

		public Vector3 gravity;

		[Tooltip("Percentage of velocity lost per second, between 0% (0) and 100% (1).")]
		[Range(0,1)]
		public float damping; 

		[Tooltip("Percentage of shock propagation applied to particle-particle collisions. Useful for particle stacking.")]
		[Range(0,1)]
		public float shockPropagation; 

		[Tooltip("Max ratio between a particle's longest and shortest axis. Use 1 for isotropic (completely round) particles.")]
		[Range(1,5)]
		public float maxAnisotropy;

        [Tooltip("Maximum depenetration velocity applied to particles that start a frame inside an object. Low values ensure no 'explosive' collision resolution. Should be > 0 unless looking for non-physical effects.")]
        public float maxDepenetration;

		[Tooltip("Kinetic energy below which particle positions arent updated. Energy values are mass-normalized, so all particles in the solver have the same threshold.")]
		public float sleepThreshold; 		              		              

		public SolverParameters(Interpolation interpolation, Vector4 gravity){
			this.mode = Mode.Mode3D;
			this.gravity = gravity;
			this.interpolation = interpolation;
			damping = 0;
			shockPropagation = 0;
			maxAnisotropy = 3;
            maxDepenetration = 5;
            sleepThreshold = 0.0001f;
		}

	}

	[Serializable]
	public struct ConstraintParameters{

		public enum EvaluationOrder
		{
			Sequential,
			Parallel
		};

		[Tooltip("Order in which constraints are evaluated. SEQUENTIAL converges faster but is not very stable. PARALLEL is very stable but converges slowly, requiring more iterations to achieve the same result.")]
		public EvaluationOrder evaluationOrder;								/**< Constraint evaluation order.*/
		
		[Tooltip("Number of relaxation iterations performed by the constraint solver. A low number of iterations will perform better, but be less accurate.")]
		public int iterations;												/**< Amount of solver iterations per step for this constraint group.*/
		
		[Tooltip("Over (or under if < 1) relaxation factor used. At 1, no overrelaxation is performed. At 2, constraints double their relaxation rate. High values reduce stability but improve convergence.")]
		[Range(0.1f,2)]
		public float SORFactor;												/**< Sucessive over-relaxation factor for parallel evaluation order.*/
		
		[Tooltip("Whether this constraint group is solved or not.")]
		[MarshalAs(UnmanagedType.I1)]
		public bool enabled;

		public ConstraintParameters(bool enabled, EvaluationOrder order, int iterations){
			this.enabled = enabled;
			this.iterations = iterations;
			this.evaluationOrder = order;
			this.SORFactor = 1;
		}
		
	}

	// In this particular case, size is forced to 128 bytes to ensure 16 byte memory alignment needed by Oni.
	[StructLayout(LayoutKind.Sequential, Size = 128)]
	public struct Contact{

		public Vector4 point; 		   /**< Speculative point of contact. */
        public Vector4 normal;		   /**< Normal direction. */
		public Vector4 tangent;		   /**< Tangent direction. */
		public Vector4 bitangent;	   /**< Bitangent direction. */

        public float distance;    /** distance between both colliding entities at the beginning of the timestep.*/
        
        public float normalImpulse;
        public float tangentImpulse;
		public float bitangentImpulse;
        public float stickImpulse;
		public float rollingFrictionImpulse;
        
        public int particle; /** particle index*/
        public int other;    /** particle or rigidbody index*/

	}

	public struct BoneWeights
	{
		public int bone0;
		public int bone1;
		public int bone2;
		public int bone3;
		public float weight0;
		public float weight1;
		public float weight2;
		public float weight3;

		public BoneWeights(BoneWeight weight){
			bone0 = weight.boneIndex0;
			bone1 = weight.boneIndex1;
			bone2 = weight.boneIndex2;
			bone3 = weight.boneIndex3;
			weight0 = weight.weight0;
			weight1 = weight.weight1;
			weight2 = weight.weight2;
			weight3 = weight.weight3;
		}
	}

	[Serializable]
	public struct Rigidbody{

		public Quaternion rotation;
        public Quaternion inertiaRotation;
        public Vector3 linearVelocity;
		public Vector3 angularVelocity;
		public Vector3 centerOfMass;
		public Vector3 inertiaTensor;
		public float inverseMass;
		
		public void Set(UnityEngine.Rigidbody source, bool kinematicForParticles){

			bool kinematic = !Application.isPlaying || source.isKinematic || kinematicForParticles;

			rotation = source.rotation;
			linearVelocity = kinematicForParticles ? Vector3.zero : source.velocity;
			angularVelocity = kinematicForParticles ? Vector3.zero : source.angularVelocity;

			// center of mass in unity is affected by local rotation and position, but not scale. We need it expressed in world space:
            centerOfMass = source.transform.position + rotation * source.centerOfMass;

			Vector3 invTensor = new Vector3((source.constraints & RigidbodyConstraints.FreezeRotationX) != 0?0:1/source.inertiaTensor.x,
											(source.constraints & RigidbodyConstraints.FreezeRotationY) != 0?0:1/source.inertiaTensor.y,
											(source.constraints & RigidbodyConstraints.FreezeRotationZ) != 0?0:1/source.inertiaTensor.z);

			// the inertia tensor is a diagonal matrix (Vector3) because it is expressed in the space generated by the principal axes of rotation (inertiaTensorRotation).
			inertiaTensor = kinematic ? Vector3.zero : invTensor;
            inertiaRotation = source.inertiaTensorRotation;
            inverseMass = kinematic ? 0 : 1/source.mass;

		}

		public void Set(UnityEngine.Rigidbody2D source, bool kinematicForParticles){

			bool kinematic = !Application.isPlaying || source.isKinematic || kinematicForParticles;

			rotation = Quaternion.AngleAxis(source.rotation,Vector3.forward);
			linearVelocity = source.velocity;

			// For some weird reason, in 2D angular velocity is measured in *degrees* per second, 
			// instead of radians. Seriously Unity, WTF??
			angularVelocity = new Vector4(0,0,source.angularVelocity * Mathf.Deg2Rad,0);

			// center of mass in unity is affected by local rotation and poistion, but not scale. We need it expressed in world space:
			centerOfMass = source.transform.position + source.transform.rotation * source.centerOfMass;

			inertiaTensor = kinematic ? Vector3.zero : new Vector3(0,0,(source.constraints & RigidbodyConstraints2D.FreezeRotation) != 0?0:1/source.inertia);
            inertiaRotation = Quaternion.identity;
            inverseMass = kinematic ? 0 : 1/source.mass;

		}
	}

	[Serializable]
	public struct RigidbodyVelocities{
		public Vector3 linearVelocity;
		public Vector3 angularVelocity;		
	}
	

	[Serializable]
	public struct Collider{

		public Quaternion rotation;
		public Vector3 translation;
		public Vector3 scale;

		public Vector3 boundsMin;
		public Vector3 boundsMax;

		public int id;
		public float contactOffset;
		public int collisionGroup;

		[MarshalAs(UnmanagedType.I1)]
		public bool trigger;

		public void Set(UnityEngine.Collider source, int phase, float thickness){
			boundsMin = source.bounds.min - Vector3.one*(thickness + source.contactOffset);
			boundsMax = source.bounds.max + Vector3.one*(thickness + source.contactOffset);
			translation = source.transform.position;
			rotation = source.transform.rotation;
			scale = source.transform.lossyScale;
			contactOffset = thickness;
			this.collisionGroup = phase;
			this.trigger = source.isTrigger;
			this.id = source.GetInstanceID();
		}

		public void Set(UnityEngine.Collider2D source, int phase, float thickness){

			boundsMin = source.bounds.min - Vector3.one * (thickness + 0.01f); //allow some room for contacts to be generated before penetration.
			boundsMax = source.bounds.max + Vector3.one * (thickness + 0.01f);
            boundsMax.z = 0;
            boundsMin.z = 0;

			translation = source.transform.position;
            translation.z = 0;

            rotation = source.transform.rotation;
			scale = source.transform.lossyScale;
			contactOffset = thickness;
			this.collisionGroup = phase;
			this.trigger = source.isTrigger;
			this.id = source.GetInstanceID();
		}
	}

	[Serializable]
	public struct Shape{

		public Vector3 center;
		public Vector3 size;

		public IntPtr data;
		public IntPtr indices;

		public int dataCount;
		public int indexCount;
		
		public int resolutionU;
		public int resolutionV;

		[MarshalAs(UnmanagedType.I1)]
		public bool is2D;

        [MarshalAs(UnmanagedType.I1)]
        public bool accurateContacts;

		public void Set(Vector3 center, float radius){ // sphere
			this.center = center;
			this.size = Vector3.one * radius; 
		}

		public void Set(Vector3 center, Vector3 size){ // box
			this.center = center;
			this.size = size; 
		}

		public void Set(Vector3 center, float radius, float height, int direction){ // capsule
			this.center = center;
			this.size = new Vector3(radius,height,direction); 
		}

		public void Set(Vector3 size, int resolutionU, int resolutionV, IntPtr data){ // terrain
			this.size = size; 
			this.resolutionU = resolutionU;
			this.resolutionV = resolutionV;
			this.data = data;
			this.dataCount = resolutionU * resolutionV;
		}

		public void Set(IntPtr data, IntPtr indices, int dataCount, int indicesCount){ // mesh
			this.data = data;
			this.indices = indices;
			this.dataCount = dataCount;
			this.indexCount = indicesCount;
		}
	}

	[Serializable]
	public struct CollisionMaterial{
		public float dynamicFriction;
		public float staticFriction;
		public float rollingFriction;
		public float stickiness;
		public float stickDistance;
		public MaterialCombineMode frictionCombine;
		public MaterialCombineMode stickinessCombine;
		[MarshalAs(UnmanagedType.I1)]
		public bool rollingContacts;
	}

	[Serializable]
	public struct ElastoplasticMaterial{
		public float stiffness;
		public float plasticYield;
		public float plasticCreep;
		public float plasticRecovery;
		public float maxDeformation;

		public ElastoplasticMaterial(float stiffness, float plasticYield, float plasticCreep, float plasticRecovery, float maxDeformation){
			this.stiffness = stiffness;
			this.plasticYield = plasticYield;
			this.plasticCreep = plasticCreep;
			this.plasticRecovery = plasticRecovery;
			this.maxDeformation = maxDeformation;
		}
	}

	[Serializable]
    [StructLayout(LayoutKind.Sequential, Size = 64)] // The last member is an int, so there are 12 bytes left at the end. We must ensure 64 bytes for correct array alignment.
	public struct DFNode{
		public Vector4 distancesA;
		public Vector4 distancesB;
		public Vector4 center;
		public int firstChild;
	}

	[Serializable]
	public struct HalfEdge{
		public int index;	
		public int indexInFace;
		public int face;
		public int nextHalfEdge;
		public int pair;
		public int endVertex;

		public HalfEdge(int index){
			this.index = index;	
			indexInFace = -1;
			face = -1;
			nextHalfEdge = -1;
			pair = -1;
			endVertex = -1;
		}
	}

	[Serializable]
	public struct Vertex{
		public int index;	
		public int halfEdge;
		public Vector3 position;

		public Vertex(Vector3 position, int index, int halfEdge){
			this.index = index;
			this.halfEdge = halfEdge;
			this.position = position;
		}
	}

	[Serializable]
	public struct Face{
		public int index;	
		public int halfEdge;

		public Face(int index){
			this.index = index;
			halfEdge = -1;
		}
	}

	[Serializable]
	public struct MeshInformation{
		public float volume;	
		public float area;
		public int borderEdgeCount;

		[MarshalAs(UnmanagedType.I1)]
		public bool closed;
		[MarshalAs(UnmanagedType.I1)]
		public bool nonManifold;
	}

	public static GCHandle PinMemory(object data){
		return GCHandle.Alloc(data, GCHandleType.Pinned);
	}

	public static void UnpinMemory(GCHandle handle){
		if (handle.IsAllocated)
			handle.Free();
	}

	#if (UNITY_IOS && !UNITY_EDITOR)
		const string LIBNAME = "__Internal";
	#elif ((UNITY_ANDROID || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR)
		const string LIBNAME = "Oni";
	#else
		const string LIBNAME = "libOni";
	#endif

	[DllImport (LIBNAME)] 
	public static extern IntPtr CreateCollider();

	[DllImport (LIBNAME)] 
	public static extern void DestroyCollider(IntPtr collider);

	[DllImport (LIBNAME)] 
	public static extern IntPtr CreateShape(ShapeType shapeType);

	[DllImport (LIBNAME)] 
	public static extern void DestroyShape(IntPtr shape);

	[DllImport (LIBNAME)] 
	public static extern IntPtr CreateRigidbody();

	[DllImport (LIBNAME)] 
	public static extern void DestroyRigidbody(IntPtr rigidbody);

	[DllImport (LIBNAME)] 
	public static extern void UpdateCollider(IntPtr collider, ref Oni.Collider adaptor);

	[DllImport (LIBNAME)] 
	public static extern void UpdateShape(IntPtr shape, ref Oni.Shape adaptor);

	[DllImport (LIBNAME)] 
	public static extern void UpdateRigidbody(IntPtr rigidbody, ref Oni.Rigidbody adaptor);

	[DllImport (LIBNAME)] 
	public static extern void GetRigidbodyVelocity(IntPtr rigidbody, ref RigidbodyVelocities velocities);

	[DllImport (LIBNAME)] 
	public static extern IntPtr CreateDistanceField();

	[DllImport (LIBNAME)] 
	public static extern void DestroyDistanceField(IntPtr df);

	[DllImport (LIBNAME)] 
	public static extern void StartBuildingDistanceField(IntPtr df, float maxError, int maxDepth, Vector3[] vertexPos, int[] triIndices, int numVertices, int numTriangles);

	[DllImport (LIBNAME)] 
	public static extern bool ContinueBuildingDistanceField(IntPtr df);

	[DllImport (LIBNAME)] 
	public static extern float SampleDistanceField(IntPtr df, float x, float y, float z);

	[DllImport (LIBNAME)] 
	public static extern int GetDistanceFieldNodeCount(IntPtr df);

	[DllImport (LIBNAME)] 
	public static extern void GetDistanceFieldNodes(IntPtr df, DFNode[] nodes);

	[DllImport (LIBNAME)] 
	public static extern void SetDistanceFieldNodes(IntPtr df, DFNode[] nodes, int num);

	[DllImport (LIBNAME)] 
	public static extern void SetShapeDistanceField(IntPtr shape, IntPtr distanceField);

	[DllImport (LIBNAME)] 
	public static extern void SetColliderShape(IntPtr collider, IntPtr shape);

	[DllImport (LIBNAME)] 
	public static extern void SetColliderRigidbody(IntPtr collider, IntPtr rigidbody);

	[DllImport (LIBNAME)] 
	public static extern void SetColliderMaterial(IntPtr collider, IntPtr material);

	[DllImport (LIBNAME)] 
	public static extern IntPtr CreateCollisionMaterial();

	[DllImport (LIBNAME)] 
	public static extern void DestroyCollisionMaterial(IntPtr material);

	[DllImport (LIBNAME)] 
	public static extern void UpdateCollisionMaterial(IntPtr material, ref CollisionMaterial adaptor);
        
	[DllImport (LIBNAME)] 
	public static extern IntPtr CreateSolver(int capacity);

	[DllImport (LIBNAME)] 
	public static extern void DestroySolver(IntPtr solver);

    [DllImport(LIBNAME)]
    public static extern void SetCapacity(IntPtr solver, int capacity);

    [DllImport (LIBNAME)] 
	public static extern void InitializeFrame(IntPtr solver,ref Vector4 translation, ref Vector4 scale, ref Quaternion rotation);

	[DllImport (LIBNAME)] 
	public static extern void UpdateFrame(IntPtr solver,ref Vector4 translation, ref Vector4 scale, ref Quaternion rotation, float dt);

	[DllImport (LIBNAME)] 
	public static extern void ApplyFrame(IntPtr solver,float linearVelocityScale, float angularVelocityScale, float linearInertiaScale, float angularInertiaScale, float dt);

	[DllImport (LIBNAME)] 
	public static extern void AddCollider(IntPtr collider);

	[DllImport (LIBNAME)] 
	public static extern void RemoveCollider(IntPtr collider);

	[DllImport (LIBNAME)] 
	public static extern void RecalculateInertiaTensors(IntPtr solver);

    [DllImport(LIBNAME)]
    public static extern void ResetForces(IntPtr solver);

	[DllImport (LIBNAME)] 
	public static extern void GetBounds(IntPtr solver, ref Vector3 min, ref Vector3 max);

	[DllImport (LIBNAME)] 
	public static extern int GetParticleGridSize(IntPtr solver);

	[DllImport (LIBNAME)] 
	public static extern void GetParticleGrid(IntPtr solver, GridCell[] cells);

	[DllImport (LIBNAME)] 
	public static extern void SetSolverParameters(IntPtr solver, ref SolverParameters parameters);

	[DllImport (LIBNAME)] 
	public static extern void GetSolverParameters(IntPtr solver, ref SolverParameters parameters);

	[DllImport (LIBNAME)] 
	public static extern int SetActiveParticles(IntPtr solver, int[] active, int num);

    [DllImport(LIBNAME)]
    public static extern IntPtr CollisionDetection(IntPtr solver, float delta_time);

	[DllImport (LIBNAME)] 
	public static extern IntPtr Step(IntPtr solver, float delta_time);

	[DllImport (LIBNAME)] 
    public static extern void ApplyPositionInterpolation(IntPtr solver, IntPtr draw_positions, IntPtr draw_orientations, float delta_seconds, float unsimulated_time);

	[DllImport (LIBNAME)] 
	public static extern void UpdateSkeletalAnimation(IntPtr solver);

	[DllImport (LIBNAME)] 
	public static extern int GetConstraintCount(IntPtr solver, int type);

	[DllImport (LIBNAME)] 
	public static extern void GetActiveConstraintIndices(IntPtr solver, int[] indices, int num , int type);

	[DllImport (LIBNAME)] 
	public static extern void SetRenderableParticlePositions(IntPtr solver, IntPtr positions);

	[DllImport (LIBNAME)] 
	public static extern void SetParticlePhases(IntPtr solver, IntPtr phases);

	[DllImport (LIBNAME)] 
	public static extern void SetParticlePositions(IntPtr solver, IntPtr positions);

	[DllImport (LIBNAME)] 
	public static extern void SetParticlePreviousPositions(IntPtr solver, IntPtr prevPositions);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleOrientations(IntPtr solver, IntPtr orientations);

	[DllImport (LIBNAME)] 
	public static extern void SetParticlePreviousOrientations(IntPtr solver, IntPtr prevOrientations);

	[DllImport (LIBNAME)] 
	public static extern void SetRenderableParticleOrientations(IntPtr solver, IntPtr orientations);
	
	[DllImport (LIBNAME)] 
	public static extern void SetParticleInverseMasses(IntPtr solver, IntPtr invMasses);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleInverseRotationalMasses(IntPtr solver, IntPtr invRotMasses);

	[DllImport (LIBNAME)] 
	public static extern void SetParticlePrincipalRadii(IntPtr solver, IntPtr principalRadii);
	
	[DllImport (LIBNAME)] 
	public static extern void SetParticleVelocities(IntPtr solver, IntPtr velocities);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleAngularVelocities(IntPtr solver, IntPtr angularVelocities);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleExternalForces(IntPtr solver, IntPtr forces);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleExternalTorques(IntPtr solver, IntPtr torques);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleWinds(IntPtr solver, IntPtr winds);

	[DllImport (LIBNAME)] 
	public static extern void SetParticlePositionDeltas(IntPtr solver, IntPtr deltas);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleOrientationDeltas(IntPtr solver, IntPtr deltas);

	[DllImport (LIBNAME)] 
	public static extern void SetParticlePositionConstraintCounts(IntPtr solver, IntPtr counts);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleOrientationConstraintCounts(IntPtr solver, IntPtr counts);	

	[DllImport (LIBNAME)] 
	public static extern void SetParticleNormals(IntPtr solver, IntPtr normals);	

	[DllImport (LIBNAME)] 
	public static extern void SetParticleInverseInertiaTensors(IntPtr solver, IntPtr tensors);	


	[DllImport (LIBNAME)] 
	public static extern void SetParticleSmoothingRadii(IntPtr solver, IntPtr radii);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleBuoyancy(IntPtr solver, IntPtr buoyancy);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleRestDensities(IntPtr solver, IntPtr rest_densities);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleViscosities(IntPtr solver, IntPtr viscosities);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleSurfaceTension(IntPtr solver, IntPtr surface_tension);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleVorticityConfinement(IntPtr solver, IntPtr vort_confinement);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleAtmosphericDragPressure(IntPtr solver, IntPtr atmospheric_drag, IntPtr atmospheric_pressure);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleDiffusion(IntPtr solver, IntPtr diffusion);



	[DllImport (LIBNAME)] 
	public static extern void SetParticleVorticities(IntPtr solver, IntPtr vorticities);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleFluidData(IntPtr solver, IntPtr fluidData);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleUserData(IntPtr solver, IntPtr userData);

	[DllImport (LIBNAME)] 
	public static extern void SetParticleAnisotropies(IntPtr solver, IntPtr anisotropies);

	[DllImport (LIBNAME)] 
	public static extern int GetDeformableTriangleCount(IntPtr solver);

	[DllImport (LIBNAME)] 
	public static extern void SetDeformableTriangles(IntPtr solver, int[] indices, int num, int destOffset);

	[DllImport (LIBNAME)] 
	public static extern int RemoveDeformableTriangles(IntPtr solver, int num, int sourceOffset);

	[DllImport (LIBNAME)] 
	public static extern void SetConstraintGroupParameters(IntPtr solver, int type, ref ConstraintParameters parameters);
	
	[DllImport (LIBNAME)] 
	public static extern void GetConstraintGroupParameters(IntPtr solver, int type, ref ConstraintParameters parameters);

	[DllImport (LIBNAME)] 
	public static extern void SetCollisionMaterials(IntPtr solver, IntPtr[] materials, int[] indices, int num);

	[DllImport (LIBNAME)] 
	public static extern void SetRestPositions(IntPtr solver, IntPtr restPositions);

	[DllImport (LIBNAME)] 
	public static extern void SetRestOrientations(IntPtr solver, IntPtr restOrientations);

	[DllImport (LIBNAME)] 
	public static extern IntPtr CreateDeformableMesh(IntPtr solver, 
													 IntPtr halfEdge,
													 IntPtr skinConstraintBatch,
													 [MarshalAs(UnmanagedType.LPArray, SizeConst=16)] float[] worldToLocal, 
													 IntPtr particleIndices, 
													 int vertexCapacity,
													 int vertexCount);

	[DllImport (LIBNAME)] 
	public static extern void DestroyDeformableMesh(IntPtr solver, IntPtr mesh);

	[DllImport (LIBNAME)] 
	public static extern bool TearDeformableMeshAtVertex(IntPtr mesh,int vertexIndex,
																	 ref Vector3 planePoint,
																	 ref Vector3 planeNormal,
																	 int[] updated_edges,
																	 ref int num_edges);

	[DllImport (LIBNAME)] 
	public static extern void SetDeformableMeshTBNUpdate(IntPtr mesh,NormalsUpdate normalsUpdate, [MarshalAs(UnmanagedType.I1)]bool skinTangents);

	[DllImport (LIBNAME)] 
	public static extern void SetDeformableMeshTransform(IntPtr mesh,[MarshalAs(UnmanagedType.LPArray, SizeConst=16)] float[] worldToLocal);

	[DllImport (LIBNAME)] 
	public static extern void SetDeformableMeshSkinMap(IntPtr mesh, IntPtr sourceMesh, IntPtr triangleSkinMap);

	[DllImport (LIBNAME)] 
	public static extern void SetDeformableMeshParticleIndices(IntPtr mesh, IntPtr particleIndices);

	[DllImport (LIBNAME)] 
	public static extern void SetDeformableMeshData(IntPtr mesh,IntPtr triangles,
																IntPtr vertices,
												  	 			IntPtr normals,
																IntPtr tangents,
																IntPtr colors,
																IntPtr uv1,
																IntPtr uv2,
																IntPtr uv3,
																IntPtr uv4);

	[DllImport (LIBNAME)] 
	public static extern void SetDeformableMeshAnimationData(IntPtr mesh,float[] bindPoses,BoneWeights[] weights, int numBones);

	[DllImport (LIBNAME)] 
	public static extern void SetDeformableMeshBoneTransforms(IntPtr mesh,float[] boneTransforms);

	[DllImport (LIBNAME)] 
	public static extern void ForceDeformableMeshSkeletalSkinning(IntPtr mesh);

	[DllImport (LIBNAME)] 
	public static extern IntPtr CreateBatch(int type);

    [DllImport(LIBNAME)]
    public static extern void SetDependency(IntPtr batch, IntPtr dependency);

	[DllImport (LIBNAME)] 
	public static extern void DestroyBatch(IntPtr batch);

	[DllImport (LIBNAME)] 
	public static extern IntPtr AddBatch(IntPtr solver, IntPtr batch);

	[DllImport (LIBNAME)] 
	public static extern void RemoveBatch(IntPtr solver, IntPtr batch);

	[DllImport (LIBNAME)] 
	public static extern bool EnableBatch(IntPtr batch, [MarshalAs(UnmanagedType.I1)]bool enabled);

	[DllImport (LIBNAME)] 
	public static extern int GetBatchConstraintCount(IntPtr batch);

	[DllImport (LIBNAME)] 
	public static extern int GetBatchConstraintForces(IntPtr batch, float[] forces, int num, int destOffset);

	
	[DllImport (LIBNAME)] 
	public static extern void SetActiveConstraints(IntPtr batch, int num);

    [DllImport(LIBNAME)]
    public static extern void SetConstraintCount(IntPtr batch, int num);

    [DllImport (LIBNAME)] 
    public static extern void SetDistanceConstraints(IntPtr batch, IntPtr indices,
                                                                   IntPtr restLengths,
                                                                   IntPtr stiffnesses,
																   int num);

	[DllImport (LIBNAME)] 
    public static extern void SetBendingConstraints(IntPtr batch, IntPtr indices,
                                                                  IntPtr restBends,
                                                                  IntPtr bendingStiffnesses,
																  int num);

	[DllImport (LIBNAME)] 
	public static extern void SetSkinConstraints(IntPtr batch, 
                                                 IntPtr indices,
                                                 IntPtr points,
                                                 IntPtr normals,
                                                 IntPtr radiiBackstops,
                                                 IntPtr stiffnesses,
												 int num);

	[DllImport (LIBNAME)] 
	public static extern void SetAerodynamicConstraints(IntPtr batch, 
                                                        IntPtr particleIndices, 
                                                        IntPtr aerodynamicCoeffs,
														int num);
    
	[DllImport (LIBNAME)]
	public static extern void SetVolumeConstraints(IntPtr batch, 
                                                   IntPtr triangleIndices,
                                                   IntPtr firstTriangle,
                                                   IntPtr restVolumes,
                                                   IntPtr pressureStiffnesses,
												   int num);

	[DllImport (LIBNAME)] 
	public static extern void SetShapeMatchingConstraints(IntPtr batch, 
                                                          IntPtr shapeIndices,
                                                          IntPtr firstIndex,
                                                          IntPtr numIndices,
                                                          IntPtr explicitGroup,
                                                          IntPtr materialParameters,
														  IntPtr restComs,
														  IntPtr coms,
														  IntPtr orientations,
													      int num);

	[DllImport (LIBNAME)] 
	public static extern void CalculateRestShapeMatching(IntPtr solver, IntPtr batch);


	[DllImport (LIBNAME)] 
	public static extern void SetStretchShearConstraints(IntPtr batch, 
                                                         IntPtr particleIndices,
                                                         IntPtr orientationIndices,
                                                         IntPtr restLengths,
                                                         IntPtr restOrientations,
                                                         IntPtr stiffnesses,
													     int num);

	[DllImport (LIBNAME)] 
	public static extern void SetBendTwistConstraints(IntPtr batch, 
                                                      IntPtr orientationIndices,
                                                      IntPtr restDarboux,
                                                      IntPtr stiffnesses,
													  int num);
	
	[DllImport (LIBNAME)] 
	public static extern void SetTetherConstraints(IntPtr batch, 
                                                   IntPtr indices,
                                                   IntPtr maxLenghtsScales,
                                                   IntPtr stiffnesses,
												   int num);

	[DllImport (LIBNAME)] 
	public static extern void SetPinConstraints(IntPtr batch, 
                                                IntPtr indices,
                                                IntPtr pinOffsets,
                                                IntPtr restDarboux,
                                                IntPtr colliders,
                                                IntPtr stiffnesses,
												int num);

	[DllImport (LIBNAME)] 
	public static extern void SetStitchConstraints(IntPtr batch, 
											       IntPtr indices,
                                                   IntPtr stiffnesses,
												   int num);

	[DllImport (LIBNAME)] 
	public static extern void SetChainConstraints(IntPtr batch, 
                                                  IntPtr indices,
                                                  IntPtr lengths,
                                                  IntPtr firstIndex,
                                                  IntPtr numIndex,
												  int num);

	[DllImport (LIBNAME)] 
	public static extern void GetCollisionContacts(IntPtr solver, Contact[] contacts, int n);

	[DllImport (LIBNAME)] 
	public static extern void GetParticleCollisionContacts(IntPtr solver, Contact[] contacts, int n);

	[DllImport (LIBNAME)] 
	public static extern int InterpolateDiffuseParticles(IntPtr solver, IntPtr properties, IntPtr diffusePositions, IntPtr diffuseProperties, IntPtr neighbourCount, int n);

	[DllImport (LIBNAME)] 
	public static extern IntPtr CreateHalfEdgeMesh();

	[DllImport (LIBNAME)] 
	public static extern void DestroyHalfEdgeMesh(IntPtr mesh);

	[DllImport (LIBNAME)] 
	public static extern void SetVertices(IntPtr mesh, IntPtr vertices, int n);

	[DllImport (LIBNAME)] 
	public static extern void SetHalfEdges(IntPtr mesh, IntPtr halfedges, int n);

	[DllImport (LIBNAME)] 
	public static extern void SetFaces(IntPtr mesh, IntPtr faces, int n);

	[DllImport (LIBNAME)] 
	public static extern void SetNormals(IntPtr mesh, IntPtr normals);

	[DllImport (LIBNAME)] 
	public static extern void SetTangents(IntPtr mesh, IntPtr tangents);

	[DllImport (LIBNAME)] 
	public static extern void SetInverseOrientations(IntPtr mesh, IntPtr orientations);

	[DllImport (LIBNAME)] 
	public static extern void SetVisualMap(IntPtr mesh, IntPtr map);

	[DllImport (LIBNAME)] 
	public static extern int GetVertexCount(IntPtr mesh);

	[DllImport (LIBNAME)] 
	public static extern int GetHalfEdgeCount(IntPtr mesh);

	[DllImport (LIBNAME)] 
	public static extern int GetFaceCount(IntPtr mesh);

	[DllImport (LIBNAME)] 
	public static extern int GetHalfEdgeMeshInfo(IntPtr mesh, ref MeshInformation meshInfo);

	[DllImport (LIBNAME)] 
	public static extern void CalculatePrimitiveCounts(IntPtr mesh, Vector3[] vertices, int[] triangles, int vertexCount, int triangleCount);

	[DllImport (LIBNAME)] 
	public static extern void Generate(IntPtr mesh, Vector3[] vertices, int[] triangles, int vertexCount, int triangleCount, ref Vector3 scale);

	[DllImport (LIBNAME)] 
	public static extern void GetPointCloudAnisotropy(Vector3[] points, int count, float maxAnisotropy, float radius, ref Vector3 hintNormal, ref Vector3 centroid, ref Quaternion orientation, ref Vector3 principalValues);

    [DllImport (LIBNAME)] 
	public static extern int MakePhase(int group, ParticleFlags flags);

	[DllImport (LIBNAME)] 
	public static extern int GetGroupFromPhase(int phase);

    [DllImport(LIBNAME)]
    public static extern int GetFlagsFromPhase(int phase);

    [DllImport (LIBNAME)] 
	public static extern float BendingConstraintRest(float[] constraintCoordinates);

	[DllImport (LIBNAME)] 
	public static extern IntPtr CreateTriangleSkinMap();

	[DllImport (LIBNAME)] 
	public static extern void DestroyTriangleSkinMap(IntPtr skinmap);

	[DllImport (LIBNAME)] 
	public static extern void Bind(IntPtr skinmap, IntPtr sourcemesh, IntPtr targetmesh,  uint[] sourceMasterFlags, uint[] targetSlaveFlags);

	[DllImport (LIBNAME)] 
	public static extern int GetSkinnedVertexCount(IntPtr skinmap);

	[DllImport (LIBNAME)] 
	public static extern void GetSkinInfo(IntPtr skinmap, 
										 int[] skinIndices, 
										 int[] sourceTriIndices,
										 Vector3[] baryPositions,
										 Vector3[] baryNormals,
										 Vector3[] baryTangents);

	[DllImport (LIBNAME)] 
	public static extern void SetSkinInfo(IntPtr skinmap, 
										 int[] skinIndices, 
										 int[] sourceTriIndices,
										 Vector3[] baryPositions,
										 Vector3[] baryNormals,
										 Vector3[] baryTangents,
										 int num);

	[DllImport (LIBNAME)] 
	public static extern void CompleteAll();

	[DllImport (LIBNAME)] 
	public static extern void Complete(IntPtr task);

    [DllImport(LIBNAME)]
    public static extern IntPtr CreateEmpty();

    [DllImport(LIBNAME)]
    public static extern void Schedule(IntPtr task);

    [DllImport(LIBNAME)]
    public static extern void AddChild(IntPtr task, IntPtr child);

	[DllImport (LIBNAME)] 
	public static extern int GetMaxSystemConcurrency();

	[DllImport (LIBNAME)] 
	public static extern void ClearProfiler();

	[DllImport (LIBNAME)] 
	public static extern void EnableProfiler([MarshalAs(UnmanagedType.I1)]bool cooked);

    [DllImport(LIBNAME)]
    public static extern void BeginSample(string name, byte type);

    [DllImport(LIBNAME)]
    public static extern void EndSample();

	[DllImport (LIBNAME)] 
	public static extern int GetProfilingInfoCount();

	[DllImport (LIBNAME)] 
	public static extern void GetProfilingInfo([Out] ProfileInfo[] info, int num);
}
