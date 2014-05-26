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

                    message.ReplyAuto(stream + " is not live. Last stream was " + lastStream.ToString("f") + " UTC with " + State.LastPeakViews.Value + " peak viewers.");
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

            void Thread()
            {
                try
                {
                    if (!silent)
                    {
                        Program.Log("Retrieving JTV XML using API: " + request.RequestUri);
                    }
                    using (WebResponse response = request.GetResponse())
                    {
                        string title = null;
                        Cache result = new Cache();
                        XmlReader reader = new XmlTextReader(response.GetResponseStream());
                        reader.MoveToContent();
                        while (reader.Read())
                        {
                            if (reader.NodeType == XmlNodeType.Element)
                            {
                                string name = reader.Name;
                                reader.Read();
                                if(reader.NodeType == XmlNodeType.Text)
                                {
                                    string value = reader.Value;
                                    switch (name)
                                    {
                                        case "channel_count":
                                            result.viewers = int.Parse(value);
                                            break;
                                        case "embed_count":
                                            result.embeds = int.Parse(value);
                                            break;
                                        case "views_count":
                                            result.total = int.Parse(value);
                                            break;
                                        case "channel_url":
                                            // API returns the Legacy URL so replace it
                                            value.Replace("justin.tv", "twitch.tv");
                                            result.stream = value;
                                            break;
                                        case "title":
                                            if(title == null) title = value;
                                            break;
                                    }
                                }
                            }
                        }
                        
                        if(result.stream == null) result.stream = stream;
                        result.retrieved = DateTime.UtcNow;
                        cache[stream] = result;
                        if (!silent)
                        {
                            result.Report(message);
                        }
                        else
                        {
                            Program.Log("Viewer statistics: " + result.viewers + " viewers (" + Program.PeakViewers + " peak), " + result.embeds + " embeds, " + result.total + " total views, title = " + title);
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
                        message.ReplyAuto("The stream at " + request.RequestUri + " appears to be down :(");
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
            return " [<JTV or Twitch stream>]: Outputs viewer statistics for a stream. Default stream is '" + DefaultChannel + "'.";
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
                string uri = "http://api.justin.tv/api/stream/list.xml?channel=" + stream;
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