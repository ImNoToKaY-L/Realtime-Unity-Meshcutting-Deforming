/*
 *  Oni.h
 *  Oni
 *
 *  Created by José María Méndez González on 21/9/15.
 *  Copyright (c) 2015 ArK. All rights reserved.
 *
 */

#ifndef Oni_
#define Oni_

#include "Solver.h"
#include "HalfEdgeMesh.h"
#include "ParticleGrid.h"

#if defined(__APPLE__) || defined(ANDROID) || defined(__linux__)
    #define EXPORT __attribute__((visibility("default")))
#else
    #define EXPORT __declspec(dllexport)
#endif

namespace Oni
{
    
    struct ConstraintGroupParameters;
    class ConstraintBatchBase;
    class Collider;
    class Rigidbody;
    class TriangleSkinMap;
    class DistanceField;
    struct SphereShape;
    struct BoxShape;
    struct CapsuleShape;
    struct HeightmapShape;
    struct TriangleMeshShape;
    struct CollisionMaterial;
    struct ProfileInfo;
    struct DFNode;
    
    template<class T>
    struct ObjHandle
    {
        std::shared_ptr<T> ptr;
        
        ObjHandle(T* obj):ptr(std::shared_ptr<T>(obj)){}
        
        ObjHandle(std::shared_ptr<T> obj):ptr(obj){}
        
        std::shared_ptr<T> operator->() const{
            return ptr;
        }
    };
    
    extern "C"
    {
    
        typedef ObjHandle<ConstraintBatchBase> ConstraintBatchHandle;
        typedef ObjHandle<Collider> ColliderHandle;
        typedef ObjHandle<Shape> ShapeHandle;
        typedef ObjHandle<Rigidbody> RigidbodyHandle;
        typedef ObjHandle<CollisionMaterial> CollisionMaterialHandle;
        typedef ObjHandle<DistanceField> DistanceFieldHandle;
        typedef ObjHandle<Task> TaskHandle;
        
        // Colliders ********************:
        
        EXPORT ColliderHandle* CreateCollider();
        EXPORT void DestroyCollider(ColliderHandle* collider);
        
        EXPORT ShapeHandle* CreateShape(const ShapeType shape);
        EXPORT void DestroyShape(ShapeHandle* shape);
        
        EXPORT RigidbodyHandle* CreateRigidbody();
        EXPORT void DestroyRigidbody(RigidbodyHandle* rigidbody);
        
        EXPORT void UpdateCollider(ColliderHandle* collider, const ColliderAdaptor& adaptor);
        EXPORT void UpdateShape(ShapeHandle* shape, const ShapeAdaptor& adaptor);
        EXPORT void UpdateRigidbody(RigidbodyHandle* rigidbody, const RigidbodyAdaptor& adaptor);
        
        EXPORT void SetShapeDistanceField(ShapeHandle* shape, DistanceFieldHandle* distance_field);
        
        EXPORT void SetColliderShape(ColliderHandle* collider, ShapeHandle* shape);
        EXPORT void SetColliderRigidbody(ColliderHandle* collider, RigidbodyHandle* rigidbody);
        EXPORT void SetColliderMaterial(ColliderHandle* collider, CollisionMaterialHandle* material);
        
        EXPORT void GetRigidbodyVelocity(RigidbodyHandle* rigidbody, RigidbodyVelocityDelta& delta);
        
        // Distance fields ********************:

        EXPORT DistanceFieldHandle* CreateDistanceField();
        EXPORT void DestroyDistanceField(DistanceFieldHandle* df);
        
        EXPORT void StartBuildingDistanceField(DistanceFieldHandle* df,float max_error,
                                                                       int max_depth,
                                                                       Eigen::Vector3f* vertex_positions,
                                                                       int* triangle_indices_,
                                                                       int num_vertices,
                                                                       int num_triangles);
        
        EXPORT bool ContinueBuildingDistanceField(DistanceFieldHandle* df);
        
        EXPORT float SampleDistanceField(DistanceFieldHandle* df, float x, float y, float z);
        
        EXPORT int GetDistanceFieldNodeCount(DistanceFieldHandle* df);
        EXPORT void GetDistanceFieldNodes(DistanceFieldHandle* df, DFNode* nodes);
        EXPORT void SetDistanceFieldNodes(DistanceFieldHandle* df, const DFNode* nodes, int num);
        
        // Collision materials *****************:
        
        EXPORT CollisionMaterialHandle* CreateCollisionMaterial();
        EXPORT void UpdateCollisionMaterial(CollisionMaterialHandle* material, const CollisionMaterial& adaptor);
        EXPORT void DestroyCollisionMaterial(CollisionMaterialHandle* collider);
        
        // Solver ********************:
        
		EXPORT Solver* CreateSolver(int capacity);
		EXPORT void DestroySolver(Solver* solver);
        
        EXPORT void SetCapacity(Solver* solver,int capacity);
        EXPORT void InitializeFrame(Solver* solver,const Vector4fUnaligned& position, const Vector4fUnaligned& scale, const QuaternionfUnaligned& rotation);
        EXPORT void UpdateFrame(Solver* solver,const Vector4fUnaligned& position, const Vector4fUnaligned& scale, const QuaternionfUnaligned& rotation, float dt);
        EXPORT void ApplyFrame(Solver* solver, float linear_velocity_scale, float angular_velocity_scale, float linear_inertia_scale,float angular_inertia_scale, float dt);
        
        EXPORT void AddCollider(ColliderHandle* collider);
        EXPORT void RemoveCollider(ColliderHandle* collider);
        
		EXPORT void GetBounds(Solver* solver, Eigen::Vector3f& min, Eigen::Vector3f& max);
        EXPORT int GetParticleGridSize(Solver* solver);
        EXPORT void GetParticleGrid(Solver* solver, ParticleGrid::GridCell* cells);
        
		EXPORT void SetSolverParameters(Solver* solver, const SolverParameters* parameters);
		EXPORT void GetSolverParameters(Solver* solver, SolverParameters* parameters);
        
        EXPORT TaskHandle* CollisionDetection(Solver* solver, const float delta_seconds);
		EXPORT TaskHandle* Step(Solver* solver, const float delta_seconds);
        
		EXPORT void ApplyPositionInterpolation(Solver* solver,
                                               Eigen::Vector4f* start_positions,
                                               Eigen::Quaternionf* start_orientations,
                                               const float delta_seconds,
                                               const float unsimulated_time);
        
        EXPORT void UpdateSkeletalAnimation(Solver* solver);
        
        EXPORT void RecalculateInertiaTensors(Solver* solver);
        
        EXPORT void ResetForces(Solver* solver);

		EXPORT int GetConstraintCount(Solver* solver, const Solver::ConstraintType type);
        EXPORT void GetActiveConstraintIndices(Solver* solver, int* indices, int num, const Solver::ConstraintType type);
        
		EXPORT int SetActiveParticles(Solver* solver, const int* active, int num);
        
		EXPORT void SetParticlePhases(Solver* solver, int* phases);
        
		EXPORT void SetParticlePositions(Solver* solver, Eigen::Vector4f* positions);
        
        EXPORT void SetParticlePreviousPositions(Solver* solver, Eigen::Vector4f* prev_positions);
        
        EXPORT void SetParticleOrientations(Solver* solver,Eigen::Quaternionf* orientations);
        
        EXPORT void SetParticlePreviousOrientations(Solver* solver, Eigen::Quaternionf* prev_orientations);
        
        EXPORT void SetRenderableParticleOrientations(Solver* solver, Eigen::Quaternionf* orientations);
        
		EXPORT void SetRenderableParticlePositions(Solver* solver, Eigen::Vector4f* positions);
        
		EXPORT void SetParticleInverseMasses(Solver* solver, float* inv_masses);
        
        EXPORT void SetParticleInverseRotationalMasses(Solver* solver, float* inv_rot_masses);
        
        EXPORT void SetParticlePrincipalRadii(Solver* solver, Eigen::Vector4f* radii);
        
		EXPORT void SetParticleVelocities(Solver* solver, Eigen::Vector4f* velocities);
        
        EXPORT void SetParticleAngularVelocities(Solver* solver, Eigen::Vector4f* velocities);
        
        EXPORT void SetParticleExternalForces(Solver* solver, Eigen::Vector4f* forces);
        
        EXPORT void SetParticleExternalTorques(Solver* solver, Eigen::Vector4f* torques);
        
        EXPORT void SetParticleWinds(Solver* solver, Eigen::Vector4f* wind);
        
        EXPORT void SetParticlePositionDeltas(Solver* solver, Eigen::Vector4f* deltas);
        
        EXPORT void SetParticleOrientationDeltas(Solver* solver, Eigen::Quaternionf* deltas);
        
        EXPORT void SetParticlePositionConstraintCounts(Solver* solver, int* counts);
        
        EXPORT void SetParticleOrientationConstraintCounts(Solver* solver, int* counts);
        
        EXPORT void SetParticleNormals(Solver* solver, Eigen::Vector4f* normals);
        
        EXPORT void SetParticleInverseInertiaTensors(Solver* solver, Eigen::Vector4f* tensors);
        
        EXPORT int GetDeformableTriangleCount(Solver* solver);
        
        EXPORT void SetDeformableTriangles(Solver* solver, const int* indices, int num, int dest_offset);
        
        EXPORT int RemoveDeformableTriangles(Solver* solver, int num, int source_offset);
        
        
        // Fluid ********************:
        
        EXPORT void SetParticleSmoothingRadii(Solver* solver,float* radii);
        
        EXPORT void SetParticleBuoyancy(Solver* solver,float* buoyancy);
        
        EXPORT void SetParticleRestDensities(Solver* solver,float* rest_densities);
        
        EXPORT void SetParticleViscosities(Solver* solver,float* viscosities);
        
        EXPORT void SetParticleSurfaceTension(Solver* solver,float* surf_tension);
        
        EXPORT void SetParticleVorticityConfinement(Solver* solver,float* vort_confinement);
        
        EXPORT void SetParticleAtmosphericDragPressure(Solver* solver,float* atmospheric_drag, float* atmospheric_pressure);
        
        EXPORT void SetParticleDiffusion(Solver* solver,float* diffusion);
        
        
        EXPORT void SetParticleVorticities(Solver* solver, Eigen::Vector4f* vorticities);
        
        EXPORT void SetParticleFluidData(Solver* solver, Eigen::Vector4f* fluid_data);
        
        EXPORT void SetParticleUserData(Solver* solver, Eigen::Vector4f* user_data);
        
        EXPORT void SetParticleAnisotropies(Solver* solver, Eigen::Vector4f* anisotropies);
        
        
        
		EXPORT void SetConstraintGroupParameters(Solver* solver, const Solver::ConstraintType type, const ConstraintGroupParameters* parameters);
        
		EXPORT void GetConstraintGroupParameters(Solver* solver, const Solver::ConstraintType type, ConstraintGroupParameters* parameters);
        
		EXPORT void SetCollisionMaterials(Solver* solver, const CollisionMaterialHandle** materials, int* indices, int num);
        
        EXPORT void SetRestPositions(Solver* solver, Eigen::Vector4f* rest_positions);
        
        EXPORT void SetRestOrientations(Solver* solver, Eigen::Quaternionf* rest_orientations);
        
        // Meshes ********************:
        
        EXPORT Mesh* CreateDeformableMesh(Solver* solver,
                                          HalfEdgeMesh* half_edge,
                                          ConstraintBatchHandle* skin_batch,
                                          float world_to_local[16],
                                          const int* particle_indices,
                                          int vertex_capacity,
                                          int vertex_count);
        
        EXPORT void DestroyDeformableMesh(Solver* solver,Mesh* mesh);
        
        EXPORT bool TearDeformableMeshAtVertex(Mesh* mesh,int vertex_index,
                                                          const Eigen::Vector3f* plane_point,
                                                          const Eigen::Vector3f* plane_normal,
                                                          int* updated_edges,
                                                          int& num_edges);
        
        EXPORT void SetDeformableMeshTBNUpdate(Mesh* mesh, const Mesh::NormalUpdate normal_update, bool skin_tangents);
        
        EXPORT void SetDeformableMeshTransform(Mesh* mesh,float world_to_local[16]);
        
        EXPORT void SetDeformableMeshSkinMap(Mesh* mesh, Mesh* source_mesh, TriangleSkinMap* map);
        
        EXPORT void SetDeformableMeshParticleIndices(Mesh* mesh,const int* indices);
        
        EXPORT void SetDeformableMeshData(Mesh* mesh,int* triangles,
                                                     Eigen::Vector3f* vertices,
                                                     Eigen::Vector3f* normals,
                                                     Vector4fUnaligned* tangents,
                                                     Vector4fUnaligned* colors,
                                                     Vector2fUnaligned* uv1,
                                                     Vector2fUnaligned* uv2,
                                                     Vector2fUnaligned* uv3,
                                                     Vector2fUnaligned* uv4);
        
        EXPORT void SetDeformableMeshAnimationData(Mesh* mesh,
                                                   float* bind_poses,
                                                   Mesh::BoneWeight* bone_weights,
                                                   int num_bones);
        
        EXPORT void SetDeformableMeshBoneTransforms(Mesh* mesh,float* bone_transforms);
        
        EXPORT void ForceDeformableMeshSkeletalSkinning(Mesh* mesh);
        
        // Batches ********************:
        
        EXPORT ConstraintBatchHandle* CreateBatch(const Solver::ConstraintType type);
        
        EXPORT void SetDependency(ConstraintBatchHandle* batch, ConstraintBatchHandle* dependency);
        
        EXPORT void DestroyBatch(ConstraintBatchHandle* batch);
        
        EXPORT void AddBatch(Solver* solver, ConstraintBatchHandle* batch);
        
        EXPORT void RemoveBatch(Solver* solver, ConstraintBatchHandle* batch);
        
        EXPORT void EnableBatch(ConstraintBatchHandle* batch, bool enabled);
        
        EXPORT int GetBatchConstraintCount(ConstraintBatchHandle* batch);
        
        EXPORT int GetBatchConstraintForces(ConstraintBatchHandle* batch, float* forces, int num, int source_offset);
        
        // Constraints ********************:
        
        EXPORT void SetActiveConstraints(ConstraintBatchHandle* batch, int num);
        
        EXPORT void SetConstraintCount(ConstraintBatchHandle* batch, int num);
        
		EXPORT void SetDistanceConstraints(ConstraintBatchHandle* batch,
                                      int* indices,
                                      float* restLengths,
                                      float* stiffnesses,
                                     int num);
    
		EXPORT void SetBendingConstraints(ConstraintBatchHandle* batch,
                                          int* indices,
                                          float* rest_bends,
                                          float* bending_stiffnesses,
                                          int num);
        
		EXPORT void SetSkinConstraints(ConstraintBatchHandle* batch,
                                       int* indices,
                                       Eigen::Vector4f* skin_points,
                                       Eigen::Vector4f* skin_normals,
                                       float* radii_backstops,
                                       float* stiffnesses,
                                       int num);
        
		EXPORT void SetAerodynamicConstraints(ConstraintBatchHandle* batch,
                                              int* triangle_indices,
                                              float* aerodynamic_coeffs,
                                              int num);
        
		EXPORT  void SetVolumeConstraints(ConstraintBatchHandle* batch,
                                          int* triangle_indices,
                                          int* first_triangle,
                                          float* rest_volumes,
                                          float* pressure_stiffnesses,
                                          int num);
        
        EXPORT  void SetShapeMatchingConstraints(ConstraintBatchHandle* batch,
                                                 int* shape_indices,
                                                 int* first_index,
                                                 int* num_indices,
                                                 int* explicit_group,
                                                 float* shape_material_parameters,
                                                 Eigen::Vector4f* rest_coms,
                                                 Eigen::Vector4f* coms,
                                                 Eigen::Quaternionf* orientations,
                                                 int num);
        
        EXPORT  void CalculateRestShapeMatching(Solver* solver, ConstraintBatchHandle* batch);
        
        EXPORT  void SetStretchShearConstraints(ConstraintBatchHandle* batch,
                                                int* particle_indices,
                                                int* orientation_indices,
                                                float* rest_lengths,
                                                Eigen::Quaternionf* rest_orientations,
                                                Eigen::Vector3f* stiffnesses,
                                                int num);
        
        EXPORT void SetBendTwistConstraints(ConstraintBatchHandle* batch,
                                                 int* orientation_indices,
                                                 Eigen::Quaternionf* rest_darboux,
                                                 Eigen::Vector3f* stiffnesses,
                                                 int num);
        
        EXPORT void SetTetherConstraints(ConstraintBatchHandle* batch,
                                         int* indices,
                                         float* max_lenght_scales,
                                         float* stiffnesses,
                                         int num);
        
        EXPORT void SetPinConstraints(ConstraintBatchHandle* batch,
                                      int* indices,
                                      Vector4f* pin_offsets,
                                      Quaternionf* rest_darboux,
                                      const ColliderHandle** colliders,
                                      float* stiffnesses,
                                      int num);
        
        EXPORT void SetStitchConstraints(ConstraintBatchHandle* batch,
                                         int* indices,
                                         float* stiffnesses,
                                         int num);
        
        EXPORT void SetChainConstraints(ConstraintBatchHandle* batch,
                                        int* indices,
                                        float* rest_lengths,
                                        int* first_index,
                                        int* num_indices,
                                        int num);
        
        // Collision data ********************:
        
        EXPORT void GetCollisionContacts(Solver* solver,Contact* contacts, int num);
        
        EXPORT void GetParticleCollisionContacts(Solver* solver,Contact* contacts, int num);
        
        // Diffuse particles ********************:
        
        EXPORT void InterpolateDiffuseParticles(Solver* solver,
                                                const Eigen::Vector4f* properties,
                                                const Eigen::Vector4f* diffuse_positions,
                                                Eigen::Vector4f* diffuse_properties,
                                                int* neighbour_count,
                                                int count);
        
        // Skin maps ********************:
        
        EXPORT TriangleSkinMap* CreateTriangleSkinMap();
        
        EXPORT void DestroyTriangleSkinMap(TriangleSkinMap* map);
        
        EXPORT void Bind(TriangleSkinMap* map, Mesh* source, Mesh* target,
                         const unsigned int* source_master_flags,
                         const unsigned int* target_slave_flags);
        
        EXPORT int GetSkinnedVertexCount(TriangleSkinMap* map);
        
        EXPORT void GetSkinInfo(TriangleSkinMap* map,
                                int* skin_indices,
                                int* source_tri_indices,
                                Eigen::Vector3f* bary_pos,
                                Eigen::Vector3f* bary_nrm,
                                Eigen::Vector3f* bary_tan);
        
       EXPORT  void SetSkinInfo(TriangleSkinMap* map,
                                const int* skin_indices,
                                const int* source_tri_indices,
                                const Eigen::Vector3f* bary_pos,
                                const Eigen::Vector3f* bary_nrm,
                                const Eigen::Vector3f* bary_tan,
                                int num);

        // Tasks ********************:
        
        EXPORT void CompleteAll();
        
        EXPORT void Complete(TaskHandle* task);
        
        EXPORT TaskHandle* CreateEmpty();
        
        EXPORT void Schedule(TaskHandle* task);
        
        EXPORT void AddChild(TaskHandle* task, TaskHandle* child);
        
        // Profiling ****************:
        
        EXPORT int GetMaxSystemConcurrency();
        
        EXPORT void ClearProfiler();
        
        EXPORT void EnableProfiler(bool enabled);
        
        EXPORT void BeginSample(const char* name, unsigned char type);
        
        EXPORT void EndSample();
        
        EXPORT int GetProfilingInfoCount();
        
        EXPORT void GetProfilingInfo(ProfileInfo* info, int count);
    }
    
}

#endif
