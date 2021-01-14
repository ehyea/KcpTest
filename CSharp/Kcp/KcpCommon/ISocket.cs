using System;
using System.Net;

namespace Core.Socket
{
	public interface ISocket : IDisposable
	{
		bool Connected
		{
			get;
		}

		long SentBytes
		{
			get;
		}

		long ReceiveBytes
		{
			get;
		}

		SocketStatus Status
		{
			get;
		}

		event Action<ISocket, object> OnAccepted;

		event Action<ISocket, IPEndPoint> OnConnecting;

		event Action<ISocket, object> OnConnected;

		event Action<ISocket, Exception> OnClosed;

		event Action<ISocket, ArraySegment<byte>> OnMessage;

		event Action<ISocket, Exception> OnError;

		event Action<ISocket, byte[]> OnBufferFull;

		event Action<ISocket> OnBufferDrain;

		IAsyncResult Accept(Action<OpenResults, Exception> callback = null);

		IAsyncResult Connect(Action<OpenResults, Exception> callback = null);

		SendResults Send(byte[] data, int offset = 0, int count = -1);

		CloseResults Disconnect(Exception exception = null);
	}
}
