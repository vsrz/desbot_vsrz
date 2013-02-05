using System;
using System.Collections.Generic;
using System.Text;

namespace desBot
{
    /// <summary>
    /// Describes privilege levels for commands
    /// </summary>
    public enum PrivilegeLevel
    {
        /// <summary>
        /// Matches anyone connected to IRC
        /// </summary>
        None,

        /// <summary>
        /// Matches anyone on the channel
        /// </summary>
        OnChannel,

        /// <summary>
        /// Matches anyone with voice on the channel (+)
        /// </summary>
        Voiced,

        /// <summary>
        /// Matches anyone that is subscribed to the channel (JTV only)
        /// </summary>
        Subscriber,

        /// <summary>
        /// Matches anyone with operator on the channel (@)
        /// </summary>
        Operator,

        /// <summary>
        /// Matches developers of the bot
        /// </summary>
        Developer,

        /// <summary>
        /// Matches commands from the console tab of the UI
        /// </summary>
        Console,

        /// <summary>
        /// Matches no-one
        /// </summary>
        Invalid,
    }

    /// <summary>
    /// Rate limiter configuration, suitable for serialization
    /// </summary>
    public class RateLimiterConfiguration : IKeyInsideValue<string>
    {
        public string key;
        public double sub, nor;
        public string GetKey() { return key; }
        public bool MatchKey(RateLimiterConfiguration other) { return other.key == key; }
    }

    /// <summary>
    /// Simple time-based rate limiter for commands
    /// </summary>
    public class RateLimiter
    {
        DateTime last = DateTime.MinValue;
        TimeSpan subscriber, normal;

        /// <summary>
        /// Constructs the rate-limiter with the given timespan configuration
        /// </summary>
        /// <param name="subscriber">The interval between accepted operations for subscribers</param>
        /// <param name="normal">The interval between accepted operations for normal users</param>
        public RateLimiter(TimeSpan subscriber, TimeSpan normal)
        {
            SetIntervals(subscriber, normal);
        }

        /// <summary>
        /// Sets the intervals
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="normal"></param>
        public void SetIntervals(TimeSpan subscriber, TimeSpan normal)
        {
            this.subscriber = subscriber;
            this.normal = normal;
        }

        /// <summary>
        /// Resets the limiter, such that the next operation will always succeed
        /// </summary>
        public void Reset()
        {
            last = DateTime.MinValue;
        }

        /// <summary>
        /// Touches the limiter, such that the operation is considered executed
        /// </summary>
        public void Touch()
        {
            AttemptOperation(PrivilegeLevel.Console);
        }

        /// <summary>
        /// Attempts the operation with the given privilegelevel. Operators attempting an operation will always succeed
        /// </summary>
        /// <param name="level">The level to attempt the operation at</param>
        /// <returns>True if the operation should be executed, False if not enough time has elapsed</returns>
        public bool AttemptOperation(PrivilegeLevel level)
        {
            bool result = false;
            if (level >= PrivilegeLevel.Operator) result = true;
            else
            {
                TimeSpan elapsed = DateTime.UtcNow - last;
                result = (level >= PrivilegeLevel.Subscriber && elapsed >= subscriber) || (elapsed > normal);
            }
            if (result) last = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// Get or set the configuration of the rate limiter
        /// </summary>
        public RateLimiterConfiguration Configuration
        {
            get
            {
                RateLimiterConfiguration config = new RateLimiterConfiguration();
                config.sub = subscriber.TotalSeconds;
                config.nor = normal.TotalSeconds;
                return config;
            }
            set
            {
                subscriber = TimeSpan.FromSeconds(value.sub);
                normal = TimeSpan.FromSeconds(value.nor);
            }
        }
    }
}
