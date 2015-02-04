using System;
using System.Collections.Generic;
namespace desBot
{
    /// <summary>
    /// Trigger instance
    /// </summary>
    class TriggerInstance : Command
    {
        string text;
        string keyword;
		bool protect;

        public TriggerInstance(string text, string keyword, bool protect) : base(true)
        {
            Privilege = PrivilegeLevel.OnChannel;
            this.text = text;
            this.keyword = keyword;
			this.protect = protect;
			CommandHandler.AddCommand(this);
        }

        public override string GetKeyword()
        {
            return keyword;
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return ": says '" + text + ControlCharacter.Restore() + "' in the channel";
        }

        public override void Execute(IrcMessage message, string args)
        {
            if(Limiter.AttemptOperation(message.Level))
            {
                message.ReplyAuto(text);
            }
        }
    }

    /// <summary>
    /// Customizable keyword trigger
    /// </summary>
    class TriggerCommand : Command
    {
        public static void AutoRegister()
        {
            new TriggerCommand();
        }

        TriggerCommand()
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "trigger";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " (add [keyword] [text])|(del [keyword]): Adds or removes a custom trigger";
        }

        public override void Execute(IrcMessage message, string args)
        {
            string[] arg = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (arg.Length < 2)
            {
                throw new Exception("Not enough parameters, expected 'trigger add <keyword> <text>' or 'trigger del <keyword>'");
            }
            if (arg[0].ToLower() == "add")
            {				
                string keyword = arg[1].ToLower();
                foreach (char c in keyword)
                {
                    if (!char.IsLetter(c)) throw new Exception("Trigger keyword may only contain letters");
                }
                string text = null;
                for (int i = 2; i < arg.Length; i++) text = (text == null) ? arg[i] : text + " " + arg[i];
                if (text == null)
                {
                    throw new Exception("Cannot add a trigger with no text");
                }
                string result = Add(keyword, text) ? "Replaced" : "Added";
                result += " trigger '" + keyword + "'";
                message.ReplyPrivate(result);
            }
            else if (arg[0].ToLower() == "del")
            {
                string keyword = arg[1].ToLower();				
                if (Remove(keyword))
                {
                    message.ReplyPrivate("Removed trigger '" + keyword + "'");
                }
                else
                {
                    message.ReplyPrivate("Trigger '" + keyword + "' could not be deleted");
                }
            }
			else if (arg[0].ToLower() == "protect")
			{
				if (message.Level == PrivilegeLevel.Developer)
				{
					if (arg.Length < 2)
					{
						throw new Exception("Not enough parameters for protect.");
					}
					else if (arg.Length == 2 )
					{
						message.ReplyPrivate(GetProtect(arg[1].ToLower()));
					}
					else
					{
						string keyword = arg[1].ToLower();
						string val = arg[2].ToString().ToLower();
						if (val.Length > 0)
						{
							bool flag = true;
							if (val == "false" || val == "0" || val == "disabled")
							{
								flag = false;
							}

							if (Protect(keyword, flag))
							{
								message.ReplyPrivate("Trigger '" + keyword + "' is " + (!flag ? "no longer " : "") + "protected ");
							}
						}
					}
					
				}
			}
        }

        /// <summary>
        /// Add or replace a trigger
        /// </summary>
        /// <param name="keyword">The keyword to trigger with</param>
        /// <param name="text">The text to respond with</param>
        /// <returns>True if replaced, false if added</returns>
        public bool Add(string keyword, string text)
        {
			foreach (Trigger needle in State.TriggerList.GetItems())
			{				
				if (needle.Keyword == keyword)
				{
					if (needle.Protect)
					{
                        Irc.SendChannelMessage("Unable to remove protected trigger " + keyword + ".", false);
						throw new Exception("Unable to remove protected trigger '" + keyword + "'");
					}
				}
			}	
			//check for existing command
            if (CommandHandler.GetCommands().ContainsKey(keyword))
            {
                Command existing = CommandHandler.GetCommands()[keyword];
                if (existing is TriggerInstance)
                {
                    //remove old trigger from command list
                    CommandHandler.GetCommands().Remove(keyword);
                    CommandHandler.RaiseChanged();
                }
                else
                {
                    //cannot remove non-trigger commands
                    throw new Exception("Cannot override existing non-trigger command '" + keyword + "'");
                }
            }

            //add new trigger command
            new TriggerInstance(text, keyword, false);
            
            //update trigger in state
            Trigger trigger = new Trigger();
            trigger.Keyword = keyword;
            trigger.Text = text;
			trigger.Protect = false;
			
            foreach (Trigger needle in State.TriggerList.GetItems())
            {
                if (needle.Keyword == keyword)
                {
                    //need to replace
                    if (needle.Text != text)
                    {
                        //remove old and add new
                        State.TriggerList.Remove(needle);
						if (needle.Protect == true) trigger.Protect = true;
                        State.TriggerList.Add(trigger);
                    }
                    return true;
                }
            }

            //new trigger
            State.TriggerList.Add(trigger);
            return false;
        }

        /// <summary>
        /// Removes a trigger
        /// </summary>
        /// <param name="keyword">The keyword of the trigger to remove</param>
		/// <param name="level">The privilege level of the user requesting the deletion</param>
        /// <returns>True if the trigger was removed, false if it didn't exist</returns>
        public bool Remove(string keyword)
        {
            //get existing command
            if (CommandHandler.GetCommands().ContainsKey(keyword))
            {
				// hacky :(
                if (CommandHandler.GetCommands()[keyword] is TriggerInstance)
                {
					foreach (Trigger needle in State.TriggerList.GetItems())
					{
						if (needle.Keyword == keyword)
						{
							if (!needle.Protect)
							{
								//remove old trigger from command list					
								CommandHandler.GetCommands().Remove(keyword);
								CommandHandler.RaiseChanged();
							}
							else
							{
								throw new Exception("Cannot remove protected trigger '" + keyword + "'");
							}
						}
					}
                }
                else
                {
                    //cannot remove non-trigger commands
                    throw new Exception("Cannot remove existing non-trigger command '" + keyword + "'");
                }
            }

            //update trigger in state
			foreach (Trigger needle in State.TriggerList.GetItems())
			{
                if (needle.Keyword == keyword && needle.Protect == false)
                {
                    //remove trigger					
                    State.TriggerList.Remove(needle);
                    return true;
                }
            }

            //trigger not found
            return false;
        }

		public bool Protect(string keyword, bool flag)
		{
			if (CommandHandler.GetCommands().ContainsKey(keyword))
			{
				if (!(CommandHandler.GetCommands()[keyword] is TriggerInstance))
				{
					throw new Exception("Cannot toggle protection on non-trigger '" + keyword + "'");
				}
			}

			foreach (Trigger needle in State.TriggerList.GetItems())
			{
				if (needle.Keyword == keyword)
				{
					needle.Protect = flag;
					return true;
				}
			}

			return false;
					
		}

		public string GetProtect(string keyword)
		{
			if (CommandHandler.GetCommands().ContainsKey(keyword))
			{
				if (!(CommandHandler.GetCommands()[keyword] is TriggerInstance))
				{
					return "'" + keyword + "' is not a valid trigger";
				}
			}

			foreach (Trigger needle in State.TriggerList.GetItems())
			{
				if (needle.Keyword == keyword)
				{
					return "'" + keyword + "' is " + (needle.Protect ? "" : "not ") + "protected";
				}
			}

			return "Invalid trigger";

		}
    }
}