using System;
using System.Threading;
namespace desBot
{
    /// <summary>
    /// Terminates the bot
    /// </summary>
    class TerminateCommand : Command
    {
        public static void AutoRegister()
        {
            new TerminateCommand();
        }

        TerminateCommand()
        {
            Privilege = PrivilegeLevel.Developer;
        }

        public override string GetKeyword()
        {
            return "terminate";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return ": Terminates the bot";
        }

        public override void Execute(IrcMessage message, string args)
        {
            Irc.SendChannelMessage("Going offline :(", true);
            Program.Terminate();
        }
    }
}