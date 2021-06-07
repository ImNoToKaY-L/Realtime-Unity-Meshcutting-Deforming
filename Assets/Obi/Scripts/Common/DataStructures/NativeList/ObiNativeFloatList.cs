using System;
using UnityEngine;

namespace Obi
{
    [Serializable]
    public class ObiNativeFloatList : ObiNativeList<float>, ISerializationCallbackReceiver
    {
        public float[] serializedContents;
        unsafe float* m_Ptr;

        public ObiNativeFloatList(int capacity = 8, int alignment = 16) : base(capacity, alignment)
        {
            for (int i = 0; i < capacity; ++i)
                this[i] = 0;
        }

        public void OnBeforeSerialize()
        {
            if (m_AlignedPtr != IntPtr.Zero)
            {
                serializedContents = new float[m_Count];
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
                m_Ptr = (float*)m_AlignedPtr.ToPointer();
            }
        }

        public override float this[int index]
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
    }
}

