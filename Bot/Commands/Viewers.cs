using System;
using System.Net;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Xml;
namespace desBot
{
    /// <summary>
    /// Command that retrieves the number of viewers on a JTV stream
    /// </summary>
    class ViewersCommand : Command
    {
        public static string DefaultChannel { get { return State.JtvSettings.Value.Channel.Substring(1); } }

        public delegate void OnDefaultChannelUpdatedHandler(bool live, string title, int viewers);
        public static OnDefaultChannelUpdatedHandler OnDefaultChannelUpdated;

        /// <summary>
        /// Cached stream info
        /// </summary>
        class Cache
        {
            public string stream;
            public DateTime retrieved;
            public int viewers = 0;
            public int embeds = 0;
            public int total = 0;
            public void Report(IrcMessage message)
            {
                if (viewers == 0 && embeds == 0 && total == 0)
                {
                    DateTime lastStream = State.LastStreamDateTime.Value;
                    string peak = State.LastPeakViews.ToString();
                    if (peak == "0")
                    {
                        peak = "an unknown number of";
                    }

                    message.ReplyAuto("Channel is not live. Last stream was " + lastStream.ToString("f") + " UTC with " + State.LastPeakViews.Value + " peak viewers.");
                }
                else
                {
                    string chatmsg = "";
                    string peak = "";
                    if (stream.EndsWith(DefaultChannel))
                    {
                        lock (State.GlobalSync)
                        {
                            //number of chatters
                            int chat = 0;
                            foreach (User user in State.UserList.GetItems())
                            {
                                if (user.Left == DateTime.MaxValue) chat++;
                            }
                            chatmsg = " and " + chat.ToString() + " chatters";

                            //peak viewer count
                            peak = Program.PeakViewers + " peak";
                        }
                    }
                    
                    // Remove the stream_count and embed_count variables, as the JTV API doesn't appear to report them properly
                    message.ReplyAuto(
                        "Currently " + ControlCharacter.Underline() + viewers.ToString() + ControlCharacter.Underline() + " viewers (" + peak + ")" 
                        + chatmsg + " are tuned in to " + ControlCharacter.Underline() + stream + ControlCharacter.Underline() + ", which has a total of "
                        + ControlCharacter.Underline() + total.ToString() + ControlCharacter.Underline() + " views"
                        );
                }
            }
        }
        static Dictionary<string, Cache> cache = new Dictionary<string,Cache>();

        /// <summary>
        /// Helper class that asynchronously retrieves JTV stream page and responds to user
        /// </summary>
        class AsyncExec
        {
            public string stream;
            public WebRequest request;
            public IrcMessage message;
            public bool silent;

            public void Execute()
            {
                new Thread(new ThreadStart(Thread)).Start();
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

            static string Parse(string json, string field)
            {
                //JSON "parser", just look for first  entry of "text":"whatever"
                var needle = "\"" + field + "\":";
                int pos = json.IndexOf(needle);
                if(pos < 0) return "";
                int start = pos + needle.Length;
                for (int i = start; ; )
                {
                    int end = json.IndexOf('"', i);
                    if (end < 0) return "";
                    if (json[end - 1] == '\\')
                    {
                        i = end + 1;

                    }
                    else if (end == start)
                    {
                        return "";
                    }
                    else
                    {
                        return Unescape(json.Substring(start, end - start - 1));
                    }
                }

            }

            void Thread()
            {
                // Lazy hard code. API currently broken
                // See: https://github.com/vsrz/desbot_vsrz/issues/21
                return;
                try
                {
                    if (!silent)
                    {
                        Program.Log("Retrieving Twitch JSON using API: " + request.RequestUri);
                    }
                    using (WebResponse response = request.GetResponse())
                    {
                        string title = null;
                        Cache result = new Cache();
                        var json = new StreamReader(response.GetResponseStream()).ReadToEnd();
                        int.TryParse(Parse(json, "viewers"), out result.viewers);
                        result.embeds = 0;
                        int.TryParse(Parse(json, "views"), out result.total);
                        result.stream = Parse(json, "url");
                        title = Parse(json, "status");
                        
                        if(result.stream == null) result.stream = stream;
                        result.retrieved = DateTime.UtcNow;
                        cache[stream] = result;
                        if (!silent)
                        {
                            result.Report(message);
                        }
                        else
                        {
                            Program.Log("Viewer statistics: " + result.viewers + " viewers (" + Program.PeakViewers + " peak), " + result.total + " total views, title = " + title);
                        }
                        if (stream == DefaultChannel && OnDefaultChannelUpdated != null)
                        {
                            OnDefaultChannelUpdated.Invoke(result.viewers != 0 || result.embeds != 0 || result.total != 0, title, result.viewers);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Program.Log("Failed to retrieve viewer count: " + ex.Message);
                    if (!silent)
                    {
                        message.ReplyAuto("The stream " + "" +  "appears to be offline");
                    }
                    try
                    {
                        cache.Remove(stream);
                    }
                    finally { }
                }
            }
        }

        public static void AutoRegister()
        {
            new ViewersCommand();
        }

        ViewersCommand()
        {
            Privilege = PrivilegeLevel.OnChannel;
        }

        public override string GetKeyword()
        {
            return "viewers";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " [Twitch Stream] Outputs viewer statistics for a stream. Default stream is '" + DefaultChannel + "'.";
        }

        public override void Execute(IrcMessage message, string args)
        {
            if (Limiter.AttemptOperation(message.Level))
            {
                bool silent = false;
                if (args == "<silent>")
                {
                    args = DefaultChannel;
                    silent = true;
                }
                string stream = args.Length == 0 ? DefaultChannel : args;
                string uri = "https://api.twitch.tv/kraken/streams/" + stream;
                if (Uri.IsWellFormedUriString(uri, UriKind.Absolute))
                {
                    if (cache.ContainsKey(stream))
                    {
                        Cache item = cache[stream];
                        if (item == null)
                        {
                            //already pending
                            return;
                        }
                        else if ((DateTime.UtcNow - item.retrieved).TotalMinutes <= 1.0)
                        {
                            //in cache
                            if (!silent)
                            {
                                item.Report(message);
                            }
                            return;
                        }
                    }

                    //add new request
                    if (cache.ContainsKey(stream)) cache.Remove(stream);
                    cache.Add(stream, null);
                    AsyncExec async = new AsyncExec();
                    async.request = WebRequest.Create(uri);
                    async.message = message;
                    async.stream = stream;
                    async.silent = silent;
                    async.Execute();
                }
                else if (!silent)
                {
                    message.ReplyPrivate("The stream you specified is invalid");
                }
            }
        }
    }
}