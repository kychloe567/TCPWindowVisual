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
using System.Windows.Interop;
using System.Drawing;
using LineSegment = TCPServer.LineSegment;
using System.Numerics;
using System.Reflection;

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

        public List<WindowLoc> windowLocs = new List<WindowLoc>();

        private bool PosUpdatedToServer = false;
        private Vec2d LastPos = null;
        private Vec2d Pos { get; set; } = new Vec2d();
        private Vec2d LastSize = null;
        private Vec2d Size { get; set; } = new Vec2d();
        private bool loaded = false;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private System.Timers.Timer HeartbeatTimer;

        private static Random rnd = new Random((int)DateTime.Now.Ticks);

        public MainWindow()
        {
            InitializeComponent();

            Left = rnd.Next(0, 1920 - 300);
            Top = rnd.Next(0, 1080 - 300);
            Pos = new Vec2d(Top, Left);
            Size = Vec2d.Zero();

            Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            loaded = true;
            PosUpdatedToServer = false;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Size = new Vec2d(e.NewSize.Width, e.NewSize.Height);
            if(loaded && (LastSize == null || (LastSize.x != Size.x || LastSize.y != Size.y)))
            {
                LastSize = new Vec2d(Size.x, Size.y);
                PosUpdatedToServer = false;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ManageLines();
                });
            }
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            Pos = new Vec2d(Left, Top);
            if (loaded && (LastPos == null || (LastPos.x != Pos.x || LastPos.y != Pos.y)))
            {
                LastPos = new Vec2d(Pos.x, Pos.y);
                PosUpdatedToServer = false;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ManageLines();
                });
            }
        }

        private void Start()
        {
            ConnectToServer();

            MainNetworkThread = new Thread(MainNetworkLoop);
            MainNetworkThread.Start();

            HeartbeatTimer = new System.Timers.Timer(1000);
            HeartbeatTimer.Elapsed += SendHeartbeat;
            HeartbeatTimer.AutoReset = true;
            HeartbeatTimer.Enabled = true;

            Cursor = Cursors.Arrow;

            //SendHeartbeat(null, null);
        }

        private void SendHeartbeat(Object source, ElapsedEventArgs e)
        {
            if (_msgStream != null && ID != -1)
            {
                byte[] msgBuffer = TCPMessage.Serialize(new TCPMessage(ID, Status.Heartbeat, Pos, Size));
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
                byte[] msgBuffer = TCPMessage.Serialize(new TCPMessage(-1, Status.RequestId, Pos, Size));
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
                if(_isDisconnected(_client))
                {
                    cancellationTokenSource.Cancel();
                    Dispatcher.Invoke(() =>
                    {
                        Stop();
                    });
                    break;
                }    

                if (!PosUpdatedToServer && ID != -1)
                {
                    _msgStream = _client.GetStream();
                    if (!Pos.isNan() && !Size.isNan())
                    {
                        byte[] msgBuffer = TCPMessage.Serialize(new TCPMessage(ID, Status.PosUpdate, Pos, Size));
                        try { _msgStream.Write(msgBuffer, 0, msgBuffer.Length); } catch { }
                    }
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
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ManageLines();
                            });
                        }
                        else if(message.Status == Status.DisconnectedWindow)
                        {
                            windowLocs.Remove(windowLocs.Where(x => x.Id == message.Id).First());
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ManageLines();
                            });
                        }
                        else if(message.Status == Status.AlignCircle)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                AlignCircle();
                            });
                        }
                    }
                }
            }
        }

        private void AlignCircle()
        {
            //float wPerCircle = 6;
            //float radius = 300;
            //float index = ID;
            //if (ID > wPerCircle)
            //{
            //    radius = 150;
            //    index = ID - wPerCircle;
            //}
            //Left = 1920 / 2 - 150 + (Math.Cos(CTR(360 / wPerCircle * index)) * radius);
            //Top = 1080 / 2 - 150 + (Math.Sin(CTR(360 / wPerCircle * index)) * radius);

            Left = 1920 / 2 - 150 + (Math.Cos(CTR(360 / 12 * ID)) * 350);
            Top = 1080 / 2 - 150 + (Math.Sin(CTR(360 / 12 * ID)) * 350);
        }

        private double CTR(double angle)
        {
            return (Math.PI / 180) * angle;
        }

        private Vec2d GetMiddle(Vec2d pos, Vec2d size)
        {
            return new Vec2d(pos.x + size.x / 2, pos.y + size.y / 2);
        }

        private int GetBrushFromIds(int num1, int num2)
        {
            // Combine the integers to create a unique index
            // This is a simple example; you might need a more complex function to ensure uniqueness
            int index = (num1 * 100) + num2; // Ensure the index is within the range of available brushes

            // Use modulo to ensure the index wraps around if it exceeds the number of brushes
            index = index % 141;

            return index;
        }

        private Brush PickBrush(int num1, int num2)
        {
            Brush result = Brushes.Transparent;
            Type brushesType = typeof(Brushes);
            PropertyInfo[] properties = brushesType.GetProperties();
            result = (Brush)properties[GetBrushFromIds(num1,num2)].GetValue(null, null);

            return result;
        }

        private void ManageLines()
        {
            canvas.Children.Clear();

            List<LineSegment> borderLines = new List<LineSegment>()
                {
                    new LineSegment(new System.Numerics.Vector2((float)Pos.x,(float)Pos.y), new System.Numerics.Vector2((float)Pos.x+(float)Size.x,(float)Pos.y)),
                    new LineSegment(new System.Numerics.Vector2((float)Pos.x+(float)Size.x,(float)Pos.y), new System.Numerics.Vector2((float)Pos.x+(float)Size.x,(float)Pos.y+(float)Size.y)),
                    new LineSegment(new System.Numerics.Vector2((float)Pos.x+(float)Size.x,(float)Pos.y+(float)Size.y), new System.Numerics.Vector2((float)Pos.x, (float)Pos.y+(float)Size.y)),
                    new LineSegment(new System.Numerics.Vector2((float)Pos.x,(float)Pos.y+(float)Size.y), new System.Numerics.Vector2((float)Pos.x,(float)Pos.y)),
                };

            foreach (WindowLoc w in windowLocs)
            {
                Line line = new Line();

                line.X1 = Size.x/2; 
                line.Y1 = Size.y/2;

                LineSegment mainLine = new LineSegment(new System.Numerics.Vector2((float)Pos.x+ (float)Size.x/2, (float)Pos.y+ (float)Size.y/2), 
                                                       new System.Numerics.Vector2((float)w.Pos.x + (float)w.Size.x / 2, (float)w.Pos.y + (float)w.Size.y / 2));

                var intersect = System.Numerics.Vector2.Zero;
                bool found = false;
                foreach (var borderLine in borderLines)
                {
                    if (mainLine.TryIntersect(borderLine, out intersect))
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    line.X2 = intersect.X - (float)Pos.x;
                    line.Y2 = intersect.Y - (float)Pos.y;
                }
                else
                {
                    line.X2 = (w.Pos.x + w.Size.x / 2) - (Pos.x);
                    line.Y2 = (w.Pos.y + w.Size.y / 2) - (Pos.y);
                }

                // Set line color and thickness
                //line.Stroke = Brushes.Red;
                if(ID < w.Id)
                    line.Stroke = PickBrush(ID, w.Id);
                else
                    line.Stroke = PickBrush(w.Id,ID);
                line.StrokeThickness = 2;

                canvas.Children.Add(line);
            }

            foreach (var w1 in windowLocs)
            {
                if (w1.Id == ID)
                    continue;

                Vec2d w1Mid = GetMiddle(w1.Pos, w1.Size);

                foreach (var w2 in windowLocs)
                {
                    if (w1.Id == w2.Id || w2.Id == ID)
                        continue;

                    Vec2d w2Mid = GetMiddle(w2.Pos, w2.Size);

                    if (!InsideOwnWindow(new LineSegment(new Vector2((float)w1Mid.x,(float)w1Mid.y), new Vector2((float)w2Mid.x, (float)w2Mid.y)), borderLines))
                        continue;

                    Line line2 = new Line();
                    line2.X1 = w1Mid.x - Pos.x;
                    line2.Y1 = w1Mid.y - Pos.y;
                    line2.X2 = w2Mid.x - Pos.x;
                    line2.Y2 = w2Mid.y - Pos.y;

                    //line2.Stroke = Brushes.Red;
                    if (w1.Id < w2.Id)
                        line2.Stroke = PickBrush(w1.Id, w2.Id);
                    else
                        line2.Stroke = PickBrush(w2.Id, w1.Id);
                    line2.StrokeThickness = 2;

                    canvas.Children.Add(line2);
                }
            }
        }

        private bool InsideOwnWindow(LineSegment line, List<LineSegment> rect)
        {
            System.Drawing.Rectangle r = new System.Drawing.Rectangle((int)Pos.x, (int)Pos.y, (int)Size.x, (int)Size.y);
            System.Drawing.Point p1 = new System.Drawing.Point((int)line.From.X, (int)line.From.Y);
            System.Drawing.Point p2 = new System.Drawing.Point((int)line.To.X, (int)line.To.Y);

            return line.TryIntersect(rect[0], out Vector2 v0) ||
                   line.TryIntersect(rect[1], out Vector2 v1) ||
                   line.TryIntersect(rect[2], out Vector2 v2) ||
                   line.TryIntersect(rect[3], out Vector2 v3) ||
                   r.Contains(p1) || r.Contains(p2);
        }

        private void Window_Closing(object sender, EventArgs e)
        {
            cancellationTokenSource.Cancel();
            if (MainNetworkThread != null)
            {
                if (!MainNetworkThread.Join(TimeSpan.FromSeconds(10)))
                {
                    MessageBox.Show("Error closing main thread!");
                    foreach (var process in Process.GetProcessesByName("TCPWindowVisual"))
                    {
                        process.Kill();
                    }
                }
            }
            Stop();
        }


        private void Stop()
        {
            cancellationTokenSource.Cancel();
            _msgStream?.Close();
            _msgStream = null;
            _client?.Close();
            Application.Current.Shutdown();
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

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}
