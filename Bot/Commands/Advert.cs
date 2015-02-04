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
        protected const string HelpText = "[key] [value]: Keys are add, delete, clear, list, interval, enable, and disable. Use !advert help key for more information.";
        
        // Minimum number of minutes that advertisements may be played
        protected const int MinimumAdInterval = 5;

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

        private static int LastAd = -1;
       
        private static bool SendAdToChannel(int TriggerIndex)
        {
            string text = Program.GetTriggerText(State.AdvertList[TriggerIndex]);
            if (text == "")
            {
                Delete(State.AdvertList[TriggerIndex].ToString());
                return false;
            }
            else
            {
                Irc.SendChannelMessage(text, false);
                return true;
            }
        }

        /// <summary>
        /// Plays the next ad in the ad-list and advances the counter
        /// </summary>
        public static void PlayAd()
        {
            lastrepeat = DateTime.Now; 
            Cleanup();
            int TotalAds = State.AdvertList.GetCount();
            int CurrentAd;

            // Do not run an ad if there are none
            if (TotalAds == 0)
            {
                return;
            }

            // If this is the first call, set the last ad index and play the first ad
            // also if there is only 1 ad, continue to repeat it
            if (LastAd == -1 || TotalAds == 1)
            {
                LastAd = 0;
                SendAdToChannel(LastAd);
                return;
            }
           
            CurrentAd = LastAd + 1;
            // Get the next trigger from the list, save it for later            
            for (int x = 0; x < TotalAds; x++)
            {
                if (CurrentAd + x >= TotalAds)
                {
                    CurrentAd = 0;
                } 
                CurrentAd += x;
                if (SendAdToChannel(CurrentAd))
                {
                    LastAd = CurrentAd;
                    return;
                }
                
            } 
        }

        /// <summary>
        /// Cleans up any ads that are stale
        /// </summary>
        private static void Cleanup()
        {
            try
            {

                foreach (string trigger in State.AdvertList.GetItems())
                {
                    if (!Program.TriggerExists(trigger))
                    {
                        Delete(trigger);
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log("Error in Cleanup(): " + ex.ToString());
            }
            
        }
        
        public static string DisableAds()
        {
            State.AdEnabled.Value = false;
            return "Ads have been disabled";
        }

        public static string EnableAds()
        {
            State.AdEnabled.Value = true;
            return "Ads have been enabled";
        }


        private static bool ExistsInAdvertList(string TriggerName)
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
        /// Lists ads in the list
        /// </summary>
        /// <returns></returns>
        private static string ListAds()
        {
            string msg = "Current ads are: ";
            foreach (string trigger in State.AdvertList.GetItems())
            {
                msg += trigger + " ";
            }
            return msg;
        }

        /// <summary>
        /// Remove a trigger from the ad list
        /// </summary>
        /// <param name="TriggerName"></param>
        /// <returns></returns>
        private static string Delete(string TriggerName)
        {
            try 
            {
                State.AdvertList.Remove(TriggerName);
            }
            catch (Exception ex)
            {
                Program.Log("Error in Advert.Delete(): " + ex.ToString());
            }
            
            return "Successfully removed " + TriggerName + " from the ad list.";
        }

        /// <summary>
        /// Clears all the advertisements
        /// </summary>
        /// <returns></returns>
        private static string ClearList()
        {
            DynamicList<string, string> emptyList = new DynamicList<string, string> { };
            State.AdvertList = emptyList;
            return "The ad list has been cleared.";
        }

        /// <summary>
        /// Sets the interval where an ad will play, in minutes
        /// </summary>
        /// <param name="Minutes"></param>
        /// <returns></returns>
        private static string SetInterval(string Minutes)
        {
            int min = 0;
            
            if (int.TryParse(Minutes, out min))
            {
               if (min < MinimumAdInterval && min != 0)
               {
                   return "Minutes must be a number " + MinimumAdInterval + " or greater.";
               }
            } else {
                return "Minutes must be a number " + MinimumAdInterval + " or greater.";
            }

            if (min == 0) 
            {
                State.AdInterval.Value = 0;
                return "Ads have been disabled";
            }
            State.AdInterval.Value = min;
            return "The ad interval has been set to " + min + " minutes.";
        }

        protected static DateTime lastrepeat = DateTime.MinValue;
        /// <summary>
        /// Check to see if the ad timer has expired
        /// </summary>
        public static void CheckAd()
        {
            /* Check if ads are enabled */
            if (State.AdEnabled.Value)
            {
                /* Check if the time has expired */
                if ((DateTime.UtcNow - lastrepeat).TotalMinutes >= State.AdInterval.Value)
                {
                    PlayAd();
                    
                }
            }
        }
        /// <summary>
        /// Adds a trigger to the advertising list
        /// </summary>
        /// <param name="TriggerName"></param>
        /// <returns></returns>
        private static string Add(string TriggerName)
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

        protected static string AddHelp()
        {
            return "Adds a trigger to the ad list. Usage: !" + AdvertCommand.Keyword + " add [trigger]";
        }

        private static string DeleteHelp()
        {
            return "Deletes a trigger from the ad list. Usage: !" + AdvertCommand.Keyword + " delete [trigger]";
        }

        private static string ClearHelp()
        {
            return "Clears all of the advertisements. Usage: !" + AdvertCommand.Keyword + " clear";
        }

        private static string IntervalHelp()
        {
            return "Sets the interval that an ad is displayed or 0 to disable. Usage: !" + AdvertCommand.Keyword + " interval [minutes]";
        }

        private static string ListHelp()
        {
            return "Lists the triggers in the current ad list. Usage: !" + AdvertCommand.Keyword + " list";
        }

        private static string EnableHelp()
        {
            return "Enable or disable ads entirely. Usage: !" + AdvertCommand.Keyword + " enable or !" + AdvertCommand.Keyword + " disable";
        }

        public override void Execute(IrcMessage message, string args)
        {
            // We don't care about case in this command, so lower everything
            args = args.ToLower();

            string[] words = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string response = "Usage: !" + AdvertCommand.Keyword + " " + HelpText;
            
            if (words.Length > 0) 
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
                                    response = DeleteHelp();
                                    break;
                                case "clear":
                                case "deleteall":
                                case "purge":
                                    response = ClearHelp();
                                    break;
                                case "interval":
                                case "timer":
                                    response = IntervalHelp();
                                    break;
                                case "list":
                                    response = ListHelp();
                                    break;
                                case "disable":
                                case "enable":
                                    response = EnableHelp();
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
                        if (words.Length > 1)
                        {
                            response = Delete(words[1]);
                        }
                        else
                        {
                            response = DeleteHelp();
                        }
                        break;
                    case "clear":
                    case "deleteall":
                    case "purge":
                        // Remove all triggers
                       response = ClearList();
                        break;
                    case "interval":
                    case "timer":
                        // Set or review the cooldown timer
                        if (words.Length > 1)
                        {
                            response = SetInterval(words[1]);
                        }
                        else
                        {
                            response = IntervalHelp();
                        }
                        
                        break;
                    case "enabled":
                    case "on":
                    case "start":
                    case "enable":
                        response = EnableAds();
                        break;
                    case "disable":
                    case "disabled":
                    case "off":
                    case "stop":
                        response = DisableAds();
                        break;
                    case "list":
                    case "show":
                        response = ListAds();
                        break;
                    case "call":
                        PlayAd();
                        response = "";
                        break;
                    default:
                        break;

                }
            }
            if (response != "")
            {
                message.ReplyAuto(response);
            }
        }
        
    
    }

       
}