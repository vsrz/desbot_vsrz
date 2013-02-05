using System;
namespace desBot
{
#if JTVBOT
    /// <summary>
    /// Command that manages slow mode
    /// </summary>
    class ClearCommand : Command
    {
        public static void AutoRegister()
        {
            new ClearCommand();
        }

        ClearCommand()
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "clear";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return ": Clears the JTV chat";
        }

        public override void Execute(IrcMessage message, string args)
        {
            JTV.Clear();
        }
    }
#endif
}