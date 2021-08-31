using DataStructures.ViliWonka.KDTree;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestController : MonoBehaviour
{
    public Camera camera;
    public float incisionDepth;
    public Material[] mat = new Material[2];
    public Vector3 gravity;
    public int iterationStep = 100;

    BasicCut instance;

    Plane currentPlane;
    public static List<Plane> allPlanes;

    private List<VertexMovement> movingVertices = new List<VertexMovement>();
    private int updateCounter = 0;



    // Start is called before the first frame update
    void Awake()
    {

        if(instance==null)
        instance = new BasicCut(camera, incisionDepth,gravity,mat);

        allPlanes = new List<Plane>();
    }
    
    

    
    // ref: http://wiki.unity3d.com/index.php/Mathfx#C.23_-_Mathfx.cs
    public static float Berp(float start, float end, float value)
    {
        value = Mathf.Clamp01(value);
        value = (Mathf.Sin(value * Mathf.PI * (0.2f + 2.5f * value * value * value)) * Mathf.Pow(1f - value, 2.2f) + value) * (1f + (1.2f * (1f - value)));
        return start + (end - start) * value;
    }

    public static Vector3 Berp(Vector3 start, Vector3 end, float value)
    {
        return new Vector3(Berp(start.x, end.x, value), Berp(start.y, end.y, value), Berp(start.z, end.z, value));
    }



    void Update()
    {
        Mesh mesh = instance.Belly.transform.GetComponent<MeshFilter>().mesh;
        int[] originalTri = mesh.triangles;
        Vector3[] verts = mesh.vertices;
        int[] submesh = mesh.GetTriangles(1);



        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, 1000.0f))
            {
                if (!instance.relatedTri.Contains(hit.triangleIndex))
                {
                    instance.relatedTri.Add(hit.triangleIndex);
                }
            }
            instance.incisionStart = hit.point;
            instance.isCapturingMovement = true;
        }


        if (Input.GetKeyDown(KeyCode.Return))
        {
            RaycastHit hit;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, 1000.0f))
            {
                if (!instance.relatedTri.Contains(hit.triangleIndex))
                {
                    instance.relatedTri.Add(hit.triangleIndex);
                }
            }
            int Count = originalTri.Length;

            instance.incisionEnd = hit.point;






            Vector3 tempPoint = instance.incisionStart + gravity;

            Vector3 difference = instance.incisionStart - instance.incisionEnd;

            int negativeCounter = 0;
            if (difference.x < 0) negativeCounter++;
            if (difference.y < 0) negativeCounter++;
            if (difference.z < 0) negativeCounter++;
            Plane temp;
            if (negativeCounter >= 2) temp = new Plane(tempPoint, instance.incisionEnd, instance.incisionStart);
            else temp = new Plane(tempPoint, instance.incisionStart, instance.incisionEnd);



            BasicCut.cutPlane = temp;
            currentPlane = BasicCut.cutPlane;

            Debug.Log("Cutplane Normal " + currentPlane.normal);
            Debug.Log("Difference of the s and e " + (instance.incisionStart - instance.incisionEnd));

            allPlanes.Add(currentPlane);
            



            Vector3 accumulation = (instance.incisionEnd - instance.incisionStart) / Count;
            instance.relatedTri.Clear();
            for (int i = 0; i < Count; i++)
            {
                RaycastHit triangleHit;
                instance.RaycastAssistant.transform.position = instance.incisionStart + i * accumulation - gravity * 20;
                Ray triangleRay = new Ray(instance.RaycastAssistant.transform.position, instance.incisionStart + i * accumulation - instance.RaycastAssistant.transform.position);
                //Debug.DrawRay(camera.transform.position, instance.incisionStart + i * accumulation - instance.RaycastAssistant.transform.position, Color.red, 10000f);
                Debug.DrawRay(camera.transform.position, instance.incisionStart - camera.transform.position, Color.red, 10000f);
                if (Physics.Raycast(triangleRay, out triangleHit, 1000.0f))
                {
                    if (!instance.relatedTri.Contains(triangleHit.triangleIndex)) instance.relatedTri.Add(triangleHit.triangleIndex);

                }
            }
            Debug.Log("Went through: " + instance.relatedTri.Count + " triangles");


            List<int> cuttedIndices = new List<int>();
            List<int> paralleledIndices = new List<int>();

            Vector3 Intersection;
            for (int i = 0; i < instance.relatedTri.Count; i++)
            {
                Vector3 p0 = instance.localToWorld.MultiplyPoint3x4(verts[originalTri[instance.relatedTri[i] * 3]]);
                Vector3 p1 = instance.localToWorld.MultiplyPoint3x4(verts[originalTri[instance.relatedTri[i] * 3 + 1]]);
                Vector3 p2 = instance.localToWorld.MultiplyPoint3x4(verts[originalTri[instance.relatedTri[i] * 3 + 2]]);


                int cutcheckResult = instance.CutCheck(out Intersection, 
                    verts[originalTri[instance.relatedTri[i] * 3]], 
                    verts[originalTri[instance.relatedTri[i] * 3 + 1]], 
                    verts[originalTri[instance.relatedTri[i] * 3 + 2]]);
                if (cutcheckResult == 2)
                {
                    cuttedIndices.Add(instance.relatedTri[i]);

                }
                else if (cutcheckResult == 0)
                {
                    paralleledIndices.Add(instance.relatedTri[i]);

                    Debug.DrawLine(p0, p1, Color.green, 1000f);
                    Debug.DrawLine(p1, p2, Color.green, 1000f);
                    Debug.DrawLine(p2, p0, Color.green, 1000f);
                }
            }





            Debug.Log("Cuttedindices.size " + cuttedIndices.Count);

            cuttedIndices.Sort();

            for (int i = 0; i < cuttedIndices.Count; i++)
            {
                cuttedIndices[i] += i * 2;
                instance.PlaneVC(originalTri, verts, submesh, instance.WorldToLocal.MultiplyPoint3x4(instance.incisionStart), instance.WorldToLocal.MultiplyPoint3x4(instance.incisionEnd), cuttedIndices[i], out verts, out originalTri, out submesh);

            }
            Vector3 midpoint = instance.incisionStart + (instance.incisionEnd - instance.incisionStart) / 2;
            midpoint = instance.WorldToLocal.MultiplyPoint3x4(midpoint);

            float radius = Vector3.Distance(instance.WorldToLocal.MultiplyPoint3x4(instance.incisionStart), instance.WorldToLocal.MultiplyPoint3x4(instance.incisionEnd)) /2;

            KDQuery query = new KDQuery();
            KDTree kdtree = new KDTree(verts, 32);
            List<int> vertsToMove = new List<int>();

            query.Radius(kdtree, midpoint, radius, vertsToMove);
            movingVertices.Clear();

            foreach(var i in vertsToMove)
            {
                if (BasicCut.positive_index.Contains(i))
                {
                    //movingVertices.Add(new VertexMovement(i, verts[i] + instance.WorldToLocal.MultiplyPoint3x4(BasicCut.cutPlane.normal) * 0.003f));
                    movingVertices.Add(new VertexMovement(i, verts[i] + BasicCut.cutPlane.normal * 0.003f));
                    continue;
                }
                else if (BasicCut.negative_index.Contains(i))
                {
                    movingVertices.Add(new VertexMovement(i, verts[i] - BasicCut.cutPlane.normal * 0.003f));
                    continue;
                }

                if (BasicCut.cutPlane.GetSide(instance.localToWorld.MultiplyPoint3x4(verts[i])))
                {
                    movingVertices.Add(new VertexMovement(i, verts[i] + BasicCut.cutPlane.normal * 0.003f));

                }
                else
                {
                    movingVertices.Add(new VertexMovement(i, verts[i] - BasicCut.cutPlane.normal* 0.003f));

                }
            }




            instance.UpdateMesh(verts, originalTri, submesh);
            //int lastRelatedIndex = instance.relatedTri[instance.relatedTri.Count - 1];
            instance.relatedTri.Clear();
            updateCounter = 0;
            instance.isUpdating = true;
            instance.isCapturingMovement = false;

        }

        if (instance.isCapturingMovement)
        {
            RaycastHit hit;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            Physics.Raycast(ray, out hit, 1000.0f);
            if (!instance.relatedTri.Contains(hit.triangleIndex))
            {
                instance.relatedTri.Add(hit.triangleIndex);
            }
        }


        if (instance.isUpdating)
        {

            float iterationSpeed = (float)((float)1 / (float)iterationStep);
            Ray currentPointing = new Ray(camera.transform.position, instance.incisionEnd - camera.transform.position);
            //Debug.DrawRay(camera.transform.position, incisionEnd - camera.transform.position, Color.green, 1000f);
            RaycastHit currentTri;
            int currentIndex;
            Vector3 Intersection;
            Physics.Raycast(currentPointing, out currentTri, 1000.0f);
            currentIndex = currentTri.triangleIndex;
            float speed = 1f;
            //float speed = Berp(0.002f,0.015f,1f);

            //float speed = 0.01f;
            foreach(var i in movingVertices)
            {
                //verts[i.index] = Vector3.MoveTowards(verts[i.index], i.Destination, speed * Time.deltaTime);
                verts[i.index] = Berp(verts[i.index],i.Destination,updateCounter*iterationSpeed);
                //verts[i.index] = Berp(verts[i.index],i.Destination,0.07f);
            }

            //Debug.Log(speed * Time.deltaTime);
            instance.UpdateMesh(verts, mesh.GetTriangles(0), submesh);


            instance.incisionStart = currentTri.point;
            updateCounter++;
            //Debug.Log("UpdateCounter" + updateCounter);
            if (verts[movingVertices[0].index] == movingVertices[0].Destination||updateCounter == iterationStep)
            {
                instance.isUpdating = false;
                Debug.Log("Update complete");
                updateCounter = 0;

            }

            //instance.isUpdating = false;
            instance.isCapturingMovement = true;
        }


    }
}

