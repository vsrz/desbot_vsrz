using System;
using System.Text.RegularExpressions;
namespace desBot
{
#if !JTVBOT 
    /// <summary>
    /// Command that bans a user
    /// </summary>
    class BanCommand : Command
    {
        public static void AutoRegister()
        {
            new BanCommand();
        }

        BanCommand()
        {
            Privilege = PrivilegeLevel.Operator;
            Alias alias = CommandHandler.AddAlias(this, "+b");
            alias.RequiresPrefix = false;
            alias.ForbidsPrefix = true;
        }

        public override string GetKeyword()
        {
            return "ban";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " <name|mask> [<duration>] [<reason>]: Bans a user by name or hostmask, for the given duration (or 10 minutes, if not specified)";
        }

        public override void Execute(IrcMessage message, string args)
        {
            string[] arg = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (arg.Length == 0)
            {
                throw new Exception("No name or hostmask was specified");
            }
            string name = arg[0];
            string hostmask = null;
            if(name.Contains("!") && name.Contains("@"))
            {
                hostmask = name;
            }
            else
            {
                if (CommandHandler.GetPrivilegeLevel(name) >= PrivilegeLevel.Operator)
                {
                    throw new Exception("Unable to ban moderator/operator");
                }
                HostMask mask = BanSystem.CreateBanMask(name);
                if (mask == null)
                {
                    throw new Exception("Name '" + name + "' not found");
                }
                hostmask = mask.Mask;
            }
            string duration = "10m";
            int next = 1;
            if (arg.Length >= 2)
            {
                string maybe_duration = arg[1];
                if (maybe_duration.StartsWith("perm"))
                {
                    duration = null;
                    next = 2;
                }
                else
                {
                    Regex duration_regex = new Regex("^(?:[0-9]+[mhdwMy])+$");
                    int minutes;
                    if (duration_regex.Match(maybe_duration).Success)
                    {
                        duration = maybe_duration;
                        next = 2;
                    }
                    else if (int.TryParse(maybe_duration, out minutes) && minutes > 0)
                    {
                        duration = minutes.ToString() + "m";
                        next = 2;
                    }
                }
            }
            string reason = "";
            for (int i = next; i < arg.Length; ++i) reason += arg[i] + " ";
            if (reason == "") reason = duration == null ? "Banned permanently" : "Banned for " + duration;
            else reason += (duration == null ? " (permanently)" : (" (for " + duration + ")"));
            reason = reason.Trim() + " by " + message.From;

            BanSystem.PerformBan(hostmask, duration, reason, message.From);
        }
    }
#endif
}