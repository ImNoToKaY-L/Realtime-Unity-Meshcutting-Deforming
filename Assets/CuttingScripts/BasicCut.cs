using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class BasicCut : MonoBehaviour
{
    // Start is called before the first frame update
    public Camera camera;
    private bool isCapturingMovement;
    private bool isUpdating;

    private Vector3 incisionStart;
    private Vector3 incisionEnd;
    private List<int> relatedTri;
    public GameObject sphere;
    public float incisionDepth = 3f;
    public Matrix4x4 localToWorld;
    public Matrix4x4 WorldToLocal;

    bool isMoving = false;
    public int movingIndex;
    private List<VertMovement> expandingDirections = new List<VertMovement>();
    private List<Vertex> allVertices = new List<Vertex>();
    private HashSet<Vertex> movingVertices = new HashSet<Vertex>();
    private int originalVertLength;
    private Vector3 previousMousePos;
    MeshRenderer rend;

    public Material[] mat = new Material[2];

    private class VertMovement
    {
        public int vertIndex;
        public Vector3 direction;
        public VertMovement(int index, Vector3 dir)
        {
            vertIndex = index;
            direction = dir;
        }
    }

    public class Vertex
    {
        public int index;
        public HashSet<int> neighbours;
        public Dictionary<int, Vector3> directions;
        public int stickingto = -1;
        public Vertex(int index)
        {
            this.index = index;
            neighbours = new HashSet<int>();
            directions = new Dictionary<int, Vector3>();
        }

        public void addNeighbours(int neighbour)
        {
            neighbours.Add(neighbour);

        }
        public void stick(int stickto)
        {
            stickingto = stickto;
        }

        public void CalculateDir(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            foreach(var i in neighbours)
            {
                Vector3 neighbourPos = vertices[i];
                Vector3 currentDir = vertices[index] - neighbourPos;
                directions.Add(i, currentDir);
            }
        }
    }



    void Start()
    {
        relatedTri = new List<int>();
        originalVertLength = transform.GetComponent<MeshFilter>().mesh.vertexCount;
        transform.GetComponent<MeshFilter>().mesh.subMeshCount = 2;
        rend = this.transform.GetComponent<MeshRenderer>();
        localToWorld = this.transform.localToWorldMatrix;
        WorldToLocal = this.transform.worldToLocalMatrix;
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



    private void IncisionCreationTest(int[] originalTri, Vector3[] vertices,int[] submesh, Vector3 startPoint, Vector3 endPoint, int index, int IV, int GV1, int GV2, int spOrder, int epOrder, out Vector3[] newvertices, out int[] newTriangles, out int[]subtri)
    {

        int[] newTri = new int[originalTri.Length + 9];
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

        Vector3 BV1 = newVert[intersect1] + new Vector3(0f, -incisionDepth, 0f);
        Vector3 BV2 = newVert[intersect2] + new Vector3(0f, -incisionDepth, 0f);
        newVert[vertices.Length + 4] = BV1;
        newVert[vertices.Length + 5] = BV2;
        int B1 = vertices.Length + 4;
        int B2 = vertices.Length + 5;


        int subLength = 12;

        subtri = new int[subLength];
        int counter = 0;

        subtri[counter++] = intersect3;
        subtri[counter++]= B2;
        subtri[counter++]= B1;

        subtri[counter++]= intersect3;
        subtri[counter++]= intersect4;
        subtri[counter++]= B2;

        subtri[counter++]= intersect1;
        subtri[counter++]= B1;
        subtri[counter++]= B2;

        subtri[counter++]= intersect2;
        subtri[counter++]= intersect1;
        subtri[counter++]= B2;



        int[] addup = new int[subtri.Length + submesh.Length];
        Array.Copy(submesh, addup, submesh.Length);
        Array.Copy(subtri, 0, addup, submesh.Length, subtri.Length);
        subtri = addup;

        expandingDirections.Add(new VertMovement(intersect1, newVert[IV] - newVert[intersect1]));
        expandingDirections.Add(new VertMovement(intersect2, newVert[IV] - newVert[intersect2]));
        expandingDirections.Add(new VertMovement(intersect3, newVert[GV1] - newVert[intersect3]));
        expandingDirections.Add(new VertMovement(intersect4, newVert[GV2] - newVert[intersect4]));

        newVert[intersect1] += (newVert[IV] - newVert[intersect1]) * 0.05f;
        newVert[intersect2] += (newVert[IV] - newVert[intersect2]) * 0.05f;
        newVert[intersect3] += (newVert[GV1] - newVert[intersect3]) * 0.05f;
        newVert[intersect4] += (newVert[GV2] - newVert[intersect4]) * 0.05f;


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



    private bool startORend(Vector3 exPoint)
    {

        float exToStart = Vector3.Distance(exPoint, incisionStart);
        float exToEnd = Vector3.Distance(exPoint, incisionEnd);
        if (exToEnd == exToStart)
        {
            Debug.LogError("extreme edge condition detected");
            return false;
        }


        return exToStart < exToEnd;
    }



    private bool startORendV2(Vector3 I1, Vector3 I2)
    {

        float I1ToStart = Vector3.Distance(I1, incisionStart);
        float I2ToStart = Vector3.Distance(I2, incisionStart);



        return I1ToStart < I2ToStart;
    }


    private void VerticesCut(int[] tri, Vector3[] vertices,int[] submesh, Vector3 startPoint, Vector3 endPoint, int index, out Vector3[] newvertices, out int[] newTriangles,out int[] subtri)
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
        bool istriggered = false;
        if (segIntersection(out Intersection, incisionStart, incisionEnd, localToWorld.MultiplyPoint3x4(v1), localToWorld.MultiplyPoint3x4(v2)))
        {
            //Instantiate(sphere, Intersection, Quaternion.identity, this.transform);
            if (startORend(Intersection)) startPoint = WorldToLocal.MultiplyPoint3x4(Intersection);
            else endPoint = WorldToLocal.MultiplyPoint3x4(Intersection);
            //planeintersect(out Vector3 temp, incisionStart, incisionEnd, v1, v2, new Vector3(0, 1, 0));


            I1 = Intersection;

            istriggered = !istriggered;

        }
        if (segIntersection(out Intersection, incisionStart, incisionEnd, localToWorld.MultiplyPoint3x4(v1), localToWorld.MultiplyPoint3x4(v3)))
        {
            //Instantiate(sphere, Intersection, Quaternion.identity, this.transform);
            if (startORend(Intersection)) startPoint = WorldToLocal.MultiplyPoint3x4(Intersection);
            else endPoint = WorldToLocal.MultiplyPoint3x4(Intersection);

            //planeintersect(out Vector3 temp, incisionStart, incisionEnd, v1, v3, new Vector3(0, 1, 0));

            if (I1 == Vector3.zero) I1 = Intersection;
            else I2 = Intersection;

            istriggered = !istriggered;

        }
        if (segIntersection(out Intersection, incisionStart, incisionEnd, localToWorld.MultiplyPoint3x4(v2), localToWorld.MultiplyPoint3x4(v3)))
        {
            //Instantiate(sphere, Intersection, Quaternion.identity, this.transform);
            if (startORend(Intersection)) startPoint = WorldToLocal.MultiplyPoint3x4(Intersection);
            else endPoint = WorldToLocal.MultiplyPoint3x4(Intersection);
            //planeintersect(out Vector3 temp, incisionStart, incisionEnd, v3, v2, new Vector3(0, 1, 0));

            I2 = Intersection;

            istriggered = !istriggered;

        }
        if (istriggered) Debug.LogError("INTERSECTION ERROR");

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
        //IncisionCreation(tri,vertices,startPoint, endPoint, index, individualVert, groupedVert1, groupedVert2, startOrder, endOrder,out newvertices,out newTriangles);
        IncisionCreationTest(tri, vertices,submesh, startPoint, endPoint, index, individualVert, groupedVert1, groupedVert2, startOrder, endOrder, out newvertices, out newTriangles,out subtri);


    }


    private bool planeintersect(out Vector3 Intersection, Vector3 planepoint1, Vector3 planepoint2,Vector3 segmentpoint1, Vector3 segmentpoint2, Vector3 gravitydir)
    {
        Vector3 tempPoint = planepoint1 + gravitydir;
        Plane p = new Plane(planepoint1, planepoint2, tempPoint);
        Vector3 s1 = localToWorld.MultiplyPoint3x4(segmentpoint1);
        Vector3 s2 = localToWorld.MultiplyPoint3x4(segmentpoint2);
        Vector3 cp = p.normal;
        Vector3 dir = Vector3.Normalize(s2 - s1);
        Ray ray = new Ray(s1, dir);
        p.Raycast(ray, out float distance);
        Vector3 tempIntersection = ray.GetPoint(distance);
        //Instantiate(sphere, tempIntersection, Quaternion.identity, this.transform);
        float originalLength = Vector3.Distance(s1, s2);
        float addedUpLength = Vector3.Distance(s1, tempIntersection) + Vector3.Distance(s2, tempIntersection);
        Debug.Log("added length: " + addedUpLength+", original: "+originalLength);
        if (addedUpLength > originalLength*1.001f)
        {
            Intersection = Vector3.zero;
            return false;
        }
        else
        {
            Intersection = tempIntersection;
            return true;
        }


        Intersection = Vector3.zero;

        return false;
    }

   


    private void PlaneVC(int[] tri, Vector3[] vertices, int[] submesh, Vector3 startPoint, Vector3 endPoint, int index, out Vector3[] newvertices, out int[] newTriangles, out int[] subtri)
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
        if (planeintersect(out Intersection,incisionStart,incisionEnd,v1,v2,new Vector3(0,1,0)))
        {
            //Instantiate(sphere, Intersection, Quaternion.identity, this.transform);
            if (startORend(Intersection)) startPoint = WorldToLocal.MultiplyPoint3x4(Intersection);
            else endPoint = WorldToLocal.MultiplyPoint3x4(Intersection);


            I1 = Intersection;

            counter++;

        }
        if (planeintersect(out Intersection, incisionStart, incisionEnd, v1, v3, new Vector3(0, 1, 0)))
        {
            //Instantiate(sphere, Intersection, Quaternion.identity, this.transform);
            if (startORend(Intersection)) startPoint = WorldToLocal.MultiplyPoint3x4(Intersection);
            else endPoint = WorldToLocal.MultiplyPoint3x4(Intersection);


            if (I1 == Vector3.zero) I1 = Intersection;
            else I2 = Intersection;

            counter++;

        }
        if (planeintersect(out Intersection, incisionStart, incisionEnd, v3, v2, new Vector3(0, 1, 0)))
        {
            //Instantiate(sphere, Intersection, Quaternion.identity, this.transform);
            if (startORend(Intersection)) startPoint = WorldToLocal.MultiplyPoint3x4(Intersection);
            else endPoint = WorldToLocal.MultiplyPoint3x4(Intersection);

            I2 = Intersection;

            counter++;

        }
        if (counter!=2) Debug.LogError("INTERSECTION ERROR: "+counter);

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
        //IncisionCreation(tri,vertices,startPoint, endPoint, index, individualVert, groupedVert1, groupedVert2, startOrder, endOrder,out newvertices,out newTriangles);
        IncisionCreationTest(tri, vertices, submesh, startPoint, endPoint, index, individualVert, groupedVert1, groupedVert2, startOrder, endOrder, out newvertices, out newTriangles, out subtri);


    }


    private void UpdateMesh(Vector3[] vertices, int[] triangles,int[] submesh)
    {
        Destroy(this.gameObject.GetComponent<MeshCollider>());
        Vector2[] uvs = new Vector2[vertices.Length];

        //for (int i = 0; i < uvs.Length; i++)
        //{
        //    uvs[i] = new Vector2(-1*vertices[i].y, vertices[i].z);
        //}

        transform.GetComponent<MeshFilter>().mesh.vertices = vertices;
        //transform.GetComponent<MeshFilter>().mesh.uv = uvs;
        //transform.GetComponent<MeshFilter>().mesh.SetTriangles(triangles, 0);
        //transform.GetComponent<MeshFilter>().mesh.SetTriangles(submesh, 1);
        transform.GetComponent<MeshFilter>().mesh.triangles = triangles;
        //rend.materials = mat;
        transform.GetComponent<MeshFilter>().mesh.RecalculateNormals();

        this.gameObject.AddComponent<MeshCollider>();

    }



    private bool CutCheck(out Vector3 Intersection, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        int intersectCount = 0;
        Vector3 tempInter = Vector3.zero;
        //if (segIntersection(out Intersection, incisionStart, incisionEnd, localToWorld.MultiplyPoint3x4(v1), localToWorld.MultiplyPoint3x4(v2)))
        //{
        //    intersectCount++;
        //    tempInter = Intersection;
        //}

        //if (segIntersection(out Intersection, incisionStart, incisionEnd, localToWorld.MultiplyPoint3x4(v3), localToWorld.MultiplyPoint3x4(v1)))
        //{
        //    intersectCount++;
        //    tempInter = Intersection;
        //}


        //if (segIntersection(out Intersection, incisionStart, incisionEnd, localToWorld.MultiplyPoint3x4(v2), localToWorld.MultiplyPoint3x4(v3)))
        //{
        //    intersectCount++;
        //    tempInter = Intersection;
        //}

        if (planeintersect(out Intersection, incisionStart, incisionEnd, v1, v2, new Vector3(0, 1, 0)))
        {
            intersectCount++;
            tempInter = Intersection;
        }

        if (planeintersect(out Intersection, incisionStart, incisionEnd, v1, v3, new Vector3(0, 1, 0)))
        {
            intersectCount++;
            tempInter = Intersection;
        }


        if (planeintersect(out Intersection, incisionStart, incisionEnd, v2, v3, new Vector3(0, 1, 0)))
        {
            intersectCount++;
            tempInter = Intersection;
        }


        Intersection = tempInter;

        if (intersectCount == 2)
        {

            return true;
        }
        else
        {
        }


        return false;
    }


    private int LocateVertex(Vector3 mousePos,int index,Mesh mesh)
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

    private Vector3 DirectionSelection(Vertex moving,Vector3 currentMousPos,out int movinginto,Mesh mesh)
    {
        Vector3 resultDir = Vector3.zero;
        float angle = float.MaxValue;
        Vector3[] vertices = mesh.vertices;
        movinginto = -1;
        Dictionary<int, Vector3> currentDir = new Dictionary<int, Vector3>();


        foreach(var neighbour in moving.neighbours)
        {
            currentDir.Add(neighbour, vertices[moving.index] - vertices[neighbour]);
        }


        foreach(var dir in currentDir)
        {
            float currentAngle = Vector3.Angle(dir.Value, WorldToLocal.MultiplyPoint3x4(previousMousePos)  - WorldToLocal.MultiplyPoint3x4(currentMousPos));

            if (currentAngle < angle)
            {
                angle = currentAngle;
                resultDir = dir.Value;
                movinginto = dir.Key;
            }
        }

        return resultDir;
    }





    // Update is called once per frame
    void Update()
    {

        Mesh mesh = transform.GetComponent<MeshFilter>().mesh;
        int[] originalTri = mesh.triangles;
        Vector3[] verts = mesh.vertices;
        //int[] submesh = mesh.GetTriangles(1);
        int[] submesh = mesh.GetTriangles(0);

        if (Input.GetMouseButtonDown(1))
        {
            long startOfPreindexing = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            allVertices.Clear();

            for (int i = 0; i < verts.Length; i++)
            {
                allVertices.Add(new Vertex(i));
            }

            for (int i = 0; i < mesh.GetTriangles(0).Length / 3; i++)
            {
                allVertices[mesh.GetTriangles(0)[i * 3]].addNeighbours(mesh.GetTriangles(0)[i * 3 + 1]);
                allVertices[mesh.GetTriangles(0)[i * 3]].addNeighbours(mesh.GetTriangles(0)[i * 3 + 2]);

                allVertices[mesh.GetTriangles(0)[i * 3 + 1]].addNeighbours(mesh.GetTriangles(0)[i * 3]);
                allVertices[mesh.GetTriangles(0)[i * 3 + 1]].addNeighbours(mesh.GetTriangles(0)[i * 3 + 2]);

                allVertices[mesh.GetTriangles(0)[i * 3 + 2]].addNeighbours(mesh.GetTriangles(0)[i * 3 + 1]);
                allVertices[mesh.GetTriangles(0)[i * 3 + 2]].addNeighbours(mesh.GetTriangles(0)[i * 3]);

            }
            foreach(var v in allVertices)
            {
                v.CalculateDir(mesh);
            }

            long endOfPreindexing = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            Debug.Log("Preindexing time cost: " + (endOfPreindexing - startOfPreindexing));

            foreach (var i in GameObject.FindGameObjectsWithTag("vertex")){
                Destroy(i);
            }

            RaycastHit hit;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, 1000.0f))
            {
                int hitvertindex = LocateVertex(hit.point, hit.triangleIndex, mesh);
                //Instantiate(sphere, localToWorld.MultiplyPoint3x4(verts[hitvertindex]), Quaternion.identity, this.transform);
                movingIndex = hitvertindex;
                isMoving = true;
                movingVertices.Add(allVertices[hitvertindex]);
                previousMousePos = hit.point;
                //for (int i = originalVertLength; i < verts.Length; i++)
                //{
                //    if (i == hitvertindex)
                //    {
                //        movingVertices.Add(allVertices[i]);
                //        isMoving = true;
                //        Instantiate(sphere, localToWorld.MultiplyPoint3x4(verts[i]), Quaternion.identity, this.transform);
                //        movingIndex = i;

                //    }
                //}

                //if (!isMoving) Debug.LogError("Selected vertex is not incision");
            }
        }

        if (isMoving)
        {
            if (Input.GetKey("a"))
            {
                RaycastHit hit;
                Ray ray = camera.ScreenPointToRay(Input.mousePosition);
                Physics.Raycast(ray, out hit, 1000.0f);
                int movinginto;
                Vector3 dir = DirectionSelection(allVertices[movingIndex], hit.point, out movinginto,mesh);

                HashSet<Vertex> neighboursToAdd = new HashSet<Vertex>();

                foreach(var v in movingVertices)
                {
                    verts[v.index] -= dir.normalized * 0.01f;
                    foreach(var neighbour in v.neighbours)
                    {
                        if(Vector3.Distance(verts[v.index],verts[neighbour])>=1.1 * Vector3.Magnitude(v.directions[neighbour])
                            || Vector3.Distance(verts[v.index], verts[neighbour]) <= 0.2 * Vector3.Magnitude(v.directions[neighbour])){
                            neighboursToAdd.Add(allVertices[neighbour]);
                        }
                    }
                }

                

                if (Vector3.Magnitude(allVertices[movingIndex].directions[movinginto]) * 0.01f >= Vector3.Distance(verts[movingIndex], verts[movinginto]))
                {
                    movingIndex = movinginto;
                    movingVertices.Add(allVertices[movinginto]);
                }
                foreach(var i in neighboursToAdd)
                {
                    movingVertices.Add(i);
                }


                UpdateMesh(verts, mesh.GetIndices(0), mesh.GetIndices(1));
            }


            if (Input.GetKeyDown("t"))
            {
                RaycastHit hit;
                Ray ray = camera.ScreenPointToRay(Input.mousePosition);
                Physics.Raycast(ray, out hit, 1000.0f);
                long startOfTest = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                for (int c = 0; c < 200; c++)
                {                    int movinginto;
                    Vector3 dir = DirectionSelection(allVertices[movingIndex], hit.point, out movinginto, mesh);

                    HashSet<Vertex> neighboursToAdd = new HashSet<Vertex>();

                    foreach (var v in movingVertices)
                    {
                        verts[v.index] -= dir.normalized * 0.01f;
                        foreach (var neighbour in v.neighbours)
                        {
                            if (Vector3.Distance(verts[v.index], verts[neighbour]) >= 1.1 * Vector3.Magnitude(v.directions[neighbour])
                                || Vector3.Distance(verts[v.index], verts[neighbour]) <= 0.2 * Vector3.Magnitude(v.directions[neighbour]))
                            {
                                neighboursToAdd.Add(allVertices[neighbour]);
                            }
                        }
                    }



                    if (Vector3.Magnitude(allVertices[movingIndex].directions[movinginto]) * 0.01f >= Vector3.Distance(verts[movingIndex], verts[movinginto]))
                    {
                        movingIndex = movinginto;
                        movingVertices.Add(allVertices[movinginto]);
                    }
                    foreach (var i in neighboursToAdd)
                    {
                        movingVertices.Add(i);
                    }


                    UpdateMesh(verts, mesh.GetIndices(0), mesh.GetIndices(1));

                }
                long endOfTest = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                Debug.Log("Test Cost: " + (endOfTest - startOfTest));
                Debug.Log("Average: "+ ((endOfTest - startOfTest)/200));


            }

        }



        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, 1000.0f))
            {
                if (!relatedTri.Contains(hit.triangleIndex))
                {
                    relatedTri.Add(hit.triangleIndex);
                    Debug.Log("Current went through triangle: " + relatedTri.Count);
                }
            }
            incisionStart = hit.point;
            isCapturingMovement = true;
        }
        //if (Input.GetMouseButtonDown(1))
        //{
        //    RaycastHit hit;
        //    Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        //    if (Physics.Raycast(ray, out hit, 1000.0f))
        //    {
        //        Debug.Log("Current index: " + hit.triangleIndex);
        //    }

        //}

        if (Input.GetMouseButtonUp(0))
        {
            RaycastHit hit;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, 1000.0f))
            {
                if (!relatedTri.Contains(hit.triangleIndex))
                {
                    relatedTri.Add(hit.triangleIndex);
                    Debug.Log("Current went through triangle: " + relatedTri.Count);
                }
            }
            incisionStart = hit.point;
            isCapturingMovement = false;


        }


        if (isUpdating)
        {
            Ray currentPointing = new Ray(camera.transform.position, incisionEnd - camera.transform.position);
            //Debug.DrawRay(camera.transform.position, incisionEnd - camera.transform.position, Color.green, 1000f);
            RaycastHit currentTri;
            int currentIndex;
            Vector3 Intersection;
            Physics.Raycast(currentPointing, out currentTri, 1000.0f);
            currentIndex = currentTri.triangleIndex;
            //Debug.Log("raycast index: " + currentIndex);
            CutCheck(out Intersection, verts[originalTri[currentIndex * 3]], verts[originalTri[currentIndex * 3 + 1]], verts[originalTri[currentIndex * 3 + 2]]);
            //Instantiate(sphere, Intersection, Quaternion.identity, this.transform);

            incisionStart = Intersection;

            isUpdating = false;
        }



        if (Input.GetKeyDown("w"))
        {

            foreach(var i in GameObject.FindGameObjectsWithTag("vertex"))
            {
                Destroy(i);
            }

            for (int i = 0; i < verts.Length; i++)
            {
                allVertices.Add(new Vertex(i));
            }

            for (int i = 0; i < originalTri.Length / 3; i++)
            {
                allVertices[originalTri[i * 3]].addNeighbours(originalTri[i * 3 + 1]);
                allVertices[originalTri[i * 3]].addNeighbours(originalTri[i * 3 + 2]);

                allVertices[originalTri[i * 3 + 1]].addNeighbours(originalTri[i * 3]);
                allVertices[originalTri[i * 3 + 1]].addNeighbours(originalTri[i * 3 + 2]);

                allVertices[originalTri[i * 3 + 2]].addNeighbours(originalTri[i * 3 + 1]);
                allVertices[originalTri[i * 3 + 2]].addNeighbours(originalTri[i * 3]);

            }


            RaycastHit hit;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, 1000.0f))
            {
                var temp = allVertices[LocateVertex(hit.point,hit.triangleIndex,mesh)];
                foreach(var i in temp.neighbours)
                {
                    Instantiate(sphere, localToWorld.MultiplyPoint3x4(verts[i]), Quaternion.identity, this.transform);

                }
            }

        }


        if (isCapturingMovement)
        {
            RaycastHit hit;
            Vector3 mouse = Input.mousePosition;
            Ray ray = camera.ScreenPointToRay(mouse);
            if (Physics.Raycast(ray, out hit, 1000.0f))
            {

                if (!relatedTri.Contains(hit.triangleIndex))
                {
                    relatedTri.Add(hit.triangleIndex);
                    Debug.Log("Current went through triangle: " + relatedTri.Count);
                }
            }

            if (relatedTri.Count >= 3)
            {
                if (!isUpdating)
                {
                    incisionEnd = hit.point;
                    //Debug.DrawLine(incisionStart, incisionEnd, Color.red, 10000f);
                    List<int> cuttedIndices = new List<int>();
                    Vector3 Intersection;
                    for (int i = 0; i < relatedTri.Count-1; i++)
                    {
                        if (relatedTri[i]>=0&&CutCheck(out Intersection, verts[originalTri[relatedTri[i] * 3]], verts[originalTri[relatedTri[i] * 3 + 1]], verts[originalTri[relatedTri[i] * 3 + 2]]) && !cuttedIndices.Contains(relatedTri[i]))
                        {
                            cuttedIndices.Add(relatedTri[i]);
                        }
                    }


                    Debug.Log("Cuttedindices.size " + cuttedIndices.Count);

                    cuttedIndices.Sort();

                    for (int i = 0; i < cuttedIndices.Count; i++)
                    {
                        cuttedIndices[i] += i * 2;
                        //cuttedIndices[i] += i * 6;

                        Debug.Log("Now operating: " + cuttedIndices[i]);

                        //VerticesCut(originalTri, verts,submesh, WorldToLocal.MultiplyPoint3x4(incisionStart), WorldToLocal.MultiplyPoint3x4(incisionEnd), cuttedIndices[i], out verts, out originalTri,out submesh);
                        PlaneVC(originalTri, verts, submesh, WorldToLocal.MultiplyPoint3x4(incisionStart), WorldToLocal.MultiplyPoint3x4(incisionEnd), cuttedIndices[i], out verts, out originalTri, out submesh);
                        //TODO: NOW

                    }
                    UpdateMesh(verts, originalTri,submesh);
                    int lastRelatedIndex = relatedTri[relatedTri.Count-1];
                    relatedTri.Clear();
                    relatedTri.Add(lastRelatedIndex);
                    isUpdating = true;
                }

            }

        }


    }
}
