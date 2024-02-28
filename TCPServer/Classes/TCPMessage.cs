using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace TCPServer
{
    [Serializable]
    public class Vec2d
    {
        public Vec2d()
        {
            x = 0; y = 0;
        }

        public Vec2d(double x, double y)
        {
            this.x = x; this.y = y;
        }

        public double x { get; set; }
        public double y { get; set; }

        public bool isNan()
        {
            return double.IsNaN(x) || double.IsNaN(y);
        }

        public static Vec2d Zero()
        {
            return new Vec2d();
        }

        public string Str()
        {
            return "(" + Math.Round(x,2).ToString() + "," + Math.Round(y, 2).ToString() + ")";
        }
    }

    [Serializable]
    public enum Status
    {
        RequestId,
        GivenId,
        PosUpdate,
        PosOk,
        Heartbeat,
        DisconnectedWindow,
        Nothing
    }


    [Serializable]
    public class TCPMessage
    {
        public TCPMessage(int id, Status status, Vec2d pos, Vec2d size)
        {
            Id = id;
            Status = status;
            Pos = pos;
            Size = size;
        }
        public TCPMessage()
        {
            Id = -1;
            Status = Status.Nothing;
            Pos = new Vec2d();
            Size = new Vec2d();
        }

        public int Id { get; set; }
        public Status Status { get; set; }
        public Vec2d Pos { get; set; }
        public Vec2d Size { get; set; }

        public static byte[] Serialize(TCPMessage msg)
        {
            var jsonString = JsonSerializer.Serialize(msg) + "\n";
            return Encoding.UTF8.GetBytes(jsonString);
        }

        public static List<TCPMessage> Deserialize(byte[] data, int length=-1)
        {
            var jsonString = "";
            if(length == -1)
                jsonString = Encoding.UTF8.GetString(data);
            else
                jsonString = Encoding.UTF8.GetString(data,0,length);

            string[] jsonMessages = jsonString.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            List<TCPMessage> messages = new List<TCPMessage>(); 
            foreach (var message in jsonMessages)
            {
                TCPMessage? msg = null;
                try
                {
                    msg = JsonSerializer.Deserialize<TCPMessage>(message);
                    messages.Add(msg);
                }
                catch (Exception e) { }
            }

            return messages;
        }
    }
}
