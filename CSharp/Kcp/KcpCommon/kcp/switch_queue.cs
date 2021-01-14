using System.Collections;

namespace Core.Socket.KCPSupport
{
	public class Utility
	{
		public static void Swap<QT>(ref QT t1, ref QT t2)
		{
			QT val = t1;
			t1 = t2;
			t2 = val;
		}
	}
	public class SwitchQueue<T> where T : class
	{
		private Queue mConsumeQueue;

		private Queue mProduceQueue;

		public SwitchQueue()
		{
			mConsumeQueue = new Queue(16);
			mProduceQueue = new Queue(16);
		}

		public SwitchQueue(int capcity)
		{
			mConsumeQueue = new Queue(capcity);
			mProduceQueue = new Queue(capcity);
		}

		public void Push(T obj)
		{
			lock (mProduceQueue)
			{
				mProduceQueue.Enqueue(obj);
			}
		}

		public T Pop()
		{
			return (T)mConsumeQueue.Dequeue();
		}

		public bool Empty()
		{
			return mConsumeQueue.Count == 0;
		}

		public void Switch()
		{
			lock (mProduceQueue)
			{
				Utility.Swap(ref mConsumeQueue, ref mProduceQueue);
			}
		}

		public void Clear()
		{
			lock (mProduceQueue)
			{
				mConsumeQueue.Clear();
				mProduceQueue.Clear();
			}
		}
	}
}
