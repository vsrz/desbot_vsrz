using System;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
namespace desBot
{
    /// <summary>
    /// Program main class
    /// </summary>
    static class Program
    {
        //flag set on JTV
        public const bool IsJTV = true;
        public static bool IsBuggyTwitch = false;
		public static string BotName = "TwitBot";

        //configuration file name
        const string ConfigFileName = "Settings.xml";

        //make log file name containing current date
        static string MakeLogFileName()
        {
            DateTime now = DateTime.UtcNow;
            return "Log_" + now.ToString("dd-MM-yy_HH-mm") + ".txt";
        }

        //log writer
        static StreamWriter logwriter = new StreamWriter(new FileStream(MakeLogFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite));

        //periodic quote timer functions
        static int quoteinterval;
        static Timer quotetimer;
        static DateTime quotelast = DateTime.UtcNow;

        static void SetQuoteTimer()
        {
            if (quotetimer != null) quotetimer.Dispose();
            if (quoteinterval > 0)
            {
                double first = (double)quoteinterval - (DateTime.UtcNow - quotelast).Duration().TotalMinutes;
                if (first < 0.0) first = 0.0;
                quotetimer = new Timer(new TimerCallback(OnQuoteTimer), null, (int)(first * 60000), quoteinterval * 60000);
            }
            else quotetimer = null;
        }

        static void OnQuoteTimer(object ignored)
        {
            Program.Log("Triggering auto-quote");
            Irc.InjectConsoleMessage("quote random");
            quotelast = DateTime.UtcNow;
        }
        static void OnQuoteIntervalChanged(DynamicProperty<int, int> prop)
        {
            quoteinterval = prop.Value;
            SetQuoteTimer();
        }


        //save timer
        static Timer savetimer;
        static void OnSaveTimer(object ignored)
        {
            Alice.Tick(null);
            if (stream_is_live)
            {
                TwitterCommand.CheckRecent();
                AdCommand.CheckAd();
            }
            Save();
        }

        //stats timer
        static Timer statstimer;
        static double prevtime = 0.0;
        static void OnStatsTimer(object ignored)
        {
            lock (State.GlobalSync)
            {
                Stats stats = new Stats();
                stats.Irc = Irc.State;
                stats.Qlevel = QAuthLevel.None;
                stats.Qquery = "n/a";
                stats.Nick = Irc.Nickname;
                stats.Channel = Irc.Channel;
                Process process = Process.GetCurrentProcess();
                stats.VirtualMemory = process.VirtualMemorySize64;
                stats.PhysicalMemory = process.WorkingSet64;
                double curtime = process.TotalProcessorTime.TotalSeconds;
                stats.CPUUsage = (curtime - prevtime) / (double)Environment.ProcessorCount;
                stats.Threads = process.Threads.Count;
                prevtime = curtime;
                Watcher.PublishStats(stats);
                statstimer = new Timer(new TimerCallback(OnStatsTimer), null, 1000, Timeout.Infinite);
            }
        }

        //cleanup timer
        static Timer cleanuptimer;
        static bool HasWarningExpired(Warning warning)
        {
            //TODO: hardcoded as 1 hour, maybe make configurable?
            return (DateTime.UtcNow - warning.Created).TotalHours >= 1.0;
        }
        static bool IsMetaUserStale(MetaUser meta)
        {
            //clear warnings older than an hour
            meta.Warnings.RemoveAll(new Predicate<Warning>(HasWarningExpired));
            
            //tag if elevated or warnings are still out
            if (meta.Elevation || meta.JTVModerator || meta.Warnings.Count > 0 || (DateTime.UtcNow - meta.DeopTime).TotalHours < 1.0)
            {
                return false;
            }

            //inform referencing users
            List<User> copy = new List<User>(meta.References);
            foreach (User user in copy)
            {
                user.Meta = null;
            }

            //list should be empty
            Debug.Assert(meta.References.Count == 0);

            //item now stale
            return true;
        }
        static object tag;
        static bool IsUserStale(User user)
        {
            //ignore tagged objects
            if (user.Tag == tag) return false;

            //ignore user with metadata
            if (user.CheckExistingMetaData() != null) return false;
            
            //ignore user with a last modification within last day
            //TODO: hardcoded as one day, consider making configurable
            if ((user.Left != DateTime.MinValue) && ((DateTime.UtcNow - user.LastChanged).TotalDays <= 1.0)) return false;

            //user data is stale
            return true;
        }

        static void OnCleanupTimer(object ignored)
        {
            lock (State.GlobalSync)
            {
                //update viewers count
                Irc.InjectConsoleMessage("viewers <silent>");

                //tag used to identify objects that we're cleaning
                tag = new object();
                
                //unban expired
                BanSystem.PerformUpdate();

                //tag all users currently in the ban list
                foreach (Ban ban in State.BanList.GetItems())
                {
                    int idx = 0;
                    while(idx != -1)
                    {
                        int nextidx = idx + 2 < ban.Affected.Length ? ban.Affected.IndexOf(", ", idx + 2) : -1;
                        int len = (nextidx == -1 ? ban.Affected.Length : nextidx) - idx;
                        if (len > 0)
                        {
                            string nick = ban.Affected.Substring(idx, len);
                            User user = State.UserList.Lookup(nick);
                            if (user != null) user.Tag = tag;
                        }
                        idx = nextidx;
                    }
                }

                //remove stale metadata entries
                State.MetaUserList.RemoveIf(new Predicate<MetaUser>(IsMetaUserStale));

                //remove 
                State.UserList.RemoveIf(new Predicate<User>(IsUserStale));

                //flush log file
                logwriter.Flush();
            }
        }

        /// <summary>
        /// Update topic if stream changes live status or title
        /// </summary>
        static bool stream_is_live = false;
        static int prev_live = 0;
        static string prev_title = null;
        static int max_viewers = 0;
        static DateTime start_time = DateTime.UtcNow;

        static void OnChannelUpdated(bool live, string title, int viewers)
        {
            lock (State.GlobalSync)
            {
                if (Irc.State == IrcState.Ready)
                {
                    int new_live = live ? 2 : 1;
                    bool condition = prev_live != 0;
                    if (condition && (new_live != prev_live || (live && title != prev_title)))
                    {
                        try
                        {
                            if (live)
                            {
                                //send if stream goes live
                                Irc.SendChannelMessage("The stream is now live with '" + title + "'", false);
                                stream_is_live = true;
                                //set start time
                                start_time = DateTime.UtcNow;
                            }
                            else
                            {
                                //parse duration
                                double mins = (DateTime.UtcNow - start_time).TotalMinutes;
                                if (mins > 1.0)
                                {
                                    int hours = (int)(mins / 60.0);
                                    int imins = (int)(mins - 60 * hours);
                                    string duration = hours > 0 ? hours + " hours and " : "";
                                    duration += imins + " minutes";
                                    State.LastPeakViews.Value = max_viewers;
                                    State.LastStreamDateTime.Value = DateTime.UtcNow;
                                    Irc.SendChannelMessage("After " + duration + " of streaming, it appears we have reached the end :(", false);
                                }
                                else Irc.SendChannelMessage("The stream has ended :(", false);

                                //reset viewer count
                                max_viewers = 0;
                            }
                        }
                        catch (Exception ex)
                        {
                            Program.Log("Failed to update topic: " + ex.Message);
                        }
                    }
                    prev_live = new_live;
                    prev_title = title;
                }
                if (live)
                {
                    //send if we break through some cap
                    CheckViewerThreshold(viewers, ref max_viewers, 5000);
                    CheckViewerThreshold(viewers, ref max_viewers, 4000);
                    CheckViewerThreshold(viewers, ref max_viewers, 3000);
                    CheckViewerThreshold(viewers, ref max_viewers, 2500);
                    CheckViewerThreshold(viewers, ref max_viewers, 2000);
                    CheckViewerThreshold(viewers, ref max_viewers, 1500);
                    CheckViewerThreshold(viewers, ref max_viewers, 1000);
                    CheckViewerThreshold(viewers, ref max_viewers, 750);
                    CheckViewerThreshold(viewers, ref max_viewers, 500);
                    CheckViewerThreshold(viewers, ref max_viewers, 250);

                    //update max if none of the thresholds is hit
                    if (viewers > max_viewers) max_viewers = viewers;
                }
                else
                {
                    //reset viewer count
                    max_viewers = 0;
                    stream_is_live = false;
                }
            }
        }

        /// <summary>
        /// Peak number for viewers this session
        /// </summary>
        static public int PeakViewers { get { return max_viewers; } }

        /// <summary>
        /// Generates flavor messages using viewer count
        /// </summary>
        /// <param name="now">Current number of viewers</param>
        /// <param name="prev">Peak viewers</param>
        /// <param name="threshold">Interesting threshold number</param>
        static void CheckViewerThreshold(int now, ref int prev, int threshold)
        {
            if (now > prev)
            {
                if (now >= threshold && prev < threshold)
                {
                    Irc.SendChannelMessage("Good news everyone! We now have more than " + threshold + " viewers!", false);
                    prev = now;
                }
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            try
            {   
                //print versions
                Log("Running desBot v" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " for " + (IsJTV ? "Twitch TV" : "QuakeNet"));
                Log("Using SmartIRC4Net library v" + Assembly.GetAssembly(typeof(Meebey.SmartIrc4net.IrcClient)).GetName().Version.ToString());

                //parse commandline
                foreach (string arg in args)
                {
                    if (arg.ToLower() == "-buggytwitch")
                    {
                        Log("Buggy Twitch Workaround enabled");
                        IsBuggyTwitch = true;
                    }
                }
				
                //load settings
                try
                {
                    using (Stream stream = new FileStream(ConfigFileName, FileMode.Open, FileAccess.Read))
                    {
                        Program.Log(Settings.Load(stream, new XmlSerializer()));
                    }
                }
                catch (Exception ex)
                {
                    //cannot open settings
                    Console.WriteLine("Failed to open configuration: " + ex.Message);
                    Console.WriteLine("Do you wish to revert to defaults?");
                    Console.WriteLine("WARNING: This will destroy ALL settings and data. [y/n]");
                    string response = Console.ReadLine().ToLower();
                    if (response.StartsWith("y"))
                    {
                        if (!Save())
                        {
                            //cannot save new default settings
                            return;
                        }
                    }
                    else
                    {
                        //no default settings and no existing settings, cannot continue
                        return;
                    }
                }

                //fix up users that didn't leave from save
                foreach (User user in State.UserList.GetItems())
                {
                    if (user.Left == DateTime.MaxValue) user.Left = Settings.LastLoadedSaveTime;
                }

                //clean up state
                OnCleanupTimer(null);

                //get public IP
                if (!GetPublicIP())
                {
                    Log("Warning: no public IP identified, remote client connection will only be available on localhost!");
                    State.PublicIP.Value = "127.0.0.1";
                }

                //save updated data
                Save();

                //set quote interval watcher
                State.QuoteInterval.OnChanged += new OnPropertyValueChanged<int, int>(OnQuoteIntervalChanged);
                OnQuoteIntervalChanged(State.QuoteInterval);

                //init commands
                CommandHandler.Init("!");
                CommandHandler.AddPrefix(".");
                CommandHandler.AddPrefix("$");

                //add triggers
                Trigger toremove = null;
                foreach (Trigger trigger in State.TriggerList.GetItems())
                {
                    if (trigger.Keyword == "time")
                    {
                        toremove = trigger;
                    }
                    else new TriggerInstance(trigger.Text, trigger.Keyword, false);
                }

                //init change watcher
                Watcher.Init();

                if (toremove != null) State.TriggerList.Remove(toremove);

                //when channel gets updated
                ViewersCommand.OnDefaultChannelUpdated += new ViewersCommand.OnDefaultChannelUpdatedHandler(OnChannelUpdated);

                //start save timer
                savetimer = new Timer(new TimerCallback(OnSaveTimer), null, 60000, 60000);

                //start stats timer
                statstimer = new Timer(new TimerCallback(OnStatsTimer), null, 1000, Timeout.Infinite);

                //start cleanup timer
                cleanuptimer = new Timer(new TimerCallback(OnCleanupTimer), null, 10000, 10000);

                //set up events
                Irc.OnMessage += new Irc.MessageEventHandler(CommandHandler.HandleMessage);

                Irc.OnMessage += new Irc.MessageEventHandler(Alice.Tick);

                //handle subscriber events
                Irc.OnMessage += new Irc.MessageEventHandler(JTV.HandleMessage);

                //start IRC service
                Irc.Init();

                //init done
                Program.Log("Main thread initalization complete");

                //read messages
                while (true)
                {
                    string line = Console.ReadLine();
                    try
                    {
                        Irc.InjectConsoleMessage(line);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                //crash :(
                Program.Log("CRASH: " + ex.Message);
                Save();
                Environment.Exit(-1);
            }
            finally
            {
                //ensure last messages are also sent to the log
                logwriter.Flush();
            }
        }

        /// <summary>
        /// Saves all settings
        /// </summary>
        public static bool Save()
        {
            lock (State.GlobalSync)
            {
                CommandHandler.PushLimiters();
                try
                {
                    using (Stream stream = new FileStream(ConfigFileName, FileMode.Create, FileAccess.Write))
                    {
                        Program.Log(Settings.Save(stream, new XmlSerializer()));
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Program.Log("Failed to save configuration to '" + ConfigFileName + "': " + ex.Message);
                }
                finally
                {
                    logwriter.Flush();
                }
                return false;
            }
        }

        /// <summary>
        /// Add entry to log
        /// </summary>
        /// <param name="text">The entry to add</param>
        public static void Log(string text)
        {
            Watcher.WriteLog(text);
            PrivateLog(text);
        }

        /// <summary>
        /// Add entry to log, but do not transmit
        /// </summary>
        /// <param name="text">The entry to add</param>
        public static void PrivateLog(string text)
        {
            lock (State.GlobalSync)
            {
                string readable = ControlCharacter.Serialize(text);
                logwriter.WriteLine(DateTime.UtcNow.ToLocalTime().ToString("u") + " PST - " + readable);
            }
        }

        /// <summary>
        /// Performs a regular expression search of the last log entries, sorted by time
        /// </summary>
        /// <param name="expr">The expression to find</param>
        /// <returns>List of matching expressions</returns>
        public static IEnumerable<string> GrepLog(string expr)
        {
            lock (State.GlobalSync)
            {
                //compile regex
                Regex regex = new Regex(expr, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                //flush log
                logwriter.Flush();

                //reopen file in a new stream
                FileStream stream = (FileStream)logwriter.BaseStream;
                using (stream = new FileStream(stream.Name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    //results
                    LinkedList<string> result = new LinkedList<string>();

                    //read line
                    StreamReader reader = new StreamReader(stream);
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        if (regex.Match(line).Success)
                        {
                            result.AddFirst(line);
                        }
                        if (result.Count > 1000)
                        {
                            throw new Exception("Too many matches, be more specific");
                        }
                    }

                    //done
                    return result;
                }
            }
        }

        /// <summary>
        /// Reconnect IRC
        /// </summary>
        public static void Reconnect()
        {
            try
            {
                Irc.Disconnect("I'll be back");
                Irc.Init();
            }
            catch (Exception ex)
            {
                Log("There seems to be a configuration error, please check the Settings tab\n\nAdditional info: " + ex.Message);
            }
        }

        /// <summary>
        /// Terminate application
        /// </summary>
        public static void Terminate()
        {
            //disconnect
            Irc.Disconnect("Termination requested");
            Thread.Sleep(1000);

            //fix up users that didn't leave to save
            foreach (User user in State.UserList.GetItems())
            {
                if (user.Left == DateTime.MaxValue) user.Left = DateTime.UtcNow;
            }
            Save();

            //quit
            Environment.Exit(0);
        }

        /// <summary>
        /// Utility ot retrieve public IP
        /// </summary>
        public static bool GetPublicIP()
        {
            try
            {
                //request IP from whatismyip.com
                string url = "http://ifconfig.me/ip";
                //string regex = @"(?<=<TITLE>.*)\d*\.\d*\.\d*\.\d*(?=</TITLE>)";
                string regex = @"\d*\.\d*\.\d*\.\d*";

                //read webpage
                System.Net.WebClient wc = new System.Net.WebClient();
                System.Text.UTF8Encoding utf8 = new System.Text.UTF8Encoding();
                string response = utf8.GetString(wc.DownloadData(url));

                //match regex
                System.Text.RegularExpressions.Match match = new System.Text.RegularExpressions.Regex(regex).Match(response);
                if (!match.Success)
                {
                    Program.Log("Failed to get parse response from IP provider");
                    return false;
                }

                //parse as IP
                State.PublicIP.Value = System.Net.IPAddress.Parse(match.Value).ToString();
                Program.Log("My IP Address is: " + State.PublicIP.Value);

                //done
                return true;
            }
            catch (Exception ex)
            {
                //failed
                Program.Log("Failed to get public IP: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Executes a remote command
        /// </summary>
        /// <param name="command"></param>
        public static void ExecuteRemoteCommand(Connection source, RemoteCommand command)
        {
            Irc.InjectConsoleMessage(ControlCharacter.Deserialize(command.Command));
            RemoteCommandResponse response = new RemoteCommandResponse();
            response.ID = command.ID;
            source.Send(response);
        }

        /// <summary>
        /// Restore a backup
        /// </summary>
        /// <param name="settings"></param>
        public static void RestoreBackup(SerializableSettings settings)
        {
            lock (State.GlobalSync)
            {
                try
                {
                    Program.Log("Restoring backup...");
                    Settings.ApplySettings(settings);
                    Save();
                    Irc.Init();
                    Program.Log("Restarting...");
                }
                catch (Exception ex)
                {
                    Program.Log("Failed to restore backup to '" + ConfigFileName + "': " + ex.Message);
                }
            }
        }
    }
}
