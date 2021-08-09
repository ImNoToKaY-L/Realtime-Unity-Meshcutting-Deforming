using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BST 
{
    public class Node
    {
        public float distance;
        public List<int> index;
        public Node left;
        public Node right;

        public Node (float dis, int index)
        {
            distance = dis;
            this.index = new List<int>();
            this.index.Add(index);
        }
    }

    public Node root;
    public List<int> resultIndex = new List<int>();


    public bool Add(float value, int index)
    {
        Node before = null, after = this.root;

        while (after != null)
        {
            before = after;
            if (value < after.distance) //Is new node in left tree? 
                after = after.left;
            else if (value > after.distance) //Is new node in right tree?
                after = after.right;
            else
            {
                if(!after.index.Contains(index))
                after.index.Add(index);
                return true;
            }
        }

        Node newNode = new Node(value,index);

        if (this.root == null)//Tree ise empty
            this.root = newNode;
        else
        {
            if (value < before.distance)
                before.left = newNode;
            else
                before.right = newNode;
        }

        return true;
    }


    public void Query(float radius)
    {

    }

    public Node Find(float distance,Node parent)
    {
        if (parent != null)
        {
            if (parent.distance == distance) return parent;
            else if (distance < parent.distance) return Find(distance, parent.left);
            else return Find(distance, parent.right);
        }
        Debug.LogError("BST Find function error");
        return null;
    }


}
