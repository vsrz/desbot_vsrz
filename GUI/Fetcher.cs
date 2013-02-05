using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
namespace desBot
{
    /// <summary>
    /// Reader for state change notifications from the Bot
    /// </summary>
    static class Fetcher
    {
        /// <summary>
        /// The last receieved command response ID
        /// </summary>
        public static int LastReceivedID { get; private set; }

        /// <summary>
        /// Updates a collection with notification object
        /// </summary>
        /// <typeparam name="T">Non-serialized type</typeparam>
        /// <typeparam name="S">Serialized type</typeparam>
        /// <param name="obj">The object to identify</param>
        /// <param name="coll">The collection to update</param>
        /// <returns>True if the collection matched the notification object</returns>
        static bool UpdateCollection<T, S>(object obj, DynamicCollection<T, S> coll)
        {
            if (obj is AddNotification<S>)
            {
                coll.AddS(((AddNotification<S>)obj).Item);
                return true;
            }
            else if (obj is RemoveNotification<S>)
            {
                coll.RemoveS(((RemoveNotification<S>)obj).Item);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Updates a property with notfication object
        /// </summary>
        /// <typeparam name="T">Non-serialized type</typeparam>
        /// <typeparam name="S">Serialized type</typeparam>
        /// <param name="obj">The object to identify</param>
        /// <param name="prop">The property to update</param>
        /// <returns>True if the property matched the notification object</returns>
        static bool UpdateProperty<T, S>(object obj, DynamicProperty<T, S> prop)
        {
            if (obj is ChangeNotification<S>)
            {
                ChangeNotification<S> not = (ChangeNotification<S>)obj;
                if (not.Name == prop.Name)
                {
                    prop.SetValue(not.Item);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Called when the Bot sends us a notification
        /// </summary>
        /// <param name="source">The source (ignored)</param>
        /// <param name="obj">The notification object</param>
        public static void OnBotObject(Connection source, object obj)
        {
            try
            {
                if (obj is SerializableSettings)
                {
                    //initial settings
                    string result = Settings.ApplySettings((SerializableSettings)obj);
                    Program.Synchronized = true;
                }
                else if (obj is RemoteCommandResponse)
                {
                    //set last ID
                    LastReceivedID = ((RemoteCommandResponse)obj).ID;
                }
                else if (obj is RemoteLogEntry)
                {
                    //write log entry
                    Program.Log(((RemoteLogEntry)obj).Entry);
                }
                else if (obj is Stats)
                {
                    //update stats
                    if (Program.Form != null)
                    {
                        Program.Form.SetStats(obj as Stats);
                    }
                }
                else if (obj is AllSettings)
                {
                    //update settings
                    if (Program.Form != null)
                    {
                        Program.Form.ApplySettings(obj as AllSettings);
                    }
                }
                else if (State.ApplyUpdate(obj))
                {
                    //automatic State change from Bot.Watcher
                }
                else
                {
                    //unknown type
                    Program.Log("Unexpected message of type " + obj.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                //put message box on error
                MessageBox.Show("Failed to parse received object of type " + obj.GetType().Name + ":\n" + ex.Message, "Communication error");
            }
        }
    }

    /// <summary>
    /// Reporter utility for remote command watching
    /// </summary>
    class LastCommandReporter
    {
        /// <summary>
        /// The command to look for
        /// </summary>
        RemoteCommand lookfor;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="lookfor">The command to look for</param>
        public LastCommandReporter(RemoteCommand lookfor)
        {
            this.lookfor = lookfor;
        }

        /// <summary>
        /// Polls the state of the command
        /// </summary>
        /// <returns>null if the command being looked for was found, the command string otherwise</returns>
        public string Poll()
        {
            if (Fetcher.LastReceivedID >= lookfor.ID) return null;
            else return lookfor.Command;
        }
    }
}
