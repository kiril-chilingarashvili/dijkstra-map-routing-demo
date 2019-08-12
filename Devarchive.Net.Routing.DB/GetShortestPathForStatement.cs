//------------------------------------------------------------------------------
// <copyright file="CSSqlFunction.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using Microsoft.SqlServer.Server;

public partial class UserDefinedFunctions
{
    #region GetShortestPath function

    [Microsoft.SqlServer.Server.SqlFunction(
        FillRowMethodName = "FillRow",
        DataAccess = DataAccessKind.Read,
        TableDefinition = "EdgeID int, IsReverse bit, Num int"
        )]
    public static IEnumerable GetShortestPathForStatement(
        string selectStatement,
        int sourceVertexID,
        int targetVertexID)
    {
        return Graph.Create(selectStatement).GetShortestPath(sourceVertexID, targetVertexID);
    }

    public static void FillRow(Object obj, out int EdgeID, out bool IsReverse, out int Num)
    {
        var item = (GraphDataItem)obj;
        EdgeID = item.EdgeID;
        IsReverse = item.IsReverse;
        Num = item.Num;
    }

    #endregion

    #region Graph class

    public class Graph
    {
        #region Class level variables

        private Dictionary<int, int> mVertexDictionary = new Dictionary<int, int>();
        private Dictionary<double, Edge> mEdges = new Dictionary<double, Edge>();
        private Dictionary<int, Vertex> mVertices = new Dictionary<int, Vertex>();
        private SortedList<double, List<Vertex>> mNextVertices = new SortedList<double, List<Vertex>>();
        private int mVertexCounter = 1;
        private int mTries = 0;

        private int mSourceVertexID;
        private int mTargetVertexID;

        #endregion

        #region Constructor

        private Graph(IEnumerable<GraphDataItem> items)
        {
            foreach (var item in items)
            {
                CreateEdge(item);
            }
        }

        #endregion

        #region Factory Methods

        private static Graph Create(IEnumerable<GraphDataItem> items)
        {
            return new Graph(items);
        }

        public static Graph Create(string selectStatement)
        {
            var data = new List<GraphDataItem>();
            using (SqlConnection cn = new SqlConnection("context connection=true"))
            {
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = selectStatement;
                cmd.CommandType = CommandType.Text;
                cmd.Connection = cn;
                cn.Open();
                try
                {
                    var dr = cmd.ExecuteReader();
                    try
                    {
                        while (dr.Read())
                        {
                            data.Add(new GraphDataItem
                            {
                                EdgeID = dr.GetInt32(0),
                                SourceVertexID = dr.GetInt32(1),
                                TargetVertexID = dr.GetInt32(2),
                                Cost = dr.GetFloat(3),
                                ReverseCost = dr.GetFloat(4),
                            });
                        }
                    }
                    finally
                    {
                        dr.Close();
                    }
                }
                finally
                {
                    cn.Close();
                }
            }
            return Create(data);
        }

        #endregion

        #region Methods

        public GraphSearchResult GetShortestPath(int sourceVertexID, int targetVertexID)
        {
            mSourceVertexID = mVertexDictionary[sourceVertexID];
            mTargetVertexID = mVertexDictionary[targetVertexID];

            mTries = 0;

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var vertex = GetShortestPathUsingDijkstraAlgorithm();
            stopwatch.Stop();
            var result = new GraphSearchResult(vertex, stopwatch.Elapsed, mTries);

            ResetVertices();

            return result;
        }

        #region Dijkstra algorithm

        private Vertex GetShortestPathUsingDijkstraAlgorithm()
        {
            var current = mVertices[mSourceVertexID];
            current.Cost = 0;
            mNextVertices.Clear();
            var unvisited = mVertices.Count;
            while (current != null && unvisited > 0)
            {
                mTries++;
                if (current.ID == mTargetVertexID)
                {
                    return current;
                }
                foreach (var neighbour in current.Neighbours)
                {
                    if (!neighbour.Visited)
                    {
                        var edge = GetEdge(current.ID, neighbour.ID);
                        var totalCost = current.Cost + edge.Cost;
                        if (totalCost < neighbour.Cost)
                        {
                            RemoveNextVertex(neighbour);
                            neighbour.Cost = totalCost;
                            neighbour.PreviousVertex = current;
                            neighbour.PreviousEdge = edge;
                            AddNextVertex(neighbour);
                        }
                    }
                }

                //mark Vertex visited
                current.Visited = true;
                RemoveNextVertex(current);

                current = GetNextVertex();
            }
            return null;
        }

        private Vertex GetNextVertex()
        {
            if (mNextVertices.Count > 0)
            {
                var dist = mNextVertices.Values[0];
                return dist != null ? dist[0] : null;
            }
            return null;
        }

        private void AddNextVertex(Vertex neighbour)
        {
            var cost = neighbour.Cost;
            if (mNextVertices.ContainsKey(cost))
            {
                var dist = mNextVertices[cost];
                if (dist == null)
                {
                    mNextVertices[cost] = new List<Vertex>()
                    {
                        neighbour
                    };
                }
                else
                {
                    dist.Add(neighbour);
                }
            }
            else
            {
                mNextVertices.Add(cost, new List<Vertex>()
                {
                    neighbour
                });
            }
        }

        private void RemoveNextVertex(Vertex neighbour)
        {
            var cost = neighbour.Cost;
            if (mNextVertices.ContainsKey(cost))
            {
                var dist = mNextVertices[cost];
                if (dist != null)
                {
                    dist.Remove(neighbour);
                    if (dist.Count == 0)
                    {
                        mNextVertices.Remove(cost);
                    }
                }
            }
        }

        private void ResetVertices()
        {
            foreach (var item in mVertices.Values)
            {
                item.Visited = false;
                item.Cost = double.PositiveInfinity;
                item.PreviousVertex = null;
                item.PreviousEdge = null;
            }
        }

        #endregion

        #region Initialization

        private void CreateEdge(GraphDataItem item)
        {
            var edge = new Edge
            {
                ID = item.EdgeID,
                SourceVertex = EnsureVertex(item.SourceVertexID),
                TargetVertex = EnsureVertex(item.TargetVertexID),
                Cost = item.Cost,
                IsReverse = false,
                DataItem = item,
            };

            edge.SourceVertex.Neighbours.Add(edge.TargetVertex);
            {
                var key = GetKey(edge.SourceVertex.ID, edge.TargetVertex.ID);
                if (!mEdges.ContainsKey(key))
                {
                    mEdges.Add(key, edge);
                }
                else if (mEdges[key].Cost > edge.Cost)
                {
                    mEdges[key] = edge;
                }
            }

            edge = new Edge
            {
                ID = item.EdgeID,
                SourceVertex = EnsureVertex(item.TargetVertexID),
                TargetVertex = EnsureVertex(item.SourceVertexID),
                Cost = item.ReverseCost,
                IsReverse = true,
                DataItem = item,
            };
            edge.SourceVertex.Neighbours.Add(edge.TargetVertex);
            {
                var key = GetKey(edge.SourceVertex.ID, edge.TargetVertex.ID);
                if (!mEdges.ContainsKey(key))
                {
                    mEdges.Add(key, edge);
                }
                else if (mEdges[key].Cost > edge.Cost)
                {
                    mEdges[key] = edge;
                }
            }
        }

        private Vertex EnsureVertex(int originalVertexID)
        {
            if (mVertexDictionary.ContainsKey(originalVertexID))
            {
                return mVertices[mVertexDictionary[originalVertexID]];
            }
            var newVertexID = mVertexCounter;
            var newVertex = new Vertex { ID = newVertexID };
            mVertexCounter++;

            mVertices.Add(newVertexID, newVertex);
            mVertexDictionary.Add(originalVertexID, newVertexID);

            return newVertex;
        }

        private double GetKey(int start, int end)
        {
            // we support 10000000 nodes graph - I dont think we need or can handle more
            return start * 10000000 + end;
        }

        private Edge GetEdge(int startVertexID, int endVertexID)
        {
            return mEdges[GetKey(startVertexID, endVertexID)];
        }

        #endregion

        #endregion
    }

    #endregion

    #region Vertex

    public class Vertex
    {
        public Vertex()
        {
            Neighbours = new List<Vertex>();
            Cost = double.PositiveInfinity;
        }

        public int ID { get; set; }
        public bool Visited { get; set; }
        public double Cost { get; set; }
        public Vertex PreviousVertex { get; set; }
        public Edge PreviousEdge { get; set; }
        public List<Vertex> Neighbours { get; private set; }
    }

    #endregion

    #region Edge

    public class Edge
    {
        public int ID { get; set; }
        public Vertex SourceVertex { get; set; }
        public Vertex TargetVertex { get; set; }
        public double Cost { get; set; }
        public bool IsReverse { get; set; }
        public GraphDataItem DataItem { get; set; }
    }

    #endregion

    #region GraphDataItem

    public class GraphDataItem
    {
        public int EdgeID { get; set; }
        public int SourceVertexID { get; set; }
        public int TargetVertexID { get; set; }
        public double Cost { get; set; }
        public double ReverseCost { get; set; }
        public bool IsReverse { get; set; }
        public int Num { get; set; }

        public GraphDataItem Clone()
        {
            return new GraphDataItem
            {
                EdgeID = EdgeID,
                SourceVertexID = SourceVertexID,
                TargetVertexID = TargetVertexID,
                Cost = Cost,
                ReverseCost = ReverseCost,
            };
        }
    }

    #endregion

    #region GraphSearchResult

    public class GraphSearchResult : IEnumerable<GraphDataItem>
    {
        private List<GraphDataItem> mList = new List<GraphDataItem>();

        public GraphSearchResult(Vertex vertex, TimeSpan time, int tries)
        {
            while (vertex != null && vertex.PreviousEdge != null)
            {
                var dataItem = vertex.PreviousEdge.DataItem.Clone();
                dataItem.IsReverse = vertex.PreviousEdge.IsReverse;
                mList.Insert(0, dataItem);
                vertex = vertex.PreviousVertex;
            }
            var i = 1;
            foreach (var item in mList)
            {
                item.Num = i;
                i++;
            }
            PathFound = vertex != null;
            TimeSpent = time;
            Tries = tries;
        }

        public bool PathFound { get; private set; }
        public TimeSpan TimeSpent { get; private set; }
        public int Tries { get; private set; }

        #region IEnumerable<GraphDataItem> Members

        public IEnumerator<GraphDataItem> GetEnumerator()
        {
            return mList.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return mList.GetEnumerator();
        }

        #endregion
    }

    #endregion
}
