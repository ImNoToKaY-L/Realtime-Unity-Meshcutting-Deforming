using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaneCutter : MonoBehaviour
{
    private Vector3 sp;
    private Vector3 ep;
    // Start is called before the first frame update
    private Camera camera;
    private GameObject plane;
    public GameObject sphere;
    void Start()
    {
        camera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        plane = GameObject.FindGameObjectWithTag("Cutter");
    }


    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Contact count"+collision.contactCount+" colliding with: "+collision.gameObject.tag);
    }


    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            Physics.Raycast(ray, out hit, 1000f);
            sp = hit.point;
        }
        if (Input.GetMouseButtonUp(0))
        {
            RaycastHit hit;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            Physics.Raycast(ray, out hit, 1000f);
            ep = hit.point;
            Vector3 Midpoint = sp + (ep - sp) / 2;
            plane.transform.position = Midpoint;
            Vector3 normal = new Plane(sp, ep, sp + new Vector3(0, 1, 0)).normal;
            plane.transform.rotation = Quaternion.FromToRotation(plane.GetComponent<MeshFilter>().mesh.normals[0],normal);
        }



    }
}
