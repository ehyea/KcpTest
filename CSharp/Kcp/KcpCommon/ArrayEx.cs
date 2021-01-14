using System;
using System.Collections.Generic;

namespace Core
{
	public static class ArrayEx
    {
        internal static void NormalizationPosition(int sourceLength, ref int start, ref int? length)
        {
            start = ((start >= 0) ? Math.Min(start, sourceLength) : Math.Max(sourceLength + start, 0));
            length = ((!length.HasValue) ? Math.Max(sourceLength - start, 0) : ((length >= 0) ? Math.Min(length.Value, sourceLength - start) : Math.Max(sourceLength + length.Value - start, 0)));
        }

        public static T[] Slice<T>(T[] source, int start, int? length = null)
		{
			NormalizationPosition(source.Length, ref start, ref length);
			T[] array = new T[length.Value];
			Array.Copy(source, start, array, 0, length.Value);
			return array;
		}
	}
}
