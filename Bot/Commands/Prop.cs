using System;
using System.Collections.Generic;
using System.Text;

namespace desBot
{
    class PropCommand : Command
    {
        public static void AutoRegister()
        {
            new PropCommand();
        }

        PropCommand()
        {
            Privilege = PrivilegeLevel.Developer;
        }

        public override string GetKeyword()
        {
            return "prop";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " <name> [<val>]: Set property <name> to <val>. If <val> not specified, return current property value.";
        }
    
        public override void Execute(IrcMessage message, string args)
        {
            int space = args.IndexOf(' ');
            if (space == -1) space = args.Length;
            string name = args.Substring(0, space);
            string val = args.Substring(space).Trim();
            if (name.Length == 0) throw new Exception("No property name specified");
            switch (name.ToLower())
            {
                case "sd":
                    if (val.Length != 0)
                    {
                        int ms = int.Parse(val);
                        if(ms <= 0) throw new Exception("Invalid value for property");
                        State.SendDelay.Value = ms;
                    }
                    message.ReplyPrivate("State.SendDelay == " + State.SendDelay.Value.ToString());
                    break;
                case "pc":
                    if(val.Length != 0) State.ParseChannel.Value = bool.Parse(val);
                    message.ReplyPrivate("State.ParseChannel == " + State.ParseChannel.Value.ToString());
                    break;
                case "wt":
                    if (val.Length != 0)
                    {
                        int wt = int.Parse(val);
                        if(wt <= 1) throw new Exception("Invalid value for property");
                        State.WarningThreshold.Value = wt;
                    }
                    message.ReplyPrivate("State.WarningThreshold == " + State.WarningThreshold.Value.ToString());
                    break;
#if QNETBOT
                case "cc":
                    if (val.Length != 0) State.ControlCharacters.Value = bool.Parse(val);
                    message.ReplyPrivate("State.ControlCharacters == " + State.ControlCharacters.Value.ToString());
                    break;
                case "qe":
                    if (val.Length != 0) State.UseQEnforce.Value = bool.Parse(val);
                    message.ReplyPrivate("State.UseQEnforce == " + State.UseQEnforce.Value.ToString());
                    break;
                case "qb":
                    if (val.Length != 0) State.UseQuietBan.Value = bool.Parse(val);
                    message.ReplyPrivate("State.UseQuietBan == " + State.UseQuietBan.Value.ToString());
                    break;
#endif
                default:
                    throw new Exception("Unknown property name: " + name);
            }
        }
    }
}
