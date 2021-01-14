using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Core.Socket
{
	internal sealed class KCPRouter
	{
		private struct Packet
		{
			internal byte[] Data;

			internal EndPoint Ep;
		}

		private const uint IOCIn = 2147483648u;

		private const uint IOCVendor = 402653184u;

		private static KCPRouter instance;

		private readonly uint sIOUDPConnreset = 2550136844u;

		private readonly float connectTimeoutInterval = 30f;

		private readonly byte[] receiveBuffer;

		private float timestamp;

		private float connectTimeout;

		private Thread timeOutThread;

		private SocketShutdown socketShutdown = SocketShutdown.Both;

		private bool threadIsRunning;

		private Thread parseDataThread;

		private Thread kcpRecvThread;

		private Thread updateThread;

		private KcpConnector master;

		private Dictionary<IPEndPoint, KcpConnector> connectors;

		private Queue<KcpConnector> invalidConnectors = new Queue<KcpConnector>();

		[CompilerGenerated]
		private ISocketProxy _003CSocket_003Ek__BackingField;

		private List<Packet> recvList = new List<Packet>();

		private List<Packet> parseList = new List<Packet>();

		private ISocketProxy Socket
		{
			[CompilerGenerated]
			get
			{
				return _003CSocket_003Ek__BackingField;
			}
			[CompilerGenerated]
			set
			{
				_003CSocket_003Ek__BackingField = value;
			}
		}

		private KCPRouter()
		{
			connectors = new Dictionary<IPEndPoint, KcpConnector>();
			receiveBuffer = new byte[8192];
			Reset();
		}

		private void StartThreads()
		{
			threadIsRunning = true;
			parseDataThread = new Thread(ParseDataThread);
			parseDataThread.IsBackground = true;
			parseDataThread.Priority = ThreadPriority.Highest;
			parseDataThread.Start();
			kcpRecvThread = new Thread(AsyncRecv);
			kcpRecvThread.IsBackground = true;
			kcpRecvThread.Priority = ThreadPriority.Highest;
			kcpRecvThread.Start();
			updateThread = new Thread(UpdateThread);
			updateThread.IsBackground = true;
			updateThread.Priority = ThreadPriority.Highest;
			updateThread.Start();
		}

		public static bool IsValid()
		{
			return instance != null;
		}

		public static KCPRouter GetInstance()
		{
			if (instance == null)
			{
				instance = new KCPRouter();
			}
			return instance;
		}

		public bool Accept(KcpConnector connector)
		{
			master = connector;
			if (master.IPEndPoint == null)
			{
				return false;
			}
			Reset();
			connector.isServer = true;
			Socket = SocketProxyFactory.Create("udp");
			Socket.Bind(master.IPEndPoint);
			Socket.IOControl((int)sIOUDPConnreset, new byte[1]
			{
				Convert.ToByte(false)
			}, null);
			master.TriggerOnAccepted(master);
			master.TriggerOpenCallback(null);
			StartThreads();
			return true;
		}

		public IAsyncResult Connect(KcpConnector connector)
		{
			master = connector;
			if (master.IPEndPoint == null)
			{
				throw new InvalidOperationException("Please call Connect(IPEndPoint,AddressFamily) first");
			}
			connectTimeout = timestamp + connectTimeoutInterval;
			master.TriggerOnConnecting(master);
			Reset();
			Socket = SocketProxyFactory.Create("udp");
			timeOutThread = new Thread(WatchTimeOut);
			timeOutThread.IsBackground = true;
			timeOutThread.Start();
			StartThreads();
			return BeginConnect(Socket, master.IPEndPoint);
		}

		public void Dispose()
		{
			Disconnect(master);
		}

		public CloseResults Disconnect(KcpConnector connector, Exception exception = null)
		{
			AddInvalidConnector(connector);
			if (connector != master)
			{
				return CloseResults.Closed;
			}
			return Close(exception);
		}

		private void WatchTimeOut()
		{
			int tickCount = Environment.TickCount;
			do
			{
				if (master.Status != SocketStatus.Establish)
				{
					Thread.Sleep(1000);
					timestamp += (float)(Environment.TickCount - tickCount) * 0.001f;
					tickCount = Environment.TickCount;
					continue;
				}
				return;
			}
			while (master.Status != SocketStatus.Connecting || !(connectTimeout <= timestamp));
			Close(new TimeoutException("Socket connect timeout"));
		}

		private IAsyncResult BeginConnect(ISocketProxy socket, IPEndPoint ipEndPoint)
		{
			master.TriggerOnConnecting(master);
			try
			{
				return socket.BeginConnect(ipEndPoint, EndConnect, socket);
			}
			catch (Exception ex)
			{
				master.TriggerOpenCallback(ex);
				Close(ex);
				return null;
			}
		}

		private void EndConnect(IAsyncResult result)
		{
			ISocketProxy socketProxy = (ISocketProxy)result.AsyncState;
			try
			{
				socketProxy.EndConnect(result);
			}
			catch (Exception ex)
			{
				master.TriggerOpenCallback(ex);
				Close(ex);
				return;
			}
			if (master.Status == SocketStatus.Connecting)
			{
				master.TriggerOpenCallback(null);
				master.TriggerOnConnected(master);
				connectors.Add(master.IPEndPoint, master);
			}
		}

		private CloseResults Close(Exception exception)
		{
			try
			{
				if (Socket.Connected)
				{
					Socket.Shutdown(socketShutdown);
				}
			}
			catch (Exception exception2)
			{
				master.TriggerOnError(exception2);
			}
			finally
			{
				KcpDispose();
				try
				{
					Socket.Close();
				}
				finally
				{
					Reset();
					master.TriggerOnClosed(master, exception);
					instance = null;
				}
			}
			return CloseResults.Closed;
		}

		private void Reset()
		{
			Socket = null;
			if (timeOutThread != null)
			{
				if (timeOutThread.IsAlive)
				{
					timeOutThread.Abort();
				}
				timeOutThread = null;
			}
		}

		private void KcpDispose()
		{
			threadIsRunning = false;
			parseDataThread = null;
			kcpRecvThread = null;
			updateThread = null;
		}

		private void UpdateThread()
		{
			while (threadIsRunning)
			{
				lock (connectors)
				{
					foreach (KeyValuePair<IPEndPoint, KcpConnector> connector in connectors)
					{
						uint currentTimeMS = connector.Value.Kcp.currentMs();
						connector.Value.OnUpdate(currentTimeMS);
					}
					lock (invalidConnectors)
					{
						while (invalidConnectors.Count > 0)
						{
							using (KcpConnector kcpConnector = invalidConnectors.Dequeue())
							{
								connectors.Remove(kcpConnector.IPEndPoint);
								kcpConnector.Dispose();
							}
						}
					}
				}
				Thread.Sleep(1);
			}
		}

		private void ParseDataThread()
		{
			while (threadIsRunning)
			{
				lock (recvList)
				{
					parseList.AddRange(recvList);
					recvList.Clear();
				}
				if (parseList.Count > 0)
				{
					for (int i = 0; i < parseList.Count; i++)
					{
						Packet packet = parseList[i];
						byte[] data = packet.Data;
						IPEndPoint iPEndPoint = (IPEndPoint)packet.Ep;
						KcpConnector value = null;
						lock (connectors)
						{
							if (!connectors.TryGetValue(iPEndPoint, out value))
							{
								uint kcpKey = 0u;
								bool flag = false;
								if (master.Kcp.fec != null && master.Kcp.fec.isEnabled())
								{
									if (BitConverter.ToUInt16(data, 4) == 241)
									{
										kcpKey = BitConverter.ToUInt32(data, 8);
										flag = true;
									}
								}
								else
								{
									kcpKey = BitConverter.ToUInt32(data, 0);
									flag = true;
								}
								if (flag)
								{
									value = new KcpConnector(iPEndPoint, kcpKey, master.dataShards, master.parityShards);
									connectors.Add(value.IPEndPoint, value);
									master.TriggerOnConnected(value);
								}
							}
							value.Recv(data);
						}
					}
					parseList.Clear();
				}
				else
				{
					Thread.Sleep(1);
				}
			}
		}

		private void AsyncRecv()
		{
			EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
			if (!master.isServer)
			{
				remoteEP = master.IPEndPoint;
			}
			while (threadIsRunning)
			{
				try
				{
					int num = Socket.ReceiveFrom(receiveBuffer, receiveBuffer.Length, SocketFlags.None, ref remoteEP);
					if (num > 0)
					{
						byte[] data = ArrayEx.Slice(receiveBuffer, 0, num);
						lock (recvList)
						{
							List<Packet> list = recvList;
							Packet item = default(Packet);
							item.Data = data;
							item.Ep = remoteEP;
							list.Add(item);
						}
					}
				}
				catch (Exception exception)
				{
					master.TriggerOnError(exception);
				}
			}
		}

		internal void HandleKcpSend(byte[] buff, int offset, int size, object param)
		{
			if (Socket == null)
			{
				return;
			}
			IPEndPoint iPEndPoint = (IPEndPoint)param;
			if (iPEndPoint == null)
			{
				return;
			}
			if (master.Kcp.fec != null && master.Kcp.fec.isEnabled())
			{
				try
				{
					byte[] array = new byte[size + 8];
					Array.Copy(buff, offset, array, 8, size);
					master.Kcp.fec.MarkData(array, (ushort)size);
					SocketSend(array, 0, array.Length, iPEndPoint);
					List<byte> list = new List<byte>(array);
					list.RemoveRange(0, 6);
					master.Kcp.shards[(int)master.Kcp.pkt_idx] = list;
					master.Kcp.pkt_idx++;
					if (master.Kcp.pkt_idx == master.Kcp.dataShards)
					{
						master.Kcp.fec.Encode(master.Kcp.shards);
						for (int i = master.Kcp.dataShards; i < master.Kcp.dataShards + master.Kcp.parityShards; i++)
						{
							array = new byte[master.Kcp.shards[i].Count + 6];
							Array.Copy(master.Kcp.shards[i].ToArray(), 0, array, 6, master.Kcp.shards[i].Count);
							master.Kcp.fec.MarkFEC(array);
							SocketSend(array, 0, array.Length, iPEndPoint);
						}
						master.Kcp.pkt_idx = 0u;
					}
				}
				catch (Exception exception)
				{
					master.TriggerOnError(exception);
				}
			}
			else
			{
				SocketSend(buff, offset, size, iPEndPoint);
			}
		}

		private SendResults SocketSend(byte[] buff, int offset, int size, IPEndPoint iep)
		{
			try
			{
				if (size > 0)
				{
					Socket.SendTo(buff, offset, size, SocketFlags.None, iep);
				}
			}
			catch (Exception exception)
			{
				Close(exception);
				return SendResults.Faild;
			}
			return SendResults.Success;
		}

		private void AddInvalidConnector(KcpConnector connector)
		{
			lock (invalidConnectors)
			{
				invalidConnectors.Enqueue(connector);
			}
		}
	}
}
