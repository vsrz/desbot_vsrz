using System;
using System.Net;
using System.Globalization;
using System.Collections.Generic;
namespace desBot
{
    class AdvertCommand : Command
    {
        // Keyword to call this command
        protected const string Keyword = "advert";
        protected const string HelpText = "[key] [value]: Keys are add, delete, clear, list, and interval. Use key by itself for help.";
        
        // Minimum number of minutes that advertisements may be played
        protected const int MinimumAdInterval = 3;

        public static void AutoRegister()
        {
            new AdvertCommand();
        }
        
        AdvertCommand()
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return AdvertCommand.Keyword;
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " " + AdvertCommand.HelpText;
        }
        
        /// <summary>
        /// Plays the next ad in the ad-list and advances the counter
        /// </summary>
        public static void PlayAd()
        {
            // Get the next trigger from the list, save it for later
            // Check that the trigger exists
            // If it does not exist, advance to the next trigger
            // If the next trigger is the original trigger, abort and turn off ads
            // Otherwise, play the trigger
            // Reset the timer
            throw new NotImplementedException();
        }
        
        private bool ExistsInAdvertList(string TriggerName)
        {
           // Can't use anything fancy here since we need to support .net 2.0
            foreach (string trigger in State.AdvertList.GetItems())
            {
                if (trigger == TriggerName)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Adds a trigger to the advertising list
        /// </summary>
        /// <param name="TriggerName"></param>
        /// <returns></returns>
        private string Add(string TriggerName)
        {

            if (!Program.TriggerExists(TriggerName))
            {
                return "Could not find a trigger '" + TriggerName + "'";
            }

            if (ExistsInAdvertList(TriggerName))
            {
                return "The trigger '" + TriggerName + "' already exists in the ad list.";
            }

            State.AdvertList.Add(TriggerName);
            return "Successfully added " + TriggerName + " to the ad list";
         
        }

        protected string AddHelp()
        {
            return "Usage: !" + AdvertCommand.Keyword + " add <trigger>";
        }

        public override void Execute(IrcMessage message, string args)
        {
            // We don't care about case in this command, so lower everything
            args = args.ToLower();

            string[] words = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string response = "Usage: !" + AdvertCommand.Keyword + " " + HelpText;
            
            if (words.Length > 1) 
            {
                // Determine which command was called and call the appropriate method
                switch (words[0])
                {
                    case "help":
                        if (words.Length > 1) {
                            switch (words[1])
                            {
                                case "add":
                                    response = AddHelp();
                                    break;
                                case "remove":
                                case "delete":
                                case "del":
                                case "rem":
                                    throw new NotImplementedException();
                                    break;
                                case "clear":
                                case "deleteall":
                                case "purge":
                                    throw new NotImplementedException();
                                    break;
                                case "interval":
                                case "timer":
                                    throw new NotImplementedException();
                                    break;
                                default:
                                    // The default help text is returned in this case
                                    break;
                            }
                        }
                        break;
                    case "add":
                        // Add a trigger, as long as they specified one
                        if (words.Length > 1)
                        {
                            response = Add(words[1]);
                        }
                        else
                        {
                            response = AddHelp();
                        }
                        break;
                    case "remove":
                    case "delete":
                    case "del":
                    case "rem":
                        // Remove or delete a trigger
                        break;
                    case "clear":
                    case "deleteall":
                    case "purge":
                        // Remove all triggers
                        break;
                    case "interval":
                    case "timer":
                        // Set or review the cooldown timer
                        break;
                    default:
                        break;

                }
            }
            message.ReplyAuto(response);
            
        }
        
    
    }

       
}