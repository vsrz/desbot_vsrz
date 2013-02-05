using System;
using System.Net;
using System.Globalization;
namespace desBot
{
    /// <summary>
    /// Command that repeats text in channel
    /// </summary>
    class TwitterCommand : Command
    {
        public static void AutoRegister()
        {
            new TwitterCommand();
        }

        TwitterCommand()
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "twitterconfig";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " [<key> [<value]]: If empty, show recent tweet. If key specified, show associated value. If key and value specified, set key to value. Keys: interval, enable, account";
        }

        //keys
        const string KeyInterval = "interval";
        const string KeyEnabled = "enabled";
        const string KeyAccount = "account";

        //min interval
        const int MinInterval = 5;

        //show value for key
        void ShowKey(IrcMessage msg, string key)
        {
            switch (key.ToLower())
            {
                case KeyInterval:
                    msg.ReplyAuto("The interval between repeats is set to " + State.TwitterInterval.Value + " minutes");
                    break;
                case KeyEnabled:
                    msg.ReplyAuto("Twitter repeat is " + (State.TwitterEnabled.Value ? "enabled" : "disabled"));
                    break;
                case KeyAccount:
                    msg.ReplyAuto("Twitter source account is " + State.TwitterAccount.Value);
                    break;
                default:
                    throw new Exception("Unknown key specified");
            }
        }

        //set value for key
        void SetKey(IrcMessage msg, string key, string value)
        {
            switch (key.ToLower())
            {
                case KeyInterval:
                    {
                        int minutes;
                        if (int.TryParse(value, out minutes) && minutes >= MinInterval)
                        {
                            State.TwitterInterval.Value = minutes;
                            msg.ReplyAuto("The interval between repeats is set to " + minutes + " minutes");
                        }
                        else throw new Exception("Interval must be integral value (minutes), >= " + MinInterval);
                    }
                    break;
                case KeyEnabled:
                    {
                        switch (value.ToLower())
                        {
                            case "1":
                            case "true":
                            case "yes":
                                State.TwitterEnabled.Value = true;
                                msg.ReplyAuto("Twitter repeat has been enabled");
                                break;
                            case "0":
                            case "false":
                            case "no":
                                State.TwitterEnabled.Value = false;
                                msg.ReplyAuto("Twitter repeat has been disabled");
                                break;
                            default:
                                throw new Exception("Enabled flag must be 'true' or 'false'");
                        }
                    }
                    break;
                case KeyAccount:
                    {
                        //strip @ from twitter account
                        if (value.StartsWith("@")) value = value.Substring(1);
                        if (string.IsNullOrEmpty(value)) throw new Exception("Account must be an twitter account-name");
                        State.TwitterAccount.Value = value;
                    }
                    break;
                default:
                    throw new Exception("Unknown key specified");
            }
        }

        //last repeat
        static DateTime lastrepeat = DateTime.MinValue;
        static string lastmessage;

        class Fetcher
        {
            static public DateTime LastFetch = DateTime.MinValue;
            static object locked = new object();

            public void Fetch()
            {
                try
                {
                    lock (locked)
                    {
                        //ignore if too quick
                        if ((DateTime.UtcNow - LastFetch).TotalMinutes < MinInterval) return;
                        LastFetch = DateTime.UtcNow;
                    }

                    //URL to request
                    string url = "http://search.twitter.com/search.json?q=from:" + State.TwitterAccount.Value;
                    url = Uri.EscapeUriString(url);

                    //get response
                    var stream = HttpWebRequest.Create(url).GetResponse().GetResponseStream();
                    string response = new System.IO.StreamReader(stream).ReadToEnd();

                    lock (locked)
                    {
                        //parse
                        string msg = Parse(response) + GetLastPostAge(response);
                        
                        if (msg != lastmessage)
                        {
                            //bump immediately if text changed                            
                            lastmessage = msg;

                            
                            if (msg != null)
                            {
                                lastrepeat = DateTime.MinValue;
                                ShowRecent(false, true);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Program.Log("Twitter Fetch Exception: " + ex.Message);
                }
            }

            string Parse(string str)
            {
                try
                {
                    //JSON "parser", just look for first  entry of "text":"whatever"
                    string needle = "\"text\":\"";
                    int pos = str.IndexOf(needle);
                    //if(pos < 0) return null;
                    int start = pos + needle.Length;
                    for (int i = start; ; )
                    {
                        int end = str.IndexOf('"', i);
                        if (str[end - 1] == '\\') i = end + 1;
                        else return Unescape(str.Substring(start, end - start));
                    }
                }
                catch (Exception)
                {
                    //ignore
                }
                return null;
            }

            static string GetLastPostAge(string str)
            {
                string txt = "";
                DateTime now = DateTime.Now;
                // first dissect tweet and get post time
                try
                {
                    //JSON "parser", just look for first  entry of "text":"whatever"
                    string needle = "\"created_at\":\"";
                    int pos = str.IndexOf(needle);
                    //if(pos < 0) return null;
                    int start = pos + needle.Length;
                    int i = start;
                    int end = str.IndexOf('"', i);
                    if (str[end - 1] == '\\') i = end + 1;
                    else txt = Unescape(str.Substring(start, end - start));
                }
                catch (Exception)
                {
                    //ignore
                }

                try
                {
                    DateTime postdate = DateTime.ParseExact(txt, "ddd, dd MMM yyyy HH:mm:ss zzzz", CultureInfo.CurrentCulture);
                    TimeSpan ts = now - postdate;

                    // Minutes
                    if (ts.Minutes > 0)
                    {
                        txt = ts.Hours + " min" + (ts.Hours > 1 ? "s" : "");
                        ts.Subtract(new TimeSpan(0, ts.Minutes, 0));
                    }

                    // Hours
                    if (ts.Hours > 0)
                    {
                        txt = ts.Hours + " hour" + (ts.Hours > 1 ? "s" : "");
                        ts.Subtract(new TimeSpan(ts.Hours, 0, 0));
                    }

                    // Days
                    if (ts.Days > 0)
                    {
                        txt = ts.Days + " day" + (ts.Days > 1 ? "s" : "");
                        ts.Subtract(new TimeSpan(ts.Days, 0, 0, 0));
                    }
                    txt.Replace("  ", " ");
                    txt = " (" + txt + " ago)";


                }
                catch (Exception ex)
                {
                    txt = "";
                    Program.Log("Unable to parse datetime from twitter feed. " + ex.Message);
                    
                }

                return txt;



            }
            static string Unescape(string str)
            {
                string result = "";
                int start = -1;
                while (true)
                {
                    int stop = str.IndexOf('\\', start + 1);
                    if (stop == -1)
                    {
                        result += str.Substring(start + 1);
                        return result;
                    }
                    result += str.Substring(start + 1, stop - start - 1);
                    start = stop;
                }
            }
        }

        //show recent tweet
        static void ShowRecent(bool fetchonly, bool nofetch)
        {
            if (!fetchonly)
            {
                if (lastmessage != null)
                {
                    lastrepeat = DateTime.UtcNow;
                    Irc.SendChannelMessage("@" + State.TwitterAccount.Value + ": " + lastmessage, false);
                }
            }

            if (!nofetch)
            {
                //start new worker thread for fetch
                new System.Threading.Thread(new System.Threading.ThreadStart(new Fetcher().Fetch)).Start();
            }
        }

        //check for recent tweet
        static public void CheckRecent()
        {

            if (State.TwitterEnabled.Value)
            {
                if ((DateTime.UtcNow - Fetcher.LastFetch).TotalMinutes >= MinInterval)
                {
                    ShowRecent(true, false);
                }
                if ((DateTime.UtcNow - lastrepeat).TotalMinutes >= State.TwitterInterval.Value)
                {
                    ShowRecent(false, false);
                }
            }
        }

        //execute command
        public override void Execute(IrcMessage message, string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                ShowRecent(false, false);
            }
            else
            {
                string[] words = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 1)
                {
                    ShowKey(message, words[0]);
                }
                else if (words.Length == 2)
                {
                    SetKey(message, words[0], words[1]);
                }
                else throw new Exception("Syntaxis error, expects: !twitter [<key> [<value>]]");
            }
        }
    }
}