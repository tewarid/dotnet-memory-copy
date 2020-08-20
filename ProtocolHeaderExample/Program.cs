using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using MemoryCopy;

namespace ProtocolHeaderExample
{
    class FixedHeader
    {
        [DataMember(Order = 1)]
        public ushort Id { get; set; }
    }

    class VariableHeader1 : FixedHeader
    {
        [DataMember(Order = 2)]
        private ushort Size
        {
            get { return Payload == null? (ushort)0 : (ushort)Payload.Length; }
            set { Payload = new byte[value]; }
        }

        [DataMember(Order = 3)]
        public byte[] Payload { get; set; }
    }

    class Program
    {
        public static void Main()
        {
            byte[] data = { 0x0, 0x1, 0x0, 0x5, 0x01, 0x02, 0x03, 0x04, 0x05 };
            MemoryStream stream = new MemoryStream(data);

            MemoryCopy.MemoryCopy copy = new MemoryCopy.MemoryCopy();
            copy.ByteOrder = ByteOrder.BigEndian; // default

            FixedHeader h;
            Task<object> t = copy.Read(typeof(FixedHeader), stream, true);
            t.Wait();
            h = (FixedHeader)t.Result;

            if (h.Id == 1)
            {
                t = copy.Read(typeof(VariableHeader1), stream, false);
                t.Wait();
                VariableHeader1 varh = (VariableHeader1)t.Result;
                varh.Id = h.Id;
                Console.WriteLine("{0:x} {1:x}", varh.Id, varh.Payload.Length, false);
            }
        }
    }
}
