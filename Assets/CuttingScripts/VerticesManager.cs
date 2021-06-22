using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VerticesManager
{
    public class Vertex {
        public int index;
        public List<int> neighbours;
        public int stickingto = -1;

        public Vertex(int index)
        {
            this.index = index;
            neighbours = new List<int>();
        }

        public void addNeighbours(int neighbour)
        {
            neighbours.Add(neighbour);
        }
        public void stick(int stickto)
        {
            stickingto = stickto;
        }
    }

}
