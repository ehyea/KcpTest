using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using Core.Socket.KCPSupport;
namespace Core.Socket
{
	public class KcpConnector : ISocket, IDisposable
	{
		private class KcpAsyncResult : IAsyncResult
		{

			public object AsyncState
			{
                get;
                set;
			}

			public WaitHandle AsyncWaitHandle
			{
                get;
                set;
			}

			public bool CompletedSynchronously
			{
                get;
                set;
			}

			public bool IsCompleted
			{
                get;
                set;
			}
		}

		private byte[] sendBuffer;

		private KCP kcpClient;

		private uint nextKcpUpdateTime;

		private Queue<byte[]> pendingDataQueue;

		private KcpAsyncResult asyncResult;

		private Action<OpenResults, Exception> openCallback;

		internal int dataShards;

		internal int parityShards;

		internal bool ackNoDelay;

		internal bool writeDelay;

		internal int headerSize;

		internal bool isServer;

		internal KCP Kcp
		{
			get
			{
				return kcpClient;
			}
		}

		public bool Connected
		{
			get
			{
				return Status == SocketStatus.Establish;
			}
		}

		public long SentBytes
		{
            get;
            set;
		}

		public long ReceiveBytes
		{
            get;
            set;
		}

		public SocketStatus Status
		{
            get;
            private set;
		}

		public IPEndPoint IPEndPoint
		{
            get;
            private set;
		}

		public uint KcpKey
		{
            get;
            private set;
		}

		public object SyncRoot
		{
            get;
            private set;
		}

		public event Action<ISocket, object> OnAccepted;

		public event Action<ISocket, IPEndPoint> OnConnecting;

		public event Action<ISocket, object> OnConnected;

		public event Action<ISocket, Exception> OnClosed;

		public event Action<ISocket, ArraySegment<byte>> OnMessage;

		public event Action<ISocket, Exception> OnError;

		public event Action<ISocket, byte[]> OnBufferFull;

		public event Action<ISocket> OnBufferDrain;

		public KcpConnector(string nsp)
			: this()
		{
			IPEndPoint = ParseNspForKcp(nsp);
			InitKCP();
		}

        public KcpConnector(IPEndPoint iep, uint kcpKey, int dataShards, int parityShards)
			: this()
		{
			IPEndPoint = iep;
			KcpKey = kcpKey;
			this.dataShards = dataShards;
			this.parityShards = parityShards;
			InitKCP();
		}

		private KcpConnector()
		{
			nextKcpUpdateTime = 0u;
			Status = SocketStatus.Initial;
			pendingDataQueue = new Queue<byte[]>();
			KcpAsyncResult kcpAsyncResult = new KcpAsyncResult();
			kcpAsyncResult.IsCompleted = false;
			asyncResult = kcpAsyncResult;
			SyncRoot = new object();
			ackNoDelay = true;
			writeDelay = false;
		}

		public void InitKCP()
		{
			kcpClient = new KCP(KcpKey, KCPRouter.GetInstance().HandleKcpSend, dataShards, parityShards);
			kcpClient.NoDelay(1, 10, 2, 1);
			kcpClient.WndSize(128, 128);
			if (dataShards > 0 && parityShards > 0)
			{
				headerSize += 8;
				kcpClient.Mtu = (uint)(1400 - headerSize);
			}
			sendBuffer = new byte[kcpClient.Mss];
		}

		public void Dispose()
		{
			Disconnect(null);
		}

		public IAsyncResult Accept(Action<OpenResults, Exception> callback = null)
		{
			openCallback = callback;
			KCPRouter.GetInstance().Accept(this);
			return asyncResult;
		}

		public IAsyncResult Connect(Action<OpenResults, Exception> callback = null)
		{
			openCallback = callback;
			return KCPRouter.GetInstance().Connect(this);
		}

		public CloseResults Disconnect(Exception exception = null)
		{
			if (!KCPRouter.IsValid() || Status == SocketStatus.Initial || Status == SocketStatus.Closed)
			{
				return CloseResults.BeClosed;
			}
			return KCPRouter.GetInstance().Disconnect(this, exception);
		}

		public SendResults Send(byte[] data, int offset = 0, int count = -1)
		{
			if (data == null || data.Length == 0)
			{
				return SendResults.Success;
			}
			byte[] buffer = data;
			if (count != -1)
			{
				int value = (count > data.Length - offset) ? (data.Length - offset) : count;
				buffer = ArrayEx.Slice(data, offset, value);
			}
			else if (offset != 0)
			{
				buffer = ArrayEx.Slice(data, offset, data.Length - offset);
			}
			SendTo(buffer, IPEndPoint);
			return SendResults.Success;
		}

		public void TriggerOpenCallback(Exception ex)
		{
			if (openCallback != null)
			{
				try
				{
					openCallback((ex != null) ? OpenResults.Faild : OpenResults.Success, ex);
				}
				catch (Exception exception)
				{
					TriggerOnError(exception);
				}
			}
		}

		public void TriggerOnAccepted(KcpConnector connector)
		{
			connector.Status = SocketStatus.Establish;
			asyncResult.IsCompleted = true;
			if (this.OnAccepted != null)
			{
				try
				{
					this.OnAccepted(connector, null);
				}
				catch (Exception exception)
				{
					TriggerOnError(exception);
				}
			}
		}

		public void TriggerOnConnecting(KcpConnector connector)
		{
			connector.Status = SocketStatus.Connecting;
			if (this.OnConnecting != null)
			{
				try
				{
					this.OnConnecting(connector, connector.IPEndPoint);
				}
				catch (Exception exception)
				{
					TriggerOnError(exception);
				}
			}
		}

		public void TriggerOnConnected(KcpConnector connector)
		{
			connector.Status = SocketStatus.Establish;
			if (this.OnConnected != null)
			{
				try
				{
					this.OnConnected(connector, null);
				}
				catch (Exception exception)
				{
					TriggerOnError(exception);
				}
			}
		}

		public void TriggerOnClosed(KcpConnector connector, Exception ex)
		{
			connector.Status = SocketStatus.Closed;
			if (this.OnClosed != null)
			{
				try
				{
					this.OnClosed(connector, ex);
				}
				catch (Exception exception)
				{
					TriggerOnError(exception);
				}
			}
		}

		public void TriggerOnError(Exception exception)
		{
			if (this.OnError == null)
			{
				try
				{
					this.OnError(this, exception);
				}
				catch (Exception)
				{
				}
			}
		}

		public void Recv(byte[] data)
		{
			lock (SyncRoot)
			{
				int errcode = kcpClient.Input(data, ackNoDelay);
				if (errcode < 0)
				{
					TriggerOnError(new Exception("recv incorrect kcp package, errcode:" + errcode));
					return;
				}
				for (int peekSize = kcpClient.PeekSize(); peekSize > 0; peekSize = kcpClient.PeekSize())
				{
					byte[] array = new byte[peekSize];
					if (kcpClient.Recv(array) > 0)
					{
						ReceiveBytes += peekSize;
						TriggerOnMessage(this, new ArraySegment<byte>(array));
					}
				}
			}
		}

		public bool OnUpdate(uint currentTimeMS)
		{
			lock (SyncRoot)
			{
				CheckSend();
				if (currentTimeMS >= nextKcpUpdateTime)
				{
					kcpClient.Update(currentTimeMS);
					nextKcpUpdateTime = kcpClient.Check(currentTimeMS);
					return true;
				}
				return false;
			}
		}

		public void CheckSend()
		{
			if (pendingDataQueue.Count == 0 || kcpClient.WaitSnd() >= kcpClient.Snd_Wnd)
			{
				return;
			}
			while (pendingDataQueue.Count > 0)
			{
				byte[] array = pendingDataQueue.Dequeue();
				while (array.Length > kcpClient.Mss)
				{
					Array.Copy(array, sendBuffer, kcpClient.Mss);
					kcpClient.Send(sendBuffer, IPEndPoint);
					array = ArrayEx.Slice(array, (int)kcpClient.Mss, array.Length - (int)kcpClient.Mss);
				}
				kcpClient.Send(array, IPEndPoint);
				if (kcpClient.WaitSnd() >= kcpClient.Snd_Wnd || !writeDelay)
				{
					kcpClient.flush(false);
				}
			}
		}

		private void TriggerOnMessage(KcpConnector connector, ArraySegment<byte> data)
		{
			if (this.OnMessage != null)
			{
				try
				{
					this.OnMessage(connector, data);
				}
				catch (Exception exception)
				{
					TriggerOnError(exception);
				}
			}
		}

		private void TriggerOnBufferDrain()
		{
			if (this.OnBufferDrain != null)
			{
				try
				{
					this.OnBufferDrain(this);
				}
				catch (Exception exception)
				{
					TriggerOnError(exception);
				}
			}
		}

		private void TriggerOnBufferFull(byte[] data)
		{
			if (this.OnBufferFull == null)
			{
				try
				{
					this.OnBufferFull(this, data);
				}
				catch (Exception exception)
				{
					TriggerOnError(exception);
				}
			}
		}

		private IPEndPoint ParseNspForKcp(string nsp)
		{
			Uri uri = new Uri(nsp);
			if (uri.Query.Length > 0)
			{
				string[] array = uri.Query.Remove(0, 1).Split(new char[1]
				{
					'&'
				}, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < array.Length; i++)
				{
					string[] array2 = array[i].Split('=');
					if (array2.Length >= 2)
					{
						if (array2[0] == "datashards")
						{
							dataShards = int.Parse(array2[1]);
						}
						else if (array2[0] == "parityshards")
						{
							parityShards = int.Parse(array2[1]);
						}
					}
				}
			}
			byte[] array3 = new byte[4];
			new RNGCryptoServiceProvider().GetBytes(array3);
			KcpKey = BitConverter.ToUInt32(array3, 0);
			return new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port);
		}

		private bool SendTo(byte[] buffer, IPEndPoint iep)
		{
			lock (SyncRoot)
			{
				if (kcpClient.WaitSnd() >= kcpClient.Snd_Wnd)
				{
					if (pendingDataQueue.Count < 1024)
					{
						pendingDataQueue.Enqueue(buffer);
					}
					else
					{
						TriggerOnError(new Exception("too many data to send, please check if your network is ok."));
					}
					return true;
				}
				while (buffer.Length > kcpClient.Mss)
				{
					Array.Copy(buffer, sendBuffer, kcpClient.Mss);
					kcpClient.Send(sendBuffer, iep);
					buffer = ArrayEx.Slice(buffer, (int)kcpClient.Mss, buffer.Length - (int)kcpClient.Mss);
				}
				kcpClient.Send(buffer, iep);
				if (kcpClient.WaitSnd() >= kcpClient.Snd_Wnd || !writeDelay)
				{
					kcpClient.flush(false);
				}
			}
			return true;
		}
	}
}
