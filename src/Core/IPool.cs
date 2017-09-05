using System;
using System.Collections.Generic;
using System.Text;

namespace CnDream.Core
{
    public interface IPool<T>
    {
        T Acquire();
        void Release( T t );

        PooledObject<T> GetPooledObject();
    }
}
