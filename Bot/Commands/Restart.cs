using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
namespace desBot
{
    class RestartCommand : Command
    {
        public static void AutoRegister()
        {
            new RestartCommand();
        }

        RestartCommand()
        {
            Privilege = PrivilegeLevel.Developer;
        }

        public override string GetKeyword()
        {
            return "restart";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return ": Restarts the bot";
        }

        public override void Execute(IrcMessage message, string args)
        {
            Irc.SendChannelMessage("I'll be back!", true);
            Irc.Disconnect("Restart pending");
        }
    }
}
