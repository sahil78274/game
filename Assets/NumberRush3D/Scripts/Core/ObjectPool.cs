using System;
using System.Collections.Generic;
using UnityEngine;

namespace NumberRush3D.Core
{
    /// <summary>
    /// Small, allocation free pool for components (bullets, enemies, sparks).
    /// Items are deactivated while idle and re-parented to the pool root so the hierarchy stays tidy.
    /// The pool never grows past <c>maxSize</c>: when it is exhausted <see cref="Get"/> returns null and
    /// the caller is expected to skip the spawn (that is how the bullet cap is enforced).
    /// </summary>
    public sealed class ObjectPool<T> where T : Component
    {
        private readonly Func<T> _factory;
        private readonly Transform _root;
        private readonly Stack<T> _idle;
        private readonly HashSet<T> _idleLookup;
        private readonly int _maxSize;

        /// <summary>Total number of instances the pool has ever created.</summary>
        public int TotalCreated { get; private set; }

        public int IdleCount => _idle.Count;

        public int ActiveCount => TotalCreated - _idle.Count;

        public ObjectPool(Func<T> factory, Transform root, int prewarm = 0, int maxSize = 512)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _root = root;
            _maxSize = Mathf.Max(1, maxSize);
            _idle = new Stack<T>(Mathf.Clamp(prewarm, 4, 512));
            _idleLookup = new HashSet<T>();

            for (int i = 0; i < prewarm; i++)
            {
                T item = CreateItem();
                if (item == null)
                {
                    break;
                }

                _idle.Push(item);
                _idleLookup.Add(item);
            }
        }

        /// <summary>
        /// Take an item out of the pool, position it and activate it.
        /// Returns null when the pool is at capacity and nothing is idle.
        /// </summary>
        public T Get(Vector3 position, Quaternion rotation)
        {
            T item = null;

            while (_idle.Count > 0 && item == null)
            {
                item = _idle.Pop();
            }

            if (item != null)
            {
                _idleLookup.Remove(item);
            }
            else if (TotalCreated < _maxSize)
            {
                item = CreateItem();
            }

            if (item == null)
            {
                return null;
            }

            Transform itemTransform = item.transform;
            itemTransform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            return item;
        }

        /// <summary>Return an item to the pool. Safe to call twice (the second call is ignored).</summary>
        public void Release(T item)
        {
            if (item == null || !_idleLookup.Add(item))
            {
                return;
            }

            item.gameObject.SetActive(false);
            if (_root != null)
            {
                item.transform.SetParent(_root, false);
            }

            _idle.Push(item);
        }

        private T CreateItem()
        {
            T item = _factory();
            if (item == null)
            {
                Debug.LogError($"[NumberRush] ObjectPool<{typeof(T).Name}> factory returned null.");
                return null;
            }

            TotalCreated++;
            item.gameObject.SetActive(false);
            if (_root != null)
            {
                item.transform.SetParent(_root, false);
            }

            return item;
        }
    }
}
