using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net;
using System.Xml;
using System.IO;
namespace desBot
{
	static class SC2Ranks
	{
		public static string ApiKey
		{
			get
			{
				return desBot.State.SC2RanksSettings.Value.ApiKey;
			}

		}

		public static bool IsEnabled
		{
			get
			{
				return desBot.State.SC2RanksSettings.Value.IsEnabled;
			}

			set
			{
				desBot.State.SC2RanksSettings.Value.IsEnabled = value;
			}
		}
	}

    class RankCommand : Command
    {
		//const string appkey = "?appKey=desBot";
		static string appkey = "?appKey=" + SC2Ranks.ApiKey;
		static TimeSpan cacheduration = TimeSpan.FromHours(1.0);

        class Cache
        {
            public class Item
            {
                public string Name;
                public int ID;
            }
            public class Team
            {
                public int Wins;
                public int Losses = -1;
                public int Rank;
                public String League;
                public int Bracket;
                public bool IsRandom;
                public int Points;
                public DateTime Refreshed;
            }
            public class Details
            {
                public List<Team> Teams = new List<Team>();
                public string Name = null;
                public string Link = null;
            }
            public class Entry
            {
                public string Region = null;
                public string SearchName = null;
                public List<Item> Items = new List<Item>();
                public DateTime Created = DateTime.UtcNow;
                public string Error = null;
                public bool Ready = false;
                public bool Exact = false;
                public List<Details> Details = new List<Details>();

                public void Write(IrcMessage source, int showbracket)
                {
                    if (Error != null)
                    {
                        source.ReplyAuto("Rank: " + Error);
                    }
                    else
                    {
                        if (Details.Count != 0)
                        {
                            List<string> results = new List<string>();
                            foreach (Cache.Details details in Details)
                            {
                                string result = "Rank: " + details.Name + " on " + Region.ToUpper() + " - ";
                                int bracket = 1;
                                bool hasbracket = false;
                                foreach (Team team in details.Teams)
                                {
                                    if (team.Bracket >= bracket && (showbracket == 0 || team.Bracket == showbracket))
                                    {
                                        result += team.Bracket.ToString() + "v" + team.Bracket.ToString() + " " + team.League.ToUpper().Substring(0, 1) + team.League.Substring(1) + " #" + team.Rank;
                                        result += " (" + team.Points.ToString() + " points, " + team.Wins.ToString() + " wins" + (team.Losses <= 0 ? "" : ", " + team.Losses.ToString() + " losses, " + (team.Wins * 100 / (team.Wins + team.Losses)).ToString() + "%") + ") ";
                                        double hours = (team.Refreshed - DateTime.UtcNow).Duration().TotalHours;
                                        string age = hours * 60.0 < 100.0 ? ((int)(hours * 60.0)).ToString() + " mins" : ((int)(hours + 0.5)).ToString() + " hours";
                                        result += "as of " + age + " ago - ";
                                        bracket = team.Bracket + 1;
                                        hasbracket = true;
                                    }
                                }
                                if (hasbracket)
                                {
                                    result += "More info at " + ControlCharacter.Underline() + ControlCharacter.Color(IrcColor.Blue) + details.Link + ControlCharacter.Restore();
                                    results.Add(result);
                                }
                            }
                            if (results.Count > 4)
                            {
                                source.ReplyAuto("Rank: Too many hits (" + Items.Count.ToString() + ") to display, see " + ControlCharacter.Underline() + ControlCharacter.Color(IrcColor.Blue) + "http://sc2ranks.com/search/contains/" + Region + "/" + SearchName + ControlCharacter.Restore() + " for all hits");
                            }
                            else
                            {
                                foreach (string result in results)
                                {
                                    source.ReplyAuto(result);
                                }
                            }
                        }
                        else if (Items.Count == 0)
                        {
                            source.ReplyAuto("Rank: No matching names found :(");
                        }
                        else if (Items.Count >= 1)
                        {
                            string result = "Rank: Are you looking for one of these names? ";
                            foreach (Item item in Items)
                            {
                                result += item.Name + ", ";
                            }
                            result = result.Substring(0, result.Length - 2);
                            source.ReplyAuto(result);
                        }
                        else
                        {
                            source.ReplyAuto("Rank lookup failed: Internal logic error");
                        }
                    }
                }
            }
            static Dictionary<string, Entry> cache = new Dictionary<string, Entry>();
            public static Entry GetCache(string key)
            {
                lock (State.GlobalSync)
                {
                    Entry result;
                    if(!cache.TryGetValue(key, out result)) return null;
                    if((DateTime.UtcNow - result.Created).Duration() > cacheduration) return null;
                    if (!result.Ready) throw new Exception("Request already pending");
                    return result;
                }
            }
            public static void RemoveEntry(Entry entry)
            {
                lock (State.GlobalSync)
                {
                    foreach (KeyValuePair<string, Entry> kvp in cache)
                    {
                        if (kvp.Value == entry)
                        {
                            cache.Remove(kvp.Key);
                            return;
                        }
                    }
                }
            }
            public static Entry CreateEntry(string key)
            {
                lock(State.GlobalSync)
                {
                    Entry result;
                    if (cache.TryGetValue(key, out result))
                    {
                        cache.Remove(key);
                    }
                    result = new Entry();
                    result.Created = DateTime.UtcNow;
                    result.Ready = false;
                    cache.Add(key, result);
                    return result;
                }
            }
        }

        class SearchRequest
        {
            Cache.Entry target;
            IrcMessage source;
            string region;
            string name;
            int bracket;

            public SearchRequest(IrcMessage source, string name, string region, int bracket)
            {
                this.bracket = bracket;
                this.region = region;
                this.name = name;
                this.source = source;
                this.target = Cache.CreateEntry((region + "!" + name).ToLower());
                new Thread(new ThreadStart(Execute)).Start();
            }

            void Execute()
            {
                try
                {
                    target.SearchName = name;
                    int total = -1;
                    for (int i = 0; i < 2; i++)
                    {
                        string type = i == 0 ? "exact" : "contains";
                        string url = "http://sc2ranks.com/api/search/" + type + "/" + region + "/" + name + ".xml" + appkey;
                        Stream stream = WebRequest.Create(url).GetResponse().GetResponseStream();
                        XmlReader reader = new XmlTextReader(stream);
                        reader.MoveToContent();
                        bool hasid = false;
                        bool hasname = false;
                        int lastid = 0;
                        string lastname = null;
                        while (reader.Read())
                        {
                            if (reader.NodeType == XmlNodeType.Element)
                            {
                                string key = reader.Name;
                                reader.Read();
                                if (reader.NodeType == XmlNodeType.Text)
                                {
                                    string value = reader.Value;
                                    switch (key)
                                    {
                                        case "error":
                                            target.Error = value;
                                            if (target.Error == "no_characters") target.Error = "No match for '" + name + "'";
                                            break;
                                        case "bnet-id":
                                            hasid = int.TryParse(value, out lastid);
                                            break;
                                        case "name":
                                            lastname = value;
                                            hasname = true;
                                            break;
                                        case "total":
                                            total = int.TryParse(value, out total) ? total : -1;
                                            break;
                                    }
                                }
                            }
                            if (hasname && hasid)
                            {
                                Cache.Item item = new Cache.Item();
                                item.ID = lastid;
                                item.Name = lastname;
                                target.Items.Add(item);
                                hasname = false;
                                hasid = false;
                            }
                            if (target.Error != null) break;
                        }
                        if (i == 0)
                        {
                            if (target.Error != null) target.Error = null;
                            else
                            {
                                target.Exact = true;
                                break;
                            }
                        }
                    }
                    if (target.Error == null && (total == -1 || total > 10))
                    {
                        target.Error = "Too many hits (" + total.ToString() + ") to display, see http://sc2ranks.com/search/contains/" + region + "/" + name + " for all hits";
                    }
                    if (target.Error == null)
                    {
                        target.Region = region;
                        foreach (Cache.Item item in target.Items)
                        {
                            Cache.Details details = new Cache.Details();
                            details.Name = item.Name;
                            details.Link = "http://sc2ranks.com/char/" + region + "/" + item.ID + "/" + item.Name;
                            string url = "http://sc2ranks.com/api/base/teams/" + region + "/" + item.Name + "!" + item.ID.ToString() + ".xml" + appkey;
                            Stream stream = WebRequest.Create(url).GetResponse().GetResponseStream();
                            XmlReader reader = new XmlTextReader(stream);
                            reader.MoveToContent();
                            Cache.Team team = new Cache.Team();
                            while (reader.Read())
                            {
                                if (reader.NodeType == XmlNodeType.Element)
                                {
                                    string key = reader.Name;
                                    reader.Read();
                                    if (reader.NodeType == XmlNodeType.Text)
                                    {
                                        string value = reader.Value;
                                        switch (key)
                                        {
                                            case "division-rank":
                                                int.TryParse(value, out team.Rank);
                                                break;
                                            case "wins":
                                                int.TryParse(value, out team.Wins);
                                                break;
                                            case "bracket":
                                                int.TryParse(value, out team.Bracket);
                                                break;
                                            case "league":
                                                team.League = value;
                                                break;
                                            case "is-random":
                                                bool.TryParse(value, out team.IsRandom);
                                                break;
                                            case "points":
                                                int.TryParse(value, out team.Points);
                                                break;
                                            case "losses":
                                                int.TryParse(value, out team.Losses);
                                                break;
                                            case "updated-at":
                                                DateTime.TryParse(value, out team.Refreshed);
                                                break;
                                        }
                                    }
                                }
                                else if (reader.NodeType == XmlNodeType.EndElement)
                                {
                                    if (reader.Name == "team")
                                    {
                                        details.Teams.Add(team);
                                        team = new Cache.Team();
                                    }
                                }
                            }
                            if (details.Teams.Count != 0)
                            {
                                details.Teams.Sort(new Comparison<Cache.Team>(CompareTeams));
                                target.Details.Add(details);
                            }
                        }
                    }
                    target.Ready = true;
                    target.Write(source, bracket);
                }
                catch (Exception ex)
                {
                    Cache.RemoveEntry(target);
                    source.ReplyChannel("Rank lookup failed: sc2ranks.com is unresponsive :(");
                    Program.Log("Rank worker thread exception: " + ex.Message);
                }
            }

            static int GetLeagueValue(string league)
            {
                switch (league.ToLower()[0])
                {
                    case 'b':
                        return 1;
                    case 's':
                        return 2;
                    case 'g':
                        return league.ToLower()[1] == 'o' ? 3 : 7;
                    case 'p':
                        return 4;
                    case 'd':
                        return 5;
                    case 'm':
                        return 6;
                }
                return -1;
            }

            static int CompareTeams(Cache.Team a, Cache.Team b)
            {
                if (a.Bracket != b.Bracket) return a.Bracket - b.Bracket;
                int la = GetLeagueValue(a.League);
                int lb = GetLeagueValue(b.League);
                if (la != lb) return lb - la;
                return b.Points - a.Points;
            }
        }

        public static void AutoRegister()
        {
            new RankCommand();
        }

        RankCommand()
        {
            Privilege = PrivilegeLevel.OnChannel;
        }

        public override string GetKeyword()
        {
            return "rank";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
			string ret = " <name> <region> [<bracket>]: Looks up SC2 rank by character name, if <bracket> not specified, 1v1 is used";           
			return ret;
        }

        public override void Execute(IrcMessage message, string args)
        {
			string[] arg = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			/* If ranks are disabled, show the options for the channel moderator */
			if (arg.Length < 2 && message.Level >= PrivilegeLevel.Operator && !SC2Ranks.IsEnabled)
			{
				throw new Exception("Usage: !rank <enabled> <true/false>: Toggles the availability of the 'rank' command");
			}
			
			/* see if we are toggling the availability of the command */
			if (arg.Length == 2)
			{
				if ((arg[0].ToLower() == "enabled" || arg[0].ToLower() == "enable") && message.Level >= PrivilegeLevel.Operator)
				{
					string toggle = arg[1].ToString().ToLower();
					bool flag = true;
					if (toggle == "false" || toggle == "0" || toggle == "off")
					{
						flag = false;
					}

					if (SC2Ranks.ApiKey == String.Empty)
					{
						SC2Ranks.IsEnabled = false;
						throw new Exception("SC2Ranks ApiKey must be set in internal configuration.");
					}

					SC2Ranks.IsEnabled = flag;
					message.ReplyPrivate("Rank lookup has been " + (SC2Ranks.IsEnabled ? "en" : "dis") + "abled");
					return;
				}
			}

			/* Regular rank command logic */
			if (Limiter.AttemptOperation(message.Level) && SC2Ranks.IsEnabled)
            {
                if (arg.Length != 2 && arg.Length != 3) throw new Exception("Usage: !rank <name> <region>");
                string region = null;
                string name = arg[1];

				/* Continue with rank lookup */
                for (int i = 0; i < 2 && region == null; i++)
                {
                    switch (arg[i].ToLower())
                    {
                        case "eu":
                        case "europe":
                            region = "eu";
                            break;
                        case "na":
                        case "us":
                        case "usa":
                        case "northamerica":
                        case "america":
                            region = "us";
                            break;
                        case "kr":
                        case "kor":
                        case "korea":
                            region = "kr";
                            break;
                        case "tw":
                        case "taiwan":
                            region = "tw";
                            break;
                        case "sea":
                        case "asia":
                            region = "sea";
                            break;
                        case "ru":
                        case "russia":
                            region = "ru";
                            break;
                        case "la":
                        case "latinamerica":
                        case "southamerica":
                            region = "la";
                            break;
                        default:
                            if (i == 0) name = arg[0];
                            else throw new Exception("Region not recognized");
                            break;
                    }
                }
                string key = region + "!" + name;
                int bracket = 1;
                if (arg.Length == 3)
                {
                    if (arg[2].ToLower() == "all")
                    {
                        bracket = 0;
                    }
                    else
                    {
                        bracket = (int)(arg[2][0] - '0');
                        if (bracket < 1 || bracket > 4) throw new Exception("Failed to parse bracket");
                    }
                }
                Cache.Entry results = Cache.GetCache(key.ToLower());
                if (results == null)
                {
                    new SearchRequest(message, name, region, bracket);
                }
                else
                {
                    results.Write(message, bracket);
                }
            }
        }
    }
}
