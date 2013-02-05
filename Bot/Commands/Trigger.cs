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

        public TriggerInstance(string text, string keyword) : base(true)
        {
            Privilege = PrivilegeLevel.OnChannel;
            this.text = text;
            this.keyword = keyword;
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
            return " (add <keyword> <text>)|(del <keyword>): Adds or removes a custom trigger";
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
                    message.ReplyPrivate("Trigger '" + keyword + "' does not exist");
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
            new TriggerInstance(text, keyword);
            
            //update trigger in state
            Trigger trigger = new Trigger();
            trigger.Keyword = keyword;
            trigger.Text = text;
            foreach (Trigger needle in State.TriggerList.GetItems())
            {
                if (needle.Keyword == keyword)
                {
                    //need to replace
                    if (needle.Text != text)
                    {
                        //remove old and add new
                        State.TriggerList.Remove(needle);
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
        /// <returns>True if the trigger was removed, false if it didn't exist</returns>
        public bool Remove(string keyword)
        {
            //get existing command
            if (CommandHandler.GetCommands().ContainsKey(keyword))
            {
                if (CommandHandler.GetCommands()[keyword] is TriggerInstance)
                {
                    //remove old trigger from command list
                    CommandHandler.GetCommands().Remove(keyword);
                    CommandHandler.RaiseChanged();
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
                if (needle.Keyword == keyword)
                {
                    //remove trigger
                    State.TriggerList.Remove(needle);
                    return true;
                }
            }

            //trigger not found
            return false;
        }
    }
}