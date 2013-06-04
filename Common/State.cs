using System;
using System.Net;
using System.Collections.Generic;
namespace desBot
{
    /// <summary>
    /// On state reset event handler
    /// </summary>
    public delegate void OnStateReset();

    /// <summary>
    /// Current bot state
    /// </summary>
    public static class State
    {
        /// <summary>
        /// List of DynamicXXX members in this class
        /// </summary>
        static List<DynamicUpdatable> members = new List<DynamicUpdatable>();

        /// <summary>
        /// Global sync object
        /// </summary>
        public static object GlobalSync { get { return members; } }

        /// <summary>
        /// Add member to collection
        /// </summary>
        /// <param name="member">The member to add</param>
        internal static void AddMember(DynamicUpdatable member) { members.Add(member); }
        
        /// <summary>
        /// Apply an update object to the dynamic objects in the collection
        /// </summary>
        /// <param name="obj">The update notification</param>
        /// <returns>True if the update notification was applied</returns>
        public static bool ApplyUpdate(object obj)
        {
            if (obj is ResetNotification)
            {
                State.Reset();
                return true;
            }
            else
            {
                foreach (DynamicUpdatable member in members) if (member.ApplyUpdate(obj)) return true;
                return false;
            }
        }

        /// <summary>
        /// Event that triggers on reset
        /// </summary>
        public static OnStateReset OnReset;

        /// <summary>
        /// Reset entire state
        /// </summary>
        public static void Reset()
        {
            foreach (DynamicUpdatable member in members) member.Reset();
            if(OnReset != null) OnReset.Invoke();
        }

        /// <summary>
        /// Accounts
        /// </summary>
        public static DynamicDictionary<IPAddress, Account, SerializableAccount> AccountList = new DynamicDictionary<IPAddress, Account, SerializableAccount>();

        /// <summary>
        /// JTV settings
        /// </summary>
        public static DynamicProperty<JtvSettings, JtvSettings> JtvSettings = new DynamicProperty<JtvSettings, JtvSettings>("JtvSettings", new JtvSettings());

        /// <summary>
        /// IRC settings
        /// </summary>
        public static DynamicProperty<IrcSettings, IrcSettings> IrcSettings = new DynamicProperty<IrcSettings, IrcSettings>("IrcSettings", new IrcSettings());

		/// <summary>
		/// SC2Ranks API Settings
		/// </summary>
		public static DynamicProperty<SC2RanksSettings, SC2RanksSettings> SC2RanksSettings = new DynamicProperty<SC2RanksSettings, SC2RanksSettings>("SC2RanksSettings", new SC2RanksSettings());
        
		/// <summary>
        /// Flag selecting if control characters are enabled
        /// </summary>
        public static DynamicProperty<bool, bool> ControlCharacters = new DynamicProperty<bool, bool>("ControlCharacters", true);

        /// <summary>
        /// If set, the default ban enforce is Q
        /// </summary>
        public static DynamicProperty<bool, bool> UseQEnforce = new DynamicProperty<bool, bool>("UseQEnforce", false);

        /// <summary>
        /// If set, the default temporary ban type is quiet
        /// </summary>
        public static DynamicProperty<bool, bool> UseQuietBan = new DynamicProperty<bool, bool>("UseQuietBan", true);

        /// <summary>
        /// List of bans
        /// </summary>
        public static DynamicDictionary<string, Ban, SerializableBan> BanList = new DynamicDictionary<string, Ban, SerializableBan>();

        /// <summary>
        /// List of users
        /// </summary>
        public static DynamicDictionary<string, User, SerializableUser> UserList = new DynamicDictionary<string, User, SerializableUser>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// List of user meta data
        /// </summary>
        public static DynamicDictionary<string, MetaUser, MetaUser> MetaUserList = new DynamicDictionary<string, MetaUser, MetaUser>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// List of triggers
        /// </summary>
        public static DynamicList<Trigger, SerializableTrigger> TriggerList = new DynamicList<Trigger, SerializableTrigger>();

        /// <summary>
        /// List of quotes
        /// </summary>
        public static DynamicList<Quote, SerializableQuote> QuoteList = new DynamicList<Quote, SerializableQuote>();

        /// <summary>
        /// Interval between quotes
        /// </summary>
        public static DynamicProperty<int, int> QuoteInterval = new DynamicProperty<int, int>("QuoteInterval", 0);

        /// <summary>
        /// Delay between IRC sends, in milliseconds
        /// </summary>
        public static DynamicProperty<int, int> SendDelay = new DynamicProperty<int, int>("SendDelay", 750);

        /// <summary>
        /// Public IP of the bot
        /// </summary>
        public static DynamicProperty<string, string> PublicIP = new DynamicProperty<string, string>("PublicIP", "");

        /// <summary>
        /// Flag selecting if channel messages are parsed
        /// </summary>
        public static DynamicProperty<bool, bool> ParseChannel = new DynamicProperty<bool, bool>("ParseChannel", true);

        /// <summary>
        /// The amount of warnings before somebody gets a tempban
        /// </summary>
        public static DynamicProperty<int, int> WarningThreshold = new DynamicProperty<int, int>("WarningThreshold", 3);

        /// <summary>
        /// The Anti-Spam level of the bot
        /// </summary>
        public static DynamicProperty<int, int> AntiSpamLevel = new DynamicProperty<int, int>("AntiSpamLevel", 1);

        /// <summary>
        /// The rate limiters in the configuration
        /// </summary>
        public static DynamicDictionary<string, RateLimiterConfiguration, RateLimiterConfiguration> LimiterList = new DynamicDictionary<string, RateLimiterConfiguration, RateLimiterConfiguration>();

        /// <summary>
        /// Interval between twitter repeats, in minutes
        /// </summary>
        public static DynamicProperty<int, int> TwitterInterval = new DynamicProperty<int, int>("TwitterInterval", 15);

        /// <summary>
        /// Flag indicating twitter repeat is enabled
        /// </summary>
        public static DynamicProperty<bool, bool> TwitterEnabled = new DynamicProperty<bool, bool>("TwitterEnabled", false);

        /// <summary>
        /// The account to repeat tweets from
        /// </summary>
        public static DynamicProperty<string, string> TwitterAccount = new DynamicProperty<string, string>("TwitterAccount", String.Empty);

        /// <summary>
        /// Interval to spam ad text, in minutes
        /// </summary>
        public static DynamicProperty<int, int> AdInterval = new DynamicProperty<int, int>("AdInterval", 15);

        /// <summary>
        /// Whether ad spamming is enabled
        /// </summary>
        public static DynamicProperty<bool, bool> AdEnabled = new DynamicProperty<bool, bool>("AdEnabled", false);

        /// <summary>
        /// Advert text
        /// </summary>
        public static DynamicProperty<string, string> AdText = new DynamicProperty<string, string>("AdText", String.Empty);


    }
}