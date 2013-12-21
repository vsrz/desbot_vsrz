using Meebey.SmartIrc4net;
using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
namespace desBot
{
    /// <summary>
    /// An IRC message
    /// </summary>
    class IrcMessage
    {
        string channel;
        bool notice;

        /// <summary>
        /// The text of the message
        /// </summary>
        public string Text { get; private set; }

        /// <summary>
        /// True if the message was received through PRIVMSG or NOTICE
        /// </summary>
        public bool IsPrivateMessage { get { return channel == null; } }

        /// <summary>
        /// True if the message was receieved on a channel
        /// </summary>
        public bool IsChannelMessage { get { return channel != null; } }

        /// <summary>
        /// The nickname of the person who sent the message
        /// </summary>
        public string From { get; private set; }

        /// <summary>
        /// If set, reply immediately
        /// </summary>
        public bool ReplyImmediately { get; set; }

        /// <summary>
        /// Retrieve privilege level assoicated with this message's sender
        /// </summary>
        public PrivilegeLevel Level { get { return CommandHandler.GetPrivilegeLevel(From); } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="source">IRC tool instance that received the message</param>
        /// <param name="from">The nickname of the sender of the message</param>
        /// <param name="text">The text of the message</param>
        /// <param name="channel">The channel on which the message was received, or null</param>
        /// <param name="notice">True if the message was received through a notice</param>
        public IrcMessage(string from, string text, string channel, bool notice)
        {
            this.channel = channel;
            this.notice = notice;
            From = from;
            Text = text;
        }

        /// <summary>
        /// Reply to the channel
        /// </summary>
        /// <param name="text">The text in the reply</param>
        public void ReplyChannel(string text)
        {
            Irc.SendChannelMessage(text, ReplyImmediately);
        }

        /// <summary>
        /// Reply to the user
        /// </summary>
        /// <param name="text">The text to reply</param>
        public void ReplyPrivate(string text)
        {
            if (From == "<console>") Program.Log(text);
            else if (!Program.IsJTV || IsPrivateMessage) Irc.SendPrivateMessage(text, From, channel != null ? true : notice, ReplyImmediately);
            else Irc.SendChannelMessage(text, ReplyImmediately);
        }

        /// <summary>
        /// Replies to the channel, if message received on the channel, otherwise, to the user
        /// </summary>
        /// <param name="text">The text to reply</param>
        public void ReplyAuto(string text)
        {
            if (IsPrivateMessage) ReplyPrivate(text);
            else ReplyChannel(text);
        }
    }

    /// <summary>
    /// IRC tool
    /// </summary>
    static class Irc
    {
        static IrcClient client;
        static IrcState state;
#if QNETBOT
        static QCommandQueue q;
#endif
        static Timer autorestart;
        static Timer detecthang;

        /// <summary>
        /// The server the IRC tool connects to
        /// </summary>
        public static string Server
        { 
            get 
            { 
#if JTVBOT
                return desBot.State.JtvSettings.Value.Hostname;
#elif QNETBOT
                return desBot.State.IrcSettings.Value.Server;
#endif
            }
        }

        /// <summary>
        /// The port number the IRC tool connects to
        /// </summary>
        public static int Port
        { 
            get
            { 
#if JTVBOT
                return 6667;
#elif QNETBOT
                return desBot.State.IrcSettings.Value.Port;
#endif
            }
        }

        /// <summary>
        /// The username used to register the IRC tool with
        /// </summary>
        public static string Username
        { 
            get 
            { 
#if JTVBOT
                return desBot.State.JtvSettings.Value.Nickname;
#elif QNETBOT
                return desBot.State.IrcSettings.Value.Username;
#endif
            }
        }

        /// <summary>
        /// The nickname used to register the IRC tool with
        /// </summary>
        public static string Nickname
        { 
            get 
            {
#if JTVBOT
                return desBot.State.JtvSettings.Value.Nickname;
#elif QNETBOT
                return desBot.State.IrcSettings.Value.Nickname;
#endif
            }
        }
        
        /// <summary>
        /// The channel to join when registered
        /// </summary>
        public static string Channel
        { 
            get
            {
#if JTVBOT
                return desBot.State.JtvSettings.Value.Channel;
#elif QNETBOT
                return desBot.State.IrcSettings.Value.Channel;
#endif
            }
        }

        /// <summary>
        /// The current state of the IRC tool
        /// </summary>
        public static IrcState State { get { return state; } private set { state = value; if (OnStateChanged != null) OnStateChanged.Invoke(); } }

#if QNETBOT
        /// <summary>
        /// The level with which was authed with Q for he channel
        /// </summary>
        public static QAuthLevel QLevel { get; private set; }

        /// <summary>
        /// The Q command queue associated with this IRC tool
        /// </summary>
        public static QCommandQueue Queue { get { return q; } }
#endif

        /// <summary>
        /// Event handler delegate for state changes
        /// </summary>
        public delegate void StateChangedEventHandler();

        /// <summary>
        /// Event handler delegate for message
        /// </summary>
        /// <param name="message">The message being received</param>
        public delegate void MessageEventHandler(IrcMessage message);

        /// <summary>
        /// Triggers if the IRC state changes
        /// </summary>
        public static event StateChangedEventHandler OnStateChanged;
        
        /// <summary>
        /// Triggers if a message is received
        /// </summary>
        public static event MessageEventHandler OnMessage;
        
        /// <summary>
        /// Initialises the IRC system
        /// </summary>
        /// <param name="settings">The settings to use while running the IRC tool</param>
        public static void Init()
        {
#if JTVBOT
            JtvSettings Settings = desBot.State.JtvSettings.Value;
            if (Settings == null) throw new Exception("No settings specified");
            if (string.IsNullOrEmpty(Settings.Channel)) throw new Exception("No channel specified");
            if (string.IsNullOrEmpty(Settings.Nickname)) throw new Exception("No JTV username specified");
            if (string.IsNullOrEmpty(Settings.Password)) throw new Exception("No JTV password specified");

#elif QNETBOT
            IrcSettings Settings = desBot.State.IrcSettings.Value;

            //check argument
            if (Settings == null) throw new Exception("No settings specified");
            if (string.IsNullOrEmpty(Settings.Server) || !Settings.Server.EndsWith("quakenet.org")) throw new Exception("Only QuakeNet servers are supported");
            if (Settings.Port <= 0 || Settings.Port >= 65536) throw new Exception("Invalid server port specified");
            if (string.IsNullOrEmpty(Settings.Username) || Settings.Username.Contains(" ")) throw new Exception("Invalid username specified");
            if (string.IsNullOrEmpty(Settings.Nickname) || Settings.Nickname.Contains(" ")) throw new Exception("Invalid nickname specified");
            if (string.IsNullOrEmpty(Settings.Channel) || Settings.Channel.Contains(" ") || !Settings.Channel.StartsWith("#")) throw new Exception("Invalid channel name specified");
            if (string.IsNullOrEmpty(Settings.QAccount) || Settings.QAccount.Contains(" ")) throw new Exception("Invalid Q account name specified");
            if (string.IsNullOrEmpty(Settings.QPassword) || Settings.QPassword.Contains(" ")) throw new Exception("Invalid Q password specified");
#endif

            //disconnect
            if (client != null && client.IsConnected) client.Disconnect();
            client = null;

            //kill restart timer
            autorestart = null;
            detecthang = null;

            //initial values
            state = IrcState.None;
#if QNETBOT
            q = null;
            QLevel = QAuthLevel.None;
#endif
            Program.Log("IRC initialising");

            //attach event handler to send delay
            desBot.State.SendDelay.OnChanged += new OnPropertyValueChanged<int, int>(sendDelay_Changed);

            //set up state
            State = IrcState.None;
            Settings.Channel = Settings.Channel.ToLower(); //lower case to prevent mismatchign of string casings
            client = new IrcClient();

            //text encoding
            Encoding encoding = null;
            try
            {
                //try UTF8-encoding
                encoding = new System.Text.UTF8Encoding(false, false);
            }
            catch(Exception)
            {
                try
                {
                    //try codepage 1252 first (western european ANSI)
                    encoding = System.Text.Encoding.GetEncoding(1252);
                }
                catch (Exception)
                {
                    //fallback to ASCII encoding
                    encoding = new System.Text.ASCIIEncoding();
                }
            }
            client.Encoding = encoding;
            Program.Log("Using IRC text encoding codepage: " + encoding.CodePage.ToString());

            //set up config
            client.SendDelay = desBot.State.SendDelay.Value;
#if QNETBOT
            client.ActiveChannelSyncing = true;
#endif
            client.AutoNickHandling = false;
            
            //set up event handlers
            client.OnConnectionError += new EventHandler(client_OnConnectionError);
            client.OnConnecting += new System.EventHandler(client_OnConnecting);
            client.OnConnected += new System.EventHandler(client_OnConnected);
            client.OnRegistered += new System.EventHandler(client_OnRegistered);
            client.OnDisconnecting += new System.EventHandler(client_OnDisconnecting);
            client.OnDisconnected += new System.EventHandler(client_OnDisconnected);
#if JTVBOT
            client.OnOp += new OpEventHandler(client_OnOp);
            client.OnDeop += new DeopEventHandler(client_OnDeop);
            client.OnNames += new NamesEventHandler(client_OnNames);
#elif QNETBOT
            client.OnBan += new BanEventHandler(client_OnBan);
            client.OnUnban += new UnbanEventHandler(client_OnUnban);
            client.OnKick += new KickEventHandler(client_OnKick);
            client.OnWho += new WhoEventHandler(client_OnWho);
            client.OnNickChange += new NickChangeEventHandler(client_OnNickChange);
            client.OnQuit += new QuitEventHandler(client_OnQuit);
#endif
            client.OnJoin += new JoinEventHandler(client_OnJoin);
            client.OnPart += new PartEventHandler(client_OnPart);
            client.OnQueryMessage += new IrcEventHandler(client_OnQueryMessage);
            client.OnQueryNotice += new IrcEventHandler(client_OnQueryNotice);
            client.OnChannelMessage += new IrcEventHandler(client_OnChannelMessage);
            client.OnRawMessage += new IrcEventHandler(client_OnRawMessage);


            //set up auto-reconnect
            OnStateChanged += new StateChangedEventHandler(Irc_OnStateChanged);
            
            //set up hang detection timer
            lastcheck = IrcState.Connecting;
            detecthang = new Timer(new TimerCallback(DetectHang), null, 60000, 60000);

            //reset bansystem
            BanSystem.Reset();

            //spawn dedicated thread
            new Thread(new ThreadStart(client_Listen)).Start();
        }

#if JTVBOT
        /// <summary>
        /// Touches the userlist to make sure a user exists, and if not, creates it
        /// </summary>
        /// <param name="nick">The nickname to touch</param>
        /// <returns>An entry in the userlist</returns>
        static User TouchUser(string nick, bool present, bool notpresent)
        {
            User existing = desBot.State.UserList.Lookup(nick);
            if (existing != null)
            {
                if (existing.Left != DateTime.MaxValue && present)
                {
                    desBot.State.UserList.Remove(existing);
                }
                else if (existing.Left == DateTime.MaxValue && notpresent)
                {
                    existing.Left = DateTime.UtcNow;
                    desBot.State.UserList.MarkChanged(existing);
                    return existing;
                }
                else
                {
                    return existing;
                }
            }
            User touch = new User(nick, new HostMask(nick));
            if (notpresent)
            {
                touch.Left = DateTime.UtcNow;
            }
            desBot.State.UserList.Add(touch);
            return touch;
        }

        static void client_OnDeop(object sender, DeopEventArgs e)
        {
            User user = TouchUser(e.Whom, false, false);
            user.Meta.JTVModerator = false;
            user.Meta.DeopTime = DateTime.UtcNow;
        }

        static void client_OnOp(object sender, OpEventArgs e)
        {
            User user = TouchUser(e.Whom, false, false);
            user.Meta.JTVModerator = true;
            user.Meta.DeopTime = DateTime.UtcNow;
        }

        static void client_OnNames(object sender, NamesEventArgs e)
        {
            foreach (string name in e.UserList)
            {
                string nick = char.IsLetter(name[0]) ? name : name.Substring(1);
                User user = TouchUser(nick, true, false);
                if (name[0] == '@') user.Meta.JTVModerator = true;
            }
        }
#endif

        /// <summary>
        /// Disconnects the IRC tool
        /// </summary>
        /// <param name="reason">The reason to specify</param>
        public static void Disconnect(string reason)
        {
            if (client != null && client.IsConnected)
            {
                Program.Log("Disconnect initiated: " + reason);
                client.RfcQuit(reason, Priority.Critical);
                client.Disconnect();
            }
            else if(client != null)
            {
                State = IrcState.Disconnected;
            }
        }

        /// <summary>
        /// Check if control characters are supported
        /// </summary>
        /// <returns>True if supported</returns>
        static bool HasControlSupport(bool channel)
        {
#if JTVBOT
            return false;
#elif QNETBOT
            Meebey.SmartIrc4net.Channel c = client.GetChannel(Channel);
            if (channel && c == null) return false;
            return (!(channel ? c.Mode.Contains("c") : false) && ControlCharacter.Enabled);
#endif
        }

        /// <summary>
        /// Sends a channel message
        /// </summary>
        /// <param name="text">The text of the message</param>
        public static void SendChannelMessage(string text, bool immediately)
        {
            if (State != IrcState.Ready) throw new Exception("Invalid state for sending messages");
            string[] lines = text.Split(new char[] { '\n' });
#if JTVBOT
            {
                //MLM: now also split lines on 250 length when on JTV, since channel messages are truncated at that point
                const string more = ">>";
                const int maxlen = 250;
                const int minlen = 200;

                //process lines
                List<string> lines2 = new List<string>();
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    while (trimmed.Length > maxlen)
                    {
                        //try to split on a space in the range [minlen, maxlen]
                        int splitidx = trimmed.LastIndexOf(' ', maxlen - more.Length - 1);
                        if (splitidx < minlen)
                        {
                            //no space found, just pick maxlen
                            splitidx = maxlen - more.Length - 1;
                        }
                        string first = trimmed.Substring(0, splitidx);
                        string second = trimmed.Substring(splitidx).Trim();
                    
                        //add first line and try second again
                        lines2.Add(first + " " + more);
                        trimmed = more + " " + second;
                    }
                    lines2.Add(trimmed);
                }

                //replace collection
                lines = new string[lines2.Count];
                int idx = 0;
                foreach(string line in lines2)
                {
                    lines[idx] = line;
                    idx++;
                }
            }
#endif
            foreach (string line in lines)
            {
                string compat = !HasControlSupport(true) ? ControlCharacter.Strip(line) : line;
                client.SendMessage(SendType.Message, Channel, compat, immediately ? Priority.High : Priority.Medium);
                Program.Log("sending to channel: " + compat);
            }
            
        }

        /// <summary>
        /// Sends a private message
        /// </summary>
        /// <param name="text">The text of the message</param>
        /// <param name="nickname">The target of the message</param>
        /// <param name="notice">If set, send as NOTICE, otherwise, as PRIVMSG</param>
        public static void SendPrivateMessage(string text, string nickname, bool notice, bool immediately)
        {
#if JTVBOT
            //on JTV, notices are not supported
            notice = false;
#endif
            if (State < IrcState.Authing || State > IrcState.Ready) throw new Exception("Invalid state for sending messages");
            if (nickname.StartsWith("#") || nickname.Contains(" ")) throw new Exception("Invalid nickname");
            string[] lines = text.Split(new char[] { '\n' });
            foreach (string line in lines)
            {
                string compat = !HasControlSupport(false) ? ControlCharacter.Strip(line) : line;
                client.SendMessage(notice ? SendType.Notice : SendType.Message, nickname, compat, immediately ? Priority.High : Priority.Medium);
                Program.Log((notice ? "/notice" : "/msg") + " to " + nickname + ": " + compat);
            }
        }

        /// <summary>
        /// Sends a raw message to the IRC server
        /// </summary>
        /// <param name="text">The message to send</param>
        public static void SendRawMessage(string text)
        {
            client.WriteLine(text, Priority.Critical);
        }

        /// <summary>
        /// Injects a message from the console as if it was received through /msg
        /// </summary>
        /// <param name="text">The text in the message</param>
        public static void InjectConsoleMessage(string text)
        {
            IrcMessage message = new IrcMessage("<console>", text, null, false);
            client_OnMessage(message);
        }

#if QNETBOT
        /// <summary>
        /// Get privilege level for nickname
        /// </summary>
        /// <param name="nick">The nick to get privilege information for</param>
        /// <returns>The privilege level associated with the nickname</returns>
        public static PrivilegeLevel GetPrivilegeLevel(string nick)
        {
            ChannelUser user = client.GetChannelUser(Channel, nick);
            if (user == null) return PrivilegeLevel.None;
            if (user.IsOp) return PrivilegeLevel.Operator;
            if (user.IsVoice) return PrivilegeLevel.Voiced;
            return PrivilegeLevel.OnChannel;
        }

        /// <summary>
        /// Count number of bans on the channel
        /// </summary>
        /// <returns>Number of bans on channel</returns>
        public static int CountBans()
        {
            Channel channel = client.GetChannel(Channel);
            if (channel == null) return 0;
            return channel.Bans.Count;
        }
#endif

        /// <summary>
        /// Ban the specified hostmask
        /// </summary>
        /// <param name="mask">The hostmask to ban</param>
        public static void Ban(string mask)
        {
            client.RfcMode(Channel, "+b " + mask);
        }

        /// <summary>
        /// Unban the specified hostmask
        /// </summary>
        /// <param name="mask">The hostmask to unban</param>
        public static void Unban(string mask)
        {
            client.RfcMode(Channel, "-b " + mask);
        }

        /// <summary>
        /// Kick specified nick from channel
        /// </summary>
        /// <param name="nick">Nick to kick</param>
        /// <param name="reason">Reason for kick</param>
        public static void Kick(string nick, string reason)
        {
            client.RfcKick(Channel, nick, reason, Priority.High);
        }

        /// <summary>
        /// Set topic of channel
        /// </summary>
        /// <param name="topic">The new topic</param>
        public static void SetTopic(string topic)
        {
            client.RfcTopic(Channel, topic);
        }

        /// <summary>
        /// Set mode on channel
        /// </summary>
        /// <param name="mode">The mode to set</param>
        public static void SetChannelMode(string mode)
        {
            client.RfcMode(Channel, mode);
        }

#if QNETBOT
        /// <summary>
        /// Checks if the specified ban is currently enforced
        /// </summary>
        /// <param name="mask">The mask to check</param>
        /// <returns>True if the ban is enforced</returns>
        public static bool IsBanEnforced(string mask)
        {
            Channel channel = client.GetChannel(Channel);
            if (channel == null || !channel.IsSycned) throw new Exception("Not synced with that channel");
            return channel.Bans.Contains(mask);
        }
#endif

        static IrcState lastcheck = IrcState.None;
        public static void DetectHang(object ignored)
        {
            lock (desBot.State.GlobalSync)
            {
                if (ignored != client) return;
                if (State < IrcState.Ready)
                {
                    if (lastcheck == State)
                    {
                        Disconnect("Logic hang detected");
                        detecthang = null;
                    }
                    else lastcheck = State;
                }
                else
                {
                    detecthang = null;
                }
            }
        }

#if QNETBOT
        static void client_OnBan(object sender, BanEventArgs e)
        {
            BanSystem.OnIrcBan(e.Hostmask, e.Who);
        }

        static void client_OnUnban(object sender, UnbanEventArgs e)
        {
            BanSystem.OnIrcUnban(e.Hostmask, e.Who);
        }
#endif

        static void client_OnChannelMessage(object sender, IrcEventArgs e)
        {
            Program.Log("channel message from " + e.Data.Nick + ": " + e.Data.Message);
#if JTVBOT
            if (BanSystem.OnIrcChannelMessage(e.Data.Nick)) return;
#endif
            client_OnMessage(new IrcMessage(e.Data.Nick, e.Data.Message, e.Data.Channel, false));
        }

        static void client_OnQueryNotice(object sender, IrcEventArgs e)
        {
#if QNETBOT
            string nick = e.Data.Nick;
#elif JTVBOT
            string nick = e.Data.From;
#endif
            if (nick == null) client_OnServerMessage(e.Data.Message);
            else
            {
                Program.Log("/notice from " + nick + ": " + e.Data.Message);
                client_OnMessage(new IrcMessage(nick, e.Data.Message, null, true));
            }
        }

        static void client_OnQueryMessage(object sender, IrcEventArgs e)
        {
#if QNETBOT
            string nick = e.Data.Nick;
#elif JTVBOT
            string nick = e.Data.From;
#endif
            if (nick == null) client_OnServerMessage(e.Data.Message);
            else
            {
                Program.Log("/msg from " + nick + ": " + e.Data.Message);
                client_OnMessage(new IrcMessage(nick, e.Data.Message, null, false));
            }
        }

        static void client_OnMessage(IrcMessage message)
        {
            lock (desBot.State.GlobalSync)
            {
#if QNETBOT
                if (message.From == "Q") client_OnQMessage(message.Text);
#endif
                if (OnMessage != null) OnMessage.Invoke(message);
            }
        }

        static void client_OnServerMessage(string text)
        {
            Program.Log("Server message: " + text);
            client_OnMessage(new IrcMessage("<server>", text, null, false));
        }

#if QNETBOT
        static void client_OnQMessage(string text)
        {
            IrcSettings Settings = desBot.State.IrcSettings.Value;
            if (State == IrcState.Authing)
            {
                if (text == "You are now logged in as " + Settings.QAccount + ".")
                {
                    Program.Log("Authed with Q as " + Settings.QAccount);
                    if (Settings.QHideIP)
                    {
                        Program.Log("Requesting usermode +x");
                        State = IrcState.Cloaking;
                        client.RfcMode(Nickname, "+x", Priority.Critical);
                    }
                    else
                    {
                        State = IrcState.Retrieving;
                        client.SendMessage(SendType.Message, "Q", "CHANLEV " + Channel + " #" + Settings.QAccount);
                    }
                }
                else
                {
                    Program.Log("Auth with Q failed");
                    State = IrcState.Error_AuthFailed;
                    Disconnect("Q AUTH failed");
                }
            }
            else if (State == IrcState.Retrieving)
            {
                string expected = "Flags for #" + Settings.QAccount + " on " + Channel + ": ";
                if (text.StartsWith(expected))
                {
                    string flags = text.Substring(expected.Length);
                    if (flags.Contains("n")) QLevel = QAuthLevel.Owner;
                    else if (flags.Contains("m")) QLevel = QAuthLevel.Master;
                    else if (flags.Contains("o")) QLevel = QAuthLevel.Operator;
                    else if (flags.Contains("v")) QLevel = QAuthLevel.Voiced;
                    else if (flags.Contains("k")) QLevel = QAuthLevel.Known;
                }
                else if (text.StartsWith("You do not have sufficient access"))
                {
                    QLevel = QAuthLevel.None;
                }
                else if (text.EndsWith("is unknown or suspended."))
                {
                    QLevel = QAuthLevel.None;
                }
                else if (text.StartsWith("Remember: NO-ONE from QuakeNet will ever ask for your password"))
                {
                    return;
                }
                else
                {
                    State = IrcState.Error_AuthFailed;
                    Disconnect("Q AUTH failed");
                }
                Program.Log("Retrieved Q auth level on " + Channel + ": " + QLevel.ToString());
                q = new QCommandQueue();
                q.OnUpdate += new QCommandQueue.UpdateEventHandler(q_OnUpdate);
                State = IrcState.JoiningChannel;
                client.RfcJoin(Channel);
            }
            else
            {
                if (q != null)
                {
                    q.OnResponse(text);
                }
                else Program.Log("Unexpected message from Q (no queue): " + text);
            }
        }

        static void q_OnUpdate()
        {
            if (OnStateChanged != null) OnStateChanged.Invoke();
        }

#endif
        static void client_OnJoin(object sender, JoinEventArgs e)
        {
#if JTVBOT
            TouchUser(e.Who, true, false);
            if (e.Who.ToLower() == Nickname.ToLower())
            {
                client.RfcNames(Channel);
                State = IrcState.Ready;
                SendRawMessage("JTVCLIENT");
				string botname = desBot.State.JtvSettings.Value.Nickname.ToString();
                SendChannelMessage(botname + " is online! (v" + Token.GetCurrentVersion() + "@twitch.tv)" + (Program.IsBuggyTwitch ? "/bt" : ""), false);
            }
            
#elif QNETBOT
            if (e.Channel != Channel) return;
            if (e.Who == Nickname)
            {
                Program.Log("Joined channel " + Channel + ", ready to receive commands");
                State = IrcState.Ready;
            }
            else
            {
                DynamicDictionary<string, User, SerializableUser> users = desBot.State.UserList;
                HostMask entry = new HostMask(e.Who + "!" + e.Data.Ident + "@" + e.Data.Host);
                try
                {
                    User old = users.Lookup(e.Who);
                    if (old != null)
                    {
                        users.Remove(old);
                    }
                }
                catch (Exception ex) { }
                users.Add(new User(e.Who, entry));
                BanSystem.OnIrcEnter(entry, e.Who);
            }
#endif
        }

        static void client_OnPart(object sender, PartEventArgs e)
        {
#if JTVBOT
            TouchUser(e.Who, false, true);
#elif QNETBOT
            DynamicDictionary<string, User, SerializableUser> users = desBot.State.UserList;
            DateTime joined = DateTime.UtcNow;
            try
            {
                User old = users.Lookup(e.Who);
                users.Remove(old);
                joined = old.Joined;
            }
            catch (Exception ex) { }
            HostMask entry = new HostMask(e.Who + "!" + e.Data.Ident + "@" + e.Data.Host);
            users.Add(new User(e.Who, entry, joined, DateTime.UtcNow));
            BanSystem.OnIrcLeave(entry);
#endif
        }

        static void client_OnConnecting(object sender, System.EventArgs e)
        {
            Program.Log("Connecting to server " + Server + ":" + Port.ToString());
            State = IrcState.Connecting;
        }

        static void client_OnConnected(object sender, System.EventArgs e)
        {
            Program.Log("Connected with server, registering as " + Nickname);
            State = IrcState.Registering;
#if JTVBOT
            client.Login(new string[] { Nickname }, Nickname, 0, Nickname, desBot.State.JtvSettings.Value.Password);
#elif QNETBOT
            client.Login(new string[] { Nickname }, "desBot v" + Token.GetCurrentVersion(), 0, Username);
#endif
        }

        static void client_OnConnectionError(object sender, EventArgs e)
        {
            Program.Log("Connection failed");
            State = IrcState.Error_ConnectFailed;
            Disconnect("CONNECT ERROR");
        }

        static void client_OnRegistered(object sender, System.EventArgs e)
        {
#if JTVBOT
            State = IrcState.JoiningChannel;
            client.RfcJoin(Channel);
#elif QNETBOT
            Program.Log("Registered, authing with Q as " + desBot.State.IrcSettings.Value.QAccount);
            State = IrcState.Authing;
            client.SendMessage(SendType.Message, "Q@CServe.quakenet.org", "AUTH " + desBot.State.IrcSettings.Value.QAccount + " " + desBot.State.IrcSettings.Value.QPassword);
#endif
        }

        static void client_OnDisconnecting(object sender, System.EventArgs e)
        {
            Program.Log("Disconnecting from IRC server");
            if(State < IrcState.Disconnecting) State = IrcState.Disconnecting;
        }

        static void client_OnDisconnected(object sender, System.EventArgs e)
        {
            Program.Log("Disconnected from IRC server");
            if(State < IrcState.Disconnected) State = IrcState.Disconnected;
        }

        static void client_OnRawMessage(object sender, IrcEventArgs e)
        {
            if (e.Data.ReplyCode == ReplyCode.ErrorNicknameInUse || e.Data.ReplyCode == ReplyCode.ErrorNicknameCollision || e.Data.ReplyCode == ReplyCode.ErrorErroneusNickname)
            {
                Program.Log("Failed to register nickname");
                State = IrcState.Error_RegisterFailed;
                Disconnect("Nickname already taken");
            }
            else if (e.Data.ReplyCode == ReplyCode.BanList)
            {
#if QNETBOT
                if (e.Data.RawMessageArray.Length >= 7)
                {
                    try
                    {
                        DateTime when = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds((double)long.Parse(e.Data.RawMessageArray[6]));
                        BanSystem.OnIrcBanList(e.Data.RawMessageArray[4], e.Data.RawMessageArray[5], when);
                    }
                    catch (Exception ex)
                    {
                        Program.Log("Failed to parse extended ban info: " + ex.Message);
                    }
                }
#elif JTVBOT
                BanSystem.OnIrcBanList(e.Data.RawMessageArray[4], "jtv", DateTime.UtcNow);
#endif
            }
            else if (e.Data.ReplyCode == ReplyCode.EndOfBanList)
            {
                BanSystem.OnIrcEndOfBanList();
            }
#if QNETBOT
            else if ((int)e.Data.ReplyCode == 396 /*RPL_HOSTHIDDEN*/ && State == IrcState.Cloaking)
            {
                State = IrcState.Retrieving;
                client.SendMessage(SendType.Message, "Q", "CHANLEV " + Channel + " #" + desBot.State.IrcSettings.Value.QAccount);
            }
#endif
        }

#if QNETBOT
        static void client_OnNickChange(object sender, NickChangeEventArgs e)
        {
            DynamicDictionary<string, User, SerializableUser> users = desBot.State.UserList;
            DateTime joined = DateTime.UtcNow;
            try
            {
                User old = users.Lookup(e.OldNickname);
                users.Remove(old);
                joined = old.Joined;
            }
            catch (Exception ex) { }
            users.Add(new User(e.OldNickname, new HostMask(e.OldNickname + "!" + e.Data.Ident + "@" + e.Data.Host), joined, DateTime.UtcNow));
            try
            {
                User current = users.Lookup(e.NewNickname);
                users.Remove(current);
            }
            catch (Exception ex) { }
            HostMask entry = new HostMask(e.NewNickname + "!" + e.Data.Ident + "@" + e.Data.Host);
            users.Add(new User(e.NewNickname, entry));
            BanSystem.OnIrcEnter(entry, e.NewNickname);
        }

        static void client_OnQuit(object sender, QuitEventArgs e)
        {
            DynamicDictionary<string, User, SerializableUser> users = desBot.State.UserList;
            DateTime joined = DateTime.UtcNow;
            try
            {
                User old = users.Lookup(e.Who);
                users.Remove(old);
                joined = old.Joined;
            }
            catch (Exception ex) { }
            HostMask entry =  new HostMask(e.Who + "!" + e.Data.Ident + "@" + e.Data.Host);
            users.Add(new User(e.Who, entry, joined, DateTime.UtcNow));
            BanSystem.OnIrcLeave(entry);
        } 

        static void client_OnKick(object sender, KickEventArgs e)
        {
            if (e.Whom == Nickname)
            {
                Disconnect("Kicked from channel");
            }
            else
            {
                DynamicDictionary<string, User, SerializableUser> users = desBot.State.UserList;
                DateTime joined = DateTime.UtcNow;
                try
                {
                    User old = users.Lookup(e.Who);
                    users.Remove(old);
                    joined = old.Joined;
                }
                catch (Exception ex) { }
                HostMask entry = new HostMask(e.Who + "!" + e.Data.Ident + "@" + e.Data.Host);
                users.Add(new User(e.Who, entry , joined, DateTime.UtcNow));
                BanSystem.OnIrcLeave(entry);
            }
        }

        static void client_OnWho(object sender, WhoEventArgs e)
        {
            DynamicDictionary<string, User, SerializableUser> users = desBot.State.UserList;
            HostMask entry = new HostMask(e.WhoInfo.Nick + "!" + e.WhoInfo.Ident + "@" + e.WhoInfo.Host);
            try
            {
                User old = users.Lookup(e.WhoInfo.Nick);
                if (old != null)
                {
                    users.Remove(old);
                }
            }
            catch (Exception ex) { }
            users.Add(new User(e.WhoInfo.Nick, entry));
            BanSystem.OnIrcEnter(entry, e.WhoInfo.Nick);
        }
#endif

        static void client_Listen()
        {
            try
            {
                Program.Log("Started IRC thread");
                bool loop = true;
                try
                {
                    client.Connect(Server, Port);
                }
                catch (Exception ex)
                {
                    Program.Log("Failed to connect to IRC: " + ex.Message);
                    Disconnect("Connect failed");
                    loop = false;
                }
                while (loop)
                {
                    try
                    {
                        client.Listen();
                        loop = false;
                    }
                    catch (Exception ex)
                    {
                        Program.Log("IRC listen thread crashed, auto-restart: " + ex.Message);
                    }
                }
                Program.Log("Stopped IRC thread");
            }
            catch (Exception ex)
            {
                Program.Log("IRC thread exception: " + ex.Message);
                State = IrcState.Error_ConnectFailed;
            }
        }

        static void Irc_OnStateChanged()
        {
            if (state >= IrcState.Disconnected)
            {
                int delay = State == IrcState.Disconnected ? 1 : 60;
                lock (desBot.State.GlobalSync)
                {
                    autorestart = new Timer(new TimerCallback(AutoRestart), client, delay * 1000, Timeout.Infinite);
                }
            }
        }

        static void sendDelay_Changed(DynamicProperty<int, int> prop)
        {
            client.SendDelay = prop.Value;
        }

        static void AutoRestart(object ignored)
        {
            lock (desBot.State.GlobalSync)
            {
                if (ignored != client) return;
                Program.Log("Automatic IRC restart");
                autorestart.Dispose();
                autorestart = null;
                Init();
            }
        }
    }
}