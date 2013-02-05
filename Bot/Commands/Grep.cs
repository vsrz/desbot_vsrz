using System;
using System.Collections.Generic;
namespace desBot
{
    /// <summary>
    /// Command that manages slow mode
    /// </summary>
    class GrepCommand : Command
    {
        const int Limit = 3;

        public static void AutoRegister()
        {
            new GrepCommand();
        }

        GrepCommand()
        {
            Privilege = PrivilegeLevel.Developer;
        }

        public override string GetKeyword()
        {
            return "grep";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " <pattern>: Performs a regular expression match of the log, returns last " + Limit + " matching entries";
        }

        public override void Execute(IrcMessage message, string args)
        {
            IEnumerable<string> results = Program.GrepLog(args);

            IEnumerator<string> it = results.GetEnumerator();
            int limit = Limit;
            bool first = true;
            while (it.MoveNext())
            {
                //ignore the logged message of this query
                if (first && it.Current.EndsWith(GetKeyword() + " " + args))
                {
                    first = false;
                    continue;
                }
                first = false;

                //return result
                message.ReplyAuto(it.Current);
                if (--limit == 0) break;
            }
            if (limit == Limit)
            {
                message.ReplyAuto("No matches");
            }
        }
    }
}