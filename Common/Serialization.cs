using System;
using System.Collections.Generic;
using System.IO;
using XS = System.Xml.Serialization;
using BS = System.Runtime.Serialization.Formatters.Binary;
namespace desBot
{
    /// <summary>
    /// All settings that should be persisted to file
    /// </summary>
    [Serializable]
    public class SerializableSettings
    {
        //JTV settings
        public JtvSettings JTV = new JtvSettings();

        //IRC settings
        public IrcSettings IRC = new IrcSettings();

        //flag for control characters
        public bool ControlChars = true;

        //flag for Q enforce
        public bool UseQEnforce = false;

        //use quiet ban
        public bool UseQuietBan = true;

        //send delay
        public int SendDelay = 750;

        //accounts
        public List<SerializableAccount> AccountList = new List<SerializableAccount>();

        //users
        public List<SerializableUser> UserList = new List<SerializableUser>();

        //bans
        public List<SerializableBan> BanList = new List<SerializableBan>();

        //quotes
        public List<SerializableQuote> Quotes = new List<SerializableQuote>();
        public int QuoteInterval = 0;

        //triggers
        public List<SerializableTrigger> Triggers = new List<SerializableTrigger>();

        //meta data for users
        public List<MetaUser> MetaUserList = new List<MetaUser>();

        //public IP
        public string PublicIP = "";

        //flag for parsing channel
        public bool ParseChannel = true;

        //warning threshold
        public int WarningThreshold = 3;

        //time of save
        public DateTime SaveTime = DateTime.UtcNow;

        //antispam levels
        public int AntiSpamLevel = 1;

        //limiters
        public List<RateLimiterConfiguration> LimiterList = new List<RateLimiterConfiguration>();

        //twitter config
        public int TwitterInterval = 15;
        public bool TwitterEnabled = false;
        public string TwitterAccount = string.Empty;

    }

    /// <summary>
    /// JTV IRC settings
    /// </summary>
    [Serializable]
    public class JtvSettings
    {
        public string Channel = "#desrowfighting";
        public string Title = "desRow";
        public string Nickname = "desbot";
        public string Password = "password";
    }

    /// <summary>
    /// IRC settings
    /// </summary>
    [Serializable]
    public class IrcSettings
    {
        public string Server = "irc.quakenet.org";
        public int Port = 6667;
        public string Username = "desBot";
        public string Nickname = "desBot";
        public string Channel = "#desRow";
        public string QAccount = "desBot";
        public string QPassword = "password";
        public bool QHideIP = true;
    }

    /// <summary>
    /// All IRC settings
    /// </summary>
    [Serializable]
    public class AllSettings
    {
        public IrcSettings QNet;
        public JtvSettings JTV;
    }

    /// <summary>
    /// Abstract object graph serializer
    /// </summary>
    public abstract class GraphSerializer
    {
        public abstract void Serialize(Stream stream, SerializableSettings source);
        public abstract SerializableSettings Deserialize(Stream stream);
    }

    /// <summary>
    /// Utility that reads and writes settings to and from file
    /// </summary>
    public static class Settings
    {
        /// <summary>
        /// Utility to serialize a list of items
        /// </summary>
        /// <typeparam name="OriginalT">The original type</typeparam>
        /// <typeparam name="SerializedT">The serialized type</typeparam>
        /// <param name="items">The input collection of original type instances</param>
        /// <param name="serializer">The serializer that describes how to serialize items</param>
        /// <returns>A list of serialized type instances</returns>
        static List<SerializedT> SerializeList<OriginalT, SerializedT>(IEnumerable<OriginalT> items, ISerializer<OriginalT, SerializedT> serializer)
        {
            List<SerializedT> result = new List<SerializedT>();
            foreach (OriginalT item in items)
            {
                result.Add(serializer.Serialize(item));
            }
            return result;
        }

        /// <summary>
        /// Utility to deserialize a list of items
        /// </summary>
        /// <typeparam name="OriginalT">The original type</typeparam>
        /// <typeparam name="SerializedT">The serialized type</typeparam>
        /// <param name="items">The input collection of serialized type instances</param>
        /// <param name="serializer">The serializer that describes how to deserialize items</param>
        /// <returns>A list of deserialized type instances</returns>
        static List<OriginalT> DeserializeList<OriginalT, SerializedT>(IEnumerable<SerializedT> items, ISerializer<OriginalT, SerializedT> serializer)
        {
            List<OriginalT> result = new List<OriginalT>();
            foreach (SerializedT item in items)
            {
                result.Add(serializer.Deserialize(item));
            }
            return result;
        }

        /// <summary>
        /// Apply settings to State
        /// </summary>
        /// <param name="result">The settings to apply</param>
        public static string ApplySettings(SerializableSettings result)
        {
            lock (State.GlobalSync)
            {
                //reset state
                State.Reset();

                //get accounts and add to accountlist
                List<Account> accts = DeserializeList(result.AccountList, new AccountSerializer());
                State.AccountList.Add(accts);

                //get user metadata and add to metdata list
                List<MetaUser> meta = DeserializeList(result.MetaUserList, new DummySerializer<MetaUser>());
                State.MetaUserList.Add(meta);

                //get users and add to userlist
                List<User> users = DeserializeList(result.UserList, new UserSerializer());
                State.UserList.Add(users);

                //get bans and add to banlist
                List<Ban> bans = DeserializeList(result.BanList, new BanSerializer());
                State.BanList.Add(bans);

                //get quotes and set to quote list
                List<Quote> quotes = DeserializeList(result.Quotes, new QuoteSerializer());
                State.QuoteList.Add(quotes);
                State.QuoteInterval.Value = result.QuoteInterval;

                //get triggers and set to trigger list
                List<Trigger> triggers = DeserializeList(result.Triggers, new TriggerSerializer());
                State.TriggerList.Add(triggers);

                //get JTV settings
                State.JtvSettings.Value = result.JTV;

                //get IRC settings
                State.IrcSettings.Value = result.IRC;

                //get control character flag
                State.ControlCharacters.Value = result.ControlChars;

                //get default enforced
                State.UseQEnforce.Value = result.UseQEnforce;

                //get quiet ban
                State.UseQuietBan.Value = result.UseQuietBan;

                //get public IP
                State.PublicIP.Value = result.PublicIP;

                //get channel parse flag
                State.ParseChannel.Value = result.ParseChannel;

                //get warning threshold
                State.WarningThreshold.Value = result.WarningThreshold;

                //anti-spam level
                State.AntiSpamLevel.Value = result.AntiSpamLevel;

                //rate-limiters
                List<RateLimiterConfiguration> limiters = DeserializeList(result.LimiterList, new DummySerializer<RateLimiterConfiguration>());
                State.LimiterList.Add(limiters);
                
                //twitter config
                State.TwitterInterval.Value = result.TwitterInterval;
                State.TwitterEnabled.Value = result.TwitterEnabled;
                State.TwitterAccount.Value = result.TwitterAccount;

                //success
                return "Applied settings: " + result.UserList.Count.ToString() + " users, " + result.BanList.Count.ToString() + " bans, " + result.AccountList.Count.ToString() + " accounts, " + result.Quotes.Count.ToString() + " quotes, " + result.Triggers.Count.ToString() + " triggers";
            }
        }

        /// <summary>
        /// The time at which the last loaded settings were saved
        /// </summary>
        public static DateTime LastLoadedSaveTime = DateTime.MinValue;

        /// <summary>
        /// Load settings and state from file
        /// </summary>
        public static string Load(Stream stream, GraphSerializer serializer)
        {
            //deserialize
            SerializableSettings result = serializer.Deserialize(stream);

            //save time
            LastLoadedSaveTime = result.SaveTime;

            //apply to state
            return ApplySettings(result);
        }

        /// <summary>
        /// Create serializable settings instance
        /// </summary>
        /// <returns>Settings</returns>
        public static SerializableSettings CreateSettings()
        {
            lock (State.GlobalSync)
            {
                SerializableSettings settings = new SerializableSettings();

                //get accounts
                settings.AccountList = SerializeList(State.AccountList.GetItems(), new AccountSerializer());

                //get users
                settings.UserList = SerializeList(State.UserList.GetItems(), new UserSerializer());
                settings.MetaUserList = SerializeList(State.MetaUserList.GetItems(), new DummySerializer<MetaUser>());

                //get bans
                settings.BanList = SerializeList(State.BanList.GetItems(), new BanSerializer());

                //get quotes
                settings.Quotes = SerializeList(State.QuoteList.GetItems(), new QuoteSerializer());
                settings.QuoteInterval = State.QuoteInterval.Value;

                //get triggers
                settings.Triggers = SerializeList(State.TriggerList.GetItems(), new TriggerSerializer());

                //get JTV settings
                settings.JTV = State.JtvSettings.Value;

                //get IRC settings
                settings.IRC = State.IrcSettings.Value;

                //control character flag
                settings.ControlChars = State.ControlCharacters.Value;

                //default enforced
                settings.UseQEnforce = State.UseQEnforce.Value;

                //quiet ban
                settings.UseQuietBan = State.UseQuietBan.Value;

                //set public IP
                settings.PublicIP = State.PublicIP.Value;

                //channel parse flag
                settings.ParseChannel = State.ParseChannel.Value;

                //warning threshold
                settings.WarningThreshold = State.WarningThreshold.Value;

                //antispam level
                settings.AntiSpamLevel = State.AntiSpamLevel.Value;

                //rate limiters
                settings.LimiterList = SerializeList(State.LimiterList.GetItems(), new DummySerializer<RateLimiterConfiguration>());

                //twitter config
                settings.TwitterInterval = State.TwitterInterval.Value;
                settings.TwitterEnabled = State.TwitterEnabled.Value;
                settings.TwitterAccount = State.TwitterAccount.Value;

                //done
                return settings;
            }
        }

        /// <summary>
        /// Save the current state and settings to file
        /// </summary>
        /// <returns>True if the save succeeded</returns>
        public static string Save(Stream stream, GraphSerializer serializer)
        {
            SerializableSettings settings = CreateSettings();

            //serialize
            serializer.Serialize(stream, settings);

            //success
            return "Saved settings: " + settings.UserList.Count.ToString() + " users, " + settings.BanList.Count.ToString() + " bans, " + settings.AccountList.Count.ToString() + " accounts, " + settings.Quotes.Count.ToString() + " quotes, " + settings.Triggers.Count.ToString() + " triggers";
        }
    }

    /// <summary>
    /// XML serializer
    /// </summary>
    public class XmlSerializer : GraphSerializer
    {
        XS.XmlSerializer serializer = new XS.XmlSerializer(typeof(SerializableSettings));

        public override void Serialize(Stream stream, SerializableSettings source)
        {
            serializer.Serialize(stream, source);
        }

        public override SerializableSettings Deserialize(Stream stream)
        {
            return (SerializableSettings)serializer.Deserialize(stream);
        }
    }

    /// <summary>
    /// Binary serializer
    /// </summary>
    public class BinarySerializer : GraphSerializer
    {
        BS.BinaryFormatter serializer = new BS.BinaryFormatter();

        public override void Serialize(Stream stream, SerializableSettings source)
        {
            serializer.Serialize(stream, source);
        }

        public override SerializableSettings Deserialize(Stream stream)
        {
            return (SerializableSettings)serializer.Deserialize(stream);
        }
    }

    /// <summary>
    /// Interface that describes a relation between an original and a serialized type, and methods to convert between them
    /// </summary>
    /// <typeparam name="OriginalT">The original type</typeparam>
    /// <typeparam name="SerializedT">The serialized type</typeparam>
    public interface ISerializer<OriginalT, SerializedT>
    {
        SerializedT Serialize(OriginalT original);
        OriginalT Deserialize(SerializedT serialized);
    }

    /// <summary>
    /// Dummy serializer, that just returns objects without modification
    /// </summary>
    /// <typeparam name="T">The type to "serialize"</typeparam>
    class DummySerializer<T> : ISerializer<T, T>
    {
        public T Serialize(T dummy) { return dummy; }
        public T Deserialize(T dummy) { return dummy; }
    }

    /// <summary>
    /// Find a serializer to map from T to S
    /// </summary>
    static class FindSerializer
    {
        public static ISerializer<T, S> Find<T, S>()
        {
            if (typeof(T) == typeof(User) && typeof(S) == typeof(SerializableUser)) return (ISerializer<T, S>)new UserSerializer();
            if (typeof(T) == typeof(Ban) && typeof(S) == typeof(SerializableBan)) return (ISerializer<T, S>)new BanSerializer();
            if (typeof(T) == typeof(Quote) && typeof(S) == typeof(SerializableQuote)) return (ISerializer<T, S>)new QuoteSerializer();
            if (typeof(T) == typeof(Trigger) && typeof(S) == typeof(SerializableTrigger)) return (ISerializer<T, S>)new TriggerSerializer();
            if (typeof(T) == typeof(Account) && typeof(S) == typeof(SerializableAccount)) return (ISerializer<T, S>)new AccountSerializer();
            if (typeof(T) == typeof(S)) return (ISerializer<T, S>)new DummySerializer<T>();
            throw new Exception("Type not serializable with mapping T->S: " + typeof(T).Name + "->" + typeof(S).Name);
        }
    }
}