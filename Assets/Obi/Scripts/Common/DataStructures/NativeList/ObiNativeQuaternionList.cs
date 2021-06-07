using System;
using UnityEngine;

namespace Obi
{
    [Serializable]
    public class ObiNativeQuaternionList : ObiNativeList<Quaternion>, ISerializationCallbackReceiver
    {

        public Quaternion[] serializedContents;
        unsafe Quaternion* m_Ptr;

        public ObiNativeQuaternionList(int capacity = 8, int alignment = 16) : base(capacity, alignment)
        {
            for (int i = 0; i < capacity; ++i)
                this[i] = Quaternion.identity;
        }

        public ObiNativeQuaternionList(int capacity, int alignment, Quaternion defaultValue) : base(capacity, alignment)
        {
            for (int i = 0; i < capacity; ++i)
                this[i] = defaultValue;
        }

        public void OnBeforeSerialize()
        {
            if (m_AlignedPtr != IntPtr.Zero)
            {
                serializedContents = new Quaternion[m_Count];
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
                m_Ptr = (Quaternion*)m_AlignedPtr.ToPointer();
            }
        }

        public override Quaternion this[int index]
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

