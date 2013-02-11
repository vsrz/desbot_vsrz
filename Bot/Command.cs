using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace desBot
{
    /// <summary>
    /// Command definition
    /// </summary>
    abstract class Command
    {
        /// <summary>
        /// If true, the command can be triggered by a channel message
        /// </summary>
        public bool TriggerOnChannel = true;

        /// <summary>
        /// If true, the command can be triggered by a private message
        /// </summary>
        public bool TriggerOnPrivate = true;

        /// <summary>
        /// If true, the command requires a registered prefix
        /// </summary>
        public bool RequiresPrefix = true;

        /// <summary>
        /// If true, the command requires that there is NO registered prefix
        /// </summary>
        public bool ForbidsPrefix = false;

        /// <summary>
        /// If true, !help will enumerate this command
        /// </summary>
        public bool IsHelpEnumerable = true;

        /// <summary>
        /// If set to anything but None, requires the bot to have that Q auth level on the channel
        /// </summary>
        public QAuthLevel QRequired = QAuthLevel.None;

        /// <summary>
        /// The privilege required by the user to run the command (default is none, you have to override or it will never work)
        /// </summary>
        public PrivilegeLevel Privilege = PrivilegeLevel.Invalid;

        /// <summary>
        /// The rate limiter that is applied to the commands
        /// </summary>
        public RateLimiter Limiter = new RateLimiter(TimeSpan.FromSeconds(30.0), TimeSpan.FromSeconds(90.0));

        /// <summary>
        /// Gets the keyword of the command
        /// </summary>
        /// <returns>The keyword that triggers the command</returns>
        public abstract string GetKeyword();

        /// <summary>
        /// Gets the help text of the command
        /// </summary>
        /// <param name="more">The rest of the help command parameters</param>
        /// <returns>The help text to display if help is requested for this command</returns>
        public abstract string GetHelpText(PrivilegeLevel current, string more);

        /// <summary>
        /// Executes the command
        /// </summary>
        /// <param name="message">The message that triggered the command</param>
        /// <param name="args">The arguments of the command</param>
        public abstract void Execute(IrcMessage message, string args);
        
        /// <summary>
        /// Constructor registers with handler
        /// </summary>
        /// <param name="delayreg">If set, does not register the command, you have to do it manually</param>
        public Command(bool delayreg)
        {
            if(!delayreg) CommandHandler.AddCommand(this);
        }

        /// <summary>
        /// Constructor redirector
        /// </summary>
        public Command() : this(false) { }

        /// <summary>
        /// Comparison test for keyword equality
        /// </summary>
        /// <param name="obj">The object to be tested for equality</param>
        /// <returns>True if keywords are equal</returns>
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is Command)) return false;
            return GetKeyword() == ((Command)obj).GetKeyword();
        }

        /// <summary>
        /// Return hash code of keyword
        /// </summary>
        /// <returns>Hash code of keyword</returns>
        public override int GetHashCode()
        {
            return GetKeyword().GetHashCode();
        }
    }

    /// <summary>
    /// Alias for an existing command
    /// </summary>
    class Alias : Command
    {
        Command actual;
        string alias;

        public Alias(Command command, string alias) : base(true)
        {
            TriggerOnChannel = command.TriggerOnChannel;
            TriggerOnPrivate = command.TriggerOnPrivate;
            RequiresPrefix = command.RequiresPrefix;
            ForbidsPrefix = command.ForbidsPrefix;
            QRequired = command.QRequired;
            Privilege = command.Privilege;
            this.alias = alias;
            this.actual = command;
            CommandHandler.AddCommand(this);
        }

        public override string GetKeyword()
        {
            return alias;
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return actual.GetHelpText(current, more);
        }

        public override void Execute(IrcMessage message, string args)
        {
            actual.Execute(message, args);
        }

        public Command TargetCommand { get { return actual; } }
    }

    /// <summary>
    /// Command handler collects commands and parses messages that invoke commands
    /// </summary>
    static class CommandHandler
    {
        static Dictionary<string, Command> commands;
        static List<string> prefixes;

        public delegate void ChangedEventHandler();
        public static event ChangedEventHandler OnChanged;
        public static void RaiseChanged()
        {
            if (OnChanged != null) OnChanged.Invoke();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="prefix">The default prefix (or null)</param>
        public static void Init(string prefix)
        {
            commands = new Dictionary<string, Command>();
            prefixes = new List<string>();
            AddPrefix(prefix);
            AutoRegisterCommands();
            PullLimiters();
            PushLimiters();
        }

        /// <summary>
        /// Adds a prefix to the command handler
        /// </summary>
        /// <param name="prefix">The prefix to add to the command handler</param>
        public static void AddPrefix(string prefix)
        {
            if(prefix == null || prefix.Length == 0) throw new Exception("Invalid prefix");
            if (prefixes.Contains(prefix)) throw new Exception("Prefix '" + prefix + "' already registered");
            prefixes.Add(prefix);
        }

        /// <summary>
        /// Adds a command to the command handler
        /// </summary>
        /// <param name="command">The command to add to the command handler</param>
        public static void AddCommand(Command command)
        {
            string keyword = command.GetKeyword().ToLower();
            if (commands.ContainsKey(keyword)) throw new Exception("Command with keyword '" + keyword + "' already exists");
            commands.Add(keyword, command);
            if (OnChanged != null) OnChanged.Invoke();
        }

        /// <summary>
        /// Adds an alias to the command handler
        /// </summary>
        /// <param name="command">The command to alias to</param>
        /// <param name="alias">The aliassed keyword</param>
        /// <returns>The new alias</returns>
        public static Alias AddAlias(Command command, string alias)
        {
            return new Alias(command, alias);
        }

        /// <summary>
        /// Handles an incoming message, and invokes any matching commands
        /// </summary>
        /// <param name="message">The message to check for commands</param>
        public static void HandleMessage(IrcMessage message)
        {
            //ignore messages from channel
            if (!State.ParseChannel.Value && message.IsChannelMessage) return;

            bool had_prefix = false;
            if (!HandleCommand(message, out had_prefix))
            {
                try
                {
                    //ignore commands from reserved names
#if JTVBOT
                    if(message.From == "jtv" || message.From == "jtvnotifier" || message.From.StartsWith("jtv!")) return;
#elif QNETBOT
                    if(message.From == "Q") return;
#endif

                    //not a command, test for bad words
                    bool isspam = ContainsBadWord(message.Text);
                    if (isspam)
                    {
                        Program.Log("This input matches spamfilter");
                    }
                    if (State.AntiSpamLevel.Value > 0 && GetPrivilegeLevel(message.From) < PrivilegeLevel.Voiced && isspam)
                    {
#if JTVBOT
                        if (State.AntiSpamLevel.Value == 1)
                        {
                            JTV.Purge(message.From);
                            message.ReplyChannel("Please don't use URLs in chat, " + message.From);
                        }
                        else
#endif
                        {
                            BanSystem.PerformBan(BanSystem.CreateBanMask(message.From).Mask, "10m", "Links not allowed", "Antispam");
                        }
                    }

                    //process for AI chatterbot
#if JTVBOT
                    string name = State.JtvSettings.Value.Nickname;
#else
                    string name = State.IrcSettings.Value.Nickname;
#endif
                    //remove characters
                    string input = message.Text.Replace(",", "").Replace(".", "").Replace("!", "").Replace("?", "").Trim();
                    int needle = input.IndexOf(name, StringComparison.OrdinalIgnoreCase);
                    if (needle >= 0 && !had_prefix)
                    {
                        try
                        {
                            //rewrite around name
                            string first = input.Substring(0, needle).Trim();
                            string second = input.Substring(needle + name.Length).Trim();
                            bool insert_you = false;
                            if (first.ToLower().EndsWith(" is"))
                            {
                                first = first.Substring(0, first.Length - 2) + "are";
                                insert_you = true;
                            }
                            else if (second.ToLower().StartsWith("is "))
                            {
                                second = "are" + second.Substring(2);
                                insert_you = true;
                            }
                            string rewritten = insert_you ? first + " you " + second : first + " " + second;

                            //trigger alice
                            Program.Log("ALICE input: " + rewritten);
                            string result = Alice.Process(message.From, rewritten, message.Level);
                            if (result.Length > 0 && result.Length < 100 && !result.StartsWith("<br",true,System.Globalization.CultureInfo.CurrentCulture))
                            {
                                message.ReplyAuto(result);
                            }
                            else
                            {
                                Program.Log("Suppressed ALICE output: " + result);
                            }
                        }
                        catch (Exception ex)
                        {
                            //parse failed
                            Program.Log("Failed to prepare input for chatterbox: " + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    //ignore exception
                    Program.Log("Exception while handling input: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Test for bad word
        /// </summary>
        /// <param name="text">Text to check</param>
        /// <returns>True if a bad word was found</returns>
        static Regex islink = new Regex(@"(\w+(?:\.\w+)+)", RegexOptions.IgnoreCase);
        static bool ContainsBadWord(string text)
        {
            MatchCollection coll = islink.Matches(text);
            for(int i = 0; i < coll.Count; i++)
            {
                Match m = coll[i];
                string url = m.Captures[0].Value;
                int index = url.LastIndexOf('.');
                string tld = url.Substring(index + 1);
                url = url.Substring(0, index);
                index = url.LastIndexOf('.');
                string domain = index == -1 ? url : url.Substring(index + 1);
                if (domain.Length <= 1) return false; //skip 1-letter domains (like, j.tv, t.tv an b.net)
                switch(tld.ToUpper())
                {
                        //list from IANA
                    case "AC":
                    case "AD":
                    case "AE":
                    case "AERO":
                    case "AF":
                    case "AG":
                    case "AI":
                    case "AL":
                    case "AM":
                    case "AN":
                    case "AO":
                    case "AQ":
                    case "AR":
                    case "ARPA":
                    case "AS":
                    case "ASIA":
                    case "AT":
                    case "AU":
                    case "AW":
                    case "AX":
                    case "AZ":
                    case "BA":
                    case "BB":
                    case "BD":
                    case "BE":
                    case "BF":
                    case "BG":
                    case "BH":
                    case "BI":
                    case "BIZ":
                    case "BJ":
                    case "BM":
                    case "BN":
                    case "BO":
                    case "BR":
                    case "BS":
                    case "BT":
                    case "BV":
                    case "BW":
                    case "BY":
                    case "BZ":
                    case "CA":
                    case "CAT":
                    case "CC":
                    case "CD":
                    case "CF":
                    case "CG":
                    case "CH":
                    case "CI":
                    case "CK":
                    case "CL":
                    case "CM":
                    case "CN":
                    case "CO":
                    case "COM":
                    case "COOP":
                    case "CR":
                    case "CU":
                    case "CV":
                    case "CW":
                    case "CX":
                    case "CY":
                    case "CZ":
                    case "DE":
                    case "DJ":
                    case "DK":
                    case "DM":
                    case "DO":
                    case "DZ":
                    case "EC":
                    case "EDU":
                    case "EE":
                    case "EG":
                    case "ER":
                    case "ES":
                    case "ET":
                    case "EU":
                    case "FI":
                    case "FJ":
                    case "FK":
                    case "FM":
                    case "FO":
                    case "FR":
                    case "GA":
                    case "GB":
                    case "GD":
                    case "GE":
                    case "GF":
                    case "GG":
                    case "GH":
                    case "GI":
                    case "GL":
                    case "GM":
                    case "GN":
                    case "GOV":
                    case "GP":
                    case "GQ":
                    case "GR":
                    case "GS":
                    case "GT":
                    case "GU":
                    case "GW":
                    case "GY":
                    case "HK":
                    case "HM":
                    case "HN":
                    case "HR":
                    case "HT":
                    case "HU":
                    case "ID":
                    case "IE":
                    case "IL":
                    case "IM":
                    case "IN":
                    case "INFO":
                    case "INT":
                    case "IO":
                    case "IQ":
                    case "IR":
                    case "IS":
                    case "IT":
                    case "JE":
                    case "JM":
                    case "JO":
                    case "JOBS":
                    case "JP":
                    case "KE":
                    case "KG":
                    case "KH":
                    case "KI":
                    case "KM":
                    case "KN":
                    case "KP":
                    case "KR":
                    case "KW":
                    case "KY":
                    case "KZ":
                    case "LA":
                    case "LB":
                    case "LC":
                    case "LI":
                    case "LK":
                    case "LR":
                    case "LS":
                    case "LT":
                    case "LU":
                    case "LV":
                    case "LY":
                    case "MA":
                    case "MC":
                    case "MD":
                    case "ME":
                    case "MG":
                    case "MH":
                    case "MIL":
                    case "MK":
                    case "ML":
                    case "MM":
                    case "MN":
                    case "MO":
                    case "MOBI":
                    case "MP":
                    case "MQ":
                    case "MR":
                    case "MS":
                    case "MT":
                    case "MU":
                    case "MUSEUM":
                    case "MV":
                    case "MW":
                    case "MX":
                    case "MY":
                    case "MZ":
                    case "NA":
                    case "NAME":
                    case "NC":
                    case "NE":
                    case "NET":
                    case "NF":
                    case "NG":
                    case "NI":
                    case "NL":
                    case "NO":
                    case "NP":
                    case "NR":
                    case "NU":
                    case "NZ":
                    case "OM":
                    case "ORG":
                    case "PA":
                    case "PE":
                    case "PF":
                    case "PG":
                    case "PH":
                    case "PK":
                    case "PL":
                    case "PM":
                    case "PN":
                    case "PR":
                    case "PRO":
                    case "PS":
                    case "PT":
                    case "PW":
                    case "PY":
                    case "QA":
                    case "RE":
                    case "RO":
                    case "RS":
                    case "RU":
                    case "RW":
                    case "SA":
                    case "SB":
                    case "SC":
                    case "SD":
                    case "SE":
                    case "SG":
                    case "SH":
                    case "SI":
                    case "SJ":
                    case "SK":
                    case "SL":
                    case "SM":
                    case "SN":
                    case "SO":
                    case "SR":
                    case "ST":
                    case "SU":
                    case "SV":
                    case "SX":
                    case "SY":
                    case "SZ":
                    case "TC":
                    case "TD":
                    case "TEL":
                    case "TF":
                    case "TG":
                    case "TH":
                    case "TJ":
                    case "TK":
                    case "TL":
                    case "TM":
                    case "TN":
                    case "TO":
                    case "TP":
                    case "TR":
                    case "TRAVEL":
                    case "TT":
                    case "TV":
                    case "TW":
                    case "TZ":
                    case "UA":
                    case "UG":
                    case "UK":
                    case "US":
                    case "UY":
                    case "UZ":
                    case "VA":
                    case "VC":
                    case "VE":
                    case "VG":
                    case "VI":
                    case "VN":
                    case "VU":
                    case "WF":
                    case "WS":
                    case "XXX":
                    case "YE":
                    case "YT":
                    case "ZA":
                    case "ZM":
                    case "ZW":
                        return true;

                }
            }
            return false;
        }

        /// <summary>
        /// Handle a command
        /// </summary>
        /// <param name="message">Message to be parsed</param>
        /// <returns>True if a command was handled</returns>
        static bool HandleCommand(IrcMessage message, out bool had_prefix)
        {
            //stripped command
            string text = message.Text;

            //look for prefix, and strip it
            bool has_prefix = false;
            foreach (string prefix in prefixes)
            {
                if (text.StartsWith(prefix))
                {
                    text = text.Substring(prefix.Length);
                    has_prefix = true;
                }
            }
            had_prefix = has_prefix;

            //look for keyword
            int spacepos = text.IndexOf(' ');
            if (spacepos == 0)
            {
                //only a prefix, no keyword
                return false;
            }
            string keyword = null;
            string args = "";
            if (spacepos == -1)
            {
                //only a keyword
                keyword = text;
            }
            else
            {
                //multiple words
                keyword = text.Substring(0, spacepos);
                args = text.Substring(spacepos + 1).Trim();
            }

            //keyword to lower case
            keyword = keyword.ToLower();

            //look up command
            Command command = commands.ContainsKey(keyword) ? commands[keyword] : null;
            if (command == null) return false;

            //check if prefix is required
            if (command.RequiresPrefix && !has_prefix && message.IsChannelMessage) return false;
            if (command.ForbidsPrefix && has_prefix) return false;

            //check if current Q level is sufficient
            if (command.QRequired > QAuthLevel.None)
            {
#if JTVBOT
                return false;
#elif QNETBOT
                QAuthLevel current = Irc.QLevel;
                if (current < command.QRequired) return false;
#endif
            }

            //check if source of message is correct
            if (!((command.TriggerOnChannel && message.IsChannelMessage) || (command.TriggerOnPrivate && message.IsPrivateMessage))) return false;

            //check for privilege level
            PrivilegeLevel level = GetPrivilegeLevel(message.From);
            if (level < command.Privilege) return false;

            //check for rate-limiting
            User lookup = State.UserList.Lookup(message.From);
            if (lookup != null)
            {
                //update configuration
                lookup.Meta.Limiter.Configuration = LimitCommand.UserLimiter.Configuration;
                if (!lookup.Meta.Limiter.AttemptOperation(level))
                {
                    Program.Log("Command ignored due to per-user limiting: '" + message.Text + "' from " + message.From);
                    return true;
                }
            }
            else
            {
                //unknown user, use shared timer instead
                if (!LimitCommand.UserLimiter.AttemptOperation(level))
                {
                    Program.Log("Command ignored due to shared limiting: '" + message.Text + "' from " + message.From);
                    return true;
                }
            }
            
            //execute command
            try
            {
                command.Execute(message, args);
            }
            catch (Exception ex)
            {
                message.ReplyAuto("Error: " + ex.Message);
            }

            //this was a command
            return true;
        }

        /// <summary>
        /// Retrieve current list of commands
        /// </summary>
        /// <returns>List of known commands</returns>
        public static Dictionary<string, Command> GetCommands() { return commands; }

        /// <summary>
        /// Retrieve the default prefix for commands
        /// </summary>
        /// <returns>The default prefix</returns>
        public static string GetDefaultPrefix() { return prefixes.Count == 0 ? "" : prefixes[0]; }

        /// <summary>
        /// Retrieve a string with the prefix removed
        /// </summary>
        /// <param name="command">The string to strip</param>
        /// <returns>The stripped string</returns>
        public static string RemovePrefixFromString(string command)
        {
            foreach (string prefix in prefixes)
            {
                if (command.StartsWith(prefix)) return command.Substring(prefix.Length);
            }
            return command;
        }

        /// <summary>
        /// Gets the privilege level associated with a nickname
        /// </summary>
        /// <param name="nickname">The nickname to look up</param>
        /// <returns>The privilege level obtained</returns>
        public static PrivilegeLevel GetPrivilegeLevel(string nickname)
        {
            if (nickname == "<console>") return PrivilegeLevel.Console;
#if JTVBOT
            User lookup = State.UserList.Lookup(nickname);
            PrivilegeLevel baselevel = (lookup != null && lookup.Meta.JTVModerator) ? PrivilegeLevel.Operator : (lookup != null && (DateTime.UtcNow - lookup.Meta.JTVSubscriber).TotalSeconds <= 10.0) ? PrivilegeLevel.Subscriber : PrivilegeLevel.OnChannel;
            if (Program.IsBuggyTwitch && baselevel < PrivilegeLevel.Operator && lookup != null && (DateTime.UtcNow - lookup.Meta.DeopTime).TotalHours < 1.0)
            {
                //buggy twitch workaround
                Program.Log("Buggy Twitch Workaround -> User " + nickname + " considered as Operator");
                baselevel = PrivilegeLevel.Operator;
            }

            if (Program.IsBuggyTwitch)
            {
                //consider the following to be always operators:
                //- the broadcaster
                //- me (the developer)
                if (string.Compare(State.JtvSettings.Value.Channel, nickname, true) == 0) return PrivilegeLevel.Operator;
                if (string.Compare("mlmnl", nickname, true) == 0) return PrivilegeLevel.Developer;
            }
#elif QNETBOT
            PrivilegeLevel baselevel =  Irc.GetPrivilegeLevel(nickname);
#endif
            if (baselevel == PrivilegeLevel.Operator)
            {
                User user = State.UserList.Lookup(nickname);
                if (user != null && user.Meta.Elevation) return PrivilegeLevel.Developer;
            }
            return baselevel;
        }
        
        /// <summary>
        /// Automatically register subclasses of Command with a "public static void AutoRegister()" signature
        /// </summary>
        static void AutoRegisterCommands()
        {
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (Type type in types)
            {
                if (type.IsSubclassOf(typeof(Command)))
                {
                    MethodInfo method = type.GetMethod("AutoRegister");
                    if (method != null && method.IsStatic)
                    {
                        ParameterInfo[] args = method.GetParameters();
                        if (args.Length == 0)
                        {
                            try
                            {
                                method.Invoke(null, new object[] { });
                                Program.Log("Auto-registered command '" + type.Name + "'");
                            }
                            catch (Exception ex)
                            {
                                Program.Log("Failed to auto-register command '" + type.Name + "':" + ex.Message);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Pull limiters
        /// </summary>
        static void PullLimiters()
        {
            lock (State.GlobalSync)
            {
                IEnumerable<RateLimiterConfiguration> limiters = State.LimiterList.GetItems();
                foreach (RateLimiterConfiguration limiter in limiters)
                {
                    if (commands.ContainsKey(limiter.key))
                    {
                        Command command = commands[limiter.key];
                        command.Limiter.Configuration = limiter;
                    }
                    else if (limiter.key == "chat")
                    {
                        Alice.limiter.Configuration = limiter;
                    }
                    else if (limiter.key == "joke")
                    {
                        Alice.joke_limiter.Configuration = limiter;
                    }
                    else if (limiter.key == "user")
                    {
                        LimitCommand.UserLimiter.Configuration = limiter;
                    }
                }
            }
        }

        /// <summary>
        /// Push limiters to state
        /// </summary>
        public static void PushLimiters()
        {
            if (commands != null)
            {
                lock (State.GlobalSync)
                {
                    foreach (KeyValuePair<string, Command> pair in commands)
                    {
                        RateLimiterConfiguration config = pair.Value.Limiter.Configuration;
                        config.key = pair.Key;
                        PushLimiter(config);
                    }
                    
                    RateLimiterConfiguration chatconfig = Alice.limiter.Configuration;
                    chatconfig.key = "chat";
                    PushLimiter(chatconfig);

                    RateLimiterConfiguration jokeconfig = Alice.joke_limiter.Configuration;
                    jokeconfig.key = "joke";
                    PushLimiter(jokeconfig);

                    RateLimiterConfiguration userconfig = LimitCommand.UserLimiter.Configuration;
                    userconfig.key = "user";
                    PushLimiter(userconfig);
                }
            }
        }

        static void PushLimiter(RateLimiterConfiguration config)
        {
            RateLimiterConfiguration other = State.LimiterList.Lookup(config.key);
            if (other == null || other.nor != config.nor || other.sub != config.sub)
            {
                if (other != null) State.LimiterList.Remove(other);
                State.LimiterList.Add(config);
            }
        }
    }
}