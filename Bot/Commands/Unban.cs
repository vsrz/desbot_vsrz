using System;
using System.Text.RegularExpressions;
namespace desBot
{
#if !JTVBOT
    /// <summary>
    /// Command that unbans a user
    /// </summary>
    class UnbanCommand : Command
    {
        public static void AutoRegister()
        {
            new UnbanCommand();
        }

        UnbanCommand()
        {
            Privilege = PrivilegeLevel.Operator;
            Alias alias = CommandHandler.AddAlias(this, "-b");
            alias.RequiresPrefix = false;
            alias.ForbidsPrefix = true;
        }

        public override string GetKeyword()
        {
            return "unban";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " <name|mask>: Unbans a user by name or hostmask";
        }

        public override void Execute(IrcMessage message, string args)
        {
            string hostmask = args.Trim();
            if(hostmask.Contains(" "))
            {
                throw new Exception("No name or hostmask was specified");
            }
            if(!hostmask.Contains("!") || !hostmask.Contains("@"))
            {
                string name = hostmask;
                HostMask mask = BanSystem.FindBanByNick(name);
#if QNETBOT
                if(mask == null)
                {
                    throw new Exception("Name '" + name + "' was not found, check spelling or specify hostmask");
                }
#elif JTVBOT
                if (mask == null) mask = new HostMask(name);
#endif
                hostmask = mask.Mask;
            }

#if JTVBOT
            BanSystem.PerformUnban(hostmask, false);
#elif QNETBOT
            BanSystem.PerformUnban(hostmask);
#endif

        }
    }
#endif
}