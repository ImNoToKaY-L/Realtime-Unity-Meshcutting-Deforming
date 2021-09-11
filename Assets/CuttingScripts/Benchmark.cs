using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using DataStructures.ViliWonka.KDTree;

public class Benchmark : MonoBehaviour
{

    public enum BenchMarkType
    {
        BurstCompiled,
        KDTree,
        ParalleledNoBurst,
        LinearSolution
    }
    public BenchMarkType strucutre;

    public int BenchmarkFactor = 100000;
    [Range(0f, 100f)]
    public float Radius = 0.1f;
    public bool RandomisedInit = false;
    public Vector3 Gravity;
    public float randomisedFactor = 1;
    public GameObject DebugSphere;



    int originalLength;

    Mesh mesh;
    private Matrix4x4 localToWorld;
    private Matrix4x4 worldToLocal;
    Vector3 queryingCentre;
    bool queryingCentreFound = false;
    bool initialised = false;
    Vector3[] vertices;


    [BurstCompile(CompileSynchronously = true)]
    public struct Initialisation : IJob
    {
        [ReadOnly]
        public NativeArray<Vector3> Input;
        public Matrix4x4 localToWorldMatrix;
        public Matrix4x4 worldToLocalMatrix;
        public int originalLength;
        public uint seed;
        public float factor;

        [WriteOnly]
        public NativeArray<Vector3> Output;

        public void Execute()
        {

            var random = Unity.Mathematics.Random.CreateFromIndex(seed);

            if (Output.Length == originalLength)
            {
                for (int i = 0; i < Input.Length; i++)
                {
                    Output[i] = localToWorldMatrix.MultiplyPoint3x4(Input[i]);
                }
            }

            else
            {
                for (int i = 0; i < originalLength; i++)
                {
                    Output[i] = localToWorldMatrix.MultiplyPoint3x4(Input[i]);
                }

                for(int i = originalLength; i < Output.Length; i++)
                {
                    Output[i] = localToWorldMatrix.MultiplyPoint3x4(Input[i%originalLength] 
                        + new Vector3(random.NextFloat()*factor, random.NextFloat()*factor, random.NextFloat()*factor));
                }
            }




        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct Initialisation_noRandom : IJob
    {
        [ReadOnly]
        public NativeArray<Vector3> Input;
        public Matrix4x4 localToWorldMatrix;
        public Matrix4x4 worldToLocalMatrix;
        public int originalLength;

        [WriteOnly]
        public NativeArray<Vector3> Output;

        public void Execute()
        {

            if (Output.Length == originalLength)
            {
                for (int i = 0; i < Input.Length; i++)
                {
                    Output[i] = localToWorldMatrix.MultiplyPoint3x4(Input[i]);
                }
            }

            else
            {
                for (int i = 0; i < originalLength; i++)
                {
                    Output[i] = localToWorldMatrix.MultiplyPoint3x4(Input[i]);
                }

                for (int i = originalLength; i < Output.Length; i++)
                {
                    Output[i] = localToWorldMatrix.MultiplyPoint3x4(Input[i % originalLength]);
                }
            }




        }
    }








    // Start is called before the first frame update
    void Start()
    {
        mesh  = GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshFilter>().mesh;
        originalLength = mesh.vertexCount;
        localToWorld = GameObject.FindGameObjectWithTag("Belly").transform.localToWorldMatrix;
        worldToLocal = GameObject.FindGameObjectWithTag("Belly").transform.worldToLocalMatrix;
    }








    // Update is called once per frame
    void Update()
    {

        if (!initialised)
        {


            RaycastHit hit;
            Ray ray = new Ray(this.transform.position, Gravity);

            //Debug.DrawRay(this.transform.position, Gravity, Color.red, 1000f);
            initialised = true;


            if (Physics.Raycast(ray, out hit, 1000f) && hit.transform.tag.Equals("Belly"))
            {
                queryingCentre = hit.point;
                queryingCentreFound = true;
            }
            else 
            {
                Debug.LogError("Invalid benchmark object position, try place this object above the model with tag Belly, press R to redo the initialisation");
            }

            if (BenchmarkFactor < mesh.vertexCount)
            {
                Debug.LogWarning("Using original mesh data for benchmark");
                BenchmarkFactor = mesh.vertexCount;
            }
            else Debug.Log("Vertices number: " + BenchmarkFactor);

            vertices = new Vector3[BenchmarkFactor];

            NativeArray<Vector3> jobInput = new NativeArray<Vector3>(mesh.vertices, Allocator.TempJob);
            NativeArray<Vector3> jobOutput = new NativeArray<Vector3>(new Vector3[BenchmarkFactor],Allocator.TempJob);



            if (RandomisedInit)
            {
                var initialisation = new Initialisation
                {
                    Input = jobInput,
                    localToWorldMatrix = localToWorld,
                    worldToLocalMatrix = worldToLocal,
                    Output = jobOutput,
                    originalLength = originalLength,
                    seed = (uint)UnityEngine.Random.Range(0, 4096),
                    factor = randomisedFactor

                };
                initialisation.Schedule().Complete();

            }
            else
            {
                var initialisation = new Initialisation_noRandom
                {
                    Input = jobInput,
                    localToWorldMatrix = localToWorld,
                    worldToLocalMatrix = worldToLocal,
                    Output = jobOutput,
                    originalLength = originalLength

                };
                initialisation.Schedule().Complete();

            }





            vertices = jobOutput.ToArray();
            jobInput.Dispose();
            jobOutput.Dispose();

            Debug.Log("Initialisation for benchmark complete, size of vertices: "+vertices.Length);



        }

        if (Input.GetKeyDown("r"))
        {
            initialised = false;
            Debug.Log("Redoing initialisation");
        }



        if (Input.GetKeyDown("b")&&queryingCentreFound)
        {
            var resultIndices = new List<int>();

            if (strucutre == BenchMarkType.LinearSolution)
            {
                long startstep = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                for(int i = 0; i < BenchmarkFactor; i++)
                {
                    if (Vector3.Distance(queryingCentre, vertices[i]) <= Radius)
                    {
                        if (!resultIndices.Contains(i))
                        {
                            resultIndices.Add(i);
                        }
                    }
                }
                long endstep = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                Debug.Log("Linear solution cost: "+ (endstep - startstep) + " ms, total of "+ resultIndices.Count + " nodes were found");
                resultIndices.Clear();

            }

            if (strucutre == BenchMarkType.KDTree)
            {
                long startstep = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                KDTree tree = new KDTree(vertices,32);
                KDQuery query = new KDQuery();
                long initstep = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                Debug.Log("Init cost: " + (initstep - startstep)+" ms");
                query.Radius(tree, queryingCentre, Radius, resultIndices);

                long querystep = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                Debug.Log("KDTree solution cost: " + (querystep - initstep) + " ms, total of " + resultIndices.Count + " nodes were found");
                Debug.Log("Total cost: " + (querystep - startstep));
                resultIndices.Clear();


            }

            if (strucutre == BenchMarkType.BurstCompiled)
            {
                long startstep = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                NativeArray<Vector3> jobInput = new NativeArray<Vector3>(vertices,Allocator.TempJob);
                NativeArray<int> jobOutput = new NativeArray<int>(vertices.Length,Allocator.TempJob);

                var BurstQurying = new DataStructures.ViliWonka.Tests.Deformation.BruteForceQuerying
                {
                    WorldPositionVertices = jobInput,
                    radius = Radius,
                    queryingCenter = queryingCentre,
                    result = jobOutput
                };

                BurstQurying.Schedule().Complete();
                int qCounter = 0;
                while (qCounter<jobOutput.Length && jobOutput[qCounter] >= 0)
                {
                    resultIndices.Add(jobOutput[qCounter]);
                    qCounter++;
                }
                jobInput.Dispose();
                jobOutput.Dispose();
                long endstep = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                Debug.Log("Burst solution cost: " + (endstep - startstep) + " ms, total of " + resultIndices.Count + " nodes were found");



                resultIndices.Clear();


            }

            if (strucutre == BenchMarkType.ParalleledNoBurst)
            {
                long startstep = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                NativeArray<Vector3> jobInput = new NativeArray<Vector3>(vertices, Allocator.TempJob);
                NativeArray<int> jobOutput = new NativeArray<int>(vertices.Length, Allocator.TempJob);

                var BurstQurying = new DataStructures.ViliWonka.Tests.Deformation.BruteForceQuerying_noBurst
                {
                    WorldPositionVertices = jobInput,
                    radius = Radius,
                    queryingCenter = queryingCentre,
                    result = jobOutput
                };

                BurstQurying.Schedule().Complete();
                int qCounter = 0;
                while (qCounter<jobOutput.Length && jobOutput[qCounter] >= 0)
                {
                    resultIndices.Add(jobOutput[qCounter]);
                    qCounter++;
                }
                jobInput.Dispose();
                jobOutput.Dispose();
                long endstep = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                Debug.Log("Simple paralleled solution cost: " + (endstep - startstep) + " ms, total of " + resultIndices.Count + " nodes were found");
                resultIndices.Clear();
            }
        }


    }
}
