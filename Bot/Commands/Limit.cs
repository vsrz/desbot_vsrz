using System;
using System.Collections.Generic;
using System.Text;

namespace desBot
{
    class LimitCommand : Command
    {
        //per-user configured rate limiter for command acceptance
        public static RateLimiter UserLimiter = new RateLimiter(TimeSpan.FromSeconds(10.0), TimeSpan.FromSeconds(60.0));

        public static void AutoRegister()
        {
            new LimitCommand();
        }

        LimitCommand()
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "limit";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " <command> [<subscriber> <other>]: Limits the usage of a command to the given interval (in seconds) for subscribers and other users";
        }

        public override void Execute(IrcMessage message, string args)
        {
            string[] elem = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (elem.Length != 1 && elem.Length != 3)
            {
                throw new Exception("Expected syntaxis: " + GetKeyword() + " <command> [<subscriber> <other>]");
            }
            RateLimiter limiter = null;
            if (!CommandHandler.GetCommands().ContainsKey(elem[0]))
            {
                string special = elem[0].ToLower();
                if (special == "chat")
                {
                    limiter = Alice.limiter;
                    elem[0] = "flavor-chat";
                }
                else if (special == "joke")
                {
                    limiter = Alice.joke_limiter;
                    elem[0] = "auto-joking";
                }
                else if (special == "user")
                {
                    limiter = UserLimiter;
                    elem[0] = "command interval per user";
                }
                else throw new Exception("Command not found");
            }
            else
            {
                limiter = CommandHandler.GetCommands()[elem[0]].Limiter;
            }
            if (elem.Length == 3)
            {
                RateLimiterConfiguration config = new RateLimiterConfiguration();
                config.sub = double.Parse(elem[1]);
                config.nor = double.Parse(elem[2]);
                limiter.Configuration = config;
                message.ReplyAuto("Limit for " + elem[0] + " has been set to once every " + config.sub.ToString() + "s for subscribers and " + config.nor.ToString() + "s for other users");
            }
            else
            {
                RateLimiterConfiguration config = limiter.Configuration;
                message.ReplyAuto("Limit for " + elem[0] + " is set to once every " + config.sub.ToString() + "s for subscribers and " + config.nor.ToString() + "s for other users");
            }
        }
    }
}
