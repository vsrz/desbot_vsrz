using System;
namespace desBot
{
    static class Watcher
    {
        /// <summary>
        /// Initialize watcher
        /// </summary>
        public static void Init()
        {
            WatchCollection(State.BanList);
            WatchCollection(State.TriggerList);
            WatchCollection(State.UserList);
            WatchCollection(State.QuoteList);
            WatchCollection(State.AccountList);
            WatchCollection(State.MetaUserList);
            WatchProperty(State.QuoteInterval);
            WatchProperty(State.SendDelay);
            //WatchProperty(State.JtvSettings);
            //WatchProperty(State.IrcSettings);
            WatchProperty(State.ControlCharacters);
            WatchProperty(State.UseQEnforce);
            WatchProperty(State.UseQuietBan);
            WatchProperty(State.PublicIP);
            WatchProperty(State.ParseChannel);
            WatchProperty(State.WarningThreshold);
            WatchProperty(State.AntiSpamLevel);

        }

        /// <summary>
        /// Event handler for DynamicCollection.OnAdded
        /// </summary>
        static void OnAdd<T, S>(DynamicCollection<T, S> coll, T item)
        {
            Program.Form.AddItem(item);
        }

        /// <summary>
        /// Event handler for DynamicCollection.OnRemoved
        /// </summary>
        static void OnRemove<T, S>(DynamicCollection<T, S> coll, T item)
        {
            Program.Form.RemoveItem(item);
        }

        /// <summary>
        /// Event handler for DynamicProperty.OnChanged
        /// </summary>
        static void OnChanged<T, S>(DynamicProperty<T, S> prop)
        {
            Program.Form.SetProperty(prop.Name, prop.Value);
        }

        /// <summary>
        /// Attach event handlers to a collection
        /// </summary>
        static void WatchCollection<T, S>(DynamicCollection<T, S> collection)
        {
            collection.OnAdded += new AddedEventHandler<T, S>(OnAdd<T, S>);
            collection.OnRemoved += new RemovedEventHandler<T, S>(OnRemove<T, S>);
        }

        /// <summary>
        /// Attach event handlers to a property
        /// </summary>
        static void WatchProperty<T, S>(DynamicProperty<T, S> property)
        {
            property.OnChanged += new OnPropertyValueChanged<T, S>(OnChanged<T, S>);
        }
    }
}
