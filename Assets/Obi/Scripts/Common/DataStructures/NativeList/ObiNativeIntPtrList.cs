using System;
using UnityEngine;

namespace Obi
{
    public class ObiNativeIntPtrList : ObiNativeList<IntPtr>
    {
        unsafe IntPtr* m_Ptr;

        public ObiNativeIntPtrList(int capacity = 8, int alignment = 16) : base(capacity, alignment)
        {
            for (int i = 0; i < capacity; ++i)
                this[i] = IntPtr.Zero;
        }

        protected override void CapacityChanged()
        {
            unsafe
            {
                m_Ptr = (IntPtr*)m_AlignedPtr.ToPointer();
            }
        }

        public override IntPtr this[int index]
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

