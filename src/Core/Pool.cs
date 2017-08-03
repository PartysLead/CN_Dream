using System;
using System.Collections.Generic;
using System.Text;

namespace CnDream.Core
{
    public abstract class Pool<T> : IPool<T>
    {
        protected T[] FreeObjects;

        public virtual T Acquire()
        {
            throw new NotImplementedException();
        }

        public virtual void Release( T t )
        {
            throw new NotImplementedException();
        }

        protected abstract bool CreateObject( out T obj );
    }
}
