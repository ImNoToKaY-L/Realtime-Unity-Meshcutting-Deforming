using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class tricut : MonoBehaviour
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

    private int prevHitTri;
    private List<VertMovement> expandingDirections = new List<VertMovement>();
    private int originalVertLength;


    private class VertMovement
    {
        public int vertIndex;
        public Vector3 direction;
        public VertMovement(int index,Vector3 dir)
        {
            vertIndex = index;
            direction = dir;
        }
    }



    void Start()
    {
        relatedTri = new List<int>();
        originalVertLength = transform.GetComponent<MeshFilter>().mesh.vertexCount;

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


    private void IncisionCreation(int[] originalTri,Vector3[] vertices,Vector3 startPoint, Vector3 endPoint, int index, int IV, int GV1, int GV2, int spOrder, int epOrder, out Vector3[] newvertices, out int[] newTriangles)
    {

        int[] newTri = new int[originalTri.Length + 6];
        Vector3[] newVert = new Vector3[vertices.Length + 4];



        Array.Copy(vertices, newVert, vertices.Length);
        newVert[vertices.Length] = startPoint;
        newVert[vertices.Length + 1] = endPoint;
        newVert[vertices.Length + 2] = startPoint;
        newVert[vertices.Length + 3] = endPoint;
        int intersect1 = spOrder == 1 ? vertices.Length :vertices.Length + 1;
        int intersect2 = spOrder == 1 ? vertices.Length + 1 : vertices.Length;
        int intersect3 = spOrder == 1 ? vertices.Length + 2 : vertices.Length + 3;
        int intersect4 = spOrder == 1 ? vertices.Length + 3 : vertices.Length + 2;


        Array.Copy(originalTri, newTri, index * 3);
        newTri[index * 3] = IV;
        newTri[index * 3 + 1] = intersect1;
        newTri[index * 3 + 2] = intersect2;

        newTri[index * 3 + 3] = GV1;
        newTri[index * 3 + 4] = GV2;
        newTri[index * 3 + 5] = intersect4;

        newTri[index * 3 + 6] = intersect4;
        newTri[index * 3 + 7] = intersect3;
        newTri[index * 3 + 8] = GV1;
        expandingDirections.Add(new VertMovement(intersect1,newVert[IV] - newVert[intersect1]));
        expandingDirections.Add(new VertMovement(intersect2, newVert[IV] - newVert[intersect2]));
        expandingDirections.Add(new VertMovement(intersect3, newVert[GV1] - newVert[intersect3]));
        expandingDirections.Add(new VertMovement(intersect4, newVert[GV2] - newVert[intersect4]));


        Array.Copy(originalTri, (index + 1) * 3, newTri, index * 3 + 9, originalTri.Length - index * 3 - 3);
        newvertices = newVert;
        newTriangles = newTri;

    }


    private void IncisionCreationTest(int[] originalTri, Vector3[] vertices, Vector3 startPoint, Vector3 endPoint, int index, int IV, int GV1, int GV2, int spOrder, int epOrder, out Vector3[] newvertices, out int[] newTriangles)
    {

        int[] newTri = new int[originalTri.Length + 18];
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

        Vector3 BV1 = newVert[intersect1] + new Vector3(0f, -incisionDepth,0f);
        Vector3 BV2 = newVert[intersect2] + new Vector3(0f, -incisionDepth,0f);
        newVert[vertices.Length + 4] = BV1;
        newVert[vertices.Length + 5] = BV2;
        int B1 = vertices.Length + 4;
        int B2 = vertices.Length + 5;

        //subtri = new int[12];
        //int counter = 0;

        newTri[index * 3 + 9] = intersect3;
        newTri[index * 3 + 10] = B2;
        newTri[index * 3 + 11] = B1;
        
        newTri[index * 3 + 12] = intersect3;
        newTri[index * 3 + 13] = intersect4;
        newTri[index * 3 + 14] = B2;

        newTri[index * 3 + 15] = intersect1;
        newTri[index * 3 + 16] = B1;
        newTri[index * 3 + 17] = B2;

        newTri[index * 3 + 18] = intersect2;
        newTri[index * 3 + 19] = intersect1;
        newTri[index * 3 + 20] = B2;
        //int[] addup = new int[subtri.Length + submesh.Length];
        //Array.Copy(submesh, addup, submesh.Length);
        //Array.Copy(subtri, 0, addup, submesh.Length, subtri.Length);
        //subtri = addup;

        expandingDirections.Add(new VertMovement(intersect1, newVert[IV] - newVert[intersect1]));
        expandingDirections.Add(new VertMovement(intersect2, newVert[IV] - newVert[intersect2]));
        expandingDirections.Add(new VertMovement(intersect3, newVert[GV1] - newVert[intersect3]));
        expandingDirections.Add(new VertMovement(intersect4, newVert[GV2] - newVert[intersect4]));

        Array.Copy(originalTri, (index + 1) * 3, newTri, (index+7)*3, originalTri.Length -((index+1) * 3));
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



    private bool startORendV2(Vector3 I1,Vector3 I2)
    {

        float I1ToStart = Vector3.Distance(I1, incisionStart);
        float I2ToStart = Vector3.Distance(I2, incisionStart);



        return I1ToStart<I2ToStart;
    }


    private void VerticesCut(int[] tri,Vector3[] vertices, Vector3 startPoint, Vector3 endPoint, int index, out Vector3[] newvertices,out int[] newTriangles)
    {
        Vector3[] localVertices = new Vector3[3];
        Vector3 v1 = vertices[tri[index * 3]];
        Vector3 v2 = vertices[tri[index * 3+1]];
        Vector3 v3 = vertices[tri[index * 3+2]];

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
        if (segIntersection(out Intersection,incisionStart,incisionEnd,v1+transform.position,v2 + transform.position))
        {
            //Instantiate(sphere, Intersection, Quaternion.identity, this.transform);
            if (startORend(Intersection)) startPoint = Intersection-transform.position;
            else endPoint = Intersection - transform.position;


            I1 = Intersection;

            istriggered = !istriggered;

        }
        if (segIntersection(out Intersection, incisionStart, incisionEnd, v1 + transform.position, v3 + transform.position))
        {
            //Instantiate(sphere, Intersection, Quaternion.identity, this.transform);
            if (startORend(Intersection)) startPoint = Intersection - transform.position;
            else endPoint = Intersection - transform.position;


            if (I1 == Vector3.zero) I1 = Intersection;
            else I2 = Intersection;

            istriggered = !istriggered;

        }
        if (segIntersection(out Intersection, incisionStart, incisionEnd, v2 + transform.position, v3 + transform.position))
        {
            //Instantiate(sphere, Intersection, Quaternion.identity, this.transform);
            if (startORend(Intersection)) startPoint = Intersection - transform.position;
            else endPoint = Intersection - transform.position;

            I2 = Intersection;

            istriggered = !istriggered;

        }
        if (istriggered) Debug.LogError("INTERSECTION ERROR");

        startPoint = startORendV2(I1, I2) ? I1 - transform.position : I2 - transform.position;
        endPoint = startORendV2(I1, I2) ? I2 - transform.position : I1 - transform.position;




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
                Debug.LogError("None of the cases are triggered, current sum: "+ sum);
                break;
        }
        //IncisionCreation(tri,vertices,startPoint, endPoint, index, individualVert, groupedVert1, groupedVert2, startOrder, endOrder,out newvertices,out newTriangles);
        IncisionCreationTest(tri, vertices, startPoint, endPoint, index, individualVert, groupedVert1, groupedVert2, startOrder, endOrder, out newvertices, out newTriangles);


    }


    private void UpdateMesh(Vector3[] vertices,int[] triangles)
    {
        Destroy(this.gameObject.GetComponent<MeshCollider>());
        Vector2[] uvs = new Vector2[vertices.Length];

        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
        }

        transform.GetComponent<MeshFilter>().mesh.vertices = vertices;
        transform.GetComponent<MeshFilter>().mesh.uv = uvs;

        transform.GetComponent<MeshFilter>().mesh.triangles = triangles;
        transform.GetComponent<MeshFilter>().mesh.RecalculateNormals();

        this.gameObject.AddComponent<MeshCollider>();

    }



    private bool CutCheck(out Vector3 Intersection,Vector3 v1, Vector3 v2, Vector3 v3)
    {
        int intersectCount = 0;
        Vector3 tempInter = Vector3.zero;
        if(segIntersection(out Intersection, incisionStart, incisionEnd, v1+transform.position, v2 + transform.position))
        {
            intersectCount++;
            tempInter = Intersection;
        }

        if (segIntersection(out Intersection, incisionStart, incisionEnd, v3 + transform.position, v1 + transform.position))
        {
            intersectCount++;
            tempInter = Intersection;
        }


        if (segIntersection(out Intersection, incisionStart, incisionEnd, v2 + transform.position, v3 + transform.position))
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
            Debug.Log("No intersection between casted line and one of the triangle, cutted through: " + intersectCount);
        }





        return false;
    }




    // Update is called once per frame
    void Update()
    {

        Mesh mesh = transform.GetComponent<MeshFilter>().mesh;
        int[] originalTri = mesh.triangles;
        Vector3[] verts = mesh.vertices;

        //if (Input.GetMouseButtonDown(0))
        //{
        //    RaycastHit hit;
        //    Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        //    if (Physics.Raycast(ray, out hit, 1000.0f))
        //    {
        //        incisionStart = hit.point;

        //        relatedTri.Add(hit.triangleIndex);

        //    }

        //    if (relatedTri.Count == 1)
        //        isCapturingMovement = true;
        //}
        //if (Input.GetMouseButtonUp(0))
        //{
        //    RaycastHit hit;
        //    int hitTri;
        //    Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        //    if (Physics.Raycast(ray, out hit, 1000.0f))
        //    {
        //        hitTri = hit.triangleIndex;
        //        relatedTri.Clear();
        //    }
        //    isCapturingMovement = false;
        //    //Debug.DrawLine(incisionStart, incisionEnd, Color.red, 10000000f);

        //}

        if (Input.GetMouseButton(1))
        {
            Destroy(this.gameObject.GetComponent<MeshCollider>());

            Mesh currentmesh = transform.GetComponent<MeshFilter>().mesh;
            Vector3[] currentvert = currentmesh.vertices;
            for (int i = 0; i < expandingDirections.Count; i++)
            {
                currentvert[expandingDirections[i].vertIndex] += expandingDirections[i].direction.normalized * 0.02f;

            }


            transform.GetComponent<MeshFilter>().mesh.vertices = currentvert;

            mesh.RecalculateNormals();

            this.gameObject.AddComponent<MeshCollider>();
        }


        //if (isCapturingMovement)
        //{
        //    RaycastHit hit;
        //    Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        //    if (Physics.Raycast(ray, out hit, 1000.0f))
        //    {
        //        int hitTri = hit.triangleIndex;
        //        if (!relatedTri.Contains(hitTri))
        //        {

        //            if (relatedTri.Count == 1)
        //            {
        //                incisionEnd = hit.point;
        //                incisionStart = incisionEnd;
        //                Instantiate(sphere, incisionStart, Quaternion.identity, this.transform);

        //                prevHitTri = hitTri;
        //            }
        //            else
        //            {
        //                incisionEnd = hit.point;

        //                //Instantiate(sphere, incisionStart, Quaternion.identity, this.transform);
        //                //Instantiate(sphere, incisionEnd, Quaternion.identity, this.transform);
        //                Debug.Log("relatedTri size: " + relatedTri.Count);
        //                VerticesCut(originalTri, verts, incisionStart - this.transform.position, incisionEnd - this.transform.position, prevHitTri);
        //                Vector3 offset = incisionStart - incisionEnd;

        //                incisionStart = incisionEnd + offset * 0.1f;
        //                if (Physics.Raycast(ray, out hit, 1000.0f))
        //                {
        //                    prevHitTri = hit.triangleIndex;
        //                    relatedTri.Clear();

        //                }
        //                else
        //                {
        //                    Debug.LogError("Raycast issue");
        //                }
        //                //isCapturingMovement = false;

        //            }

        //            relatedTri.Add(prevHitTri);

        //        }
        //    }
        //}

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
            Debug.DrawRay(camera.transform.position, incisionEnd - camera.transform.position, Color.green, 1000f);
            RaycastHit currentTri;
            int currentIndex;
            Vector3 Intersection;
            Physics.Raycast(currentPointing, out currentTri, 1000.0f);
            currentIndex = currentTri.triangleIndex;
            Debug.Log("raycast index: " + currentIndex);
            CutCheck(out Intersection,verts[originalTri[currentIndex * 3]], verts[originalTri[currentIndex * 3 + 1]], verts[originalTri[currentIndex * 3 + 2]]);
            Instantiate(sphere, Intersection, Quaternion.identity, this.transform);

            incisionStart = Intersection;

            isUpdating = false;
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
                    Debug.DrawLine(incisionStart, incisionEnd, Color.red, 10000f);
                    List<int> cuttedIndices = new List<int>();
                    Vector3 Intersection;
                    for (int i = 0; i < relatedTri.Count; i++)
                    {
                        if (CutCheck(out Intersection, verts[originalTri[relatedTri[i] * 3]], verts[originalTri[relatedTri[i] * 3 + 1]], verts[originalTri[relatedTri[i] * 3 + 2]]) && !cuttedIndices.Contains(relatedTri[i]))
                        {
                            cuttedIndices.Add(relatedTri[i]);
                        }
                    }
                    Debug.Log("Cuttedindices.size " + cuttedIndices.Count);

                    cuttedIndices.Sort();

                    for (int i = 0; i < cuttedIndices.Count; i++)
                    {
                        //cuttedIndices[i] += i * 2;
                        cuttedIndices[i] += i * 6;

                        Debug.Log("Now operating: " + cuttedIndices[i]);

                        VerticesCut(originalTri, verts, incisionStart - this.transform.position, incisionEnd - this.transform.position, cuttedIndices[i],out verts,out originalTri);
                    }
                    UpdateMesh(verts,originalTri);

                    relatedTri.Clear();
                    isUpdating = true;
                }

            }




        }


    }
}
