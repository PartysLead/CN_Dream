using System;
using System.Collections.Concurrent;

namespace CnDream.Core
{
    public abstract class Pool<T> : IPool<T>
    {
        protected ConcurrentBag<T> FreeObjects = new ConcurrentBag<T>();

        public virtual T Acquire()
        {
            if ( FreeObjects.TryTake(out var result) || CreateObject(out result) )
            {
                return result;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public virtual void Release( T t )
        {
            FreeObjects.Add(t);
        }

        protected abstract bool CreateObject( out T obj );
    }
}
