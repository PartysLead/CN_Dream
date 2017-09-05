using System;

namespace CnDream.Core
{
    public struct PooledObject<T> : IDisposable
    {
        readonly IPool<T> Pool;

        public T Value { get; }

        public PooledObject( IPool<T> pool, T value )
        {
            Pool = pool;
            Value = value;
        }

        public void Dispose() => Pool.Release(Value);
    }
}
