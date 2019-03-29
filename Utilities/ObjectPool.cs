using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace StreamCore
{
    /// <summary>
    /// A dynamic pool of unity components of type T, that recycles old objects when possible, and allocates new objects when required.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObjectPool<T> where T : Component
    {
        private Stack<T> _freeObjects;
        private Action<T> FirstAlloc;
        private Action<T> OnAlloc;
        private Action<T> OnFree;

        /// <summary>
        /// ObjectPool constructor function, used to setup the initial pool size and callbacks.
        /// </summary>
        /// <param name="initialCount">The number of components of type T to allocate right away.</param>
        /// <param name="FirstAlloc">The callback function you want to occur only the first time when a new component of type T is allocated.</param>
        /// <param name="OnAlloc">The callback function to be called everytime ObjectPool.Alloc() is called.</param>
        /// <param name="OnFree">The callback function to be called everytime ObjectPool.Free() is called</param>
        public ObjectPool(int initialCount = 0, Action<T> FirstAlloc = null, Action<T> OnAlloc = null, Action<T> OnFree = null)
        {
            this.FirstAlloc = FirstAlloc;
            this.OnAlloc = OnAlloc;
            this.OnFree = OnFree;
            this._freeObjects = new Stack<T>();

            while (initialCount > 0)
            {
                _freeObjects.Push(internalAlloc());
                initialCount--;
            }
        }
        
        ~ObjectPool()
        {
            foreach(T obj in _freeObjects)
                UnityEngine.Object.Destroy(obj.gameObject);
        }

        private T internalAlloc()
        {
            T newObj = new GameObject().AddComponent<T>();
            UnityEngine.GameObject.DontDestroyOnLoad(newObj.gameObject);
            FirstAlloc?.Invoke(newObj);
            return newObj;
        }

        /// <summary>
        /// Allocates a component of type T from a pre-allocated pool, or instantiates a new one if required.
        /// </summary>
        /// <returns></returns>
        public T Alloc()
        {
            T obj = null;
            if (_freeObjects.Count > 0)
                obj = _freeObjects.Pop();
            if(!obj)
                obj = internalAlloc();
            OnAlloc?.Invoke(obj);
            return obj;
        }

        /// <summary>
        /// Inserts a component of type T into the stack of free objects. Note: the component does *not* need to be allocated using ObjectPool.Alloc() to be freed with this function!
        /// </summary>
        /// <param name="obj"></param>
        public void Free(T obj) {
            if (obj == null) return;
            _freeObjects.Push(obj);
            OnFree?.Invoke(obj);
        }
    }
}
