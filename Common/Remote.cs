using System;
using System.Collections.Generic;

namespace desBot
{
    /// <summary>
    /// A command sent by a remote GUI
    /// </summary>
    [Serializable]
    public class RemoteCommand
    {
        public int ID;
        public string Command;
    }

    /// <summary>
    /// A response sent to a remote GUI
    /// </summary>
    [Serializable]
    public class RemoteCommandResponse
    {
        public int ID;
    }

    /// <summary>
    /// An entry for a remote log
    /// </summary>
    [Serializable]
    public class RemoteLogEntry
    {
        public string Entry;
    }
}
