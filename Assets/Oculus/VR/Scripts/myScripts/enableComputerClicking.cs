using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class enableComputerClicking : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
#if UNITY_EDITOR
        this.GetComponent<StandaloneInputModule>().enabled = true;
        this.GetComponent<OVRInputModule>().enabled = false;
#else
        this.GetComponent<StandaloneInputModule>().enabled = false;
        this.GetComponent<OVRInputModule>().enabled = true;
#endif
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
