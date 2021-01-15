using System;
using System.Collections.Generic;

namespace Core.Socket.KCPSupport
{
	public class KCP
	{
        // KCP Segment Definition
        internal class Segment
		{
			internal uint conv;

			internal uint cmd;

			internal uint frg;

			internal uint wnd;

			internal uint ts;

			internal uint sn;

			internal uint una;

			internal uint resendts;

			internal uint rto;

			internal uint fastack;

			internal uint xmit;

			internal byte[] data;

			internal Segment(int size)
			{
				data = new byte[size];
			}
            // encode a segment into buffer
            internal int encode(byte[] ptr, int offset)
			{
				int offset_ = offset;
				offset += ikcp_encode32u(ptr, offset, conv);
				offset += ikcp_encode8u(ptr, offset, (byte)cmd);
				offset += ikcp_encode8u(ptr, offset, (byte)frg);
				offset += ikcp_encode16u(ptr, offset, (ushort)wnd);
				offset += ikcp_encode32u(ptr, offset, ts);
				offset += ikcp_encode32u(ptr, offset, sn);
				offset += ikcp_encode32u(ptr, offset, una);
				offset += ikcp_encode32u(ptr, offset, (uint)data.Length);
				return offset - offset_;
			}
		}

		public const int IKCP_RTO_NDL = 30;// no delay min rto

        public const int IKCP_RTO_MIN = 100;// normal min rto

        public const int IKCP_RTO_DEF = 200;

		public const int IKCP_RTO_MAX = 60000;

		public const int IKCP_CMD_PUSH = 81;// cmd: push data

        public const int IKCP_CMD_ACK = 82;// cmd: ack

        public const int IKCP_CMD_WASK = 83;// cmd: window probe (ask)

        public const int IKCP_CMD_WINS = 84;// cmd: window size (tell)

        public const int IKCP_ASK_SEND = 1;// need to send IKCP_CMD_WASK

        public const int IKCP_ASK_TELL = 2;// need to send IKCP_CMD_WINS

        public const int IKCP_WND_SND = 32;

		public const int IKCP_WND_RCV = 32;

		public const int IKCP_MTU_DEF = 1400;

		public const int IKCP_ACK_FAST = 3;

		public const int IKCP_INTERVAL = 100;

		public const int IKCP_OVERHEAD = 24;

		public const int IKCP_DEADLINK = 10;

		public const int IKCP_THRESH_INIT = 2;

		public const int IKCP_THRESH_MIN = 2;

		public const int IKCP_PROBE_INIT = 7000;// 7 secs to probe window size

        public const int IKCP_PROBE_LIMIT = 120000;// up to 120 secs to probe window

        // kcp members.
        private uint conv;

		private uint mtu;

		private uint mss;

		private uint state;

		private uint snd_una;

		private uint snd_nxt;

		private uint rcv_nxt;

		private uint ts_recent;

		private uint ts_lastack;

		private uint ssthresh;

		private uint rx_rttval;

		private uint rx_srtt;

		private uint rx_rto;

		private uint rx_minrto;

		private uint snd_wnd;

		private uint rcv_wnd;

		private uint rmt_wnd;

		private uint cwnd;

		private uint probe;

		private uint current;

		private uint interval;

		private uint ts_flush;

		private uint xmit;

		private uint nodelay;

		private uint updated;

		private uint ts_probe;

		private uint probe_wait;

		private uint dead_link;

		private uint incr;

		private Segment[] snd_queue = new Segment[0];

		private Segment[] rcv_queue = new Segment[0];

		private Segment[] snd_buf = new Segment[0];

		private Segment[] rcv_buf = new Segment[0];

		private uint[] acklist = new uint[0];

		private byte[] buffer;

		private int fastresend;

		private int nocwnd;

		private int logmask;

		private Action<byte[], int, int, object> output;

		internal FEC fec;

		internal uint pkt_idx;

		internal List<List<byte>> shards;

		internal int dataShards;

		internal int parityShards;

		private object userParam;

		internal uint Mtu
		{
			get
			{
				return mtu;
			}
			set
			{
				mtu = value;
				mss = mtu - 24;
			}
		}

		internal uint Snd_Wnd
		{
			get
			{
				return snd_wnd;
			}
		}

		internal uint Mss
		{
			get
			{
				return mss;
			}
		}

		internal uint GetInterval
		{
			get
			{
				return interval;
			}
		}
        // encode 8 bits unsigned int
        public static int ikcp_encode8u(byte[] p, int offset, byte c)
		{
			p[0 + offset] = c;
			return 1;
		}
        // decode 8 bits unsigned int
        public static int ikcp_decode8u(byte[] p, int offset, ref byte c)
		{
			c = p[0 + offset];
			return 1;
		}
        /* encode 16 bits unsigned int (lsb) */
        public static int ikcp_encode16u(byte[] p, int offset, ushort w)
		{
            p[0 + offset] = (byte)(w >> 0);
            p[1 + offset] = (byte)(w >> 8);
            return 2;
		}
        /* decode 16 bits unsigned int (lsb) */
        public static int ikcp_decode16u(byte[] p, int offset, ref ushort c)
		{
            ushort result = 0;
            result |= (ushort)p[0 + offset];
            result |= (ushort)(p[1 + offset] << 8);
            c = result;
            return 2;
		}
        /* encode 32 bits unsigned int (lsb) */
        public static int ikcp_encode32u(byte[] p, int offset, uint l)
		{
            p[0 + offset] = (byte)(l >> 0);
            p[1 + offset] = (byte)(l >> 8);
            p[2 + offset] = (byte)(l >> 16);
            p[3 + offset] = (byte)(l >> 24);
            return 4;
		}
        /* decode 32 bits unsigned int (lsb) */
        public static int ikcp_decode32u(byte[] p, int offset, ref uint c)
		{
            uint result = 0;
            result |= (uint)p[0 + offset];
            result |= (uint)(p[1 + offset] << 8);
            result |= (uint)(p[2 + offset] << 16);
            result |= (uint)(p[3 + offset] << 24);
            c = result;
            return 4;
		}

		public static byte[] slice(byte[] p, int start, int stop)
		{
			byte[] array = new byte[stop - start];
			Array.Copy(p, start, array, 0, array.Length);
			return array;
		}

		public static T[] slice<T>(T[] p, int start, int stop)
		{
			T[] array = new T[stop - start];
			int num = 0;
			for (int i = start; i < stop; i++)
			{
				array[num] = p[i];
				num++;
			}
			return array;
		}

		public static byte[] append(byte[] p, byte c)
		{
			byte[] array = new byte[p.Length + 1];
			Array.Copy(p, array, p.Length);
			array[p.Length] = c;
			return array;
		}

		public static T[] append<T>(T[] p, T c)
		{
			T[] array = new T[p.Length + 1];
			for (int i = 0; i < p.Length; i++)
			{
				array[i] = p[i];
			}
			array[p.Length] = c;
			return array;
		}

		public static T[] append<T>(T[] p, T[] cs)
		{
			T[] array = new T[p.Length + cs.Length];
			for (int i = 0; i < p.Length; i++)
			{
				array[i] = p[i];
			}
			for (int j = 0; j < cs.Length; j++)
			{
				array[p.Length + j] = cs[j];
			}
			return array;
		}

		private static uint _imin_(uint a, uint b)
		{
			if (a > b)
			{
				return b;
			}
			return a;
		}

		private static uint _imax_(uint a, uint b)
		{
			if (a < b)
			{
				return b;
			}
			return a;
		}

		private static uint _ibound_(uint lower, uint middle, uint upper)
		{
			return _imin_(_imax_(lower, middle), upper);
		}

		private static int _itimediff(uint later, uint earlier)
		{
			return (int)(later - earlier);
        }
        // create a new kcp control object, 'conv' must equal in two endpoint
        // from the same connection.
        public KCP(uint conv_, Action<byte[], int, int, object> output_, int dataShards, int parityShards)
		{
			conv = conv_;
			snd_wnd = IKCP_WND_SND;
			rcv_wnd = IKCP_WND_RCV;
			rmt_wnd = IKCP_WND_RCV;
			mtu = IKCP_MTU_DEF;
			mss = mtu - IKCP_OVERHEAD;
			rx_rto = IKCP_RTO_DEF;
			rx_minrto = IKCP_RTO_MIN;
			interval = IKCP_INTERVAL;
			ts_flush = IKCP_INTERVAL;
			ssthresh = IKCP_THRESH_INIT;
			dead_link = IKCP_DEADLINK;
			buffer = new byte[(mtu + IKCP_OVERHEAD) * 3];
			output = output_;
			if (dataShards > 0 && parityShards > 0)
			{
				fec = FEC.New(3 * (dataShards + parityShards), dataShards, parityShards);
				shards = new List<List<byte>>(new List<byte>[dataShards + parityShards]);
				this.dataShards = dataShards;
				this.parityShards = parityShards;
			}
		}

		public int PeekSize()
		{
			if (rcv_queue.Length == 0)
			{
				return -1;
			}
			Segment seq = rcv_queue[0];
			if (seq.frg == 0)
			{
				return seq.data.Length;
			}
			if (rcv_queue.Length < seq.frg + 1)
			{
				return -1;
			}
			int length = 0;
			foreach (Segment item in rcv_queue)
			{
                length += item.data.Length;
				if (item.frg == 0)
				{
					break;
				}
			}
			return length;
		}
        // user/upper level recv: returns size, returns below zero for EAGAIN
        public int Recv(byte[] buffer)
		{
			if (rcv_queue.Length == 0)
			{
				return -1;
			}
			int peekSize = PeekSize();
			if (0 > peekSize)
			{
				return -2;
			}
			if (peekSize > buffer.Length)
			{
				return -3;
			}
			bool fast_recover = false;
			if (rcv_queue.Length >= rcv_wnd)
			{
                fast_recover = true;
			}
            // merge fragment.
            int count = 0;
			int len = 0;
			Segment[] array = rcv_queue;
			foreach (Segment seg in rcv_queue)
			{
				Array.Copy(seg.data, 0, buffer, len, seg.data.Length);
                len += seg.data.Length;
                count++;
				if (seg.frg == 0)
				{
					break;
				}
			}
			if (0 < count)
			{
				rcv_queue = slice(rcv_queue, count, rcv_queue.Length);
			}
            // move available data from rcv_buf -> rcv_queue
            count = 0;
			foreach (Segment seg in rcv_buf)
			{
				if (seg.sn != rcv_nxt || rcv_queue.Length >= rcv_wnd)
				{
					break;
				}
				rcv_queue = append(rcv_queue, seg);
				rcv_nxt++;
                count++;
			}
			if (0 < count)
			{
				rcv_buf = slice(rcv_buf, count, rcv_buf.Length);
			}
            // fast recover
            if (rcv_queue.Length < rcv_wnd && fast_recover)
			{
                // ready to send back IKCP_CMD_WINS in ikcp_flush
                // tell remote my window size
                probe |= IKCP_ASK_TELL;
            }
            return len;
		}
        // user/upper level send, returns below zero for error
        public int Send(byte[] buffer, object param = null)
		{
			if (buffer.Length == 0)
			{
				return -1;
			}
            int count = 0;
            if (buffer.Length < mss)
            {
                count = 1;
            }  
            else
            {
                count = (int)(buffer.Length + mss - 1) / (int)mss;
            } 
            if (255 < count)
            {
                return -2;
            }

            if (0 == count)
            {
                count = 1;
            }

            int offset = 0;
			for (int i = 0; i < count; i++)
			{
				int size = 0;
                if (buffer.Length - offset > mss)
                    size = (int)mss;
                else
                    size = buffer.Length - offset;
                Segment seg = new Segment(size);
				Array.Copy(buffer, offset, seg.data, 0, size);
                offset += size;
                seg.frg = (uint)(count - i - 1);
				snd_queue = append(snd_queue, seg);
			}
			userParam = param;
			return 0;
		}
        // update ack.
        private void update_ack(int rtt)
		{
			if (rx_srtt == 0)
			{
				rx_srtt = (uint)rtt;
				rx_rttval = (uint)rtt / 2u;
			}
			else
			{
				int delta = rtt - (int)rx_srtt;
				if (0 > delta)
				{
                    delta = -delta;
				}
				rx_rttval = (3 * rx_rttval + (uint)delta) / 4;
				rx_srtt = (uint)((7 * rx_srtt + rtt) / 8);
				if (rx_srtt < 1)
				{
					rx_srtt = 1;
				}
			}
			int middle = (int)(rx_srtt + _imax_(1, 4 * rx_rttval));
			rx_rto = _ibound_(rx_minrto, (uint)middle, IKCP_RTO_MAX);
		}

		private void shrink_buf()
		{
			if (snd_buf.Length != 0)
			{
				snd_una = snd_buf[0].sn;
			}
			else
			{
				snd_una = snd_nxt;
			}
		}

		private void parse_ack(uint sn)
		{
			if (_itimediff(sn, snd_una) < 0 || _itimediff(sn, snd_nxt) >= 0)
			{
				return;
			}
			int after_idx = 0;
			int index = 0;
			while (true)
			{
				if (index < snd_buf.Length)
				{
					Segment seg = snd_buf[index];
					if (sn == seg.sn)
					{
						break;
					}
					seg.fastack++;
                    after_idx++;
                    index++;
					continue;
				}
				return;
			}
			snd_buf = append(slice(snd_buf, 0, after_idx), slice(snd_buf, after_idx + 1, snd_buf.Length));
		}

		private void parse_una(uint una)
		{
			int count = 0;
			foreach (Segment seg in snd_buf)
			{
				if (_itimediff(una, seg.sn) <= 0)
				{
					break;
				}
                count++;
			}
			if (0 < count)
			{
				snd_buf = slice(snd_buf, count, snd_buf.Length);
			}
		}

		private void ack_push(uint sn, uint ts)
		{
			acklist = append(acklist, new uint[2]
			{
				sn,
				ts
			});
		}

		private void ack_get(int p, ref uint sn, ref uint ts)
		{
			sn = acklist[p * 2 + 0];
			ts = acklist[p * 2 + 1];
		}

		private void parse_data(Segment newseg)
		{
			uint sn = newseg.sn;
			if (_itimediff(sn, rcv_nxt + rcv_wnd) >= 0 || _itimediff(sn, rcv_nxt) < 0)
			{
				return;
			}
			int n = rcv_buf.Length - 1;
			int after_idx = -1;
			bool repeat = false;
			for (int i = n; i >= 0; i--)
			{
				Segment seg = rcv_buf[i];
				if (seg.sn == sn)
				{
                    repeat = true;
					break;
				}
				if (_itimediff(sn, seg.sn) > 0)
				{
                    after_idx = i;
					break;
				}
			}
			if (!repeat)
			{
				if (after_idx == -1)
				{
					rcv_buf = append(new Segment[1]
					{
						newseg
					}, rcv_buf);
				}
				else
				{
					rcv_buf = append(slice(rcv_buf, 0, after_idx + 1), append(new Segment[1]
					{
						newseg
					}, slice(rcv_buf, after_idx + 1, rcv_buf.Length)));
				}
			}
            // move available data from rcv_buf -> rcv_queue
            int count = 0;
			foreach (Segment seg in rcv_buf)
			{
				if (seg.sn != rcv_nxt || rcv_queue.Length >= rcv_wnd)
				{
					break;
				}
				rcv_queue = append(rcv_queue, seg);
				rcv_nxt++;
                count++;
			}
			if (0 < count)
			{
				rcv_buf = slice(rcv_buf, count, rcv_buf.Length);
			}
		}
        // when you received a low level packet (eg. UDP packet), call it
        public int Input(byte[] data, bool ackNoDelay)
		{
			ulong errorCount = 0;
			ulong recoverCount = 0;
			ulong successCount = 0;
			int errorCode = 0;
			if (fec != null && fec.isEnabled())
			{
                // decode FEC packet
                FEC.fecPacket pkt = FEC.Decode(data, data.Length);
				if (pkt.flag == FEC.typeData)
				{
                    List<byte> newData = new List<byte>(pkt.data);
                    // we have 2B size, ignore for typeData
                    newData.RemoveRange(0, 2);
                    errorCode = doInput(newData.ToArray(), ackNoDelay);
					if (errorCode != 0)
					{
                        errorCount++;
					}
				}
                // allow FEC packet processing with correct flags.
                if (pkt.flag == FEC.typeData || pkt.flag == FEC.typeFEC)
				{
					try
					{
                        // input to FEC, and see if we can recover data.
                        List<List<byte>> recovered = fec.Input(pkt);
                        // we have some data recovered.
                        for (int i = 0; i < recovered.Count; i++)
						{
							List<byte> recoveredData = recovered[i];
                            // recovered data has at least 2B size.
                            if (recoveredData.Count > 2)
							{
								byte[] p = recoveredData.ToArray();
                                // decode packet size, which is also recovered.
                                ushort sz = 0;
                                ikcp_decode16u(p, 0, ref sz);
                                // the recovered packet size must be in the correct range.
                                if (sz >= 2 && sz <= recoveredData.Count)
								{
                                    // input proper data to kcp
                                    recoveredData.RemoveRange(sz, recoveredData.Count - sz);
                                    recoveredData.RemoveRange(0, 2);
                                    errorCode = doInput(recoveredData.ToArray(), ackNoDelay);
									if (errorCode == 0)
									{
                                        successCount++;
									}
									else
									{
                                        errorCount++;
									}
								}
								else
								{
                                    recoverCount++;
								}
							}
							else
							{
                                recoverCount++;
							}
						}
						return errorCode;
					}
					catch (Exception)
					{
						return -4;
					}
				}
			}
			else
			{
                // fec disabled
                errorCode = doInput(data, ackNoDelay);
				if (errorCode != 0)
				{
                    errorCount++;
				}
			}
			return errorCode;
		}

		private int doInput(byte[] data, bool ackNoDelay = false)
		{
			uint earlier = snd_una;
			if (data.Length < IKCP_OVERHEAD)
			{
				return -1;
			}
			int offset = 0;
			while (true)
			{
				uint ts = 0;
				uint sn = 0;
				uint length = 0;
				uint una = 0;
				uint conv_ = 0;
				ushort wnd = 0;
				byte cmd = 0;
				byte frg = 0;
				if (data.Length - offset < IKCP_OVERHEAD)
				{
					break;
				}
                offset += ikcp_decode32u(data, offset, ref conv_);
				if (conv != conv_)
				{
					return -1;
				}
                offset += ikcp_decode8u(data, offset, ref cmd);
                offset += ikcp_decode8u(data, offset, ref frg);
                offset += ikcp_decode16u(data, offset, ref wnd);
                offset += ikcp_decode32u(data, offset, ref ts);
                offset += ikcp_decode32u(data, offset, ref sn);
                offset += ikcp_decode32u(data, offset, ref una);
                offset += ikcp_decode32u(data, offset, ref length);
				if (data.Length - offset < length)
				{
					return -2;
				}
				if ((uint)(cmd - IKCP_CMD_PUSH) > 3)
				{
					return -3;
				}
				rmt_wnd = wnd;
				parse_una(una);
				shrink_buf();
				if (IKCP_CMD_ACK == cmd)
				{
					if (_itimediff(current, ts) >= 0)
					{
						update_ack(_itimediff(current, ts));
					}
					parse_ack(sn);
					shrink_buf();
				}
				else if (IKCP_CMD_PUSH == cmd)
				{
					if (_itimediff(sn, rcv_nxt + rcv_wnd) < 0)
					{
						ack_push(sn, ts);
						if (_itimediff(sn, rcv_nxt) >= 0)
						{
							Segment seg = new Segment((int)length);
                            seg.conv = conv_;
                            seg.cmd = cmd;
                            seg.frg = frg;
                            seg.wnd = wnd;
                            seg.ts = ts;
                            seg.sn = sn;
                            seg.una = una;
							if (length != 0)
							{
								Array.Copy(data, offset, seg.data, 0, length);
							}
							parse_data(seg);
						}
					}
				}
				else if (IKCP_CMD_WASK == cmd)
				{
                    // ready to send back IKCP_CMD_WINS in Ikcp_flush
                    // tell remote my window size
                    probe |= IKCP_ASK_TELL;
				}
                else if (IKCP_CMD_WINS == cmd)
                {
                    // do nothing
                }
                else
                {
                    return -3;
                }
                offset += (int)length;
			}
			if (_itimediff(snd_una, earlier) > 0 && cwnd < rmt_wnd)
			{
				uint mss_ = mss;
				if (cwnd < ssthresh)
				{
					cwnd++;
					incr += mss_;
				}
				else
				{
					if (incr < mss_)
					{
						incr = mss_;
					}
					incr += mss_ * mss_ / incr + mss_ / 16;
					if ((cwnd + 1) * mss_ <= incr)
					{
						cwnd++;
					}
				}
				if (cwnd > rmt_wnd)
				{
					cwnd = rmt_wnd;
					incr = rmt_wnd * mss_;
				}
			}
			return 0;
		}

		public uint currentMs()
		{
			return (uint)DateTime.UtcNow.Subtract(DateTime.MinValue).TotalMilliseconds;
		}

		private int wnd_unused()
		{
			if (rcv_queue.Length < rcv_wnd)
			{
				return (int)rcv_wnd - rcv_queue.Length;
			}
			return 0;
		}
        // flush pending data
        internal void flush(bool ackOnly = false)
		{
			uint current_ = current;
			byte[] buffer_ = buffer;
			int change = 0;
			int lost = 0;
			if (updated == 0)
			{
				return;
			}
			Segment seg = new Segment(0);
            seg.conv = conv;
            seg.cmd = IKCP_CMD_ACK;
            seg.wnd = (uint)wnd_unused();
            seg.una = rcv_nxt;
            // flush acknowledges
            int count = acklist.Length / 2;
			int offset = 0;
			for (int i = 0; i < count; i++)
			{
				if (offset + IKCP_OVERHEAD > mtu)
				{
					output(buffer, 0, offset, userParam);
                    offset = 0;
				}
				ack_get(i, ref seg.sn, ref seg.ts);
                offset += seg.encode(buffer, offset);
			}
			acklist = new uint[0];
			if (rmt_wnd == 0)
			{
				if (probe_wait == 0)
				{
					probe_wait = IKCP_PROBE_INIT;
					ts_probe = current + probe_wait;
				}
				else if (_itimediff(current, ts_probe) >= 0)
				{
					if (probe_wait < IKCP_PROBE_INIT)
					{
						probe_wait = IKCP_PROBE_INIT;
					}
					probe_wait += probe_wait / 2;
					if (probe_wait > IKCP_PROBE_LIMIT)
					{
						probe_wait = IKCP_PROBE_LIMIT;
					}
					ts_probe = current + probe_wait;
					probe |= IKCP_ASK_SEND;
				}
			}
			else
			{
				ts_probe = 0u;
				probe_wait = 0u;
			}
            // flush window probing commands
            if ((probe & IKCP_ASK_SEND) != 0)
			{
                seg.cmd = IKCP_CMD_WASK;
				if (offset + IKCP_OVERHEAD > (int)mtu)
				{
					output(buffer, 0, offset, userParam);
                    offset = 0;
				}
                offset += seg.encode(buffer, offset);
			}
			probe = 0u;
            // calculate window size
            uint cwnd_ = _imin_(snd_wnd, rmt_wnd);
			if (nocwnd == 0)
			{
                cwnd_ = _imin_(cwnd, cwnd_);
			}
            count = 0;
			for (int k = 0; k < snd_queue.Length; k++)
			{
				if (_itimediff(snd_nxt, snd_una + cwnd_) >= 0)
				{
					break;
				}
				Segment newseg = snd_queue[k];
                newseg.conv = conv;
                newseg.cmd = IKCP_CMD_PUSH;
                newseg.wnd = seg.wnd;
                newseg.ts = current_;
                newseg.sn = snd_nxt;
                newseg.una = rcv_nxt;
                newseg.resendts = current_;
                newseg.rto = rx_rto;
                newseg.fastack = 0u;
                newseg.xmit = 0u;
				snd_buf = append(snd_buf, newseg);
				snd_nxt++;
                count++;
			}
			if (0 < count)
			{
				snd_queue = slice(snd_queue, count, snd_queue.Length);
			}
            // calculate resent
            uint resent = (uint)fastresend;
			if (fastresend <= 0)
			{
                resent = 0xffffffff;
            }
			uint rtomin = rx_rto >> 3;
			if (nodelay != 0)
			{
                rtomin = 0;
			}
			foreach (Segment segment in snd_buf)
			{
				bool needsend = false;
				_itimediff(current_, segment.resendts);
				if (segment.xmit == 0)
				{
                    needsend = true;
					segment.xmit++;
					segment.rto = rx_rto;
					segment.resendts = current_ + segment.rto + rtomin;
				}
				else if (_itimediff(current_, segment.resendts) >= 0)
				{
                    needsend = true;
					segment.xmit++;
					xmit++;
					if (nodelay == 0)
					{
						segment.rto += rx_rto;
					}
					else
					{
						segment.rto += rx_rto / 2;
					}
					segment.resendts = current_ + segment.rto;
                    lost = 1;
				}
				else if (segment.fastack >= resent)
				{
                    needsend = true;
					segment.xmit++;
					segment.fastack = 0u;
					segment.resendts = current_ + segment.rto;
                    change++;
				}
				if (needsend)
				{
					segment.ts = current_;
					segment.wnd = seg.wnd;
					segment.una = rcv_nxt;
					int need = IKCP_OVERHEAD + segment.data.Length;
					if (offset + need > mtu)
					{
						output(buffer, 0, offset, userParam);
                        offset = 0;
					}
                    offset += segment.encode(buffer, offset);
					if (segment.data.Length != 0)
					{
						Array.Copy(segment.data, 0, buffer, offset, segment.data.Length);
                        offset += segment.data.Length;
					}
					if (segment.xmit >= dead_link)
					{
						state = 0u;
					}
				}
			}
			if (offset > 0)
			{
				output(buffer, 0, offset, userParam);
                offset = 0;
			}
			if (change != 0)
			{
				uint inflight = snd_nxt - snd_una;
				ssthresh = inflight / 2;
				if (ssthresh < IKCP_THRESH_MIN)
				{
					ssthresh = IKCP_THRESH_MIN;
				}
				cwnd = ssthresh + resent;
				incr = cwnd * mss;
			}
			if (lost != 0)
			{
				ssthresh = cwnd / 2;
				if (ssthresh < IKCP_THRESH_MIN)
				{
					ssthresh = IKCP_THRESH_MIN;
				}
				cwnd = 1;
				incr = mss;
			}
			if (cwnd < 1)
			{
				cwnd = 1;
				incr = mss;
			}
		}
        // update state (call it repeatedly, every 10ms-100ms), or you can ask
        // ikcp_check when to call it again (without ikcp_input/_send calling).
        // 'current' - current timestamp in millisec.
        public void Update(uint current_)
		{
			current = current_;
			if (updated == 0)
			{
				updated = 1u;
				ts_flush = current;
			}
			int slap = _itimediff(current, ts_flush);
			if (slap >= 10000 || slap < -10000)
			{
				ts_flush = current;
                slap = 0;
			}
			if (slap >= 0)
			{
				ts_flush += interval;
				if (_itimediff(current, ts_flush) >= 0)
				{
					ts_flush = current + interval;
				}
				flush(false);
			}
		}
        // Determine when should you invoke ikcp_update:
        // returns when you should invoke ikcp_update in millisec, if there
        // is no ikcp_input/_send calling. you can call ikcp_update in that
        // time, instead of call update repeatly.
        // Important to reduce unnacessary ikcp_update invoking. use it to
        // schedule ikcp_update (eg. implementing an epoll-like mechanism,
        // or optimize ikcp_update when handling massive kcp connections)
        public uint Check(uint current_)
		{
			if (updated == 0)
			{
				return current_;
			}
			uint ts_flush_ = ts_flush;
            int tm_flush_ = 0x7fffffff;
            int tm_packet = 0x7fffffff;
            int minimal = 0;
			if (_itimediff(current_, ts_flush_) >= 10000 || _itimediff(current_, ts_flush_) < -10000)
			{
                ts_flush_ = current_;
			}
			if (_itimediff(current_, ts_flush_) >= 0)
			{
				return current_;
			}
            tm_flush_ = _itimediff(ts_flush_, current_);
			for (int i = 0; i < snd_buf.Length; i++)
			{
				int diff = _itimediff(snd_buf[i].resendts, current_);
				if (diff <= 0)
				{
					return current_;
				}
				if (diff < tm_packet)
				{
                    tm_packet = diff;
				}
			}
            minimal = tm_packet;
			if (tm_packet >= tm_flush_)
			{
                minimal = tm_flush_;
			}
			if (minimal >= interval)
			{
                minimal = (int)interval;
			}
			return (uint)((int)current_ + minimal);
		}

		public int SetMtu(int mtu_)
		{
			if (mtu_ < 50 || mtu_ < IKCP_OVERHEAD)
			{
				return -1;
			}
			byte[] buffer_ = new byte[(mtu_ + IKCP_OVERHEAD) * 3];
			if (buffer_ == null)
			{
				return -2;
			}
			mtu = (uint)mtu_;
			mss = mtu - IKCP_OVERHEAD;
			buffer = buffer_;
			return 0;
		}

		public int Interval(int interval_)
		{
			if (interval_ > 5000)
			{
				interval_ = 5000;
			}
			else if (interval_ < 10)
			{
				interval_ = 10;
			}
			interval = (uint)interval_;
			return 0;
		}
        // fastest: ikcp_nodelay(kcp, 1, 20, 2, 1)
        // nodelay: 0:disable(default), 1:enable
        // interval: internal update timer interval in millisec, default is 100ms
        // resend: 0:disable fast resend(default), 1:enable fast resend
        // nc: 0:normal congestion control(default), 1:disable congestion control
        public int NoDelay(int nodelay_, int interval_, int resend_, int nc_)
		{
			if (nodelay_ > 0)
			{
				nodelay = (uint)nodelay_;
				if (nodelay_ != 0)
				{
					rx_minrto = IKCP_RTO_NDL;
				}
				else
				{
					rx_minrto = IKCP_RTO_MIN;
				}
			}
			if (interval_ >= 0)
			{
				if (interval_ > 5000)
				{
					interval_ = 5000;
				}
				else if (interval_ < 10)
				{
					interval_ = 10;
				}
				interval = (uint)interval_;
			}
			if (resend_ >= 0)
			{
				fastresend = resend_;
			}
			if (nc_ >= 0)
			{
				nocwnd = nc_;
			}
			return 0;
		}
        // set maximum window size: sndwnd=32, rcvwnd=32 by default
        public int WndSize(int sndwnd, int rcvwnd)
		{
			if (sndwnd > 0)
			{
				snd_wnd = (uint)sndwnd;
			}
			if (rcvwnd > 0)
			{
				rcv_wnd = (uint)rcvwnd;
			}
			return 0;
		}
        // get how many packet is waiting to be sent
        public int WaitSnd()
		{
			return snd_buf.Length + snd_queue.Length;
		}
	}
}
