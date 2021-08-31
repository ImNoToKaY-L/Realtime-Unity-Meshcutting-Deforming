

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DataStructures.ViliWonka.Tests
{

using KDTree;
    using System;
    using Unity.Burst;
    using Unity.Collections;
using Unity.Jobs;

    public enum BenchMarkType
    {
        ParalleledBruteForce,
        KDTree,
        ParalleledNoBurst,
        BruteForce
    }


    public class KDTest : MonoBehaviour
    {
        public BenchMarkType structureType;
        private Vector3[] vertices;

        private Vector3[] rendVert;

        [Range(0f, 100f)]
        public float Radius = 0.1f;
        public int repeatingCount = 0;


        private Vector3 PreviousPos;

        private Matrix4x4 localToWorld;
        private Matrix4x4 worldToLocal;
        private bool initialised = false;
        private bool isMoving = false;


        KDTree tree;

        KDQuery query;

        List<VertexMovement> movingVertices = new List<VertexMovement>();

        void Awake()
        {
            localToWorld = GameObject.FindGameObjectWithTag("Belly").transform.localToWorldMatrix;
            worldToLocal = GameObject.FindGameObjectWithTag("Belly").transform.worldToLocalMatrix;
            if (repeatingCount < GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshFilter>().mesh.vertexCount) repeatingCount = GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshFilter>().mesh.vertexCount;
            Debug.LogWarning("Repeating Count set to a lower value than the original vertex count, now using the vertex count: "+ GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshFilter>().mesh.vertexCount);
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct Initialisation : IJob
        {
            [ReadOnly]
            public NativeArray<Vector3> Input;
            public Matrix4x4 localToWorldMatrix;
            public Matrix4x4 worldToLocalMatrix;

            [WriteOnly]
            public NativeArray<Vector3> Output;

            public void Execute()
            {
                for (int i = 0; i < Input.Length; i++)
                {
                    Output[i] = localToWorldMatrix.MultiplyPoint3x4(Input[i]);
                }
                
            }
        }



        private struct Initialisation_noBurst : IJob
        {
            [ReadOnly]
            public NativeArray<Vector3> Input;
            public Matrix4x4 localToWorldMatrix;
            public Matrix4x4 worldToLocalMatrix;

            [WriteOnly]
            public NativeArray<Vector3> Output;

            public void Execute()
            {
                for (int i = 0; i < Input.Length; i++)
                {
                    Output[i] = localToWorldMatrix.MultiplyPoint3x4(Input[i]);
                }

            }
        }

        [BurstCompile(CompileSynchronously = true, FloatPrecision = FloatPrecision.High)]
        private struct BruteForceQuerying : IJob
        {
            [ReadOnly]
            public NativeArray<Vector3> WorldPositionVertices;
            public float radius;
            public Vector3 queryingCenter;
            [WriteOnly]
            public NativeArray<int> result;

            public void Execute()
            {
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = -1;
                }


                int Counter = 0;
                for (int i = 0; i < WorldPositionVertices.Length; i++)
                {
                    if (Vector3.Distance(queryingCenter, WorldPositionVertices[i]) <= radius)
                    {
                        result[Counter] = i;
                        Counter++;
                    }
                }
            }
        }

        //[BurstCompile(CompileSynchronously = true,FloatPrecision =FloatPrecision.High)]
        //private struct BruteForceQuerying : IJob
        //{
        //    [ReadOnly]
        //    public NativeArray<Vector3> WorldPositionVertices;
        //    public float radius;
        //    public Vector3 queryingCenter;
        //    //Testing
        //    public int repeatingLength;

        //    [WriteOnly]
        //    public NativeArray<int> result;

        //    public void Execute()
        //    {
        //        for (int i = 0; i < result.Length; i++)
        //        {
        //            result[i] = -1;
        //        }


        //        int Counter = 0;
        //        for(int i = 0; i < repeatingLength; i++)
        //        {
        //            if (Vector3.Distance(queryingCenter, (WorldPositionVertices[i%(WorldPositionVertices.Length-1)])) <= radius)
        //            {
        //                if (Counter < result.Length-1) {
        //                    result[Counter] = i % (WorldPositionVertices.Length - 1);
        //                    Counter++;
        //                }
        //            }
        //        }
        //    }
        //}

        private struct BruteForceQuerying_noBurst : IJob
        {
            [ReadOnly]
            public NativeArray<Vector3> WorldPositionVertices;
            public float radius;
            public Vector3 queryingCenter;
            [WriteOnly]
            public NativeArray<int> result;

            public void Execute()
            {
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = -1;
                }


                int Counter = 0;
                for (int i = 0; i < WorldPositionVertices.Length; i++)
                {
                    if (Vector3.Distance(queryingCenter, WorldPositionVertices[i]) <= radius)
                    {
                        result[Counter] = i;
                        Counter++;
                    }
                }
            }
        }



        private bool multiplePlaneGetSide(Vector3 Point)
        {
            //int positive = 0;
            //int negative = 0;

            //foreach (var i in TestController.allPlanes)
            //{
            //    if (!i.GetSide(Point)) negative++;
            //    else positive++;
            //}
            //if (positive > negative) return true;
            //else return false;

            return BasicCut.cutPlane.GetSide(Point);


        }



        void Update()
        {


            RaycastHit mousehit;
            Ray mouseray = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(mouseray, out mousehit, 1000f) && mousehit.transform.tag.Equals("Belly"))  
            transform.position = mousehit.point;

            if (Input.GetKeyDown("i"))
            {
                if (!initialised)
                {
                    Mesh mesh = GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshFilter>().sharedMesh;
                    vertices = new Vector3[mesh.vertexCount];
                    rendVert = mesh.vertices;

                    long StartOfInitialisation = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    

                    NativeArray<Vector3> jobInput = new NativeArray<Vector3>(mesh.vertices, Allocator.TempJob);
                    NativeArray<Vector3> jobOut = new NativeArray<Vector3>(vertices, Allocator.TempJob);

                    var initialisation = new Initialisation
                    {
                        Input = jobInput,
                        localToWorldMatrix = localToWorld,
                        worldToLocalMatrix = worldToLocal,
                        Output = jobOut

                    };


                    initialisation.Schedule().Complete();


                    vertices = jobOut.ToArray();

                    jobInput.Dispose();
                    jobOut.Dispose();

                    Debug.Log("Initialisation cost: " + (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - StartOfInitialisation));




                    if(structureType == BenchMarkType.KDTree)
                    {
                        PreviousPos = transform.position;
                        StartOfInitialisation = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                        tree = new KDTree(vertices, 32);
                        Debug.Log("KDTree initialise cost: " + (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - StartOfInitialisation));
                    }
                    else
                    {
                        Debug.Log("Using Paralleled solution, no extra intialisation required");
                    }

                    query = new KDQuery();



                    initialised = true;
                    Debug.Log("Initialisation complete");
                }
            }


            if (Input.GetKeyDown("a"))
            {
                PreviousPos = transform.position;

            }



            if (Input.GetKeyUp("a"))
            {
                RaycastHit hit;
                Ray ray = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray,out hit,1000f)&&hit.transform.tag.Equals("Belly"))
                {
                    //Vector3 direction = (hit.point - PreviousPos).normalized;

                    if (query == null)
                    {
                        return;
                    }

                    var resultIndices = new List<int>();

                    if(structureType == BenchMarkType.KDTree)
                    {
                        query.Radius(tree, PreviousPos, Radius, resultIndices);
                    }
                    else if(structureType == BenchMarkType.ParalleledBruteForce)
                    {

                        NativeArray<Vector3> verticesInput = new NativeArray<Vector3>(vertices,Allocator.TempJob);
                        NativeArray<int> result = new NativeArray<int>(vertices.Length,Allocator.TempJob);

                        var bfQuerying = new BruteForceQuerying
                        {
                            WorldPositionVertices = verticesInput,
                            radius = Radius,
                            queryingCenter = PreviousPos,
                            result = result,
                            //repeatingLength = repeatingCount
                        };
                        long timestep = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                        bfQuerying.Schedule().Complete();
                        int queryingCounter = 0;
                        while (result[queryingCounter] >= 0)
                        {
                            resultIndices.Add(result[queryingCounter]);
                            queryingCounter++;
                        }
                        verticesInput.Dispose();
                        result.Dispose();

                        Debug.Log("Paralleled solution cost: "+((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - timestep));
                    }
                    else if (structureType == BenchMarkType.ParalleledNoBurst)
                    {

                        NativeArray<Vector3> verticesInput = new NativeArray<Vector3>(vertices, Allocator.TempJob);
                        NativeArray<int> result = new NativeArray<int>(vertices.Length, Allocator.TempJob);

                        var bfQuerying = new BruteForceQuerying_noBurst
                        {
                            WorldPositionVertices = verticesInput,
                            radius = Radius,
                            queryingCenter = PreviousPos,
                            result = result
                        };
                        long timestep = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                        bfQuerying.Schedule().Complete();
                        int queryingCounter = 0;
                        while (result[queryingCounter] >= 0)
                        {
                            resultIndices.Add(result[queryingCounter]);
                            queryingCounter++;
                        }
                        verticesInput.Dispose();
                        result.Dispose();

                        Debug.Log("No Burst paralleled solution cost: " + ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - timestep));
                    }

                    else
                    {
                        long timestep = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                        for (int i = 0; i < repeatingCount; i++)
                        {
                            if (Vector3.Distance(PreviousPos, vertices[(i%(vertices.Length-1))]) <= Radius)
                            {
                                if (!resultIndices.Contains(i % (vertices.Length - 1))) {
                                    resultIndices.Add(i % (vertices.Length - 1));
                                }
                            }
                        }

                        Debug.Log("Normal solution cost: " + ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - timestep));

                    }



                    PreviousPos = transform.position;
                    //Debug.Log("Result Indices size " + resultIndices.Count);
                    //for (int i = 0; i < resultIndices.Count; i++)
                    //{

                    //    if (multiplePlaneGetSide(transform.position))
                    //    {
                    //        if (BasicCut.negative_index.Contains(resultIndices[i]) || BasicCut.bot_index.Contains(resultIndices[i])) continue;
                    //        if (BasicCut.positive_index.Contains(resultIndices[i]))
                    //        {
                    //            Vector3 direction = hit.point - vertices[resultIndices[i]];
                    //            Vector3 endRendVert = worldToLocal.MultiplyPoint3x4(localToWorld.MultiplyPoint3x4(rendVert[resultIndices[i]]) + direction * 0.1f);
                    //            Vector3 endVert = vertices[resultIndices[i]] + direction * 0.1f;
                    //            rendVert[resultIndices[i]] = endRendVert;
                    //            vertices[resultIndices[i]] = endVert;
                    //            continue;
                    //        }
                    //    }
                    //    else
                    //    {
                    //        if (BasicCut.positive_index.Contains(resultIndices[i]) || BasicCut.bot_index.Contains(resultIndices[i])) continue;
                    //        if (BasicCut.negative_index.Contains(resultIndices[i]))
                    //        {
                    //            Vector3 direction = hit.point - vertices[resultIndices[i]];

                    //            Vector3 endRendVert = worldToLocal.MultiplyPoint3x4(localToWorld.MultiplyPoint3x4(rendVert[resultIndices[i]]) + direction * 0.1f);
                    //            Vector3 endVert = vertices[resultIndices[i]] + direction * 0.1f;

                    //            rendVert[resultIndices[i]] = endRendVert;
                    //            vertices[resultIndices[i]] = endVert;
                    //            continue;
                    //        }
                    //    }


                    //    if (multiplePlaneGetSide(transform.position) == multiplePlaneGetSide(vertices[resultIndices[i]]))
                    //    {
                    //        Vector3 direction = hit.point - vertices[resultIndices[i]];

                    //        Vector3 endRendVert = worldToLocal.MultiplyPoint3x4(localToWorld.MultiplyPoint3x4(rendVert[resultIndices[i]]) + direction * 0.1f);
                    //        Vector3 endVert = vertices[resultIndices[i]] + direction * 0.1f;

                    //        rendVert[resultIndices[i]] = endRendVert;
                    //        vertices[resultIndices[i]] = endVert;
                    //    }
                    //}

                    for (int i = 0; i < resultIndices.Count; i++)
                    {

                        if (multiplePlaneGetSide(transform.position))
                        {
                            if (BasicCut.negative_index.Contains(resultIndices[i]) || BasicCut.bot_index.Contains(resultIndices[i])) continue;
                            if (BasicCut.positive_index.Contains(resultIndices[i]))
                            {
                                Vector3 direction = hit.point - vertices[resultIndices[i]];
                                Vector3 endRendVert = worldToLocal.MultiplyPoint3x4(localToWorld.MultiplyPoint3x4(rendVert[resultIndices[i]]) + direction * 0.1f);
                                Vector3 endVert = vertices[resultIndices[i]] + direction * 0.1f;
                                //rendVert[resultIndices[i]] = endRendVert;

                                movingVertices.Add(new VertexMovement(resultIndices[i], endRendVert));

                                vertices[resultIndices[i]] = endVert;
                                continue;
                            }
                        }
                        else
                        {
                            if (BasicCut.positive_index.Contains(resultIndices[i]) || BasicCut.bot_index.Contains(resultIndices[i])) continue;
                            if (BasicCut.negative_index.Contains(resultIndices[i]))
                            {
                                Vector3 direction = hit.point - vertices[resultIndices[i]];

                                Vector3 endRendVert = worldToLocal.MultiplyPoint3x4(localToWorld.MultiplyPoint3x4(rendVert[resultIndices[i]]) + direction * 0.1f);
                                Vector3 endVert = vertices[resultIndices[i]] + direction * 0.1f;

                                movingVertices.Add(new VertexMovement(resultIndices[i], endRendVert));
                                vertices[resultIndices[i]] = endVert;
                                continue;
                            }
                        }


                        if (multiplePlaneGetSide(transform.position) == multiplePlaneGetSide(vertices[resultIndices[i]]))
                        {
                            Vector3 direction = hit.point - vertices[resultIndices[i]];

                            Vector3 endRendVert = worldToLocal.MultiplyPoint3x4(localToWorld.MultiplyPoint3x4(rendVert[resultIndices[i]]) + direction * 0.1f);
                            Vector3 endVert = vertices[resultIndices[i]] + direction * 0.1f;

                            movingVertices.Add(new VertexMovement(resultIndices[i], endRendVert));
                            vertices[resultIndices[i]] = endVert;
                        }
                    }

                    isMoving = true;





                    if(structureType == BenchMarkType.KDTree)
                    {
                        long StartOfInitialisation = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                        tree = new KDTree(vertices, 32);
                        Debug.Log("KDTree update cost: " + (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - StartOfInitialisation));
                    }


                }

            }


            if (isMoving)
            {

                float speed = 0.01f;
                foreach (var i in movingVertices)
                {
                    rendVert[i.index] = Vector3.MoveTowards(rendVert[i.index], i.Destination, speed * Time.deltaTime);

                }

                if (rendVert[movingVertices[0].index] == movingVertices[0].Destination)
                {
                    movingVertices.Clear();
                    isMoving = false;
                }


                Destroy(GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshCollider>());
                GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshFilter>().mesh.vertices = rendVert;
                GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshFilter>().mesh.RecalculateNormals();
                GameObject.FindGameObjectWithTag("Belly").AddComponent<MeshCollider>();
            }


        }

    }
}