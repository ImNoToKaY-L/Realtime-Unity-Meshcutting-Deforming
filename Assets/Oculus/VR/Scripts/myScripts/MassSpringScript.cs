using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class Spring
{

    public static float kh = 0.0001f;
    public static float kn = 0.0001f;
    public static float push_distance;
    private float cur_pos_ratio = 0f;
    private int id;
    private Vector3 ori_pos;
    private HashSet<int> neighbour;
    private float mass;
    private float neighbour_distance;

    public Spring()
    {
        neighbour = new HashSet<int>();
    }


    public void SetCurPosRatio(float ratio)
    {
        this.cur_pos_ratio = ratio;
    }

    public void SetID(int id)
    {
        this.id = id;
    }

    public void CalculateMass(float total_dis)
    {
        this.mass = 20f * neighbour_distance / total_dis;
    }
    
    public void SetPushDistance(float dis)
    {
        push_distance = dis;
    }
    public void SetNeighbourDistance(float dis)
    {
        this.neighbour_distance = dis;
    }
    public void AddNeighbour(int neighboor_index)
    {
        this.neighbour.Add(neighboor_index);
    }
    public void SetOriPos(Vector3 pos)
    {
        this.ori_pos = pos;
    }

    public Vector3 GetOriPos()
    {
        return ori_pos;
    }
    public int GetID()
    {
        return id;
    }
    public HashSet<int> GetNeighbour()
    {
        return neighbour;
    }
    public Vector3 GetPosition(Vector3 dir)
    {
        Vector3 cur_pos = new Vector3(ori_pos.x - dir.x * push_distance * cur_pos_ratio,
                                    ori_pos.y - dir.y * push_distance * cur_pos_ratio,
                                    ori_pos.z - dir.z * push_distance * cur_pos_ratio);
        return cur_pos;
    }

    public float GetCurPosRatio()
    {
        return cur_pos_ratio;
    }
    private float CalProjection(Spring A, Spring B, Vector3 dir)
    {
        // A for local point, B for neighboor point
        // get position information
        Vector3 a_oripos = A.GetOriPos();
        Vector3 a_endpos = new Vector3(a_oripos.x + dir.x * push_distance, a_oripos.y + dir.y * push_distance, a_oripos.z + dir.z * push_distance);

        // update the position
        Vector3 a = A.GetPosition(dir);
        Vector3 b = B.GetPosition(dir);

        // calculate A spring length
        float[] vera = new float[3];
        vera[0] = a_endpos.x - a_oripos.x;
        vera[1] = a_endpos.y - a_oripos.y;
        vera[2] = a_endpos.z - a_oripos.z;
        float lena = Mathf.Sqrt(vera[0] * vera[0] + vera[1] * vera[1] + vera[2] * vera[2]);

        // spring between mass A and mass B
        float[] verb = new float[3];
        verb[0] = b[0] - a[0];
        verb[1] = b[1] - a[1];
        verb[2] = b[2] - a[2];

        // vector projection on the vertical spring
        float proj = (vera[0] * verb[0] + vera[1] * verb[1] + vera[2] * verb[2]) / lena;
        return proj;
    }

    public void PosUpdate(Spring[] springs, Vector3 dir)
    {
        float mpf = -this.cur_pos_ratio * kn;
        foreach (int i in this.neighbour)
        {
            //Console.WriteLine("({0,2}", i);
            mpf += CalProjection(springs[this.id], springs[i], dir) * kh;
        }
        //Console.WriteLine("");
        this.cur_pos_ratio += mpf / mass;
        if (this.cur_pos_ratio > 1)
        {
            this.cur_pos_ratio = 1;
        }
        this.cur_pos_ratio -= 0.023f;

        if (this.cur_pos_ratio < 0)
        {
            this.cur_pos_ratio = 0;
        }
    }
}
