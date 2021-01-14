using System;
using System.Net.Sockets;

namespace Core.Socket
{
	internal class SocketProxyFactory
	{
		private static SocketProxyFactory instance;

		private SocketProxyFactory()
		{
			
		}

		public static ISocketProxy Create(string name = null)
		{
			switch(name)
            {
                case "udp":
                    return GetInstance().CreateUdpProxy();
                case "tcp":
                    return GetInstance().CreateTcpProxy();
                default:
                    return GetInstance().CreateTcpProxy();
            }
		}

		private static SocketProxyFactory GetInstance()
		{
			if (instance == null)
			{
				instance = new SocketProxyFactory();
			}
			return instance;
		}

		private ISocketProxy CreateTcpProxy()
		{
			return new SocketProxy(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		}

		private ISocketProxy CreateUdpProxy()
		{
			return new SocketProxy(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		}
	}
}
