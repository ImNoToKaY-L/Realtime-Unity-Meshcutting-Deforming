using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ControllerScript : MonoBehaviour
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
    public DebugUISample uiScript;
    private bool push;
    private int modify_index;
    private Vector3[] original_pos;
    private Vector3[] original_normals;
    private bool m_isOculusGo;


    // Gaussian
    float gaussian_amplitude;
    public float sigma = 0.5f;
    public float max_dz = 0.02f;
    public float affect_region = 0.04f;
    public float touch_vibration_freq = 0.1f;

    // Start is called before the first frame update
    void Start()
    {
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        uiStop = false;
        push = false;
        modify_index = -1;
        help_text.text = "No detection";
        original_pos = belly.GetComponent<MeshFilter>().sharedMesh.vertices;
        original_normals = belly.GetComponent<MeshFilter>().sharedMesh.normals;
        // range_factor = 1 / (2 * Mathf.PI * theta * theta); 
        gaussian_amplitude = 0.000000000000000000000001f; // This will not influence the result
        m_isOculusGo = (OVRPlugin.productName == "Oculus Go");

        sw.Stop();
        System.TimeSpan ts = sw.Elapsed;
        print("Initial time for gaussian method: " + ts.TotalMilliseconds);
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

        // update the scene if no ui is shown
        if (!uiStop)
        {
            Vector2 dir = GetDirection();
            ChangePosition(dir);

            CheckIntersection();

            if (modify_index != -1)
            {
                ModifyModel();
                modify_index = -1;
            }
            else
            {
                ResetModel();
            }
        }
    }

    void OnApplicationQuit()
    {
        belly.GetComponent<MeshFilter>().sharedMesh.vertices = original_pos;
        belly.GetComponent<MeshFilter>().sharedMesh.RecalculateNormals();
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
        push = (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger) || OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger)); // Get the current state of the trigger button
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
                    modify_index = -1;

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

                    // avoid continuous push action
                    if (hit_vertex != modify_index)
                        ResetModel();
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

    float Gaussian_function(float dx, float dy, float dz)
    {
        float e_term = Mathf.Exp(-(dx * dx + dy * dy + dz * dz) / (2 * sigma * sigma));
        float ret = gaussian_amplitude * e_term;
        return ret;
    }

    void ModifyModel()
    {
        System.Diagnostics.Stopwatch sw1 = new System.Diagnostics.Stopwatch();
        sw1.Start();
        Mesh mesh_to_modify = belly.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = mesh_to_modify.vertices;
        Vector3[] normals = mesh_to_modify.normals;
        Vector3 centre_point = vertices[modify_index];

        Dictionary<int, float> change_vertices = new Dictionary<int, float>();
        float max_gaussian = -1f;
        float min_gaussian = float.MaxValue;

        for (int i = 0; i < vertices.Length; i++)
        {
            float dx = vertices[i].x - centre_point.x;
            float dy = vertices[i].y - centre_point.y;
            float dz = vertices[i].z - centre_point.z;
            float distance = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
            //print(distance);
            if (distance < affect_region)
            {
                float gau_result = Gaussian_function(dx, dy, dz);
                change_vertices.Add(i, gau_result);
                if (max_gaussian < gau_result)
                    max_gaussian = gau_result;
                if (min_gaussian > gau_result)
                    min_gaussian = gau_result;
            }
        }

        // calculate ratio and interpolate between 0 and max dz
        change_vertices = change_vertices.ToDictionary(x => x.Key, x => (x.Value - min_gaussian) / (max_gaussian - min_gaussian));
        int[] fetus_status_count = { 0, 0, 0 };
        Vector3 push_point_dir = belly.transform.InverseTransformDirection(-transform.forward); // to local position
        foreach (var vertex in change_vertices)
        {
            // use push direction and factor to calculate the change 
            float push_delta_factor = Mathf.Lerp(0, max_dz, vertex.Value);
            Vector3 push_delta_change = new Vector3(push_point_dir.x * push_delta_factor, push_point_dir.y * push_delta_factor, push_point_dir.z * push_delta_factor);

            vertices[vertex.Key].x -= push_delta_change.x;
            vertices[vertex.Key].y -= push_delta_change.y;
            vertices[vertex.Key].z -= push_delta_change.z;

            Vector3 push_point = belly.transform.TransformPoint(vertices[vertex.Key]); // to world position
            int fetus_status = CheckFetusPos(push_point, transform.forward);

            if (fetus_status == 1 || fetus_status == 2)
            {
                vertices[vertex.Key].x += 0.2f * push_delta_change.x;
                vertices[vertex.Key].y += 0.2f * push_delta_change.y;
                vertices[vertex.Key].z += 0.2f * push_delta_change.z;
            }

            fetus_status_count[fetus_status]++;
        }

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

        // update the belly mesh filter
        mesh_to_modify.vertices = vertices;
        mesh_to_modify.RecalculateNormals();

        sw1.Stop();
        System.TimeSpan ts1 = sw1.Elapsed;
        print("Time for push some point: " + ts1.TotalMilliseconds);


        /*
         * DEBUG POSITION PRECISION
         *
        Mesh mesh_to_modify = fetus.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        Vector3 vertex_pos = mesh_to_modify.vertices[modify_index];
        Color[] colors = new Color[mesh_to_modify.vertices.Length];
        colors[modify_index] = new Color(1, 1, 1);
        mesh_to_modify.colors = colors;
        help_text.text = "cut at vertex " + modify_index + ", Local pos:"  + vertex_pos.x + ", " + vertex_pos.y + ", " + vertex_pos.z ;
        */
    }

    int CheckFetusPos(Vector3 pos, Vector3 norm)
    {
        Ray ray = new Ray(pos, norm); // norm dir

        
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 13f))
        {
            if (hit.collider.tag == "Head")
            {
                return 1;
            }
            else
                return 2;
        }
        else
            return 0;
    }

    void ResetModel()
    {
        Mesh mesh_to_modify = belly.GetComponent<MeshFilter>().sharedMesh;
        mesh_to_modify.vertices = original_pos;
        mesh_to_modify.normals = original_normals;
    }
}
