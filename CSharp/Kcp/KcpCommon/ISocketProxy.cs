using System;
using System.Net;
using System.Net.Sockets;

namespace Core.Socket
{
	public interface ISocketProxy : IDisposable
	{
		bool Connected
		{
			get;
		}

		EndPoint RemoteEndPoint
		{
			get;
		}

		bool NoDelay
		{
			get;
			set;
		}

		int Available
		{
			get;
		}

		void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, bool optionValue);

		void Bind(EndPoint localEP);

		void Listen(int backlog);

		int IOControl(int ioControlCode, byte[] optionInValue, byte[] optionOutValue);

		IAsyncResult BeginAccept(AsyncCallback callback, object state);

		ISocketProxy EndAccept(IAsyncResult asyncResult);

		IAsyncResult BeginConnect(EndPoint remoteEP, AsyncCallback callback, object state);

		void EndConnect(IAsyncResult asyncResult);

		IAsyncResult BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state);

		int EndSend(IAsyncResult asyncResult);

		IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state);

		int EndReceive(IAsyncResult asyncResult);

		int SendTo(byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP);

		int ReceiveFrom(byte[] buffer, int size, SocketFlags socketFlags, ref EndPoint remoteEP);

		void Close();

		void Shutdown(SocketShutdown how);
	}
}
