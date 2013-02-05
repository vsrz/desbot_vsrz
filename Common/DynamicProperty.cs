using System;
namespace desBot
{
    /// <summary>
    /// Base class for a dynamic updatable object
    /// </summary>
    public abstract class DynamicUpdatable
    {
        /// <summary>
        /// Constructor
        /// </summary>
        internal protected DynamicUpdatable()
        {
            State.AddMember(this);
        }

        /// <summary>
        /// Apply update object
        /// </summary>
        /// <param name="obj">The update notification object to apply</param>
        /// <returns>True if the notification was applied</returns>
        internal abstract bool ApplyUpdate(object obj);

        /// <summary>
        /// Reset object
        /// </summary>
        internal abstract void Reset();
    }

    /// <summary>
    /// Event handler for when a DynamicProperty value changes
    /// </summary>
    /// <typeparam name="T">The property type</typeparam>
    /// <typeparam name="S">The serialized property type</typeparam>
    /// <param name="property">The property that had it's value changed</param>
    public delegate void OnPropertyValueChanged<T, S>(DynamicProperty<T, S> property);

    /// <summary>
    /// Notification for when an item is removed from a collection
    /// </summary>
    /// <typeparam name="T">The type of item</typeparam>
    [Serializable]
    public class ChangeNotification<T>
    {
        /// <summary>
        /// The name of the property
        /// </summary>
        public string Name;

        /// <summary>
        /// The new value of the property
        /// </summary>
        public T Item;
    }

    /// <summary>
    /// Notification for when state is reset
    /// </summary>
    [Serializable]
    public class ResetNotification
    {
        public int Dummy;
    }
    
    /// <summary>
    /// A dynamic property with change tracking
    /// </summary>
    /// <typeparam name="T">The property type</typeparam>
    public class DynamicProperty<T, S> : DynamicUpdatable
    {
        static ISerializer<T, S> serializer = FindSerializer.Find<T, S>();
        string name;
        T val;
        T initial;

        public OnPropertyValueChanged<T, S> OnChanged;
        public string Name { get { return name; } }
        public static ISerializer<T, S> Serializer { get { return serializer; } }

        public DynamicProperty(string name, T initial)
        {
            this.initial = initial;
            this.name = name;
            val = initial;
        }

        public void SetValue(T value)
        {
            if (!val.Equals(value))
            {
                val = value;
                if (OnChanged != null) OnChanged.Invoke(this);
            }
        }

        public void SetValue(S value)
        {
            SetValue(serializer.Deserialize(value));
        }

        public T Value { get { return val; } set { SetValue(value); } }

        internal override bool ApplyUpdate(object obj)
        {
            if (obj is ChangeNotification<S>)
            {
                ChangeNotification<S> notification = (ChangeNotification<S>)obj;
                if (notification.Name == Name)
                {
                    SetValue(notification.Item);
                    return true;
                }
            }
            return false;
        }

        internal override void Reset()
        {
            val = initial;
        }
    }
}