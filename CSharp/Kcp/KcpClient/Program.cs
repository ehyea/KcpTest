using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Core.Socket;
using System.Threading;

namespace KcpClient
{
    class Program
    {
        static void Main(string[] args)
        {
            IPEndPoint ip = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 10086);
            uint conv = 0x12345678;
            KcpConnector kcp = new KcpConnector(ip, conv,10, 10);
           IAsyncResult asyncResult = kcp.Connect((open, exp) => {
               Console.Write("Open:" + open);
            });
            while (!asyncResult.IsCompleted)
            {
                Console.Write("*");
                Thread.Sleep(100);
            }
            string msg = "hello kcp!";
            byte[] msgBytes = System.Text.Encoding.ASCII.GetBytes(msg);
            while (true)
            {
                kcp.Send(msgBytes, 0, msgBytes.Length);
                Thread.Sleep(1000);
            }
        }
    }
}
