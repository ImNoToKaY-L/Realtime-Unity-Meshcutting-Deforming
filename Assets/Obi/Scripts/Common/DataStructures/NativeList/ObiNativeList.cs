using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

namespace Obi
{
    public abstract class ObiNativeList<T> : IDisposable where T : struct
    {
        private IntPtr m_RawPtr = IntPtr.Zero;
        protected IntPtr m_AlignedPtr = IntPtr.Zero;

        protected int m_TypeSize;
        protected int m_Alignment;
        protected int m_Capacity;
        protected int m_Count;

        public int count
        {
            set 
            {
                if (value != m_Count)
                {
                    EnsureCapacity(m_Count);
                    m_Count = value;
                }
            }
            get { return m_Count; }
        }

        public int capacity
        {
            set
            {
                if (value != m_Capacity)
                    ChangeCapacity(value);
            }
            get { return m_Capacity; }
        }

        public abstract T this[int index]
        {
            get;
            set;
        }

        public ObiNativeList(int capacity = 8, int alignment = 16)
        {
            this.m_Alignment = 16;
            ChangeCapacity(capacity);
        }

        ~ObiNativeList()
        {
            Dispose(false);
        }

        protected void Dispose(bool disposing)
        {
            if (m_RawPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(m_RawPtr);
                m_RawPtr = IntPtr.Zero;
            }
        }

        public void Dispose() 
        {
            Dispose(true);
        }

        public void Clear()
        {
            m_Count = 0;
        }

        protected void ChangeCapacity(int newCapacity, int byteAlignment = 16)
        {
            // allocate slightly more memory than necessary, allowing for some "wiggle" for alignment
            m_TypeSize = Marshal.SizeOf(default(T));
            var newRawPtr = Marshal.AllocHGlobal(newCapacity * m_TypeSize + byteAlignment);

            // round up rawPtr to nearest 'byteAlignment' boundary
            var newAlignedPtr = new IntPtr(((long)newRawPtr + byteAlignment - 1) & ~(byteAlignment - 1));

            // if there was a previous allocation:
            if (m_AlignedPtr != IntPtr.Zero)
            {
                // copy contents from previous memory region
                unsafe
                {
                    UnsafeUtility.MemCpy(newAlignedPtr.ToPointer(), m_AlignedPtr.ToPointer(), Mathf.Min(newCapacity,m_Capacity) * m_TypeSize);
                }

                // free previous memory region
                Marshal.FreeHGlobal(m_RawPtr);
            }

            // get hold of new pointers/capacity.
            m_RawPtr = newRawPtr;
            m_AlignedPtr = newAlignedPtr;
            m_Capacity = newCapacity;

            CapacityChanged();
        }

        protected virtual void CapacityChanged(){}

        public void CopyFrom(ObiNativeList<T> other)
        {
            if (other == null)
                throw new ArgumentNullException();

            if (m_Count < other.m_Count)
                throw new ArgumentException();

            unsafe
            {
                UnsafeUtility.MemCpy(m_AlignedPtr.ToPointer(), other.m_AlignedPtr.ToPointer(), other.count * m_TypeSize);
            }
        }

        public void Add(T item)
        {
            this[m_Count] = item;
            EnsureCapacity(++m_Count);
        }


        public void AddRange(IEnumerable<T> enumerable)
        {
            ICollection<T> collection = enumerable as ICollection<T>;
            if (collection != null)
            {
                if (collection.Count > 0)
                {
                    EnsureCapacity(m_Count + collection.Count);
                }
            }

            using (IEnumerator<T> enumerator = collection.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    Add(enumerator.Current);
                }
            }
        }

        /**
         * Ensures a minimal capacity of count elements, then sets the new count. Useful when passing the backing array to C++
         * for being filled with new data.
         */
        public void ResizeUninitialized(int newCount)
        {
            EnsureCapacity(newCount);
            m_Count = newCount;
        }

        public void ResizeInitialized(int newCount, T value = default(T))
        {
            bool initialize = newCount >= m_Capacity || m_AlignedPtr == IntPtr.Zero;

            ResizeUninitialized(newCount);

            if (initialize)
            {
                for (int i = m_Count; i < m_Capacity; ++i)
                    this[i] = value;
            }
        }

        protected void EnsureCapacity(int min)
        {
            if (min >= m_Capacity || m_AlignedPtr == IntPtr.Zero)
                ChangeCapacity(min * 2);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('[');

            for (int t = 0; t < m_Count; t++)
            {
                sb.Append(this[t].ToString());

                if (t < (m_Count - 1)) sb.Append(',');

            }
            sb.Append(']');
            return sb.ToString();
        }

        public IntPtr GetIntPtr()
        {
            return m_AlignedPtr;
        }

        public void Swap(int index1, int index2)
        {
            // check to avoid out of bounds access:
            if (index1 >= 0 && index1 < count && index2 >= 0 && index2 < count)
            {
                var aux = this[index1];
                this[index1] = this[index2];
                this[index2] = aux;
            }
        }
    }
}

