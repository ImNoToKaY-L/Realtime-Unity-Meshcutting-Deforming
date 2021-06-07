using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class control : MonoBehaviour
{
    // Start is called before the first frame update
    GameObject belly;
    GameObject bellyCutted;

    void Start()
    {
        belly = GameObject.FindWithTag("Belly");
        bellyCutted = GameObject.FindWithTag("BellyCutted");
        //bellyCutted.SetActive(false);

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            belly.SetActive(!belly.activeSelf);
            //bellyCutted.SetActive(!bellyCutted.activeSelf);
        }
    }
}
