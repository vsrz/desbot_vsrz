using System;
using System.Collections.Generic;
using System.Text;

namespace desBot
{
    /// <summary>
    /// Possible states the IRC tool can be in
    /// </summary>
    public enum IrcState
    {
        /// <summary>
        /// The IRC tool has not connected yet
        /// </summary>
        None,

        /// <summary>
        /// The IRC tool is attempting to connect
        /// </summary>
        Connecting,

        /// <summary>
        /// The IRC tool is attempting to register
        /// </summary>
        Registering,

        /// <summary>
        /// The IRC tool is authing with Q
        /// </summary>
        Authing,

        /// <summary>
        /// The IRC tool is setting mode +x
        /// </summary>
        Cloaking,

        /// <summary>
        /// The IRC tool is retrieving channel permissions
        /// </summary>
        Retrieving,

        /// <summary>
        /// The IRC tool is joining the channel
        /// </summary>
        JoiningChannel,

        /// <summary>
        /// The IRC tool is ready
        /// </summary>
        Ready,

        /// <summary>
        /// The IRC tool is disconnecting
        /// </summary>
        Disconnecting,

        /// <summary>
        /// The IRC tool has disconnected
        /// </summary>
        Disconnected,

        /// <summary>
        /// The IRC tool failed to connect
        /// </summary>
        Error_ConnectFailed,

        /// <summary>
        /// The IRC tool failed to register (nickname already taken?)
        /// </summary>
        Error_RegisterFailed,

        /// <summary>
        /// The IRC tool failed to auth with Q
        /// </summary>
        Error_AuthFailed,
    }

    /// <summary>
    /// Possible Q auth levels
    /// </summary>
    public enum QAuthLevel
    {
        /// <summary>
        /// No auth level
        /// </summary>
        None,

        /// <summary>
        /// +k
        /// </summary>
        Known,

        /// <summary>
        /// +v
        /// </summary>
        Voiced,

        /// <summary>
        /// +o
        /// </summary>
        Operator,

        /// <summary>
        /// +m
        /// </summary>
        Master,

        /// <summary>
        /// +n
        /// </summary>
        Owner,
    }

    /// <summary>
    /// Latest statistics communication
    /// </summary>
    [Serializable]
    public class Stats
    {
        /// <summary>
        /// Current IRC state
        /// </summary>
        public IrcState Irc;

        /// <summary>
        /// Current Q auth level
        /// </summary>
        public QAuthLevel Qlevel;

        /// <summary>
        /// Current Q query
        /// </summary>
        public string Qquery;

        /// <summary>
        /// Current IRC nickname
        /// </summary>
        public string Nick;

        /// <summary>
        /// Current IRC channel
        /// </summary>
        public string Channel;

        /// <summary>
        /// Current virtual memory usage
        /// </summary>
        public long VirtualMemory;

        /// <summary>
        /// Current physical memory usage
        /// </summary>
        public long PhysicalMemory;

        /// <summary>
        /// Current CPU usage
        /// </summary>
        public double CPUUsage;

        /// <summary>
        /// Number of threads
        /// </summary>
        public int Threads;
    }
}
