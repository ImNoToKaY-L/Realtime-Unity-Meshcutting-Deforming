using System;
using UnityEngine;

namespace Obi
{
    [Serializable]
    public class ObiNativeVector3List : ObiNativeList<Vector3>, ISerializationCallbackReceiver
    {
        public Vector3[] serializedContents;
        unsafe Vector3* m_Ptr;

        public ObiNativeVector3List(int capacity = 8, int alignment = 16) : base(capacity, alignment)
        {
            for (int i = 0; i < capacity; ++i)
                this[i] = Vector3.zero;
        }

        public void OnBeforeSerialize()
        {
            if (m_AlignedPtr != IntPtr.Zero)
            {
                serializedContents = new Vector3[m_Count];
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
                m_Ptr = (Vector3*)m_AlignedPtr.ToPointer();
            }
        }

        public override Vector3 this[int index]
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

