using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Obi
{

    public static class Constants
    {
        public const int maxVertsPerMesh = 65000;
        public const int maxInstancesPerBatch = 1023;
    }

    public static class ObiUtils
    {

        // Colour alphabet from https://www.aic-color.org/resources/Documents/jaic_v5_06.pdf
        public static readonly Color32[] colorAlphabet = new Color32[26]
            {
                new Color32(240,163,255,255), 
                new Color32(0,117,220,255),
                new Color32(153,63,0,255),
                new Color32(76,0,92,255),
                new Color32(25,25,25,255),
                new Color32(0,92,49,255),
                new Color32(43,206,72,255),
                new Color32(255,204,153,255),
                new Color32(128,128,128,255),
                new Color32(148,255,181,255),
                new Color32(143,124,0,255),
                new Color32(157,204,0,255),
                new Color32(194,0,136,255),
                new Color32(0,51,128,255),
                new Color32(255,164,5,255),
                new Color32(255,168,187,255),
                new Color32(66,102,0,255),
                new Color32(255,0,16,255),
                new Color32(94,241,242,255),
                new Color32(0,153,143,255),
                new Color32(224,255,102,255),
                new Color32(116,10,255,255),
                new Color32(153,0,0,255),
                new Color32(255,255,128,255),  
                new Color32(255,255,0,255),
                new Color32(255,80,5,255)
            };

    public static void DrawArrowGizmo(float bodyLenght, float bodyWidth, float headLenght, float headWidth)
        {

            float halfBodyLenght = bodyLenght * 0.5f;
            float halfBodyWidth = bodyWidth * 0.5f;

            // arrow body:
            Gizmos.DrawLine(new Vector3(halfBodyWidth, 0, -halfBodyLenght), new Vector3(halfBodyWidth, 0, halfBodyLenght));
            Gizmos.DrawLine(new Vector3(-halfBodyWidth, 0, -halfBodyLenght), new Vector3(-halfBodyWidth, 0, halfBodyLenght));
            Gizmos.DrawLine(new Vector3(-halfBodyWidth, 0, -halfBodyLenght), new Vector3(halfBodyWidth, 0, -halfBodyLenght));

            // arrow head:
            Gizmos.DrawLine(new Vector3(halfBodyWidth, 0, halfBodyLenght), new Vector3(headWidth, 0, halfBodyLenght));
            Gizmos.DrawLine(new Vector3(-halfBodyWidth, 0, halfBodyLenght), new Vector3(-headWidth, 0, halfBodyLenght));
            Gizmos.DrawLine(new Vector3(0, 0, halfBodyLenght + headLenght), new Vector3(headWidth, 0, halfBodyLenght));
            Gizmos.DrawLine(new Vector3(0, 0, halfBodyLenght + headLenght), new Vector3(-headWidth, 0, halfBodyLenght));
        }

        public static void DebugDrawCross(Vector3 pos, float size, Color color)
        {
            Debug.DrawLine(pos - Vector3.right * size, pos + Vector3.right * size, color);
            Debug.DrawLine(pos - Vector3.up * size, pos + Vector3.up * size, color);
            Debug.DrawLine(pos - Vector3.forward * size, pos + Vector3.forward * size, color);
        }

        public static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp = lhs;
            lhs = rhs;
            rhs = temp;
        }

        public static void Swap<T>(this T[] source, int index1, int index2)
        {
            if (source != null && index1 >= 0 && index2 != 0 && index1 < source.Length && index2 < source.Length)
            {
                T temp = source[index1];
                source[index1] = source[index2];
                source[index2] = temp;
            }
        }

        public static void Swap<T>(this IList<T> list, int index1, int index2)
        {
            if (list != null && index1 >= 0 && index2 != 0 && index1 < list.Count && index2 < list.Count)
            {
                T temp = list[index1];
                list[index1] = list[index2];
                list[index2] = temp;
            }
        }

        public static void ShiftLeft<T>(this T[] source, int index, int count, int positions)
        {
            for (int j = 0; j < positions; ++j)
            {
                for (int i = index; i < index + count; ++i)
                    source.Swap(i, i - 1);
                index--;
            }
        }

        public static void ShiftRight<T>(this T[] source, int index, int count, int positions)
        {
            for (int j = 0; j < positions; ++j)
            {
                for (int i = index + count - 1; i >= index; --i)
                    source.Swap(i, i + 1);
                index++;
            }
        }

        public static bool AreValid(this Bounds bounds)
        {
            return !(float.IsNaN(bounds.center.x) || float.IsInfinity(bounds.center.x) ||
                     float.IsNaN(bounds.center.y) || float.IsInfinity(bounds.center.y) ||
                     float.IsNaN(bounds.center.z) || float.IsInfinity(bounds.center.z));
        }

        public static Bounds Transform(this Bounds b, Matrix4x4 m)
        {
            var xa = m.GetColumn(0) * b.min.x;
            var xb = m.GetColumn(0) * b.max.x;

            var ya = m.GetColumn(1) * b.min.y;
            var yb = m.GetColumn(1) * b.max.y;

            var za = m.GetColumn(2) * b.min.z;
            var zb = m.GetColumn(2) * b.max.z;

            Bounds result = new Bounds();
            Vector3 pos = m.GetColumn(3);
            result.SetMinMax(Vector3.Min(xa, xb) + Vector3.Min(ya, yb) + Vector3.Min(za, zb) + pos,
                             Vector3.Max(xa, xb) + Vector3.Max(ya, yb) + Vector3.Max(za, zb) + pos);


            return result;
        }

        public static void Add(Vector3 a, Vector3 b, ref Vector3 result)
        {
            result.x = a.x + b.x;
            result.y = a.y + b.y;
            result.z = a.z + b.z;
        }

        public static float Remap(this float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }

        /**
         * Modulo operator that also follows intuition for negative arguments. That is , -1 mod 3 = 2, not -1.
         */
        public static float Mod(float a, float b)
        {
            return a - b * Mathf.Floor(a / b);
        }

        public static Matrix4x4 Add(this Matrix4x4 a, Matrix4x4 other)
        {
            for (int i = 0; i < 16; ++i)
                a[i] += other[i];
            return a;
        }

        public static Matrix4x4 ScalarMultiply(this Matrix4x4 a, float s)
        {
            for (int i = 0; i < 16; ++i)
                a[i] *= s;
            return a;
        }

        public static Vector3 ProjectPointLine(Vector3 point, Vector3 lineStart, Vector3 lineEnd, out float mu, bool clampToSegment = true)
        {
            Vector3 ap = point - lineStart;
            Vector3 ab = lineEnd - lineStart;

            mu = Vector3.Dot(ap, ab) / Vector3.Dot(ab, ab);

            if (clampToSegment)
                mu = Mathf.Clamp01(mu);

            return lineStart + ab * mu;
        }

        public static bool LinePlaneIntersection(Vector3 planePoint, Vector3 planeNormal, Vector3 linePoint, Vector3 lineDirection, out Vector3 point)
        {
            point = linePoint;
            Vector3 lineNormal = lineDirection.normalized;
            float denom = Vector3.Dot(planeNormal, lineNormal);

            if (Mathf.Approximately(denom, 0))
                return false;

            float t = (Vector3.Dot(planeNormal,planePoint) - Vector3.Dot(planeNormal,linePoint)) / denom;
            point = linePoint + lineNormal * t;
            return true;
        }

        public static float InvMassToMass(float invMass)
        {
            return 1.0f / invMass;
        }

        public static float MassToInvMass(float mass)
        {
            return 1.0f / Mathf.Max(mass, 0.00001f);
        }

        /**
         * Calculates the area of a triangle.
         */
        public static float TriangleArea(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            return Mathf.Sqrt(Vector3.Cross(p2 - p1, p3 - p1).sqrMagnitude) / 2f;
        }

        public static float EllipsoidVolume(Vector3 principalRadii)
        {
            return 4.0f / 3.0f * Mathf.PI * principalRadii.x * principalRadii.y * principalRadii.z;
        }

        public static Quaternion RestDarboux(Quaternion q1, Quaternion q2)
        {
            Quaternion darboux = Quaternion.Inverse(q1) * q2;
            Vector4 omega_plus, omega_minus;
            omega_plus = new Vector4(darboux.w, darboux.x, darboux.y, darboux.z) + new Vector4(1, 0, 0, 0);
            omega_minus = new Vector4(darboux.w, darboux.x, darboux.y, darboux.z) - new Vector4(1, 0, 0, 0);
            if (omega_minus.sqrMagnitude > omega_plus.sqrMagnitude)
            {
                darboux = new Quaternion(darboux.x * -1, darboux.y * -1, darboux.z * -1, darboux.w * -1);
            }
            return darboux;
        }

        public static System.Collections.IEnumerable BilateralInterleaved(int count)
        {
            for (int i = 0; i < count; ++i)
            {
                if (i % 2 != 0)
                    yield return count - (count % 2) - i;
                else yield return i;
            }
        }

        public static Vector3 BarycentricInterpolation(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 coords)
        {
            return coords[0] * p1 + coords[1] * p2 + coords[2] * p3;
        }

        public static float BarycentricInterpolation(float p1, float p2, float p3, Vector3 coords)
        {
            return coords[0] * p1 + coords[1] * p2 + coords[2] * p3;
        }

        public static float BarycentricExtrapolationScale(Vector3 coords)
        {

            return 1.0f / (coords[0] * coords[0] +
                           coords[1] * coords[1] +
                           coords[2] * coords[2]);

        }

    }
}

