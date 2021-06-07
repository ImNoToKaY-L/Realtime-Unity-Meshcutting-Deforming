using System;
using UnityEngine;

namespace Obi
{
    [Serializable]
    public class ObiNativeVector4List : ObiNativeList<Vector4>, ISerializationCallbackReceiver
    {
        public Vector4[] serializedContents;
        unsafe Vector4* m_Ptr;

        public ObiNativeVector4List(int capacity = 8, int alignment = 16) : base(capacity, alignment)
        {
            for (int i = 0; i < capacity; ++i)
                this[i] = Vector4.zero;
        }

        public void OnBeforeSerialize()
        {
            if (m_AlignedPtr != IntPtr.Zero)
            {
                serializedContents = new Vector4[m_Count];
                for (int i = 0; i < m_Count; ++i)
                    serializedContents[i] = this[i];
            }
        }

        public void OnAfterDeserialize()
        {
            if (serializedContents != null)
            {
                ResizeUninitialized(serializedContents.Length);
                for (int i = 0; i < serializedContents.Length; ++i)
                    this[i] = serializedContents[i];
            }
        }

        protected override void CapacityChanged()
        {
            unsafe
            {
                m_Ptr = (Vector4*)m_AlignedPtr.ToPointer();
            }
        }

        public override Vector4 this[int index]
        {
            get
            {
                unsafe
                {
                    return m_Ptr[index];
                }
            }
            set
            {
                unsafe
                {
                    m_Ptr[index] = value;
                }
            }
        }

        public Vector3 GetVector3(int index)
        {
            unsafe
            {
                byte* start = (byte*)m_Ptr + index * sizeof(Vector4);
                return *(Vector3*)start;
            }
        }

        public void SetVector3(int index, Vector3 value)
        {
            unsafe
            {
                byte* start = (byte*)m_Ptr + index * sizeof(Vector4);
                *(Vector3*)start = value;
            }
        }
    }
}

