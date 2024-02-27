using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCPServer
{
    public class WindowLoc
    {
        public int Id;
        public Vec2d Pos;
        public Vec2d Size;
        public DateTime LastHeartbeat;

        public WindowLoc(int id, Vec2d pos, Vec2d size)
        {
            Id = id;
            Pos = pos;
            Size = size;
            LastHeartbeat = DateTime.Now;
        }

        public WindowLoc()
        {
            Pos = Vec2d.Zero();
            Size = Vec2d.Zero();
            LastHeartbeat = DateTime.Now;
        }
    }
}
