using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Core.Socket.KCPSupport
{
	internal class inversionTree
	{
		internal class inversionNode
		{

			internal matrix m_matrix
			{
                get;
                set;
			}

			internal List<inversionNode> m_children
			{
                get;
                set;
			}

			internal inversionNode()
			{
				m_matrix = new matrix();
				m_children = new List<inversionNode>();
			}

			internal matrix getInvertedMatrix(List<int> invalidIndices, int parent)
			{
                // Get the child node to search next from the list of m_children.  The
                // list of m_children starts relative to the parent index passed in
                // because the indices of invalid rows is sorted (by default).  As we
                // search recursively, the first invalid index gets popped off the list,
                // so when searching through the list of m_children, use that first invalid
                // index to find the child node.
                int firstIndex = invalidIndices[0];
				inversionNode node = m_children[firstIndex - parent];
                // If the child node doesn't exist in the list yet, fail fast by
                // returning, so we can construct and insert the proper inverted matrix.
                if (node == null)
				{
					return new matrix();
				}
                // If there's more than one invalid index left in the list we should
                // keep searching recursively.
                if (invalidIndices.Count > 1)
				{
                    // Search recursively on the child node by passing in the invalid indices
                    // with the first index popped off the front.  Also the parent index to
                    // pass down is the first index plus one.
                    List<int> v = new List<int>(invalidIndices);
					v.RemoveAt(0);
					return node.getInvertedMatrix(v, firstIndex + 1);
				}
                // If there aren't any more invalid indices to search, we've found our
                // node.  Return it, however keep in mind that the matrix could still be
                // nil because intermediary nodes in the tree are created sometimes with
                // their inversion matrices uninitialized.
                return node.m_matrix;
			}

			internal void insertInvertedMatrix(List<int> invalidIndices, matrix mat, int shards, int parent)
			{
                // As above, get the child node to search next from the list of m_children.
                // The list of m_children starts relative to the parent index passed in
                // because the indices of invalid rows is sorted (by default).  As we
                // search recursively, the first invalid index gets popped off the list,
                // so when searching through the list of m_children, use that first invalid
                // index to find the child node.s
                int firstIndex = invalidIndices[0];
				inversionNode node = m_children[firstIndex - parent];
                // If the child node doesn't exist in the list yet, create a new
                // node because we have the writer lock and add it to the list
                // of m_children.
                if (node == null)
				{
                    // Make the length of the list of m_children equal to the number
                    // of shards minus the first invalid index because the list of
                    // invalid indices is sorted, so only this length of errors
                    // are possible in the tree.
                    node = new inversionNode();
					inversionNode[] array = node.m_children.ToArray();
					Array.Resize(ref array, shards - firstIndex);
                    node.m_children = new List<inversionNode>(array);
					m_children[firstIndex - parent] = node;
				}
                // If there's more than one invalid index left in the list we should
                // keep searching recursively in order to find the node to add our
                // matrix.
                if (invalidIndices.Count > 1)
				{
                    // As above, search recursively on the child node by passing in
                    // the invalid indices with the first index popped off the front.
                    // Also the total number of shards and parent index are passed down
                    // which is equal to the first index plus one.
                    List<int> v = new List<int>(invalidIndices);
					v.RemoveAt(0);
                    node.insertInvertedMatrix(v, mat, shards, firstIndex + 1);
				}
				else
				{
                    node.m_matrix = mat;
				}
			}
		}

		private inversionNode m_root = new inversionNode();

		internal static inversionTree newInversionTree(int dataShards, int parityShards)
		{
			inversionTree tree = new inversionTree();
            tree.m_root.m_children = new List<inversionNode>(new inversionNode[dataShards + parityShards]);
            tree.m_root.m_matrix = matrix.identityMatrix(dataShards);
			return tree;
		}

		internal matrix GetInvertedMatrix(List<int> invalidIndices)
		{
			if (invalidIndices.Count == 0)
			{
				return m_root.m_matrix;
			}
			return m_root.getInvertedMatrix(invalidIndices, 0);
		}

		internal int InsertInvertedMatrix(List<int> invalidIndices, matrix mat, int shards)
		{
			if (invalidIndices.Count == 0)
			{
				return -1;
			}
			if (!mat.IsSquare())
			{
				return -2;
			}
			m_root.insertInvertedMatrix(invalidIndices, mat, shards, 0);
			return 0;
		}
	}
}
