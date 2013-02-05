using System;
using System.Net;
using System.Security.Cryptography;
namespace desBot
{
    class TokenCommand : Command
    {
        public static void AutoRegister()
        {
            new TokenCommand();
        }

        TokenCommand()
        {
            Privilege = PrivilegeLevel.Developer;
        }

        public override string GetKeyword()
        {
            return "token";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " (add <IP>)|(get <IP>)|(del <IP>): Adds, gets or deletes an access token for the given IP";
        }

        public override void Execute(IrcMessage message, string args)
        {
            string[] arg = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (arg.Length != 2) throw new Exception("Expected two arguments, got " + arg.Length.ToString());
            IPAddress ip = null;
            if (!IPAddress.TryParse(arg[1], out ip)) throw new Exception("Failed to parse '" + arg[1] + "' as an IP address");
            Account acct = State.AccountList.Lookup(ip);
            switch (arg[0].ToLower())
            {
                case "add":
                    if (acct != null) throw new Exception("An account already exists for IP " + ip.ToString());
                    acct = Account.Generate(ip);
                    string result = acct.GetToken().ToXML();
                    State.AccountList.Add(acct);
                    message.ReplyPrivate(result);
                    break;
                case "get":
                    if (acct == null) throw new Exception("No account exists for IP " + ip.ToString());
                    string reply = acct.GetToken().ToXML();
                    message.ReplyPrivate(reply);
                    break;
                case "del":
                    if (acct == null) throw new Exception("No account exists for IP " + ip.ToString());
                    State.AccountList.Remove(acct);
                    message.ReplyPrivate("The account for IP " + ip.ToString() + " was removed");
                    break;
            }
        }
    }
}