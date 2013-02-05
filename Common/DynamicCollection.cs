using System;
using System.Collections.Generic;
namespace desBot
{
    //event handlers
    public delegate void AddedEventHandler<T, S>(DynamicCollection<T, S> collection, T item);
    public delegate void RemovedEventHandler<T, S>(DynamicCollection<T, S> collection, T item);

    /// <summary>
    /// Notification for when an item is added to a collection
    /// </summary>
    /// <typeparam name="T">The type of item</typeparam>
    [Serializable]
    public class AddNotification<T>
    {
        /// <summary>
        /// The item being added
        /// </summary>
        public T Item;
    }

    /// <summary>
    /// Notification for when an item is removed from a collection
    /// </summary>
    /// <typeparam name="T">The type of item</typeparam>
    [Serializable]
    public class RemoveNotification<T>
    {
        /// <summary>
        /// The item being removed
        /// </summary>
        public T Item;
    }

    /// <summary>
    /// Interface for a dynamic collection that has some events
    /// </summary>
    /// <typeparam name="T">The type inside the collection</typeparam>
    public abstract class DynamicCollection<T, S> : DynamicUpdatable
    {
        static ISerializer<T, S> serializer = FindSerializer.Find<T, S>();
        public static ISerializer<T, S> Serializer { get { return serializer; } }

        public event AddedEventHandler<T, S> OnAdded;
        public event RemovedEventHandler<T, S> OnRemoved;
        protected void RaiseAdded(T item) { if (OnAdded != null) OnAdded.Invoke(this, item); }
        protected void RaiseRemoved(T item) { if (OnRemoved != null) OnRemoved.Invoke(this, item); }

        public abstract IEnumerable<T> GetItems();
        public abstract void Add(T item);
        
        public abstract void Remove(T item);
        public abstract int RemoveIf(Predicate<T> predicate);
        public abstract int GetCount();
        public int CountIf(Predicate<T> predicate)
        {
            int count = 0;
            foreach (T item in GetItems())
            {
                if (predicate(item)) count++;
            }
            return count;
        }

        public void AddS(S item)
        {
            Add(Serializer.Deserialize(item));
        }
        public void Add(IEnumerable<T> items)
        {
            foreach (T item in items) Add(item);
        }
        public void RemoveS(S item)
        {
            T d = serializer.Deserialize(item);
            foreach (T needle in GetItems())
            {
                if (needle.Equals(d))
                {
                    Remove(needle);
                    return;
                }
            }
            throw new Exception("Item not in collection");
        }

        public void MarkChanged(T item)
        {
            Remove(item);
            Add(item);
        }

        internal override bool ApplyUpdate(object obj)
        {
            if (obj is AddNotification<S>)
            {
                AddNotification<S> notification = (AddNotification<S>)obj;
                AddS(notification.Item);
                return true;
            }
            if (obj is RemoveNotification<S>)
            {
                RemoveNotification<S> notification = (RemoveNotification<S>)obj;
                RemoveS(notification.Item);
                return true;
            }
            return false;
        }
        
    }

    /// <summary>
    /// Implementation of IDynamicCollection using a List
    /// </summary>
    /// <typeparam name="T">The type inside the collection</typeparam>
    public class DynamicList<T, S> : DynamicCollection<T, S>
    {
        List<T> items = new List<T>();

        public override IEnumerable<T> GetItems() { return items; }

        public override void Add(T item)
        {
            if (items.Contains(item)) throw new Exception("Item already in collection");
            items.Add(item);
            RaiseAdded(item);
        }

        public override void Remove(T item)
        {
            if (!items.Contains(item)) throw new Exception("Item not in collection");
            items.Remove(item);
            RaiseRemoved(item);
        }

        public override int RemoveIf(Predicate<T> predicate)
        {
            return items.RemoveAll(predicate);
        }

        public override int GetCount()
        {
            return items.Count;
        }

        internal override void Reset()
        {
            items.Clear();
        }

        public T this[int index]
	    {
		    get
            {
                return items[index];
            }
	    }
    }

    /// <summary>
    /// Interface for a class to be used as a value in a dictionary
    /// </summary>
    public interface IKeyInsideValue<K>
    {
        K GetKey();
    }

    /// <summary>
    /// Implements IDynamicCollection using a Dictionary
    /// </summary>
    /// <typeparam name="K">The key type (returned by T)</typeparam>
    /// <typeparam name="T">The type inside the collection</typeparam>
    public class DynamicDictionary<K, T, S> : DynamicCollection<T, S> where T : IKeyInsideValue<K>
    {
        Dictionary<K, T> items;

        public DynamicDictionary() { items = new Dictionary<K, T>(); }
        public DynamicDictionary(IEqualityComparer<K> comp) { items = new Dictionary<K, T>(comp); }

        public override IEnumerable<T> GetItems() { return items.Values; }

        public override void Add(T item)
        {
            if (item == null) throw new Exception("Cannot add null-reference");
            K key = item.GetKey();
            if (items.ContainsKey(key)) throw new Exception("Item already in collection");
            items.Add(key, item);
            RaiseAdded(item);
        }

        public override void Remove(T item)
        {
            if (item == null) throw new Exception("Item not in collection");
            K key = item.GetKey();
            if (!items.ContainsKey(key)) throw new Exception("Item not in collection");
            items.Remove(key);
            RaiseRemoved(item);
        }

        public override int RemoveIf(Predicate<T> predicate)
        {
            List<K> keys = new List<K>();
            foreach (T needle in items.Values)
            {
                if (predicate(needle)) keys.Add(needle.GetKey());
            }
            foreach (K key in keys)
            {
                items.Remove(key);
            }
            return keys.Count;
        }

        public override int GetCount()
        {
            return items.Count;
        }

        internal override void Reset()
        {
            items.Clear();
        }

        public T Lookup(K key)
        {
            T result;
            return items.TryGetValue(key, out result) ? result : default(T);
        }

        public T this[K key]
        {
            get
            {
                return Lookup(key);
            }
        }
    }
}