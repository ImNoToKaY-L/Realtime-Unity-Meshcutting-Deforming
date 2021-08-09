using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class VertexMovement
{
    public int index;
    public Vector3 Destination;

    public VertexMovement(int index, Vector3 target)
    {
        this.index = index;
        Destination = target;
    }

}
