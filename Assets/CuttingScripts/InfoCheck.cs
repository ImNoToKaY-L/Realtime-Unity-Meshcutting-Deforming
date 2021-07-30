using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
public class InfoCheck : MonoBehaviour
{
    long timecost = 0;
    int counter = 0;
    public GameObject sphere;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("i"))
        {
            Mesh mesh = transform.GetComponent<MeshFilter>().mesh;
            int[] originalTri = mesh.triangles;
            Vector3[] verts = mesh.vertices;
            int[] submesh = mesh.GetTriangles(1);

            Debug.Log("Current mesh has vertices: "+verts.Length);
            Debug.Log("     has triangles: " + originalTri.Length);

        }

        if (Input.GetMouseButtonDown(0))
        {
            Mesh mesh = transform.GetComponent<MeshFilter>().mesh;
            int[] originalTri = mesh.triangles;
            Vector3[] verts = mesh.vertices;
            int[] submesh = mesh.GetTriangles(1);

            Ray ray = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            Physics.Raycast(ray, out hit, 1000f);




            Debug.Log("Hit BC" + hit.barycentricCoordinate);
            Instantiate(sphere, hit.point, Quaternion.identity, this.transform);

            Instantiate(sphere, this.transform.localToWorldMatrix.MultiplyPoint3x4(verts[originalTri[hit.triangleIndex * 3]]), Quaternion.identity, this.transform);
            Instantiate(sphere, this.transform.localToWorldMatrix.MultiplyPoint3x4(verts[originalTri[hit.triangleIndex * 3+1]]), Quaternion.identity, this.transform);
            Instantiate(sphere, this.transform.localToWorldMatrix.MultiplyPoint3x4(verts[originalTri[hit.triangleIndex * 3+2]]), Quaternion.identity, this.transform);




        }

        if (Input.GetKeyDown("u"))
        {
            long timestep = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;


            Mesh mesh = transform.GetComponent<MeshFilter>().mesh;
            int[] originalTri = mesh.triangles;
            Vector3[] verts = mesh.vertices;
            int[] submesh = mesh.GetTriangles(1);
            Destroy(this.gameObject.GetComponent<MeshCollider>());
            Vector2[] uvs = new Vector2[verts.Length];

            for (int i = 0; i < uvs.Length; i++)
            {
                uvs[i] = new Vector2(verts[i].x, verts[i].z);
            }

            transform.GetComponent<MeshFilter>().mesh.vertices = verts;
            transform.GetComponent<MeshFilter>().mesh.uv = uvs;
            transform.GetComponent<MeshFilter>().mesh.triangles = originalTri;
            //transform.GetComponent<MeshFilter>().mesh.triangles = triangles;
            transform.GetComponent<MeshFilter>().mesh.RecalculateNormals();

            this.gameObject.AddComponent<MeshCollider>();
            timecost += (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - timestep;
            counter++;
            Debug.Log("Update time cost: " + (timecost/counter));
        }
    }
}
