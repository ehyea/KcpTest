using System;
using System.Collections.Generic;

namespace Core.Socket.KCPSupport
{
	public class FEC
	{
		public class fecPacket
		{
			public uint seqid;

			public ushort flag;

			public List<byte> data;

			public uint ts;
		}

		internal const int fecHeaderSize = 6;

		internal const int fecHeaderSizePlus2 = 8;

		internal const ushort typeData = 241;

		internal const ushort typeFEC = 242;

		internal const int fecExpire = 30000;

		private List<fecPacket> rx = new List<fecPacket>();

		private int rxlimit;

		private int dataShards;

		private int parityShards;

		private int totalShards;

		private uint next;

		private ReedSolomon enc;

		private uint paws;

		private uint lastCheck;

		public FEC(ReedSolomon enc)
		{
			this.enc = enc;
		}

		public static FEC New(int rxlimit, int dataShards, int parityShards)
		{
			if (dataShards <= 0 || parityShards <= 0)
			{
				throw new ArgumentException("invalid arguments");
			}
			if (rxlimit < dataShards + parityShards)
			{
				throw new ArgumentException("invalid arguments");
			}
			FEC fec = new FEC(ReedSolomon.New(dataShards, parityShards));
            fec.rxlimit = rxlimit;
            fec.dataShards = dataShards;
            fec.parityShards = parityShards;
            fec.totalShards = dataShards + parityShards;
            fec.paws = (uint)((int)(0xffffffff / (uint)fec.totalShards - 1) * fec.totalShards);
			return fec;
		}

		public static fecPacket Decode(byte[] data, int sz)
		{
			fecPacket pkt = new fecPacket();
			int offset = 0;
            offset += KCP.ikcp_decode32u(data, offset, ref pkt.seqid);
            offset += KCP.ikcp_decode16u(data, offset, ref pkt.flag);
            pkt.ts = (uint)DateTime.UtcNow.Subtract(DateTime.MinValue).TotalMilliseconds;
            pkt.data = new List<byte>(data);
			if (data.Length > sz)
			{
                pkt.data.RemoveRange(sz, data.Length - sz);
			}
            pkt.data.RemoveRange(0, 6);
			return pkt;
		}

		public bool isEnabled()
		{
			if (dataShards > 0)
			{
				return parityShards > 0;
			}
			return false;
		}

		public List<List<byte>> Input(fecPacket pkt)
		{
			List<List<byte>> recovered = new List<List<byte>>();
			uint now = (uint)DateTime.UtcNow.Subtract(DateTime.MinValue).TotalMilliseconds;
			if (now - lastCheck >= fecExpire)
			{
				List<fecPacket> removePkts = new List<fecPacket>();
				for (int i = 0; i < rx.Count; i++)
				{
					fecPacket fecPkt = rx[i];
					if (now - fecPkt.ts > fecExpire)
					{
                        removePkts.Add(fecPkt);
					}
				}
				for (int j = 0; j < removePkts.Count; j++)
				{
					rx.Remove(removePkts[j]);
				}
				lastCheck = now;
			}
            // insertion
            int n = rx.Count - 1;
			int insertIdx = 0;
			for (int i = n; i >= 0; i--)
			{
				if (pkt.seqid == rx[i].seqid)
				{
					return recovered;
				}
				if (pkt.seqid > rx[i].seqid)
				{
                    insertIdx = i + 1;
					break;
				}
			}
            // insert into ordered rx queue
            rx.Insert(insertIdx, pkt);
            // shard range for current packet
            long shardBegin = pkt.seqid - (long)pkt.seqid % (long)totalShards;
			long shardEnd = shardBegin + totalShards - 1;
            // max search range in ordered queue for current shard
            int searchBegin = insertIdx - (int)((long)pkt.seqid % (long)totalShards);
			if (searchBegin < 0)
			{
                searchBegin = 0;
			}
			int searchEnd = searchBegin + totalShards - 1;
			if (searchEnd >= rx.Count)
			{
                searchEnd = rx.Count - 1;
			}
			if (searchEnd > searchBegin && searchEnd - searchBegin + 1 >= dataShards)
			{
                int numshard = 0;
                int numDataShard = 0;
                int first = 0;
                int maxlen = 0;
                List<List<byte>> shardVec = new List<List<byte>>(new List<byte>[totalShards]);
				List<bool> shardflag = new List<bool>(new bool[totalShards]);
				for (int k = searchBegin; k <= searchEnd; k++)
				{
					uint seqid = rx[k].seqid;
					if (seqid > shardEnd)
					{
						break;
					}
					if (seqid >= shardBegin)
					{
                        shardVec[(int)((long)seqid % (long)totalShards)] = rx[k].data;
                        shardflag[(int)((long)seqid % (long)totalShards)] = true;
                        numshard++;
						if (rx[k].flag == typeData)
						{
                            numDataShard++;
						}
						if (numshard == 1)
						{
							first = k;
						}
						if (rx[k].data.Count > maxlen)
						{
							maxlen = rx[k].data.Count;
						}
					}
				}
				if (numDataShard == dataShards)
				{
                    // no lost
                    rx.RemoveRange(first, numshard);
				}
				else if (numshard >= dataShards)
				{
                    // recoverable
                    // equally resized
                    for (int l = 0; l < shardVec.Count; l++)
					{
						if (shardVec[l] != null)
						{
							byte[] array = shardVec[l].ToArray();
							Array.Resize(ref array, maxlen);
                            shardVec[l] = new List<byte>(array);
						}
					}
                    // reconstruct shards
                    enc.Reconstruct(shardVec);
					for (int m = 0; m < dataShards; m++)
					{
						if (!shardflag[m])
						{
							recovered.Add(shardVec[m]);
						}
					}
					rx.RemoveRange(first, numshard);
				}
			}
			if (rx.Count > rxlimit)
			{
				rx.RemoveAt(0);
			}
			return recovered;
		}

		public void Encode(List<List<byte>> shards)
		{
            // resize elements with 0 appending
            int max = 0;
			for (int i = 0; i < dataShards; i++)
			{
				if (shards[i].Count > max)
				{
                    max = shards[i].Count;
				}
			}
			for (int j = 0; j < shards.Count; j++)
			{
				List<byte> list = shards[j];
				if (list == null)
				{
					shards[j] = new List<byte>(new byte[max]);
					continue;
				}
				byte[] array = list.ToArray();
				Array.Resize(ref array, max);
				shards[j] = new List<byte>(array);
			}
			enc.Encode(shards);
		}

		internal void MarkData(byte[] data, ushort sz)
		{
			int offset = 0;
            offset += KCP.ikcp_encode32u(data, offset, next);
            offset += KCP.ikcp_encode16u(data, offset, typeData);
			KCP.ikcp_encode16u(data, offset, (ushort)(sz + 2));
			next++;
		}

		internal void MarkFEC(byte[] data)
		{
			int offset = 0;
            offset += KCP.ikcp_encode32u(data, offset, next);
			KCP.ikcp_encode16u(data, offset, typeFEC);
			next++;
			if (next >= paws)
			{
				next = 0u;
			}
		}
	}
}
