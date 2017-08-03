using System;
using System.Collections.Generic;
using System.Text;

namespace CnDream.Core
{
    public abstract class Pool<T> : IPool<T>
    {
        public T Acquire()
        {
            throw new NotImplementedException();
        }

        public void Release( T t )
        {
            throw new NotImplementedException();
        }

        protected abstract T CreateObject();
    }
}
