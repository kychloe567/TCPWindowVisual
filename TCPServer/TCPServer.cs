using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Xml.Linq;
using System.Diagnostics;

namespace TCPServer
{
    public class TCPServer
    {
        // What listens in
        private TcpListener? _listener;

        // types of clients connected
        private List<TcpClient> _clients = new List<TcpClient>();
        private Dictionary<TcpClient, WindowLoc> windowLocs = new Dictionary<TcpClient, WindowLoc>();

        private int currentFreeId = 1;

        // Messages that need to be sent
        private Queue<TCPMessage> _messageQueue = new Queue<TCPMessage>();

        // Extra fun data
        public readonly string ServerName;
        public readonly int Port;
        public bool Running { get; private set; }

        // Buffer
        public readonly int BufferSize = 4 * 1024;  // 2KB

        public TCPServer(string serverName, int port)
        {
            // Set the basic data
            ServerName = serverName;
            Port = port;
            Running = false;

            // Make the listener listen for connections on any network device
            _listener = new TcpListener(IPAddress.Any, Port);
        }

        /// If the server is running, this will shut down the server
        public void Shutdown()
        {
            Running = false;
            Console.WriteLine("Shutting down server");
        }

        // Start running the server.  Will stop when `Shutdown()` has been called
        public void Run()
        {
            // Some info
            Console.WriteLine("Starting the \"{0}\" TCP Server on port {1}.", ServerName, Port);
            Console.WriteLine("Press Ctrl-C to shut down the server at any time.");

            // Make the server run
            if(_listener == null) 
            {
                throw new Exception("Class constuctor failed to initialize the Listener!");
            }
            _listener.Start();
            Running = true;

            for (int i = 0; i < 5; i++)
            {
                Process.Start("C:\\Users\\Chloe\\Desktop\\GithubProjects\\TCPWindowVisual\\TCPWindowVisual\\bin\\Debug\\net6.0-windows\\TCPWindowVisual.exe");
            }

            // Main server loop
            while (Running)
            {
                // Check for new clients
                if (_listener.Pending())
                    _handleNewConnection();

                // Do the rest
                _checkForDisconnects();
                _checkForNewMessages();
                _sendMessages();

                // Use less CPU
                Thread.Sleep(10);
            }

            // Stop the server, and clean up any connected clients
            foreach (TcpClient v in _clients)
                _cleanupClient(v);
            _listener.Stop();

            // Some info
            Console.WriteLine("Server is shut down.");
        }

        private void _handleNewConnection()
        {
            bool good = false;
            TcpClient newClient = _listener.AcceptTcpClient();
            NetworkStream netStream = newClient.GetStream();

            // Modify the default buffer sizes
            newClient.SendBufferSize = BufferSize;
            newClient.ReceiveBufferSize = BufferSize;

            // Print some info
            EndPoint endPoint = newClient.Client.RemoteEndPoint;
            Console.WriteLine("Handling a new client from {0}...", endPoint);

            // Let them identify themselves
            byte[] msgBuffer = new byte[BufferSize];
            int bytesRead = netStream.Read(msgBuffer, 0, msgBuffer.Length);
            //Console.WriteLine("Got {0} bytes.", bytesRead);
            if (bytesRead > 0)
            {
                //string msg = Encoding.UTF8.GetString(msgBuffer, 0, bytesRead);
                List<TCPMessage> messages = TCPMessage.Deserialize(msgBuffer, bytesRead);

                foreach (TCPMessage message in messages)
                {
                    if(message.Status == Status.RequestId)
                    {
                        _clients.Add(newClient);
                        windowLocs.Add(newClient, new WindowLoc(currentFreeId, Vec2d.Zero(), Vec2d.Zero()));

                        byte[] assignIdMsg = TCPMessage.Serialize(new TCPMessage(currentFreeId, Status.GivenId, Vec2d.Zero(),Vec2d.Zero()));
                        newClient.GetStream().Write(assignIdMsg, 0, assignIdMsg.Length);

                        Console.WriteLine("{0} given Id of {1}.", endPoint, currentFreeId);



                        currentFreeId++;
                    }
                }
            }
        }

        // Sees if any of the clients have left the chat server
        private void _checkForDisconnects()
        {
            foreach (TcpClient m in _clients.ToArray())
            {
                if (_isDisconnected(m))
                {
                    Console.WriteLine("Window with ID of {0} closed.", windowLocs[m].Id);
                    WindowDisconnectedBroadcast(windowLocs[m].Id);
                    _clients.Remove(m);
                    windowLocs.Remove(m);
                    _cleanupClient(m);
                }
                else if ((DateTime.Now - windowLocs[m].LastHeartbeat).TotalSeconds > 3)
                {
                    //Console.WriteLine("Window with ID of {0} closed.", windowLocs[m].Id);
                    //WindowDisconnectedBroadcast(windowLocs[m].Id);
                    //_clients.Remove(m);
                    //windowLocs.Remove(m);
                    //_cleanupClient(m);
                }
                
            }
        }

        // See if any of our messengers have sent us a new message, put it in the queue
        private void _checkForNewMessages()
        {
            foreach (TcpClient m in _clients)
            {
                int messageLength = m.Available;
                if (messageLength > 0)
                {
                    byte[] msgBuffer = new byte[messageLength];
                    m.GetStream().Read(msgBuffer, 0, msgBuffer.Length);

                    // Attach a name to it and shove it into the queue
                    List<TCPMessage> messages = TCPMessage.Deserialize(msgBuffer);
                    foreach(var message in messages)
                    {
                        if(message.Status == Status.PosUpdate)
                        {
                            windowLocs[m].Pos = message.Pos;
                            windowLocs[m].Size = message.Size;
                            Console.WriteLine("Window (" + windowLocs[m].Id + ") - POS: " + windowLocs[m].Pos.Str() + " - SIZE: " + windowLocs[m].Size.Str());
                            message.Status = Status.PosOk;
                            _messageQueue.Enqueue(message);
                        }
                        else if(message.Status == Status.Heartbeat)
                        {
                            windowLocs[m].LastHeartbeat = DateTime.Now;
                            windowLocs[m].Pos = message.Pos;
                            windowLocs[m].Size = message.Size;
                            message.Status = Status.PosOk;
                            _messageQueue.Enqueue(message);

                        }
                    }
                }
            }
        }

        // Clears out the message queue (and sends it to all of the viewers)
        private void _sendMessages()
        {
            foreach (TCPMessage msg in _messageQueue)
            {
                if (msg.Pos.isNan()) msg.Pos = Vec2d.Zero();
                if (msg.Size.isNan()) msg.Size = Vec2d.Zero();
                // Encode the message
                byte[] msgBuffer = TCPMessage.Serialize(msg);

                // Send the message to each viewer
                foreach (TcpClient v in _clients)
                    v.GetStream().Write(msgBuffer, 0, msgBuffer.Length);
            }

            // clear out the queue
            _messageQueue.Clear();
        }

        private void WindowDisconnectedBroadcast(int id)
        {
            var disconnectMsg = new TCPMessage(id, Status.DisconnectedWindow, Vec2d.Zero(), Vec2d.Zero());
            _messageQueue.Enqueue(disconnectMsg);
        }

        // Checks if a socket has disconnected
        private static bool _isDisconnected(TcpClient client)
        {
            try
            {
                Socket s = client.Client;
                return s.Poll(10 * 1000, SelectMode.SelectRead) && (s.Available == 0);
            }
            catch (SocketException se)
            {
                // We got a socket error, assume it's disconnected
                return true;
            }
        }

        // cleans up resources for a TcpClient
        private static void _cleanupClient(TcpClient client)
        {
            client.GetStream().Close();     // Close network stream
            client.Close();                 // Close client
        }


    }
}
