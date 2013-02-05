using System;
using System.Collections.Generic;
namespace desBot
{
#if !JTVBOT
    /* MLM: I've moved this into UserMeta, that way, it gets saved to file, so warnings don't get lost as easily
    [Serializable]
    public class SerializableWarning
    {
        public int ID;
        public string Reason;		// Reason for the warning
        public User IssuedBy;	 	// ref to User that isued the warning
        public DateTime Created;	// DateTime indicating when the Warning was created
		public User Target;			// ref to user to which the warning was commissioned
    }
    
	
	static class Warnings
	{
        public delegate void ChangedEventHandler();
        public static event ChangedEventHandler OnChanged;
        public static void RaiseChanged() { if (Warnings.OnChanged != null) Warnings.OnChanged.Invoke(); }


		public static List<SerializableWarning> WarningList = new List<SerializableWarning>();
		public static int HighestID = 0;
		public static int WarningThreshold = 3; 	// the amount of warning a user can recieve before he will be kicked from the server on new infractions.
		public static List<int> FreeUnderMax = new List<int>();
		
		// helper method to get all warnings issued to a particular user
		public static List<SerializableWarning> GetUserWarns(User user)
		{
			List<SerializableWarning> returnList = new List<SerializableWarning>(); //create a return object
			foreach(SerializableWarning warn in WarningList)
			{
				if(warn.Target == user)
				{
					returnList.Add(warn);
				}
			}	
			return returnList;
		}
	}
     * */
	
	class Warn : Command
	{
		// PrivilegeLevel for viewing any User's warnings
		PrivilegeLevel read = PrivilegeLevel.Voiced;
		// PrivilegeLevel for adding/removing warnings
		PrivilegeLevel edit = PrivilegeLevel.Operator;
		
		public static void AutoRegister()
        {
            new Warn();
        }
		
		public Warn()
		{
#if JTVBOT
            Privilege = PrivilegeLevel.Voiced;
#else
            Privilege = PrivilegeLevel.None;
#endif
            Alias alias = new Alias(this, "+w");
            alias.RequiresPrefix = false;
            alias.ForbidsPrefix = true;
		}
		
		public override string GetKeyword()
		{
			return "warn";
		}

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " ([add] <user> <reason>)|(list [<user>])|(del <user> <id>): Issues, lists or deletes warnings for a given user. If no argument is provided, defaults to list <self>.";
        }
		
		public override void Execute(IrcMessage message, string args)
		{
			if(args.Length == 0) 
			{
                //defaults to listing the warnings of the caller
                args = "list";
			}
				
			//cut off the part of the message that represents the sub-command
			int space = args.IndexOf(' ');
			string command = space == -1 ? args : args.Substring(0, space);
			string rest = space == -1 ? "" : args.Substring(space + 1);
			command = command.ToLower(); // puts subcommand to lowercase
			switch(command)
			{
                case "list":
                    {
                        //name of user to look up
                        space = rest.IndexOf(' ');
                        string lookUp = space == -1 ? rest : rest.Substring(0, space).Trim();
                        if (lookUp.Length == 0) lookUp = message.From;
                        if (lookUp == message.From)
                        {
                            //list <self> is allowed always
                        }
                        else if (CommandHandler.GetPrivilegeLevel(message.From) < read)
                        {
                            throw new Exception("Access denied");
                        }

                        //get warnings
                        User target = State.UserList.Lookup(lookUp);
                        if (target == null) throw new Exception("The target user '" + lookUp + "' was not found");
                        List<Warning> _warnlist = target.Meta.Warnings;
                        if (_warnlist.Count == 0)
                        {
                            message.ReplyPrivate("No warning entries found for '" + lookUp + "'");
                        }
                        else
                        {
                            message.ReplyPrivate("+++++ List of warnings for user: " + lookUp + " +++++");
                            foreach (Warning wrn in _warnlist)
                            {
                                message.ReplyPrivate("#" + wrn.ID + ", created at " + wrn.Created.ToString() + ": " + ControlCharacter.Color(IrcColor.Purple) + wrn.Reason + ControlCharacter.ColorRestore() + " by " + wrn.IssuedBy);
                            }
                            message.ReplyPrivate("+++++ End of list +++++");
                        }
                    }
                    break;
                default:
                case "add":
                    {
                        //if default command, re-add command to rest
                        if (command != "add") rest = command + " " + rest;

                        //check privilege
                        if (CommandHandler.GetPrivilegeLevel(message.From) < edit)
                        {
                            throw new Exception("Access denied");
                        }

                        //get current warnings
                        space = rest.IndexOf(' ');
                        string toWarn = space == -1 ? rest : rest.Substring(0, space);
                        rest = space == -1 ? "Violation of the rules" : rest.Substring(space + 1);
                        User target = State.UserList.Lookup(toWarn);
                        if (target == null) throw new Exception("The target user '" + toWarn + "' was not found");
                        List<Warning> warnings = target.Meta.Warnings;

                        //check if already warned in past 5s, also get next highest ID
                        int maxid = 0;
                        foreach (Warning tst in warnings)
                        {
                            if (maxid < tst.ID) maxid = tst.ID;
                            if (DateTime.UtcNow.Subtract(tst.Created).CompareTo(new TimeSpan(0, 0, 5)) < 0)
                            {
                                message.ReplyPrivate("This user has already been warned in the last 5 seconds, your warning was omitted.");
                                return;
                            }
                        }

                        //add new warning			
                        Warning warnNew = new Warning();
                        warnNew.ID = maxid + 1;
                        warnNew.Reason = rest;
                        warnNew.IssuedBy = message.From;
                        warnNew.Created = DateTime.UtcNow;
                        warnings.Add(warnNew);
                        State.MetaUserList.MarkChanged(target.Meta);

                        //tempban
                        if (warnings.Count >= State.WarningThreshold.Value)
                        {
                            HostMask mask = BanSystem.CreateBanMask(toWarn);
                            if (mask == null)
                            {
                                throw new Exception("Name '" + toWarn + "' not found");
                            }

                            BanSystem.PerformBan(mask.Mask, "15m", "You have been warned " + warnings.Count.ToString() + " times", "<warnings>");
                        }
                        else
                        {
                            message.ReplyChannel("A warning was issued to " + ControlCharacter.Bold() + toWarn + ControlCharacter.Bold() + " by " + message.From + " Reason: " + ControlCharacter.Bold() + warnNew.Reason);
                        }
                    }
                    break;
                case "del":
                    {
                        if (CommandHandler.GetPrivilegeLevel(message.From) < edit)
                        {
                            throw new Exception("Access denied");
                        }

                        //split arguments
                        space = rest.IndexOf(' ');
                        if(space <= 0) throw new Exception("Expected two arguments: <user> <id>");
                        string userarg = rest.Substring(0, space);
                        string idarg = rest.Substring(space + 1).Trim();
                            
                        //look up warning list for user
                        User target = State.UserList.Lookup(userarg);
                        if (target == null) throw new Exception("The target user '" + userarg + "' was not found");
                        List<Warning> warnings = target.Meta.Warnings;
                            
                        //look for warning with ID
                        int id = -1;
                        Warning warn = null;
                        if (int.TryParse(idarg, out id))
                        {
                            foreach (Warning needle in warnings)
                            {
                                if (needle.ID == id)
                                {
                                    warn = needle;
                                    break;
                                }
                            }
                        }
                        if (warn == null)
                        {
                            //not found
                            message.ReplyAuto("No warning for '" + userarg + "' with ID '" + idarg + "' was found");
                        }
                        else
                        {
                            //remove warning
                            warnings.Remove(warn);
                            message.ReplyAuto("Removed warning for '" + userarg + "' with ID '" + idarg + "'");
                            State.MetaUserList.MarkChanged(target.Meta);
                        }
                        break;
                    }
			}	
		}
	}
#endif
}