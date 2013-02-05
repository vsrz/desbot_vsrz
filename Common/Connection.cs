using System;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
namespace desBot
{
    /// <summary>
    /// Possible connection states
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        /// Connecting to remote host
        /// </summary>
        Connecting,

        /// <summary>
        /// Authenticating and handshaking
        /// </summary>
        Authing,

        /// <summary>
        /// Ready for communication
        /// </summary>
        Ready,

        /// <summary>
        /// Disconnected
        /// </summary>
        Disconnected,

        /// <summary>
        /// Error during connection
        /// </summary>
        Error,
    }

    /// <summary>
    /// Utility class that performs encrypted object transfer between the bot and a GUI
    /// </summary>
    public class Connection
    {
        TcpClient client;
        NetworkStream stream;
        Exception error;
        BinaryFormatter serializer = new BinaryFormatter();
        Listener source;
        Listener.LogDelegate log;
        RijndaelManaged rijndael;
        Thread worker;
#if TEST_STREAM
        int encryptorpos = 0;
        int decryptorpos = 0;
#endif

        /// <summary>
        /// Symmetric key transfer utility
        /// This is always sent encrypted using RSA
        /// </summary>
        [Serializable]
        struct Auth
        {
            public string Version;
            public byte[] Key;
            public byte[] IV;
        }

        /// <summary>
        /// Object receive event handler
        /// </summary>
        /// <param name="source">The connection that raised the event</param>
        /// <param name="obj">The object that was received</param>
        public delegate void OnReceiveEventHandler(Connection source, object obj);

        /// <summary>
        /// Disconnection event handler
        /// </summary>
        /// <param name="source">The connection that raised the event</param>
        public delegate void OnDisconnectEventHandler(Connection source);
        
        /// <summary>
        /// Triggers when an object is received
        /// </summary>
        public event OnReceiveEventHandler OnReceive;

        /// <summary>
        /// Triggers when the connection gets disconnected
        /// </summary>
        public event OnDisconnectEventHandler OnDisconnect;
        
        /// <summary>
        /// The current connection state
        /// </summary>
        public ConnectionState State { get; private set; }
        
        /// <summary>
        /// Creates a connection form the bot to a GUI
        /// </summary>
        /// <param name="source">The listener on the bot side</param>
        /// <param name="client">The TCP connection to the GUI</param>
        internal Connection(Listener source, TcpClient client)
        {
            State = ConnectionState.Authing;
            this.client = client;
            this.source = source;
            worker = new Thread(new ThreadStart(client_Listen));
            worker.Start();
        }

        /// <summary>
        /// Creates a connection from a GUI to the bot
        /// </summary>
        /// <param name="token">The token that specifies how to connect to the bot</param>
        public Connection(Listener.LogDelegate log, Token token)
        {
            this.log = log;
            State = ConnectionState.Connecting;
            worker = new Thread(new ParameterizedThreadStart(client_Connect));
            worker.Start(token);
        }

        /// <summary>
        /// Add item to log
        /// </summary>
        /// <param name="text">Entry to add</param>
        public void Log(string text)
        {
            if (log != null) log(text);
            else source.Log(text);
        }

        /// <summary>
        /// Stop the connection, cleaning up all resources
        /// </summary>
        public void Stop()
        {
            lock (this)
            {
                if (State >= ConnectionState.Disconnected) return;
                State = ConnectionState.Disconnected;
            }
            Log("Stopping connection with GUI");
            if (stream != null)
            {
                Log("Closing network stream");
                stream.Close();
                stream = null;
            }
            Socket s = null;
            if (client != null)
            {
                Log("Closing TCP client");
                s = client.Client;
                client.Close();
                client = null;
            }
            if (OnDisconnect != null)
            {
                Log("Invoking disconnect event handlers");
                OnDisconnect.Invoke(this);
            }
            if (s != null)
            {
                try
                {
                    Log("Shutting down socket");
                    s.Shutdown(SocketShutdown.Both);
                    Log("Closing socket");
                    s.Close(1);
                }
                catch (Exception ex)
                {
                    Log("Exception: " + ex.Message);
                }
            }
            if (worker != null)
            {
                Log("Aborting worker thread");
                worker.Abort();
                worker = null;
            }
            Log("Stop sequence completed");
        }

        /// <summary>
        /// Send an object over the connection
        /// Requires State == ConnectionState.Ready
        /// </summary>
        /// <param name="obj">The object to send</param>
        public void Send(object obj)
        {
            if (State != ConnectionState.Ready) throw new Exception("Connection not ready");
            Send(obj, null);
        }

        /// <summary>
        /// Get a description of the error that caused the connection to disconnect
        /// </summary>
        /// <returns>A string describing the error</returns>
        public string GetError()
        {
            lock (this)
            {
                if (error == null) return "<no error>";
                else return error.Message;
            }
        }

        /// <summary>
        /// Send an object
        /// </summary>
        /// <param name="obj">The object to send</param>
        /// <param name="rsa">The RSA service to encrypt with, or null, if symmetric key is to be used</param>
        void Send(object obj, RSACryptoServiceProvider rsa)
        {
            lock (this)
            {
                if (!stream.CanWrite || !client.Connected) throw new Exception("Cannot write to connection");
#if TEST_STREAM
                int packet = 0;
                int hash = 0;
                int pos = encryptorpos;
                string crypto = "<unknown>";
                try {
#endif
                byte[] result;
                if (rsa == null)
                {
                    if (State == ConnectionState.Ready)
                    {
#if TEST_STREAM
                        //test locally
                        MemoryStream tstream = new MemoryStream();
                        serializer.Serialize(tstream, obj);
                        byte[] test = tstream.ToArray();
                        MemoryStream dstream = new MemoryStream(test);
                        object same = serializer.Deserialize(dstream);
                        packet = test.Length;
                        for (int i = 0; i < packet; i++) hash += test[i];
#endif

                        //encrypt using symmetric key
                        MemoryStream buffer = new MemoryStream();
                        CryptoStream cstream = new CryptoStream(buffer, rijndael.CreateEncryptor(), CryptoStreamMode.Write);

#if TEST_STREAM
                        crypto = "symmetric";
                        cstream.Write(test, 0, packet);
                        encryptorpos += packet;
#else
                        serializer.Serialize(cstream, obj);
#endif
                        cstream.FlushFinalBlock();
                        cstream.Close();
                        result = buffer.ToArray();

#if TEST_STREAM
                        //test decode
                        tstream = new MemoryStream(result);
                        cstream = new CryptoStream(tstream, rijndael.CreateDecryptor(), CryptoStreamMode.Read);
                        byte[] decr = new byte[packet];
                        cstream.Read(decr, 0, packet);
                        tstream = new MemoryStream(decr);
                        object res = serializer.Deserialize(tstream);
                        for (int i = 0; i < packet; i++) if (test[i] != decr[i]) throw new Exception("Not identical");
                        tstream = null;
#endif
                    }
                    else
                    {
                        //do not encrypt (localhost)
#if TEST_STREAM
                        crypto = "unencrypted";
#endif
                        MemoryStream buffer = new MemoryStream();
                        serializer.Serialize(buffer, obj);
                        result = buffer.ToArray();
                    }
                }
                else
                {
                    //encrypt using RSA (key exchange)
#if TEST_STREAM
                    crypto = "assymmetric";
#endif
                    MemoryStream buffer = new MemoryStream();
                    serializer.Serialize(buffer, obj);
                    result = rsa.Encrypt(buffer.ToArray(), true);
                }

                //error
                if (result.Length <= 0) throw new Exception("Serialization/encryption yielded no data");

                //write to stream
                stream.Write(BitConverter.GetBytes(result.Length), 0, 4);
#if TEST_STREAM
                stream.Write(BitConverter.GetBytes(packet), 0, 4);
                stream.Write(BitConverter.GetBytes(hash), 0, 4);
                stream.Write(BitConverter.GetBytes(pos), 0, 4);
                Log("Sending packet (" + crypto + "): length = " + result.Length.ToString() + ", packet = " + packet.ToString() + ", hash = " + hash.ToString() + ", pos = " + pos.ToString() + "->" + encryptorpos.ToString());
#endif
                stream.Write(result, 0, result.Length);
#if TEST_STREAM
                } 
                catch(Exception ex)
                {
                    Log("Exception during send: " + ex.Message);
                    throw ex;
                }
#endif
            }
        }

        /// <summary>
        /// Reads from the network stream, blocking until data is available
        /// </summary>
        /// <param name="buffer">The buffer to fill</param>
        /// <param name="offset">The offset of the buffer to start reading data into</param>
        /// <param name="length">The number of bytes to read</param>
        void ReadStream(Stream stream, byte[] buffer, int offset, int length)
        {
            while (length != 0)
            {
                if (!stream.CanRead || !client.Connected) throw new Exception("Stream not readable");
                int read = stream.Read(buffer, offset, length);
                offset += read;
                length -= read;
            }
        }

        /// <summary>
        /// Receive an object (blocking)
        /// </summary>
        /// <param name="rsa">The RSA service to decrypt data with, or null, if symmetric key is to be used</param>
        /// <returns>The object that was received</returns>
        object Receive(RSACryptoServiceProvider rsa, BinaryFormatter serializer)
        {
#if TEST_STREAM
            try
            {
#endif
                //read length of packet from stream
                byte[] data = new byte[4];
                ReadStream(stream, data, 0, 4);
                int length = BitConverter.ToInt32(data, 0);

#if TEST_STREAM
                string crypto = (rsa == null ? (State == ConnectionState.Ready ? "symmetric" : "unencrypted") : "assymmetric");
                ReadStream(stream, data, 0, 4);
                int packet = BitConverter.ToInt32(data, 0);
                ReadStream(stream, data, 0, 4);
                int hash = BitConverter.ToInt32(data, 0);
                ReadStream(stream, data, 0, 4);
                int pos = BitConverter.ToInt32(data, 0);
                Log("Receiving packet (" + crypto + "): length = " + length.ToString() + ", packet = " + packet.ToString() + ", hash = " + hash.ToString() + ", pos = " + pos.ToString());
                if (crypto == "symmetric" && pos != decryptorpos) throw new Exception("Bad cryptography state");
#endif

                //read packet from stream
                data = new byte[length];
                ReadStream(stream, data, 0, length);

                if (rsa == null)
                {
                    if (State == ConnectionState.Ready)
                    {
                        //decrypt packet using symmetric key
                        MemoryStream buffer = new MemoryStream(data);
                        Stream cstream = new CryptoStream(buffer, rijndael.CreateDecryptor(), CryptoStreamMode.Read);

#if TEST_STREAM
                        //fetch data
                        byte[] test = new byte[packet];
                        ReadStream(cstream, test, 0, packet);
                        decryptorpos += packet;
                        cstream.Close();
                        for (int i = 0; i < packet; i++) hash -= test[i];
                        if (hash != 0) throw new Exception("Data damaged: Hash remainder of " + hash.ToString());
                        cstream = new MemoryStream(test);
#endif

                        //deserialize
                        object result = serializer.Deserialize(cstream);
                        cstream.Close();
                        return result;
                    }
                    else
                    {
                        //not encrypted, localhost
                        MemoryStream buffer = new MemoryStream(data);
                        return serializer.Deserialize(buffer);
                    }
                }
                else
                {
                    //decrypt packet using RSA (key exchange)
                    MemoryStream buffer = new MemoryStream(rsa.Decrypt(data, true));

                    //deserialize
                    return serializer.Deserialize(buffer);
                }
#if TEST_STREAM
            }
            catch (Exception ex)
            {
                Log("Exception during receive: " + ex.Message);
                throw ex;
            }
#endif
        }

        static string ByteArrayToString(byte[] arr)
        {
            string result = "[" + arr.Length.ToString() + "|";
            foreach(byte b in arr)
            {
                result += b.ToString("x2");
            }
            result += "]";
            return result;
        }

        /// <summary>
        /// GUI side of handshake
        /// </summary>
        /// <param name="obj">Token to use</param>
        void client_Connect(object obj)
        {
            try
            {
                //the token to use
                Token token = (Token)obj;

                //connect
                client = new TcpClient(token.IP, Listener.Port);
                stream = client.GetStream();

                //begin auth
                State = ConnectionState.Authing;

                //set up symmetric key
                RijndaelManaged rd = new RijndaelManaged();
                rd.GenerateKey();
                rd.GenerateIV();
                rd.Padding = PaddingMode.ANSIX923;
                Log("Setting up Rijndael key/IV: " + ByteArrayToString(rd.Key) + "/" + ByteArrayToString(rd.IV));
                rijndael = rd;

                //set up auth token
                Auth auth = new Auth();
                auth.Version = Token.GetCurrentVersion();
                auth.Key = rd.Key;
                auth.IV = rd.IV;
                
                //send auth token using RSA
                RSACryptoServiceProvider rsa = null;
                if (token.IP != "127.0.0.1")
                {
                    rsa = new RSACryptoServiceProvider(Account.KeySize);
                    rsa.ImportParameters(token.Key);
                    Log("Using RSA, key-length is " + rsa.KeySize.ToString() + " bits");
                }
                Send(auth, rsa);

                //handshake complete
                State = ConnectionState.Ready;

                //wait for version string
                string version = (string)Receive(null, serializer);
                if (version != Token.GetCurrentVersion())
                {
                    throw new Exception("Bad version");
                }
                else Log("Bot version: " + version);

                //run thread
                client_Thread();
            }
            catch (Exception ex)
            {
                try
                {
                    Log("Exception during connection handling: " + ex.Message);
                    if (State < ConnectionState.Disconnected)
                    {
                        //error
                        error = ex;
                        Stop();
                        State = ConnectionState.Error;
                    }
                }
                catch (Exception ex2)
                {
                    Log("Exception during connection termination: " + ex2.Message);
                }
            } 
        }

        /// <summary>
        /// Bot side handshake
        /// </summary>
        /// <param name="obj">RSA key to use</param>
        void client_Listen()
        {
            try
            {
                //check if account matches
                IPAddress ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
                Account account = desBot.State.AccountList.Lookup(ip);
                if (account == null && ip.ToString() != "127.0.0.1")
                {
                    throw new Exception("No account matches the source IP");
                }

                //get stream
                stream = client.GetStream();

                //get auth token from client
                RSACryptoServiceProvider rsa = null;
                if (ip.ToString() != "127.0.0.1")
                {
                    rsa = new RSACryptoServiceProvider(Account.KeySize);
                    rsa.ImportParameters(account.Key);
                    Log("Accessed RSA key for " + ip.ToString() + ", key-length is " + rsa.KeySize.ToString() + " bits");
                }
                Auth auth = (Auth)Receive(rsa, serializer);
                if (auth.Version != Token.GetCurrentVersion())
                {
                    throw new Exception("The GUI version is different from the bot version");
                }
                else Log("GUI version: " + auth.Version);

                //set up crypto
                RijndaelManaged rd = new RijndaelManaged();
                rd.Key = auth.Key;
                rd.IV = auth.IV;
                rd.Padding = PaddingMode.ANSIX923;
                Log("Setting up Rijndael key/IV: " + ByteArrayToString(rd.Key) + "/" + ByteArrayToString(rd.IV));
                rijndael = rd;

                //trigger ready event
                State = ConnectionState.Ready;
                Send(Token.GetCurrentVersion());
                source.OnReady(this);
                source.AddConnection(this);

                //handshake complete
                client_Thread();
            }
            catch (Exception ex)
            {
                try
                {
                    Log("Exception during connection handling: " + ex.Message);
                    if (State < ConnectionState.Disconnected)
                    {
                        //error
                        error = ex;
                        Stop();
                        State = ConnectionState.Error;
                    }
                }
                catch (Exception ex2)
                {
                    Log("Exception during connection termination: " + ex2.Message);
                }
            }
        }

        /// <summary>
        /// After handshaking is done, run connection on the current thread
        /// </summary>
        void client_Thread()
        {
            Log("GUI worker thread started");
            BinaryFormatter serializer = new BinaryFormatter();
            while (State == ConnectionState.Ready && client.Connected)
            {
                //get next object
                object obj = Receive(null, serializer);
                if (OnReceive != null)
                {
                    //raise event
                    OnReceive.Invoke(this, obj);
                }
                else Log("Received an object, but no handler was installed");
            }

            //disconnect logic
            Log("GUI worker thread stopping");
            Stop();
        }
    }

    /// <summary>
    /// Listener utility (for Bot side)
    /// Manages a set of Connections to GUIs
    /// </summary>
    public class Listener
    {
        /// <summary>
        /// The port to listen for incoming connections
        /// </summary>
        public const int Port = 49160;

        /// <summary>
        /// Delegate for logging
        /// </summary>
        /// <param name="text">The log entry</param>
        public delegate void LogDelegate(string text);

        TcpListener listener = new TcpListener(IPAddress.Any, Port);
        Thread thread;
        List<Connection> connections = new List<Connection>();
        LogDelegate log;

        /// <summary>
        /// Triggers when any connection receives an object
        /// </summary>
        public event Connection.OnReceiveEventHandler OnReceive;

        /// <summary>
        /// Constructor
        /// </summary>
        public Listener(LogDelegate log)
        {
            this.log = log;
            thread = new Thread(new ThreadStart(listener_Thread));
            thread.Start();
        }

        /// <summary>
        /// Append to the listener log
        /// </summary>
        /// <param name="text">The text to append</param>
        public void Log(string text)
        {
            log(text);
        }

        /// <summary>
        /// Stops all connections and the listener
        /// </summary>
        public void Stop()
        {
            lock (this)
            {
                thread.Abort();
                log("Listener is stopping...");
                listener.Stop();
                List<Connection> copy = new List<Connection>(connections);
                foreach (Connection connection in copy)
                {
                    try
                    {
                        connection.Stop();
                    }
                    catch (Exception ex)
                    {
                        log("Failed to close a connection: " + ex.Message);
                    }
                }
            }
            log("Listener has stopped");
        }

        /// <summary>
        /// Send an object to all connected GUIs
        /// </summary>
        /// <param name="obj">The object to send</param>
        public void Send(object obj)
        {
            lock (this)
            {
                foreach (Connection connection in connections)
                {
                    try
                    {
                        connection.Send(obj);
                    }
                    catch (Exception ex)
                    {
                        log("Exception while sending object of type " + obj.GetType().Name + ": " + ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Triggers when a connection to a GUI enters the ConnectionState.Ready state
        /// </summary>
        /// <param name="connection">The connection that entered the ConnectionState.Ready state</param>
        public void OnReady(Connection connection)
        {
            log("Connection entering ready state, sending settings");
            connection.Send(Settings.CreateSettings());
            log("GUI has successfully connected!");
        }

        /// <summary>
        /// Adds a connection to the listener (used internally)
        /// </summary>
        /// <param name="connection">The connection to be added</param>
        internal void AddConnection(Connection connection)
        {
            lock (this)
            {
                connections.Add(connection);
                connection.OnReceive += new Connection.OnReceiveEventHandler(connection_OnReceive);
                connection.OnDisconnect += new Connection.OnDisconnectEventHandler(connection_OnDisconnect);
            }
        }

        /// <summary>
        /// Removes a connection from the listener (used internally)
        /// </summary>
        /// <param name="connection">The connection to remove</param>
        void RemoveConnection(Connection connection)
        {
            lock (this)
            {
                if (connections.Contains(connection))
                {
                    connections.Remove(connection);
                }
                log("Removing connection with state " + connection.State.ToString() + " and error: " + connection.GetError());
            }
        }

        /// <summary>
        /// Event handler for all connections
        /// </summary>
        /// <param name="source">Source connection</param>
        /// <param name="obj">The object veign received</param>
        void connection_OnReceive(Connection source, object obj)
        {
            if (OnReceive != null) OnReceive.Invoke(source, obj);
        }

        /// <summary>
        /// Event handler for disconnect
        /// </summary>
        /// <param name="source"></param>
        void connection_OnDisconnect(Connection source)
        {
            log("GUI disconnected");
            RemoveConnection(source);
        }

        /// <summary>
        /// Listener thread
        /// </summary>
        void listener_Thread()
        {
            try
            {
                listener.Start();
                log("Started to listen for connections on port " + Port.ToString());
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    log("Incoming connection from " + (client.Client.RemoteEndPoint as IPEndPoint).Address.ToString());
                    new Connection(this, client);
                }
            }
            catch (Exception ex)
            {
                log("Exception in listener thread: " + ex.Message);
            }
        }
    }
}