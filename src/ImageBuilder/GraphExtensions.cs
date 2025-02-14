// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.DockerTools.ImageBuilder
{
    public static class GraphExtensions
    {
        public static IEnumerable<IEnumerable<T>> GetCompleteSubgraphs<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> getParents)
        {
            List<T[]> subgraphs = new List<T[]>();

            List<Node<T>> nodes = CreateNodeList(source, getParents);
            while (nodes.Any())
            {
                HashSet<Node<T>> subgraph = new HashSet<Node<T>>();
                AddSubgraphNode(subgraph, nodes.First(), nodes);
                subgraphs.Add(subgraph.Select(node => node.Item).ToArray());
            }

            return subgraphs;
        }

        private static List<Node<T>> CreateNodeList<T>(IEnumerable<T> source, Func<T, IEnumerable<T>> getParents)
        {
            Dictionary<T, Node<T>> nodes = new Dictionary<T, Node<T>>();

            foreach (T item in source)
            {
                CreateNode(item, nodes, getParents);
            }

            return nodes.Values.ToList();
        }

        private static Node<T> CreateNode<T>(T item, Dictionary<T, Node<T>> nodes, Func<T, IEnumerable<T>> getParents)
        {
            if (nodes.TryGetValue(item, out Node<T> itemNode))
            {
                return itemNode;
            }

            itemNode = new Node<T>() { Item = item };
            nodes.Add(item, itemNode);

            foreach (T parent in getParents(item))
            {
                Node<T> parentNode = CreateNode(parent, nodes, getParents);
                parentNode.Children.Add(itemNode);
                itemNode.Parents.Add(parentNode);
            }

            return itemNode;
        }

        private static void AddSubgraphNode<T>(HashSet<Node<T>> subgraph, Node<T> node, List<Node<T>> unvisitedNodes)
        {
            if (!subgraph.Contains(node))
            {
                unvisitedNodes.Remove(node);
                subgraph.Add(node);
                node.Parents.ForEach(parent => AddSubgraphNode(subgraph, parent, unvisitedNodes));
                node.Children.ForEach(child => AddSubgraphNode(subgraph, child, unvisitedNodes));
            }
        }

        private class Node<T>
        {
            public T Item;
            public List<Node<T>> Parents { get; } = new List<Node<T>>();
            public List<Node<T>> Children { get; } = new List<Node<T>>();
        }
    }
}
