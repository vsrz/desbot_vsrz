using System;
using System.Net;
namespace desBot
{
    /// <summary>
    /// Watches the state for changes
    /// </summary>
    static class Watcher
    {
        /// <summary>
        /// The listener to send notifications on
        /// </summary>
        static Listener listener;

        /// <summary>
        /// Initialize watcher
        /// </summary>
        public static void Init()
        {
            listener = new Listener(new Listener.LogDelegate(Program.PrivateLog));
            listener.OnReceive += new Connection.OnReceiveEventHandler(listener_OnReceive);
            WatchState();
        }

        /// <summary>
        /// Triggers when an object is received
        /// </summary>
        /// <param name="source">Source connection</param>
        /// <param name="obj">The received object</param>
        static void listener_OnReceive(Connection source, object obj)
        {
            lock (State.GlobalSync)
            {
                if (obj is RemoteCommand)
                {
                    Program.ExecuteRemoteCommand(source, obj as RemoteCommand);
                }
                else if (obj is SerializableSettings)
                {
                    Program.RestoreBackup(obj as SerializableSettings);
                }
                else if (obj is AllSettings)
                {
                    State.JtvSettings.Value = ((AllSettings)obj).JTV;
                    State.IrcSettings.Value = ((AllSettings)obj).QNet;
                    try
                    {
                        Irc.Init();
                    }
                    catch (Exception ex)
                    {
                        Program.PrivateLog("IRC init exception: " + ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Event handler for state reset
        /// </summary>
        static void OnReset()
        {
            listener.Send(new ResetNotification());
        }

        /// <summary>
        /// Event handler for DynamicCollection.OnAdded
        /// </summary>
        static void OnAdd<T, S>(DynamicCollection<T, S> coll, T item)
        {
            AddNotification<S> notification = new AddNotification<S>();
            notification.Item = DynamicCollection<T, S>.Serializer.Serialize(item);
            listener.Send(notification);
        }

        /// <summary>
        /// Event handler for DynamicCollection.OnRemoved
        /// </summary>
        static void OnRemove<T, S>(DynamicCollection<T, S> coll, T item)
        {
            RemoveNotification<S> notification = new RemoveNotification<S>();
            notification.Item = DynamicCollection<T, S>.Serializer.Serialize(item);
            listener.Send(notification);
        }

        /// <summary>
        /// Event handler for DynamicProperty.OnChanged
        /// </summary>
        static void OnChanged<T, S>(DynamicProperty<T, S> prop)
        {
            ChangeNotification<S> notification = new ChangeNotification<S>();
            notification.Name = prop.Name;
            notification.Item = DynamicProperty<T, S>.Serializer.Serialize(prop.Value);
            listener.Send(notification);
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

        /// <summary>
        /// Attach event handlers to the State
        /// </summary>
        static void WatchState()
        {
            State.OnReset += new OnStateReset(OnReset);
            WatchCollection(State.BanList);
            WatchCollection(State.TriggerList);
            WatchCollection(State.UserList);
            WatchCollection(State.QuoteList);
            WatchCollection(State.AccountList);
            WatchCollection(State.MetaUserList);
            WatchCollection(State.LimiterList);
            WatchProperty(State.SendDelay);
            WatchProperty(State.QuoteInterval);
            WatchProperty(State.JtvSettings);
            WatchProperty(State.IrcSettings);
            WatchProperty(State.ControlCharacters);
            WatchProperty(State.UseQEnforce);
            WatchProperty(State.UseQuietBan);
            WatchProperty(State.PublicIP);
            WatchProperty(State.ParseChannel);
            WatchProperty(State.WarningThreshold);
            WatchProperty(State.AntiSpamLevel);
        }

        /// <summary>
        /// Write the log entry to remote GUIs
        /// </summary>
        /// <param name="entry">The entry to write</param>
        public static void WriteLog(string text)
        {
            if (listener != null)
            {
                RemoteLogEntry entry = new RemoteLogEntry();
                entry.Entry = text;
                listener.Send(entry);
            }
        }

        /// <summary>
        /// Publish statistics
        /// </summary>
        /// <param name="stats">The statistics to publish</param>
        public static void PublishStats(Stats stats)
        {
            if (listener != null)
            {
                listener.Send(stats);
            }
        }
    }
}