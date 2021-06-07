using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ControllerScript_massVer : MonoBehaviour
{
    public Text help_text;
    public LineRenderer laser_line_renderer;
    public OVRCameraRig cam;
    public GameObject belly;
    public GameObject fetus_head;
    public Material m_surgeon_area;
    public Material m_non_surgeon_area;
    public Material m_fetus_area;
    public AudioSource sound_effect;
    public bool uiStop;
    public DebugUISampleMassVer uiScript;
    public float touch_vibration_freq;
    private Spring[] springs;
    private bool push;
    private int modify_index;
    private Vector3[] original_pos;
    private Vector3[] original_normals;
    private bool m_isOculusGo;
    public Material ladySkin;
    public Material transparentSkin;
    public GameObject fetus;



    private bool transparent;

    // change param
    private float last_kh;
    private float last_kn;
    private float last_dis;
    public float kh;
    public float kn;
    public float press_distance;


    // Start is called before the first frame update
    void Start()
    {
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        uiStop = false;
        push = false;
        press_distance = 0.0002f;
        touch_vibration_freq = 0.1f;
        modify_index = -1;
        help_text.text = "No detection";
        original_pos = belly.GetComponent<MeshFilter>().sharedMesh.vertices;
        original_normals = belly.GetComponent<MeshFilter>().sharedMesh.normals;
        m_isOculusGo = (OVRPlugin.productName == "Oculus Go");
        springs = new Spring[original_pos.Length];
        InitSprings();
        last_kh = Spring.kh;
        last_kn = Spring.kn;
        kh = last_kh;
        kn = last_kn;
        sw.Stop();
        System.TimeSpan ts = sw.Elapsed;
        print("Initial time for mass spring method: " + ts.TotalMilliseconds);
    }

    // Update is called once per frame
    void Update()
    {

        // call out the ui screen
        if (OVRInput.Get(OVRInput.Button.Two) || OVRInput.Get(OVRInput.Button.Back) || Input.GetKeyDown(KeyCode.A))
        {
            uiStop = !uiStop;
            uiScript.buttonReceived = true;
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            if (!transparent)
            {
                belly.GetComponent<MeshRenderer>().material = transparentSkin;
                Debug.Log("Skin set transparent");
                transparent = !transparent;
            }
            else
            {
                belly.GetComponent<MeshRenderer>().material = ladySkin;
                Debug.Log("Skin set normal");
                transparent = !transparent;
            }
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            fetus.transform.position = new Vector3(180f, -59.4f, 66f);
            fetus.transform.rotation = Quaternion.Euler(-90f, 0f, 90f);
            fetus.transform.localScale = new Vector3(1.8f, 1.8f, 1.8f);

        }


        // check to change the param when ui is on
        if  (uiStop)
        {
            // Reset the model at UI interface
            ResetModel();
            
            if (last_kh != kh)
            {
                Spring.kh = kh;
                last_kh = kh;
            }

            if (last_kn != kn)
            {
                Spring.kn = kn;
                last_kn = kn;
            }

            if (last_dis != press_distance)
            {
                Spring.push_distance = press_distance;
                last_dis = press_distance;
            }
        }
        else
        {
            // update direction 
            Vector2 dir = GetDirection();
            ChangePosition(dir);

            // check mouse or pointer position
            CheckIntersection();

            if (push && modify_index != -1)
            {
                (springs[modify_index]).SetCurPosRatio(1f);
            }

            // update position
            ModifyModel();
        }
    }

    void OnApplicationQuit()
    {
        belly.GetComponent<MeshFilter>().sharedMesh.vertices = original_pos;
        belly.GetComponent<MeshFilter>().sharedMesh.RecalculateNormals();
    }

    void InitSprings()
    {
        Mesh mesh = belly.GetComponent<MeshFilter>().sharedMesh;
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            Spring spr = new Spring();

            // get the position info from the vertex
            spr.SetPushDistance(press_distance);
            spr.SetID(i);
            spr.SetOriPos(vertex);
            springs[i] = spr;
        }

        // get neighbour info
        for (int index = 0; index < triangles.Length; index += 3)
        {
            int ver1 = triangles[index];
            int ver2 = triangles[index + 1];
            int ver3 = triangles[index + 2];
            springs[ver1].AddNeighbour(ver2); springs[ver1].AddNeighbour(ver3);
            springs[ver2].AddNeighbour(ver1); springs[ver2].AddNeighbour(ver3);
            springs[ver3].AddNeighbour(ver1); springs[ver3].AddNeighbour(ver2);
        }

        // calculate the total distance to neighbours for each particle
        float total_dis = 0;
        foreach (Spring spr in springs)
        {
            HashSet<int> neighs = spr.GetNeighbour();
            float spr_total_dis = 0;
            foreach (int neigh in neighs)
            {
                float dis = Vector3.Distance(vertices[neigh], vertices[spr.GetID()]);
                spr_total_dis += dis;
            }
            spr.SetNeighbourDistance(spr_total_dis);
            total_dis += spr_total_dis;
        }

        // use the distance to calculate the mass
        foreach (Spring spr in springs)
        {
            spr.CalculateMass(total_dis);
        }
        
    }

    // Check the laser collision point with the belly mesh
    void CheckIntersection()
    {
        // Change the position of the controller

#if UNITY_EDITOR
        push = Input.GetMouseButton(0);
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        transform.LookAt(ray.GetPoint(10000f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
#else
        // Oculus version
        push = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger); // Get the current state of the trigger button
        RaycastHit hit;
        transform.rotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTrackedRemote);
        if (Physics.Raycast(transform.position, transform.forward, out hit))
#endif
        {
            // hit the surgeon area
            if (hit.collider != null && hit.collider.gameObject == belly)
            {
                if (!push) // not triggered
                {
                    help_text.text = "Not pushed";

                    // change the line material
                    laser_line_renderer.sharedMaterial = m_surgeon_area;
                    if (sound_effect.isPlaying)
                        sound_effect.Stop();

                    // disable the vibration
                    OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);

                    // disable the sound in Oculus Go
                    if (m_isOculusGo)
                    {
                        
                        if (sound_effect.isPlaying)
                            sound_effect.Stop();
                    }
                }
                else //triggered
                {
                    // find the push vertex
                    int hit_vertex = findClosestVertex(hit);
                    help_text.text = "Pushed";

                    modify_index = hit_vertex;
                    
                    // enable the vibration
                    OVRInput.SetControllerVibration(touch_vibration_freq, touch_vibration_freq, OVRInput.Controller.RTouch);

                    // enable the sound in Oculus Go
                    if (m_isOculusGo)
                    {
                        if (!sound_effect.isPlaying)
                            sound_effect.Play();
                        sound_effect.volume = touch_vibration_freq;
                    }

                }
            }

            //set the laser end position
            laser_line_renderer.SetPosition(1, hit.point);
        }
        else // not in the surgeon area
        {
            help_text.text = "No intersection";
            laser_line_renderer.SetPosition(1, transform.forward * 10000);
            laser_line_renderer.sharedMaterial = m_non_surgeon_area;

            // disable the vibration
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);

            // disable the sound in Oculus Go
            if (m_isOculusGo)
            {

                if (sound_effect.isPlaying)
                    sound_effect.Stop();
            }

            // add another function to call out the ui
            // to solve the problem of Oculus Go 'back' button)
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger)
                || OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger)
                || Input.GetMouseButtonDown(0))
            {
                uiStop = !uiStop;
                uiScript.buttonReceived = true;
            }
        }
        laser_line_renderer.SetPosition(0, transform.position);
    }

    // get the move direction from user
    Vector2 GetDirection()
    {
        // find the correct direction
        Vector2[] directions = new Vector2[]
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right
        };

#if UNITY_EDITOR
        if (Input.GetKey(KeyCode.UpArrow))
            return directions[0];
        else if (Input.GetKey(KeyCode.DownArrow))
            return directions[1];
        else if (Input.GetKey(KeyCode.LeftArrow))
            return directions[2];
        else if (Input.GetKey(KeyCode.RightArrow))
            return directions[3];
        else
            return Vector2.zero;

#else

        if (m_isOculusGo) // Oculus Go
        {
            Vector2 coord = Vector2.zero;

            // check input
            if (!OVRInput.Get(OVRInput.Button.PrimaryTouchpad))
                return coord;
            
            coord = OVRInput.Get(OVRInput.Axis2D.PrimaryTouchpad, OVRInput.Controller.RTrackedRemote);
            Vector2 best_match_dir = Vector2.zero;
            float max = Mathf.NegativeInfinity;
            foreach (Vector2 vec in directions)
            {
                float dot_result = Vector2.Dot(vec, coord);
                if (dot_result > max)
                {
                    best_match_dir = vec;
                    max = dot_result;
                }
            }
            return best_match_dir;
        }
        else // Oculus Quest
        {
            if (OVRInput.Get(OVRInput.Button.SecondaryThumbstickUp))
                return directions[0];
            else if (OVRInput.Get(OVRInput.Button.SecondaryThumbstickDown))
                return directions[1];
            else if (OVRInput.Get(OVRInput.Button.SecondaryThumbstickLeft))
                return directions[2];
            else if (OVRInput.Get(OVRInput.Button.SecondaryThumbstickRight))
                return directions[3];
            else
                return Vector2.zero;
        }
#endif
    }

    // move the user and camera
    void ChangePosition(Vector2 dir)
    {
        float horizontalInput = dir.x;
        float verticalInput = dir.y;
        float movementSpeed = 20f;
        transform.position = transform.position + new Vector3(horizontalInput * movementSpeed * Time.deltaTime, 0, verticalInput * movementSpeed * Time.deltaTime);
        cam.transform.position = cam.transform.position + new Vector3(horizontalInput * movementSpeed * Time.deltaTime, 0, verticalInput * movementSpeed * Time.deltaTime);
        // get rotation
#if UNITY_EDITOR
        if (Input.GetKey(KeyCode.S))
            cam.transform.rotation *= Quaternion.Euler(2f * Time.deltaTime, 0, 0);
        else if (Input.GetKey(KeyCode.W))
            cam.transform.rotation *= Quaternion.Euler(-2f * Time.deltaTime, 0, 0);
        else if (Input.GetKey(KeyCode.E))
            cam.transform.rotation *= Quaternion.Euler(0, 2f * Time.deltaTime, 0);
        else if (Input.GetKey(KeyCode.Q))
            cam.transform.rotation *= Quaternion.Euler(0, -2f * Time.deltaTime, 0);
# else
        if (OVRInput.Get(OVRInput.Button.PrimaryThumbstickUp))
            cam.transform.rotation *= Quaternion.Euler(-2f * Time.deltaTime, 0, 0);
        else if (OVRInput.Get(OVRInput.Button.PrimaryThumbstickDown))
            cam.transform.rotation *= Quaternion.Euler(2f * Time.deltaTime, 0, 0);
        else if (OVRInput.Get(OVRInput.Button.PrimaryThumbstickLeft))
            cam.transform.rotation *= Quaternion.Euler(0, -2f * Time.deltaTime, 0);
        else if (OVRInput.Get(OVRInput.Button.PrimaryThumbstickRight))
            cam.transform.rotation *= Quaternion.Euler(0, 2f * Time.deltaTime, 0);
#endif
        transform.rotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTrackedRemote);
        laser_line_renderer.SetPosition(1, transform.forward * 10000);
        laser_line_renderer.SetPosition(0, transform.position);
    }

    //  find the collision vertex
    int findClosestVertex(RaycastHit hit)
    {
        Vector3 bary_coor = hit.barycentricCoordinate;
        float[] bary_coors = { bary_coor.x, bary_coor.y, bary_coor.z };
        float max = -1000;
        int max_index = -1;
        for (int i = 0; i < 3; i++)
        {
            float coor = bary_coors[i];
            if (coor > max)
            {
                max = coor;
                max_index = i;
            }
        }
        if (hit.collider.gameObject.GetComponent<MeshFilter>())
            return hit.collider.gameObject.GetComponent<MeshFilter>().sharedMesh.triangles[hit.triangleIndex * 3 + max_index];
        else // error: no renderer
            return -1;
    }

    void ModifyModel()
    {
        System.Diagnostics.Stopwatch sw1 = new System.Diagnostics.Stopwatch();
        sw1.Start();
        Mesh mesh_to_modify = belly.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = mesh_to_modify.vertices;
        int[] fetus_status_count = { 0, 0, 0 };
        Vector3 push_point_dir = belly.transform.InverseTransformDirection(-transform.forward);
        if (modify_index != -1)
        {
            foreach (Spring spr in springs)
            {
                Vector3 ori_vec = spr.GetOriPos();
                float distance_to_centre = Vector3.Distance(ori_vec, vertices[modify_index]);
                spr.PosUpdate(springs, push_point_dir);
                Vector3 vec = spr.GetPosition(push_point_dir);
                if (push && spr.GetCurPosRatio() > 0.2) // pushed, check fetus position
                {
                    Vector3 push_point = belly.transform.TransformPoint(vec); // to world position
                    int fetus_status = CheckFetusPos(push_point, push_point_dir);
                    if (fetus_status == 1 || fetus_status == 2)
                    {
                        vec = new Vector3((vec.x + ori_vec.x) * 0.5f, (vec.y + ori_vec.y) * 0.5f, (vec.z + ori_vec.z) * 0.5f);
                    }
                    fetus_status_count[fetus_status]++;
                }

                vertices[spr.GetID()] = vec;
            }
        }
        
        if (push && help_text.text != "No intersection") // pushed, check fetus component info
        {
            // change the material according to the max region
            if (fetus_status_count.Max() == fetus_status_count[0])
            {
                help_text.text = "Not touched anything";
                laser_line_renderer.sharedMaterial = m_surgeon_area;
            }
            else if (fetus_status_count.Max() == fetus_status_count[1])
            {
                help_text.text = "Head is here";
                laser_line_renderer.sharedMaterial = m_fetus_area;
            }
            else
            {
                help_text.text = "Something is here";
                laser_line_renderer.sharedMaterial = m_fetus_area;
            }

            // calculate the ratio of fetus component in the pushed areas
            float ratio = (float)(fetus_status_count[1] + fetus_status_count[2]) / (float)fetus_status_count.Sum();

            // enable the vibration
            float freq = touch_vibration_freq + (1f - touch_vibration_freq) * ratio;
            OVRInput.SetControllerVibration(freq, freq, OVRInput.Controller.RTouch); // not in Oculus go

            // play the sound in Oculus Go
            if (m_isOculusGo)
            {
                if (!sound_effect.isPlaying)
                    sound_effect.Play();
                sound_effect.volume = freq;
            }
        }

        belly.GetComponent<MeshFilter>().sharedMesh.vertices = vertices;
        belly.GetComponent<MeshFilter>().sharedMesh.RecalculateNormals();

        sw1.Stop();
        System.TimeSpan ts1 = sw1.Elapsed;
        if (push)
            print("Time for push some point: " + ts1.TotalMilliseconds);

    }

    int CheckFetusPos(Vector3 pos, Vector3 dir)
    {
        //  Ray ray = new Ray(pos, new Vector3(0f, -1f, 0f)); // vertical dir
        Ray ray = new Ray(pos, dir); // norm dir


        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 13f))
        {
            if (hit.collider.tag == "Head")
            {
                return 1;
            }
            else
            {
                return 2;
            }
        }
        else
            return 0;
    }

    void ResetModel()
    {
        Mesh mesh_to_modify = belly.GetComponent<MeshFilter>().sharedMesh;
        mesh_to_modify.vertices = original_pos;
        mesh_to_modify.normals = original_normals;
        foreach (Spring spr in springs)
        {
            spr.SetCurPosRatio(0f);
        }
    }
}



