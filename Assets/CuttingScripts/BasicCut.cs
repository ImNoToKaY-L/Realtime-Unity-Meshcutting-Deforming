using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicCut : MonoBehaviour
{
    // Start is called before the first frame update
    public Camera camera;
    public GameObject sphere;
    void Start()
    {
        Mesh mesh = transform.GetComponent<MeshFilter>().mesh;
        Vector3[] meshVertices = mesh.vertices;
    }

    void basicDelete(int index1,int index2)
    {
        Destroy(this.gameObject.GetComponent<MeshCollider>());
        Mesh mesh = transform.GetComponent<MeshFilter>().mesh;
        int[] originalTri = mesh.triangles;
        int[] newTri = new int[mesh.triangles.Length - 3];

        int i = 0;
        int j = 0;
        long timer = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

        //while (j < mesh.triangles.Length)
        //{
        //    if (j != index1 * 3 && j != index2*3)
        //    {
        //        newTri[i++] = originalTri[j++];
        //        newTri[i++] = originalTri[j++];
        //        newTri[i++] = originalTri[j++];

        //    }
        //    else
        //    {
        //        j += 3;
        //    }
        //}

        int smallerIndex = index1 > index2 ? index2 : index1;
        int biggerIndex = index1 > index2 ? index1 : index2;

        Debug.Log("triangle number: " + originalTri.Length);
        Debug.Log("Vertices number: " + mesh.vertices.Length);


        Array.Copy(originalTri, 0, newTri, 0, smallerIndex * 3);
        Array.Copy(originalTri, (smallerIndex + 1) * 3, newTri, smallerIndex * 3, biggerIndex*3 - (smallerIndex + 1) * 3);
        Array.Copy(originalTri, (biggerIndex + 1) * 3, newTri, biggerIndex * 3, originalTri.Length - (biggerIndex + 1) * 3);

        long timer2 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

        Debug.Log("Looping complete, time cost " + (timer2 - timer));
        transform.GetComponent<MeshFilter>().mesh.triangles = newTri;
        this.gameObject.AddComponent<MeshCollider>();
        long timer3 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        Debug.Log("Mesh creation complete, time: " + (timer3 - timer2));
    }


    int findVertex(Vector3 v)
    {
        Vector3[] vertices = transform.GetComponent<MeshFilter>().mesh.vertices;
        for(int i = 0; i < vertices.Length; i++)
        {
            if (vertices[i] == v) return i;
        }
        return -1;
    }

    int findTriangle(Vector3 v1, Vector3 v2,int notTriIndex)
    {
        int[] triangles = transform.GetComponent<MeshFilter>().mesh.triangles;
        Vector3[] vertices = transform.GetComponent<MeshFilter>().mesh.vertices;
        int i = 0;
        int j = 0;
        int found = 0;
        while (j < triangles.Length)
        {
            if(j/3 != notTriIndex)
            {
                if (vertices[triangles[j]] == v1 && (vertices[triangles[j + 1]] == v2 || vertices[triangles[j + 2]] == v2))
                    return j / 3;
                else if (vertices[triangles[j]] == v2 && (vertices[triangles[j + 1]] == v1 || vertices[triangles[j + 2]] == v1))
                    return j / 3;
                else if (vertices[triangles[j + 1]] == v2 && (vertices[triangles[j]] == v1 || vertices[triangles[j + 2]] == v1))
                    return j / 3;
                else if (vertices[triangles[j + 1]] == v1 && (vertices[triangles[j]] == v2 || vertices[triangles[j + 2]] == v2))
                    return j / 3;
            }

            j += 3;
        }
        return -1;
    }
    //private void IncisionCreation(int index)
    //{
    //    Destroy(this.gameObject.GetComponent<MeshCollider>());
    //    Mesh mesh = transform.GetComponent<MeshFilter>().mesh;
    //    int[] originalTri = mesh.triangles;
    //    int[] newTri = new int[mesh.triangles.Length + 6];
    //    Vector3[] newVert = new Vector3[mesh.vertices.Length + 4];


    //    Vector3[] vertices = mesh.vertices;



    //    Vector3 v1 = vertices[originalTri[index * 3]];
    //    Vector3 v3 = vertices[originalTri[index * 3 + 2]];
    //    Vector3 v2 = vertices[originalTri[index * 3 + 1]];
    //    int indexV1 = originalTri[index * 3];
    //    int indexV2 = originalTri[index * 3 + 1];
    //    int indexV3 = originalTri[index * 3 + 2];




    //    Vector3 v13 = (v1 + v3) / 2;
    //    Vector3 v12 = (v1 + v2) / 2;

    //    Vector3 v131 = midpointGeneration(v13, v1);
    //    Vector3 v133 = midpointGeneration(v13, v3);
    //    Vector3 v121 = midpointGeneration(v12, v1);
    //    Vector3 v122 = midpointGeneration(v12, v2);


    //    Array.Copy(mesh.vertices, newVert, mesh.vertices.Length);
    //    newVert[mesh.vertices.Length] = v131;
    //    newVert[mesh.vertices.Length + 1] = v133;
    //    newVert[mesh.vertices.Length + 2] = v121;
    //    newVert[mesh.vertices.Length + 3] = v122;

    //    Array.Copy(originalTri, newTri, index * 3);
    //    newTri[index * 3] = indexV1;//index of v1
    //    newTri[index * 3 + 2] = mesh.vertices.Length;//index of v131
    //    newTri[index * 3 + 1] = mesh.vertices.Length + 2;//index of v121

    //    newTri[index * 3 + 3] = mesh.vertices.Length + 1;//index of v133
    //    newTri[index * 3 + 4] = indexV2;//index of v2
    //    newTri[index * 3 + 5] = indexV3;//index of v3

    //    newTri[index * 3 + 6] = mesh.vertices.Length + 1;//index of v133
    //    newTri[index * 3 + 7] = mesh.vertices.Length + 3;//index of v122
    //    newTri[index * 3 + 8] = indexV2;//index of v2

    //    Array.Copy(originalTri, (index + 1) * 3, newTri, index * 3 + 9, originalTri.Length - index * 3 - 3);

    //    Vector2[] uvs = new Vector2[newVert.Length];

    //    for (int i = 0; i < uvs.Length; i++)
    //    {
    //        uvs[i] = new Vector2(newVert[i].x, newVert[i].z);
    //    }

    //    transform.GetComponent<MeshFilter>().mesh.vertices = newVert;
    //    transform.GetComponent<MeshFilter>().mesh.uv = uvs;

    //    transform.GetComponent<MeshFilter>().mesh.triangles = newTri;
    //    mesh.RecalculateNormals();

    //    this.gameObject.AddComponent<MeshCollider>();
    //}



    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            RaycastHit hit;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            if(Physics.Raycast(ray,out hit, 1000.0f))
            {
                int hitTri = hit.triangleIndex;

                int[] triangles = transform.GetComponent<MeshFilter>().mesh.triangles;
                Vector3[] vertices = transform.GetComponent<MeshFilter>().mesh.vertices;
                Vector3 p0 = vertices[triangles[hitTri * 3]];
                Vector3 p1 = vertices[triangles[hitTri * 3+1]];
                Vector3 p2 = vertices[triangles[hitTri * 3+2]];

                float edge1 = Vector3.Distance(p0, p1);
                float edge2 = Vector3.Distance(p0, p2);
                float edge3 = Vector3.Distance(p1, p2);
                
                Vector3 shared1;
                Vector3 shared2;

                if (edge1 > edge2 && edge1 > edge3)
                {
                    shared1 = p0;
                    shared2 = p1;
                }
                else if(edge2 > edge1 && edge2 > edge3)
                {
                    shared1 = p0;
                    shared2 = p2;
                }
                else
                {
                    shared1 = p1;
                    shared2 = p2;
                }

                int v1 = findVertex(shared1);
                int v2 = findVertex(shared2);
                basicDelete(hitTri, findTriangle(vertices[v1], vertices[v2], hitTri));


            }



        }
    }
}
