using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TCPServer;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Timers;

namespace TCPWindowVisual
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string IpAddress = "localhost";
        public int Port = 6000;
        public int ID = -1;

        // Connection objects
        private TcpClient _client;

        // Buffer & messaging
        public readonly int BufferSize = 4 * 1024;  // 2KB
        private NetworkStream? _msgStream = null;

        private Thread MainNetworkThread;

        private bool PosUpdatedToServer = false;
        public List<WindowLoc> windowLocs = new List<WindowLoc>();

        private Vec2d Pos { get; set; } = new Vec2d();
        private Vec2d Size { get; set; } = new Vec2d();
        private bool loaded = false;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private System.Timers.Timer HeartbeatTimer;


        public MainWindow()
        {
            InitializeComponent();

            Pos = new Vec2d(Application.Current.MainWindow.Top, Application.Current.MainWindow.Left);
            Size = new Vec2d(Application.Current.MainWindow.Width, Application.Current.MainWindow.Height);


            Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            loaded = true;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Size = new Vec2d(e.NewSize.Width, e.NewSize.Height);
            if(loaded)
            {
                PosUpdatedToServer = false;
            }
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            Pos = new Vec2d(Top, Left);
            if(loaded)
            {
                PosUpdatedToServer = false;
            }
        }

        private void Start()
        {
            ConnectToServer();

            MainNetworkThread = new Thread(MainNetworkLoop);
            MainNetworkThread.Start();

            HeartbeatTimer = new System.Timers.Timer(2000);
            HeartbeatTimer.Elapsed += SendHeartbeat;
            HeartbeatTimer.AutoReset = true;
            HeartbeatTimer.Enabled = true;

            Cursor = Cursors.Arrow;
        }

        private void SendHeartbeat(Object source, ElapsedEventArgs e)
        {
            if (_msgStream != null && ID != -1)
            { 
                byte[] msgBuffer = TCPMessage.Serialize(new TCPMessage(ID, Status.Heartbeat, Pos, Size)).Concat(Encoding.UTF8.GetBytes("\n")).ToArray();
                _msgStream.Write(msgBuffer, 0, msgBuffer.Length);
            }
        }

        private void ConnectToServer()
        {
            _client = new TcpClient();          // Other constructors will start a connection
            _client.SendBufferSize = BufferSize;
            _client.ReceiveBufferSize = BufferSize;

            // Try to connect
            try { _client.Connect(IpAddress, Port); }
            catch (Exception e) { return; }
            EndPoint endPoint = _client.Client.RemoteEndPoint;

            // Make sure we're connected
            if (_client.Connected)
            {
                // Tell them that we're a messenger
                _msgStream = _client.GetStream();
                byte[] msgBuffer = TCPMessage.Serialize(new TCPMessage(-1, Status.RequestId, Pos, Size)).Concat(Encoding.UTF8.GetBytes("\n")).ToArray();
                _msgStream.Write(msgBuffer, 0, msgBuffer.Length);

                // If we're still connected after sending our name, that means the server accepts us
                if (_isDisconnected(_client))
                { 
                    MessageBox.Show($"Wasn't able to request ID from the server at {endPoint}.");
                    cancellationTokenSource.Cancel();
                }
            }
            else
            {
                MessageBox.Show($"Wasn't able to connect to the server at {endPoint}.");
                Stop();
            }
        }

        private void MainNetworkLoop()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                if (!PosUpdatedToServer && ID != -1)
                {
                    _msgStream = _client.GetStream();
                    byte[] msgBuffer = TCPMessage.Serialize(new TCPMessage(ID, Status.PosUpdate, Pos, Size)).Concat(Encoding.UTF8.GetBytes("\n")).ToArray();
                    try { _msgStream.Write(msgBuffer, 0, msgBuffer.Length); } catch { }
                }

                int messageLength = _client.Available;
                if (messageLength > 0)
                {
                    // Read the whole message
                    byte[] msgBuffer = new byte[messageLength];

                    try { _msgStream.Read(msgBuffer, 0, messageLength); } catch { }

                    // Decode it and print it
                    List<TCPMessage> messages = TCPMessage.Deserialize(msgBuffer);
                    foreach (TCPMessage message in messages)
                    {
                        if (message.Status == Status.GivenId)
                        {
                            ID = message.Id;
                        }
                        else if(ID != -1 && message.Status == Status.PosOk && message.Id == ID)
                        {
                            PosUpdatedToServer = true;
                        }    
                        else if(message.Status == Status.PosOk)
                        {
                            var found = windowLocs.Where(x => x.Id == message.Id).ToList();
                            if (found.Count > 0)
                            {
                                found.First().Pos = message.Pos;
                                found.First().Size = message.Size;
                            }
                            else
                            {
                                windowLocs.Add(new WindowLoc(message.Id, message.Pos, message.Size));
                            }
                        }
                    }
                }
            }
        }

        //private void AddMessage(TCPMessage message)
        //{
        //    messageQueue.Enqueue(message);

        //    // Use Dispatcher to update the UI thread safely
        //    Dispatcher.Invoke(() =>
        //    {
        //        UpdateMessages();
        //    });
        //}

        //private void UpdateMessages()
        //{
        //    while (messageQueue.TryDequeue(out TCPMessage message))
        //    {
        //        // Create a new TextBlock for each message and add it to the StackPanel
        //        MessagesView.Children.Add(new TextBlock { Text = message.Timestamp.ToString("yyyy.MM.dd HH:mm:ss") + " - " + message.Name + ": " + message.Content });
        //    }
        //}

        private void Window_Closing(object sender, EventArgs e)
        {
            cancellationTokenSource.Cancel();
            if (MainNetworkThread != null)
            {
                if (!MainNetworkThread.Join(TimeSpan.FromSeconds(10)))
                {
                    MessageBox.Show("Error closing main thread!");
                    Stop();
                    foreach (var process in Process.GetProcessesByName("TCPWindowVisual"))
                    {
                        process.Kill();
                    }
                }
            }
            Stop();
            Application.Current.Shutdown();
        }


        private void Stop()
        {
            cancellationTokenSource.Cancel();
            _msgStream?.Close();
            _msgStream = null;
            _client?.Close();
        }

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

        
    }
}
