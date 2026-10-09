
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeurekaGames.SmartBuilder
{
    public class OrderedQueue<T> where T:class
    {
        private readonly Dictionary<string, T> duplicates = new Dictionary<string, T>();
        private readonly Queue<string> queue = new Queue<string>();
        public int queueSize = 50;

        public bool Enqueue(string key, T item)
        {
            if (!duplicates.ContainsKey(key))
            {
                duplicates[key] = item;
                queue.Enqueue(key);

                while (queue.Count > queueSize)
                    Dequeue();

                return true;
            }

            return false;
        }

        internal void Clear()
        {
            duplicates.Clear();
            queue.Clear();
        }

        public T Dequeue()
        {
            if (queue.Count > 0)
            {
                var item = queue.Dequeue();
                T dequeued;
                if (!duplicates.ContainsKey(item))
                    throw new InvalidOperationException("The dictionary should have contained an item");
                else
                {
                    dequeued = duplicates[item];
                    duplicates.Remove(item);
                }

                return dequeued;
            }

            throw new InvalidOperationException("Can't dequeue on an empty queue.");
        }

        internal bool TryGetElement(string id, out T item)
        {
            if (duplicates.ContainsKey(id))
            {
                item = duplicates[id];
                return true;
            }

            item = null;
            return false;
        }

        internal int Count()
        {
            return duplicates.Count;
        }

        internal void SetCacheSize(int maxSimilarItems)
        {
            queueSize = maxSimilarItems;
        }
    }
}