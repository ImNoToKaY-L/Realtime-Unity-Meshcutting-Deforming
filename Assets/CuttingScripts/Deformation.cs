

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DataStructures.ViliWonka.Tests
{

using KDTree;
    using System;
    using System.Text;
    using Unity.Burst;
    using Unity.Collections;
using Unity.Jobs;

    public enum BenchMarkType
    {
        Burst,
        KDTree,
        Multithreaded,
        Linear
    }


    public class Deformation : MonoBehaviour
    {
        public BenchMarkType structureType;
        private Vector3[] vertices;

        private Vector3[] rendVert;

        [Range(0f, 100f)]
        public float Radius = 0.1f;


        private Vector3 PreviousPos;

        private Matrix4x4 localToWorld;
        private Matrix4x4 worldToLocal;
        private bool initialised = false;
        private bool isMoving = false;

        private long updateTimeCost = 0;
        private long totalTimeCost = 0;
        private List<float> BerpRecord = new List<float>();

        KDTree tree;

        KDQuery query;

        List<VertexMovement> movingVertices = new List<VertexMovement>();

        void Awake()
        {
            localToWorld = GameObject.FindGameObjectWithTag("Belly").transform.localToWorldMatrix;
            worldToLocal = GameObject.FindGameObjectWithTag("Belly").transform.worldToLocalMatrix;
        }


        [BurstCompile(CompileSynchronously = true)]
        public struct Initialisation : IJob
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



        public struct Initialisation_noBurst : IJob
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
        public struct BruteForceQuerying : IJob
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

       public struct BruteForceQuerying_noBurst : IJob
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


            return BasicCut.cutPlane.GetSide(Point);


        }



        void Update()
        {


            RaycastHit mousehit;
            Ray mouseray = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(mouseray, out mousehit, 1000f) && mousehit.transform.tag.Equals("Belly"))  
            transform.position = mousehit.point;

            if (Input.GetKeyUp(KeyCode.Return))
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


            if (Input.GetKeyDown("d"))
            {
                PreviousPos = transform.position;

            }



            if (Input.GetKeyUp("d"))
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
                        long start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                        query.Radius(tree, PreviousPos, Radius, resultIndices);

                        Debug.Log("KDTree querying cost: " + ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - start));
                    }
                    else if(structureType == BenchMarkType.Burst)
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
                    else if (structureType == BenchMarkType.Multithreaded)
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

                        for (int i = 0; i < vertices.Length; i++)
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


                    for (int i = 0; i < resultIndices.Count; i++)
                    {

                        if (multiplePlaneGetSide(transform.position))
                        {
                            //if (BasicCut.negative_index.Contains(resultIndices[i]) || BasicCut.bot_index.Contains(resultIndices[i])) continue;
                            if (BasicCut.negative_index.Contains(resultIndices[i])) continue;
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
                            //if (BasicCut.positive_index.Contains(resultIndices[i]) || BasicCut.bot_index.Contains(resultIndices[i])) continue;
                            if (BasicCut.positive_index.Contains(resultIndices[i])) continue;
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
                long start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;


                foreach (var i in movingVertices)
                {
                    rendVert[i.index] = TestController.Berp(rendVert[i.index],i.Destination,0.05f);

                }


                if (rendVert[movingVertices[0].index] == movingVertices[0].Destination)
                {
                    movingVertices.Clear();
                    Debug.Log("Moving Complete");



                    //Debug.Log("Moving Complete, average update cost: " + updateTimeCost+"ms, total deforming cost: "+ totalTimeCost+"ms");
                    isMoving = false;
                    updateTimeCost = 0;
                    totalTimeCost = 0;
                }


                Destroy(GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshCollider>());
                GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshFilter>().mesh.vertices = rendVert;
                GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshFilter>().mesh.RecalculateNormals();
                GameObject.FindGameObjectWithTag("Belly").AddComponent<MeshCollider>();

                //if (updateTimeCost == 0) updateTimeCost = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - start;
                //else updateTimeCost = ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - start + updateTimeCost) / 2;

                //totalTimeCost += (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - start;
            }


        }

    }
}