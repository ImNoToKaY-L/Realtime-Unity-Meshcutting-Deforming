

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

    public enum QType
    {

        ClosestPoint,
        KNearest,
        Radius,
        Interval
    }

    public class KDTest : MonoBehaviour
    {
        private Vector3[] vertices;

        private Vector3[] rendVert;

        [Range(0f, 100f)]
        public float Radius = 0.1f;

        public bool DrawQueryNodes = false;

        private Vector3 PreviousPos;

        private Matrix4x4 localToWorld;
        private Matrix4x4 worldToLocal;
        private bool initialised = false;


        KDTree tree;

        KDQuery query;

        void Awake()
        {
            localToWorld = GameObject.FindGameObjectWithTag("Belly").transform.localToWorldMatrix;
            worldToLocal = GameObject.FindGameObjectWithTag("Belly").transform.worldToLocalMatrix;
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
                    Mesh mesh = GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshFilter>().mesh;
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


                    query = new KDQuery();

                    PreviousPos = transform.position;
                    StartOfInitialisation = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    tree = new KDTree(vertices, 32);
                    Debug.Log("KDTree initialise cost: " + (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - StartOfInitialisation));

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

                    Color markColor = Color.red;
                    markColor.a = 0.05f;
                    //Gizmos.color = markColor;


                    query.Radius(tree, PreviousPos, Radius, resultIndices);


                    PreviousPos = transform.position;
                    Debug.Log("Result Indices size " + resultIndices.Count);
                    for (int i = 0; i < resultIndices.Count; i++)
                    {

                        if (tricut.cutPlane.GetSide(transform.position))
                        {
                            if (tricut.negative_index.Contains(resultIndices[i]) || tricut.bot_index.Contains(resultIndices[i])) continue;
                            if (tricut.positive_index.Contains(resultIndices[i]))
                            {
                                Vector3 direction = hit.point - vertices[resultIndices[i]];
                                Vector3 endRendVert = worldToLocal.MultiplyPoint3x4(localToWorld.MultiplyPoint3x4(rendVert[resultIndices[i]]) + direction * 0.1f);
                                Vector3 endVert = vertices[resultIndices[i]] + direction * 0.1f;
                                Vector3.Lerp(rendVert[resultIndices[i]], endRendVert, 0.1f);
                                Vector3.Lerp(vertices[resultIndices[i]], endVert, 0.1f);

                                rendVert[resultIndices[i]] = endRendVert;
                                vertices[resultIndices[i]] = endVert;
                                continue;
                            }
                        }
                        else
                        {
                            if (tricut.positive_index.Contains(resultIndices[i]) || tricut.bot_index.Contains(resultIndices[i])) continue;
                            if (tricut.negative_index.Contains(resultIndices[i]))
                            {
                                Vector3 direction = hit.point - vertices[resultIndices[i]];

                                Vector3 endRendVert = worldToLocal.MultiplyPoint3x4(localToWorld.MultiplyPoint3x4(rendVert[resultIndices[i]]) + direction * 0.1f);
                                Vector3 endVert = vertices[resultIndices[i]] + direction * 0.1f;
                                Vector3.Lerp(rendVert[resultIndices[i]], endRendVert, 0.1f);
                                Vector3.Lerp(vertices[resultIndices[i]], endVert, 0.1f);

                                rendVert[resultIndices[i]] = endRendVert;
                                vertices[resultIndices[i]] = endVert;
                                continue;
                            }
                        }


                        if (tricut.cutPlane.GetSide(transform.position) == tricut.cutPlane.GetSide(vertices[resultIndices[i]]))
                        {
                            Vector3 direction = hit.point - vertices[resultIndices[i]];

                            Vector3 endRendVert = worldToLocal.MultiplyPoint3x4(localToWorld.MultiplyPoint3x4(rendVert[resultIndices[i]]) + direction * 0.1f);
                            Vector3 endVert = vertices[resultIndices[i]] + direction * 0.1f;
                            Vector3.Lerp(rendVert[resultIndices[i]], endRendVert, 0.1f);
                            Vector3.Lerp(vertices[resultIndices[i]], endVert, 0.1f);

                            rendVert[resultIndices[i]] = endRendVert;
                            vertices[resultIndices[i]] = endVert;
                        }
                    }
                    

                    




                    //Gizmos.color = Color.green;
                    //Gizmos.DrawCube(transform.position, 1f * size);

                    if (DrawQueryNodes)
                    {
                        query.DrawLastQuery();
                    }

                    Destroy(GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshCollider>());
                    GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshFilter>().mesh.vertices = rendVert;
                    GameObject.FindGameObjectWithTag("Belly").GetComponent<MeshFilter>().mesh.RecalculateNormals();
                    GameObject.FindGameObjectWithTag("Belly").AddComponent<MeshCollider>();

                    long StartOfInitialisation = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                    tree = new KDTree(vertices, 32);
                    Debug.Log("KDTree update cost: " +(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - StartOfInitialisation));
                }

            }


        }

    }
}