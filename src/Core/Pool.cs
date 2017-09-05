using System;
using System.Collections.Concurrent;

namespace CnDream.Core
{
    public abstract class Pool<T> : IPool<T>
    {
        readonly ConcurrentBag<T> FreeObjects = new ConcurrentBag<T>();

        public virtual T Acquire()
        {
            if ( FreeObjects.TryTake(out var result) )
            {
                return result;
            }
            else
            {
                return CreateObject();
            }
        }

        public virtual void Release( T t )
        {
            FreeObjects.Add(t);
        }

        public PooledObject<T> GetPooledObject() => new PooledObject<T>(this, Acquire());

        protected abstract T CreateObject();
    }
}
