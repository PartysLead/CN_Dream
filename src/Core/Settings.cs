using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace CnDream.Core
{
    public class Settings
    {
        public string Password { get; set; }
        public int SaltSize { get; set; }
        public int Iterations { get; set; }
        public int KeySize { get; set; }

        public LocalPeerSettings Local { get; set; }
        public PeerSettings Remote { get; set; }
    }

    public class PeerSettings
    {
        public IPAddress Listen { get; set; }
        public int Port { get; set; }
    }

    public class LocalPeerSettings : PeerSettings
    {
        public IPAddress PeerAddress { get; set; }
        public int PeerPort { get; set; }
    }
}
