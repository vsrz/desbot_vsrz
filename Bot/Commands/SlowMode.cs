using System;
namespace desBot
{
#if JTVBOT
    /// <summary>
    /// Command that manages slow mode
    /// </summary>
    class SlowModeCommand : Command
    {
        public static void AutoRegister()
        {
            new SlowModeCommand();
        }

        SlowModeCommand()
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "slowmode";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " <on|off>: Enables or disables slow mode";
        }

        public override void Execute(IrcMessage message, string args)
        {
            if (args.ToLower() == "on")
            {
                JTV.Slowmode(true);
            }
            else if (args.ToLower() == "off")
            {
                JTV.Slowmode(false);
            }
            else
            {
                message.ReplyAuto("Did you mean: '!slowmode on' or '!slowmode off'");
            }
        }
    }
#endif
}