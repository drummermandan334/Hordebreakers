using System.Collections.Generic;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Lightweight runtime pool for Components. Created in code (no per-frame allocation).
    /// Pre-warms on construction; grows on demand if exhausted.
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly Stack<T> _available = new Stack<T>();
        private readonly T _prefab;
        private readonly Transform _parent;

        public ObjectPool(T prefab, int initialSize, Transform parent = null)
        {
            _prefab = prefab;
            _parent = parent;
            for (int i = 0; i < initialSize; i++)
            {
                T inst = Object.Instantiate(_prefab, _parent);
                inst.gameObject.SetActive(false);
                _available.Push(inst);
            }
        }

        public T Get()
        {
            T inst = _available.Count > 0 ? _available.Pop() : Object.Instantiate(_prefab, _parent);
            inst.gameObject.SetActive(true);
            return inst;
        }

        public void Return(T inst)
        {
            inst.gameObject.SetActive(false);
            _available.Push(inst);
        }
    }
}
