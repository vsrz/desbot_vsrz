using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace desBot
{
    /// <summary>
    /// BotLimiter limits the bot in a way that prevents it from being G-lined
    /// </summary>
    static class BotLimiter
    {
        // Holds the chat count per tick. This should never exceed the interval
        private static Queue<int> Totals;

        // Maximum number of messages per interval
        public const int MAX_MESSAGES_PER_INTERVAL = 15;

        // Interval, in seconds
        public const int INTERVAL = 30;

        // Last Tick
        private static DateTime LastTick;

        private static bool firstRun = false;

        private static void init()
        {
            if (firstRun == false)
            {
                Totals = new Queue<int>();
                LastTick = DateTime.UtcNow;
                firstRun = true;
            }

        }

        // Pops the front of the queue so that there are only 
        // INTERVAL items in the queue
        public static void Tick()
        {
            if (!firstRun) init();
        
            if (DateTime.UtcNow > LastTick.AddSeconds(1))
            {
                LastTick = DateTime.UtcNow;
                // Only pop the queue if we've reached the interval
                if (Totals.Count() > INTERVAL)
                {
                    Totals.Dequeue();
                }

                Totals.Enqueue(0);

            }

        }

        public static void AddMessage(int count = 1)
        {
            int last = Totals.Last();
            last += count;
            Totals.Enqueue(last);
        }

        public static int GetMessageCount()
        {
            if (!firstRun) init();

            if (Totals.Count() == 0) return 0;
            int[] list = new int[Totals.Count()];
            int cnt = 0;
            Totals.CopyTo(list, 0);
            for (int i = list.Length - 1; i >= 0; i--)
            {
                cnt += list[i];
            }
            return cnt;
        }

        public static bool CanSendMessage()
        {
        
            if (GetMessageCount() >= MAX_MESSAGES_PER_INTERVAL)
            {
                return false;
            }

            return true;

        }


    }
}
