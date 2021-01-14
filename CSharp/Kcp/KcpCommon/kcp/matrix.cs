using System;
using System.Collections.Generic;

namespace Core.Socket.KCPSupport
{
	internal class matrix
	{
		private List<List<byte>> data;

		private int rows;

		private int cols;

		internal List<byte> this[int row]
		{
			get
			{
				return data[row];
			}
			set
			{
				data[row] = value;
			}
		}

		internal byte this[int row, int col]
		{
			get
			{
				return data[row][col];
			}
			set
			{
				data[row][col] = value;
			}
		}

		public static matrix newMatrix(int rows, int cols)
		{
			if (rows <= 0 || cols <= 0)
			{
				throw new ArgumentException("invalid arguments");
			}
            matrix m = new matrix();
            m.rows = rows;
            m.cols = cols;
            m.data = new List<List<byte>>(new List<byte>[rows]);
			for (int i = 0; i < rows; i++)
			{
                m.data[i] = new List<byte>(new byte[cols]);
			}
			return m;
		}

		public static matrix identityMatrix(int size)
		{
			matrix matrix = newMatrix(size, size);
			for (int i = 0; i < size; i++)
			{
				matrix[i, i] = 1;
			}
			return matrix;
		}

		public static matrix vandermonde(int rows, int cols)
		{
			matrix matrix = newMatrix(rows, cols);
			for (int r = 0; r < rows; r++)
			{
				for (int c = 0; c < cols; c++)
				{
					matrix[r, c] = galois.galExp((byte)r, (byte)c);
				}
			}
			return matrix;
		}

		public matrix Multiply(matrix right)
		{
			if (cols != right.rows)
			{
				return new matrix();
			}
			matrix matrix = newMatrix(rows, right.cols);
			for (int r = 0; r < matrix.rows; r++)
			{
				for (int c = 0; c < matrix.cols; c++)
				{
					byte value = 0;
					for (int i = 0; i < cols; i++)
					{
                        value = (byte)(value ^ galois.galMultiply(this[r, i], right[i, c]));
					}
					matrix[r, c] = value;
				}
			}
			return matrix;
		}

		public matrix Augment(matrix right)
		{
			matrix result = newMatrix(rows, cols + right.cols);
			for (int r = 0; r < rows; r++)
			{
				for (int c = 0; c < cols; c++)
				{
                    result[r, c] = this[r, c];
				}
				int cols_ = cols;
				for (int c = 0; c < right.cols; c++)
				{
                    result[r, cols_ + c] = right[r, c];
				}
			}
			return result;
		}

		public matrix SubMatrix(int rmin, int cmin, int rmax, int cmax)
		{
			matrix result = newMatrix(rmax - rmin, cmax - cmin);
			for (int r = rmin; r < rmax; r++)
			{
				for (int c = cmin; c < cmax; c++)
				{
                    result[r - rmin, c - cmin] = this[r, c];
				}
			}
			return result;
		}

		public bool IsSquare()
		{
			return rows == cols;
		}
        // SwapRows Exchanges two rows in the matrix.
        public int SwapRows(int r1, int r2)
		{
			if (r1 < 0 || rows <= r1 || r2 < 0 || rows <= r2)
			{
				return -1;
			}
			List<byte> value = data[r1];
			data[r1] = data[r2];
			data[r2] = value;
			return 0;
		}

		public matrix Invert()
		{
			if (!IsSquare())
			{
				return new matrix();
			}
			matrix work = identityMatrix(rows);
            work = Augment(work);
			if (work.gaussianElimination() != 0)
			{
				return new matrix();
			}
			return work.SubMatrix(0, rows, rows, rows * 2);
		}

		public int gaussianElimination()
		{
			int rows_ = rows;
			int cols_ = cols;
            // Clear out the part below the main diagonal and scale the main
            // diagonal to be 1.
            for (int r = 0; r < rows_; r++)
			{
                // If the element on the diagonal is 0, find a row below
                // that has a non-zero and swap them.
                if (this[r, r] == 0)
				{
					for (int rowBelow = r + 1; rowBelow < rows_; rowBelow++)
					{
						if (this[rowBelow, r] != 0)
						{
							SwapRows(r, rowBelow);
							break;
						}
					}
				}
                // If we couldn't find one, the matrix is singular.
                if (this[r, r] == 0)
				{
					return -1;
				}
                // Scale to 1.
                if (this[r, r] != 1)
				{
					byte scale = galois.galDivide(1, this[r, r]);
					for (int c = 0; c < cols_; c++)
					{
						this[r, c] = galois.galMultiply(this[r, c], scale);
					}
				}
                // Make everything below the 1 be a 0 by subtracting
                // a multiple of it.  (Subtraction and addition are
                // both exclusive or in the Galois field.)
                for (int rowBelow = r + 1; rowBelow < rows_; rowBelow++)
				{
					if (this[rowBelow, r] != 0)
					{
						byte scale = this[rowBelow, r];
						for (int c = 0; c < cols_; c++)
						{
							this[rowBelow, c] ^= galois.galMultiply(scale, this[r, c]);
						}
					}
				}
			}
			for (int d = 0; d < rows_; d++)
			{
				for (int rowAbove = 0; rowAbove < d; rowAbove++)
				{
					if (this[rowAbove, d] != 0)
					{
						byte scale = this[rowAbove, d];
						for (int c = 0; c < cols_; c++)
						{
							this[rowAbove, c] ^= galois.galMultiply(scale, this[d, c]);
						}
					}
				}
			}
			return 0;
		}

		internal bool empty()
		{
			if (rows != 0)
			{
				return cols == 0;
			}
			return true;
		}
	}
}
