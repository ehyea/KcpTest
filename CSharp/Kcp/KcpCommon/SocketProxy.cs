using System;
using System.Net;
using System.Net.Sockets;

namespace Core.Socket
{
	internal class SocketProxy : ISocketProxy
	{
		private System.Net.Sockets.Socket socket;

		public bool NoDelay
		{
			get
			{
				return socket.NoDelay;
			}
			set
			{
				socket.NoDelay = value;
			}
		}

		public bool Connected
		{
			get
			{
				return socket.Connected;
			}
		}

		public EndPoint RemoteEndPoint
		{
			get
			{
				return socket.RemoteEndPoint;
			}
		}

		public int Available
		{
			get
			{
				return socket.Available;
			}
		}

		public SocketProxy(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
		{
			socket = new System.Net.Sockets.Socket(addressFamily, socketType, protocolType);
		}

		private SocketProxy(System.Net.Sockets.Socket socket)
		{
			this.socket = socket;
		}

		public void Dispose()
		{
			socket.Shutdown(SocketShutdown.Both);
			socket.Close();
			socket = null;
		}

		public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, bool optionValue)
		{
			socket.SetSocketOption(optionLevel, optionName, optionValue);
		}

		public void Listen(int backlog)
		{
			socket.Listen(backlog);
		}

		public IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state)
		{
			return socket.BeginReceive(buffer, offset, size, socketFlags, callback, state);
		}

		public int EndReceive(IAsyncResult asyncResult)
		{
			return socket.EndReceive(asyncResult);
		}

		public void Bind(EndPoint localEP)
		{
			socket.Bind(localEP);
		}

		public int IOControl(int ioControlCode, byte[] optionInValue, byte[] optionOutValue)
		{
			return socket.IOControl(ioControlCode, optionInValue, optionOutValue);
		}

		public IAsyncResult BeginAccept(AsyncCallback callback, object state)
		{
			return socket.BeginAccept(callback, state);
		}

		public ISocketProxy EndAccept(IAsyncResult asyncResult)
		{
			return new SocketProxy(socket.EndAccept(asyncResult));
		}

		public IAsyncResult BeginConnect(EndPoint remoteEP, AsyncCallback callback, object state)
		{
			return socket.BeginConnect(remoteEP, callback, state);
		}

		public void EndConnect(IAsyncResult asyncResult)
		{
			socket.EndConnect(asyncResult);
		}

		public IAsyncResult BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state)
		{
			return socket.BeginSend(buffer, offset, size, socketFlags, callback, state);
		}

		public int EndSend(IAsyncResult asyncResult)
		{
			return socket.EndSend(asyncResult);
		}

		public int SendTo(byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP)
		{
			return socket.SendTo(buffer, offset, size, socketFlags, remoteEP);
		}

		public int ReceiveFrom(byte[] buffer, int size, SocketFlags socketFlags, ref EndPoint remoteEP)
		{
			return socket.ReceiveFrom(buffer, size, socketFlags, ref remoteEP);
		}

		public void Close()
		{
			socket.Close();
		}

		public void Shutdown(SocketShutdown how)
		{
			socket.Shutdown(how);
		}
	}
}
