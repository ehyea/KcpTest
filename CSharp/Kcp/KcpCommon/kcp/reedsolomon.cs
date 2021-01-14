using System;
using System.Collections.Generic;

namespace Core.Socket.KCPSupport
{
	public class ReedSolomon
	{
		private int m_dataShards;

		private int m_parityShards;

		private int m_totalShards;

		private matrix m;

		private inversionTree tree;

		private List<List<byte>> parity;

		public ReedSolomon(int dataShards, int parityShards)
		{
			m_dataShards = dataShards;
			m_parityShards = parityShards;
			m_totalShards = dataShards + parityShards;
			tree = inversionTree.newInversionTree(dataShards, parityShards);
		}

		public static ReedSolomon New(int dataShards, int parityShards)
		{
			if (dataShards <= 0 || parityShards <= 0)
			{
				throw new ArgumentException("cannot create Encoder with zero or less data/parity shards");
			}
			if (dataShards + parityShards > 255)
			{
				throw new ArgumentException("cannot create Encoder with 255 or more data+parity shards");
			}
			ReedSolomon r = new ReedSolomon(dataShards, parityShards);
            // Start with a Vandermonde matrix.  This matrix would work,
            // in theory, but doesn't have the property that the data
            // shards are unchanged after encoding.
            matrix vm = matrix.vandermonde(r.m_totalShards, r.m_dataShards);
            // Multiply by the inverse of the top square of the matrix.
            // This will make the top square be the identity matrix, but
            // preserve the property that any square subset of rows  is
            // invertible.
            matrix top = vm.SubMatrix(0, 0, dataShards, dataShards);
            top = top.Invert();
			r.m = vm.Multiply(top);
            // Inverted matrices are cached in a tree keyed by the indices
            // of the invalid rows of the data to reconstruct.
            // The inversion m_root node will have the identity matrix as
            // its inversion matrix because it implies there are no errors
            // with the original data.
            r.tree = inversionTree.newInversionTree(dataShards, parityShards);
			r.parity = new List<List<byte>>(new List<byte>[parityShards]);
			for (int i = 0; i < parityShards; i++)
			{
				r.parity[i] = r.m[dataShards + i];
			}
			return r;
		}

		public void Encode(List<List<byte>> shards)
		{
			if (shards.Count != m_totalShards)
			{
				throw new ArgumentException("too few shards given");
			}
			checkShards(shards, false);
            // Get the slice of output buffers.
            List<List<byte>> output = new List<List<byte>>(shards);
            output.RemoveRange(0, m_dataShards);
            // Do the coding.
            List<List<byte>> input = new List<List<byte>>(shards);
            input.RemoveRange(m_dataShards, shards.Count - m_dataShards);
			codeSomeShards(parity, input, output, m_parityShards);
		}

		public void Reconstruct(List<List<byte>> shards)
		{
			if (shards.Count != m_totalShards)
			{
				throw new ArgumentException("too few shards given");
			}
            // Check arguments
            checkShards(shards, true);
			int shardSize_ = shardSize(shards);
            // Quick check: are all of the shards present?  If so, there's
            // nothing to do.
            int numberPresent = 0;
			for (int i = 0; i < m_totalShards; i++)
			{
				if (shards[i] != null)
				{
                    numberPresent++;
				}
			}
			if (numberPresent == m_totalShards)
			{
                // Cool.  All of the shards data data.  We don't
                // need to do anything.
                return;
			}
            // More complete sanity check
            if (numberPresent < m_dataShards)
			{
				throw new ArgumentException("too few shards given");
			}
            // Pull out an array holding just the shards that
            // correspond to the rows of the submatrix.  These shards
            // will be the Input to the decoding process that re-creates
            // the missing data shards.
            //
            // Also, create an array of indices of the valid rows we do have
            // and the invalid rows we don't have up until we have enough valid rows.
            List<List<byte>> subShards = new List<List<byte>>(new List<byte>[m_dataShards]);
			List<int> validIndices = new List<int>(new int[m_dataShards]);
			List<int> invalidIndices = new List<int>();
			int subMatrixRow = 0;
			for (int matrixRow = 0; matrixRow < m_totalShards; matrixRow++)
			{
				if (subMatrixRow >= m_dataShards)
				{
					break;
				}
				if (shards[matrixRow] != null)
				{
                    subShards[subMatrixRow] = shards[matrixRow];
                    validIndices[subMatrixRow] = matrixRow;
                    subMatrixRow++;
				}
				else
				{
                    invalidIndices.Add(matrixRow);
				}
			}
            // Attempt to get the cached inverted matrix out of the tree
            // based on the indices of the invalid rows.
            matrix dataDecodeMatrix = tree.GetInvertedMatrix(invalidIndices);
            // If the inverted matrix isn't cached in the tree yet we must
            // construct it ourselves and insert it into the tree for the
            // future.  In this way the inversion tree is lazily loaded.
            if (dataDecodeMatrix.empty())
			{
                // Pull out the rows of the matrix that correspond to the
                // shards that we have and build a square matrix.  This
                // matrix could be used to generate the shards that we have
                // from the original data.
                matrix subMatrix = matrix.newMatrix(m_dataShards, m_dataShards);
				for (subMatrixRow = 0; subMatrixRow < validIndices.Count; subMatrixRow++)
				{
					for (int c = 0; c < m_dataShards; c++)
					{
                        subMatrix[subMatrixRow, c] = this.m[validIndices[subMatrixRow], c];
					}
				}
                // Invert the matrix, so we can go from the encoded shards
                // back to the original data.  Then pull out the row that
                // generates the shard that we want to Decode.  Note that
                // since this matrix maps back to the original data, it can
                // be used to create a data shard, but not a parity shard.
                dataDecodeMatrix = subMatrix.Invert();
				if (dataDecodeMatrix.empty())
				{
					throw new Exception("cannot get matrix invert");
				}
                // Cache the inverted matrix in the tree for future use keyed on the
                // indices of the invalid rows.
                if (tree.InsertInvertedMatrix(invalidIndices, dataDecodeMatrix, m_totalShards) != 0)
				{
					throw new Exception("cannot insert matrix invert");
				}
			}
            // Re-create any data shards that were missing.
            //
            // The Input to the coding is all of the shards we actually
            // have, and the output is the missing data shards.  The computation
            // is done using the special Decode matrix we just built.
            List<List<byte>> outputs = new List<List<byte>>(new List<byte>[m_parityShards]);
			List<List<byte>> matrixRows = new List<List<byte>>(new List<byte>[m_parityShards]);
			int outputCount = 0;
			for (int iShard = 0; iShard < m_dataShards; iShard++)
			{
				if (shards[iShard] == null)
				{
					shards[iShard] = new List<byte>(new byte[shardSize_]);
                    outputs[outputCount] = shards[iShard];
                    matrixRows[outputCount] = dataDecodeMatrix[iShard];
                    outputCount++;
				}
			}
			codeSomeShards(matrixRows, subShards, outputs, outputCount);
            // Now that we have all of the data shards intact, we can
            // compute any of the parity that is missing.
            //
            // The Input to the coding is ALL of the data shards, including
            // any that we just calculated.  The output is whichever of the
            // data shards were missing.
            outputCount = 0;
			for (int iShard = m_dataShards; iShard < m_totalShards; iShard++)
			{
				if (shards[iShard] == null)
				{
					shards[iShard] = new List<byte>(new byte[iShard]);
					outputs[outputCount] = shards[iShard];
                    matrixRows[outputCount] = parity[iShard - m_dataShards];
                    outputCount++;
				}
			}
			codeSomeShards(matrixRows, shards, outputs, outputCount);
		}

		private static int shardSize(List<List<byte>> shards)
		{
			for (int i = 0; i < shards.Count; i++)
			{
				if (shards[i] != null)
				{
					return shards[i].Count;
				}
			}
			return 0;
		}

		private void codeSomeShards(List<List<byte>> matrixRows, List<List<byte>> inputs, List<List<byte>> outputs, int outputCount)
		{
			for (int c = 0; c < m_dataShards; c++)
			{
				List<byte> indata = inputs[c];
				for (int iRow = 0; iRow < outputCount; iRow++)
				{
					if (c == 0)
					{
						galois.galMulSlice(matrixRows[iRow][c], indata, outputs[iRow]);
					}
					else
					{
						galois.galMulSliceXor(matrixRows[iRow][c], indata, outputs[iRow]);
					}
				}
			}
		}

		private void checkShards(List<List<byte>> shards, bool nilok)
		{
			int size = shardSize(shards);
			if (size == 0)
			{
				throw new ArgumentException("no shard data");
			}
			int index = 0;
			while (true)
			{
				if (index >= shards.Count)
				{
					return;
				}
				if (shards[index] == null)
				{
					if (!nilok)
					{
						throw new ArgumentException("shard sizes does not match");
					}
				}
				else if (shards[index].Count != size)
				{
					break;
				}
                index++;
			}
			throw new ArgumentException("shard sizes does not match");
		}
	}
}
