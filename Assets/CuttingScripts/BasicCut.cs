using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;

public class BasicCut
{
    // Paased in parameters
    public Camera camera;
    public float incisionDepth = 3f;
    public float judgingLimit = 0.8f;
    public Vector3 gravity;
    public Material[] mat;


    //Transform related
    public GameObject RaycastAssistant;
    public GameObject Belly;
    public Matrix4x4 localToWorld;
    public Matrix4x4 WorldToLocal;

    //Static storage of incision
    public static HashSet<int> positive_index = new HashSet<int>();
    public static HashSet<int> negative_index = new HashSet<int>();
    public static HashSet<int> bot_index = new HashSet<int>();


    //Operational parameters
    public List<int> relatedTri;
    public Vector3 incisionStart;
    public Vector3 incisionEnd;
    public static Plane cutPlane;
    public MeshRenderer rend;
    public bool isCapturingMovement;
    public bool isUpdating;




    [BurstCompile(CompileSynchronously = true)]
    private struct SharedVertexSearch : IJob
    {
        [ReadOnly]
        public NativeArray<int> triangles;
        public int vertexA;
        public int vertexB;

        [WriteOnly]
        public NativeArray<int> sharedA;
        public NativeArray<int> sharedB;


        public void Execute()
        {
            int CounterA = 0;
            int CounterB = 0;
            for (int i = 0; i < triangles.Length / 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (triangles[i * 3 + j] == vertexA)
                    {
                        sharedA[CounterA] = i;
                        CounterA++;
                    }

                    if (triangles[i * 3 + j] == vertexB)
                    {
                        sharedB[CounterB] = i;
                        CounterB++;
                    }
                }
            }

        }
    }

    public BasicCut(Camera camera, float incisionDepth, Vector3 Gravity, Material[] mat)
    {

        this.camera = camera;
        this.incisionDepth = incisionDepth;
        this.gravity = Gravity;
        this.mat = mat;



        relatedTri = new List<int>();
        RaycastAssistant = GameObject.FindGameObjectWithTag("RCAssist");
        Belly = GameObject.FindGameObjectWithTag("Belly");
        Belly.transform.GetComponent<MeshFilter>().mesh.subMeshCount = 2;
        rend = Belly.transform.GetComponent<MeshRenderer>();
        localToWorld = Belly.transform.localToWorldMatrix;
        WorldToLocal = Belly.transform.worldToLocalMatrix;

        //Vector3 world_v = localToWorld.MultiplyPoint3x4(mf.mesh.vertices[i]);
    }



    public static bool LineLineIntersection(out Vector3 intersection, Vector3 linePoint1,
        Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2)
    {

        Vector3 lineVec3 = linePoint2 - linePoint1;
        Vector3 crossVec1and2 = Vector3.Cross(lineVec1, lineVec2);
        Vector3 crossVec3and2 = Vector3.Cross(lineVec3, lineVec2);

        float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);

        //is coplanar, and not parallel
        if (Mathf.Abs(planarFactor) < 0.0001f
             && crossVec1and2.sqrMagnitude > 0.0001f)
        {
            float s = Vector3.Dot(crossVec3and2, crossVec1and2) / crossVec1and2.sqrMagnitude;
            intersection = linePoint1 + (lineVec1 * s);
            return true;
        }
        else
        {
            intersection = Vector3.zero;
            return false;
        }
    }




    public static bool segIntersection(out Vector3 intersection, Vector3 seg1v1, Vector3 seg1v2, Vector3 seg2v1, Vector3 seg2v2)
    {
        Vector3 RayIntersection;
        Vector3 seg1Diff = seg1v2 - seg1v1;
        Vector3 seg2Diff = seg2v2 - seg2v1;
        if (LineLineIntersection(out RayIntersection, seg1v1, seg1Diff, seg2v2, seg2Diff))
        {
            float seg1Mag = seg1Diff.sqrMagnitude;
            float seg2Mag = seg2Diff.sqrMagnitude;
            if ((RayIntersection - seg1v1).sqrMagnitude <= seg1Mag
        && (RayIntersection - seg1v2).sqrMagnitude <= seg1Mag
        && (RayIntersection - seg2v1).sqrMagnitude <= seg2Mag
        && (RayIntersection - seg2v2).sqrMagnitude <= seg2Mag)
            {
                intersection = RayIntersection;
                return true;
            }
        }

        intersection = Vector3.zero;

        return false;
    }



    private void Common_incision_creation(int[] originalTri, Vector3[] vertices, int[] submesh, Vector3 startPoint, Vector3 endPoint, int index, int IV, int GV1, int GV2, int spOrder, int epOrder, out Vector3[] newvertices, out int[] newTriangles, out int[] subtri)
    {

        int[] newTri = new int[originalTri.Length + 6];
        Vector3[] newVert = new Vector3[vertices.Length + 6];



        Array.Copy(vertices, newVert, vertices.Length);
        newVert[vertices.Length] = startPoint;
        newVert[vertices.Length + 1] = endPoint;
        newVert[vertices.Length + 2] = startPoint;
        newVert[vertices.Length + 3] = endPoint;
        int intersect1 = spOrder == 1 ? vertices.Length : vertices.Length + 1;
        int intersect2 = spOrder == 1 ? vertices.Length + 1 : vertices.Length;
        int intersect3 = spOrder == 1 ? vertices.Length + 2 : vertices.Length + 3;
        int intersect4 = spOrder == 1 ? vertices.Length + 3 : vertices.Length + 2;

        if (cutPlane.GetSide(localToWorld.MultiplyPoint3x4(vertices[IV])))
        {
            positive_index.Add(intersect1);
            positive_index.Add(intersect2);
            negative_index.Add(intersect4);
            negative_index.Add(intersect3);

        }
        else
        {
            positive_index.Add(intersect3);
            positive_index.Add(intersect4);
            negative_index.Add(intersect1);
            negative_index.Add(intersect2);
        }



        Vector3 projectedPoint;
        Array.Copy(originalTri, newTri, index * 3);
        newTri[index * 3] = IV;
        newTri[index * 3 + 1] = intersect1;
        newTri[index * 3 + 2] = intersect2;

        projectedPoint = Vector3.Project((newVert[IV] - newVert[intersect1]), (newVert[intersect2] - newVert[intersect1]));


        newTri[index * 3 + 3] = GV1;
        newTri[index * 3 + 4] = GV2;
        newTri[index * 3 + 5] = intersect4;

        newTri[index * 3 + 6] = intersect4;
        newTri[index * 3 + 7] = intersect3;
        newTri[index * 3 + 8] = GV1;

        Vector3 BV1 = newVert[intersect1] + WorldToLocal.MultiplyPoint3x4(gravity) * incisionDepth;
        Vector3 BV2 = newVert[intersect2] + WorldToLocal.MultiplyPoint3x4(gravity) * incisionDepth;
        //Vector3 BV1 = newVert[intersect1] + gravity.normalized * incisionDepth;
        //Vector3 BV2 = newVert[intersect2] + gravity.normalized * incisionDepth;
        newVert[vertices.Length + 4] = BV1;
        newVert[vertices.Length + 5] = BV2;
        int B1 = vertices.Length + 4;
        int B2 = vertices.Length + 5;
        bot_index.Add(B1);
        bot_index.Add(B2);

        int subLength = 12;

        subtri = new int[subLength];
        int counter = 0;

        subtri[counter++] = intersect3;
        subtri[counter++] = B2;
        subtri[counter++] = B1;

        subtri[counter++] = intersect3;
        subtri[counter++] = intersect4;
        subtri[counter++] = B2;

        subtri[counter++] = intersect1;
        subtri[counter++] = B1;
        subtri[counter++] = B2;

        subtri[counter++] = intersect2;
        subtri[counter++] = intersect1;
        subtri[counter++] = B2;



        int[] addup = new int[subtri.Length + submesh.Length];
        Array.Copy(submesh, addup, submesh.Length);
        Array.Copy(subtri, 0, addup, submesh.Length, subtri.Length);
        subtri = addup;


        Array.Copy(originalTri, (index + 1) * 3, newTri, (index + 3) * 3, originalTri.Length - ((index + 1) * 3));
        newvertices = newVert;
        newTriangles = newTri;

    }


    private int EdgeLocate(Vector3[] vertices, Vector3 point, float[] edges)
    {
        float pv1 = Vector3.Distance(vertices[0], point);
        float pv2 = Vector3.Distance(vertices[1], point);
        float pv3 = Vector3.Distance(vertices[2], point);

        float[] lengthSub = new float[3];
        lengthSub[0] = pv1 + pv2 - edges[0];
        lengthSub[1] = pv1 + pv3 - edges[1];
        lengthSub[2] = pv2 + pv3 - edges[2];


        return Array.IndexOf(lengthSub, lengthSub.Min());

    }





    private bool startORendV2(Vector3 I1, Vector3 I2)
    {

        float I1ToStart = Vector3.Distance(I1, incisionStart);
        float I2ToStart = Vector3.Distance(I2, incisionStart);



        return I1ToStart < I2ToStart;
    }




    private bool planeintersect(out Vector3 Intersection, Vector3 planepoint1, Vector3 planepoint2, Vector3 segmentpoint1, Vector3 segmentpoint2, Vector3 gravitydir)
    {
        Vector3 tempPoint = planepoint1 + gravitydir;
        Plane p = new Plane(planepoint1, planepoint2, tempPoint);
        Vector3 s1 = localToWorld.MultiplyPoint3x4(segmentpoint1);
        Vector3 s2 = localToWorld.MultiplyPoint3x4(segmentpoint2);

        Vector3 dir = Vector3.Normalize(s2 - s1);
        Ray ray = new Ray(s1, dir);
        p.Raycast(ray, out float distance);
        Vector3 tempIntersection = ray.GetPoint(distance);
        //Instantiate(sphere, tempIntersection, Quaternion.identity, this.transform);
        float originalLength = Vector3.Distance(s1, s2);
        float addedUpLength = Vector3.Distance(s1, tempIntersection) + Vector3.Distance(s2, tempIntersection);


        if (p.GetSide(s1) != p.GetSide(s2))
        {
            Intersection = tempIntersection;
            return true;
        }
        //else if(addedUpLength<=originalLength*1.1f)
        //{
        //    Debug.Log("length edge");
        //    Intersection = tempIntersection;
        //    return true;
        //}
        else
        {
            //Debug.LogWarning("length difference: added:"+addedUpLength+" original:"+originalLength);
            Intersection = tempIntersection;
            return false;
        }



    }


    public int barycentricJudging(List<int> reshapeIndex, Vector3 Intersection, int index, int[] tri)
    {
        RaycastHit hit;
        Ray ray = new Ray(camera.transform.position, Intersection - camera.transform.position);
        Physics.Raycast(ray, out hit, 1000.0f);
        int reshapeCounter = 0;

        if (hit.barycentricCoordinate.x >= judgingLimit)
        {
            reshapeCounter++;
            reshapeIndex.Add(tri[index * 3]);
        }

        if (hit.barycentricCoordinate.y >= judgingLimit)
        {
            reshapeCounter++;
            reshapeIndex.Add(tri[index * 3 + 1]);
        }

        if (hit.barycentricCoordinate.z >= judgingLimit)
        {
            reshapeCounter++;
            reshapeIndex.Add(tri[index * 3 + 2]);
        }
        return reshapeCounter;
    }



    public int PlaneVC(int[] tri, Vector3[] vertices, int[] submesh, Vector3 startPoint, Vector3 endPoint, int index, out Vector3[] newvertices, out int[] newTriangles, out int[] subtri)
    {
        Vector3[] localVertices = new Vector3[3];
        Vector3 v1 = vertices[tri[index * 3]];
        Vector3 v2 = vertices[tri[index * 3 + 1]];
        Vector3 v3 = vertices[tri[index * 3 + 2]];

        int indexV1 = tri[index * 3];
        int indexV2 = tri[index * 3 + 1];
        int indexV3 = tri[index * 3 + 2];

        localVertices[0] = vertices[tri[index * 3]];
        localVertices[1] = vertices[tri[index * 3 + 1]];
        localVertices[2] = vertices[tri[index * 3 + 2]];
        float[] edges = new float[3];
        //index 0 refers to edge 12, index 1 to edge 13, index 2 to edge 23
        edges[0] = Vector3.Distance(v1, v2);
        edges[1] = Vector3.Distance(v1, v3);
        edges[2] = Vector3.Distance(v2, v3);

        Vector3 Intersection;
        Vector3 I1 = Vector3.zero, I2 = Vector3.zero;
        int counter = 0;
        int reshapeCounter = 0;
        List<int> reshapeIndex = new List<int>();
        if (planeintersect(out Intersection, incisionStart, incisionEnd, v1, v2, gravity))
        {
            //Instantiate(sphere, Intersection, Quaternion.identity, this.transform);
            if (I1 == Vector3.zero) I1 = Intersection;
            else I2 = Intersection;
            reshapeCounter += barycentricJudging(reshapeIndex, Intersection, index, tri);


            counter++;

        }
        if (planeintersect(out Intersection, incisionStart, incisionEnd, v1, v3, gravity))
        {
            //Instantiate(sphere, Intersection, Quaternion.identity, this.transform);


            if (I1 == Vector3.zero) I1 = Intersection;
            else I2 = Intersection;
            reshapeCounter += barycentricJudging(reshapeIndex, Intersection, index, tri);

            counter++;

        }
        if (planeintersect(out Intersection, incisionStart, incisionEnd, v3, v2, gravity))
        {
            //Instantiate(sphere, Intersection, Quaternion.identity, this.transform);
            if (I1 == Vector3.zero) I1 = Intersection;
            else I2 = Intersection;
            reshapeCounter += barycentricJudging(reshapeIndex, Intersection, index, tri);

            counter++;

        }
        if (counter != 2) Debug.LogError("INTERSECTION ERROR: " + counter);
        //Debug.Log("ReshapeCounter for index: "+ index + " is "+reshapeCounter);
        //Debug.Log("Size of reshaped vertices of : " + index + " is " + reshapeIndex.Count());
        //if(reshapeCounter!=0) Debug.Log("Index of reshaped vertices of : " + index + " is " + reshapeIndex[0]);


        startPoint = startORendV2(I1, I2) ? WorldToLocal.MultiplyPoint3x4(I1) : WorldToLocal.MultiplyPoint3x4(I2);
        endPoint = startORendV2(I1, I2) ? WorldToLocal.MultiplyPoint3x4(I2) : WorldToLocal.MultiplyPoint3x4(I1);




        int startPointEdge = EdgeLocate(localVertices, startPoint, edges);
        int endPointEdge = EdgeLocate(localVertices, endPoint, edges);


        //sum==1 means vertices are on e12 and e13, order doesnt metter so the same applies on the rest
        int sum = startPointEdge + endPointEdge;
        int individualVert = 0;
        int groupedVert1 = 1;
        int groupedVert2 = 2;
        int startOrder = 1;
        int endOrder = 2;
        switch (sum)
        {
            case 1:
                individualVert = indexV1;
                groupedVert1 = indexV2;
                groupedVert2 = indexV3;
                startOrder = startPointEdge == 0 ? 1 : 2;
                endOrder = endPointEdge == 1 ? 2 : 1;
                break;

            case 2:
                individualVert = indexV2;
                groupedVert1 = indexV3;
                groupedVert2 = indexV1;
                startOrder = startPointEdge == 0 ? 2 : 1;
                endOrder = endPointEdge == 0 ? 2 : 1;
                break;

            case 3:
                individualVert = indexV3;
                groupedVert1 = indexV1;
                groupedVert2 = indexV2;
                startOrder = startPointEdge == 1 ? 1 : 2;
                endOrder = endPointEdge == 1 ? 1 : 2;
                break;

            default:
                Debug.LogError("None of the cases are triggered, current sum: " + sum);
                break;
        }



        Common_incision_creation(tri, vertices, submesh, startPoint, endPoint, index, individualVert, groupedVert1, groupedVert2, startOrder, endOrder, out newvertices, out newTriangles, out subtri);


        //2 means next batch process should move the pointer by 2 since there are two newly created triangles
        return 2;


    }


    public void UpdateMesh(Vector3[] vertices, int[] triangles, int[] submesh)
    {
        GameObject.Destroy(Belly.gameObject.GetComponent<MeshCollider>());
        Vector2[] uvs = new Vector2[vertices.Length];

        rend.materials = mat;

        Belly.transform.GetComponent<MeshFilter>().mesh.vertices = vertices;
        Belly.transform.GetComponent<MeshFilter>().mesh.SetTriangles(triangles, 0);
        Belly.transform.GetComponent<MeshFilter>().mesh.SetTriangles(submesh, 1);
        Belly.transform.GetComponent<MeshFilter>().mesh.RecalculateNormals();

        Belly.gameObject.AddComponent<MeshCollider>();

    }


    private void DrawPlane(Vector3 position, Vector3 normal)
    {

        Vector3 v3;

        if (normal.normalized != Vector3.forward)
            v3 = Vector3.Cross(normal, Vector3.forward).normalized * normal.magnitude * 10;
        else
            v3 = Vector3.Cross(normal, Vector3.up).normalized * normal.magnitude * 10; ;

        var corner0 = position + v3;
        var corner2 = position - v3;
        var q = Quaternion.AngleAxis(90.0f, normal);
        v3 = q * v3;
        var corner1 = position + v3;
        var corner3 = position - v3;

        Debug.DrawLine(corner0, corner2, Color.green, 1000f);
        Debug.DrawLine(corner1, corner3, Color.green, 1000f);
        Debug.DrawLine(corner0, corner1, Color.green, 1000f);
        Debug.DrawLine(corner1, corner2, Color.green, 1000f);
        Debug.DrawLine(corner2, corner3, Color.green, 1000f);
        Debug.DrawLine(corner3, corner0, Color.green, 1000f);
        Debug.DrawRay(position, normal, Color.red, 1000f);
    }


    public int CutCheck(out Vector3 Intersection, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        int intersectCount = 0;
        Vector3 tempInter = Vector3.zero;


        if (planeintersect(out Intersection, incisionStart, incisionEnd, v1, v2, gravity))
        {
            intersectCount++;
            tempInter = Intersection;

        }

        if (planeintersect(out Intersection, incisionStart, incisionEnd, v1, v3, gravity))
        {
            intersectCount++;
            tempInter = Intersection;
        }


        if (planeintersect(out Intersection, incisionStart, incisionEnd, v2, v3, gravity))
        {
            intersectCount++;
            tempInter = Intersection;
        }


        Intersection = tempInter;
        //return true;

        if (intersectCount == 2)
        {

            return intersectCount;
        }

        else
        {
            Debug.LogWarning("Not intersected triangle detected, intersection count: " + intersectCount);

            //if(Intersection!=Vector3.zero)

            //Instantiate(redsphere, Intersection, Quaternion.identity, this.transform);

        }



        return intersectCount;
    }


    private int LocateVertex(Vector3 mousePos, int index, Mesh mesh)
    {
        int[] triangles = mesh.triangles;
        Vector3[] verts = mesh.vertices;
        float distance = float.MaxValue;
        int result = -1;

        for (int i = 0; i < 3; i++)
        {
            float currentDis = Vector3.Distance(verts[triangles[index * 3 + i]], WorldToLocal.MultiplyPoint3x4(mousePos));
            if (currentDis < distance)
            {
                result = triangles[index * 3 + i];
                distance = currentDis;
                Debug.Log("Index: " + triangles[index * 3 + i] + "Distance: " + currentDis);
            }
        }

        if (result != -1) return result;

        Debug.LogError("No near vertex located");
        return -1;
    }

}
