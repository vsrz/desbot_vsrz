using System;
namespace desBot
{
    /// <summary>
    /// Command that repeats text in channel
    /// </summary>
    class SayCommand : Command
    {
        public static void AutoRegister()
        {
            new SayCommand();
        }

        SayCommand()
        {
            Privilege = PrivilegeLevel.Developer;
        }

        public override string GetKeyword()
        {
            return "say";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " <text>: Makes the bot say <text> on the channel";
        }

        public override void Execute(IrcMessage message, string args)
        {
            message.ReplyChannel(args);
        }
    }
}