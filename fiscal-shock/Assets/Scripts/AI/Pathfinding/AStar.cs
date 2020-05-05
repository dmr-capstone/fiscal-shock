using UnityEngine;
using System;
using System.Collections.Generic;
using FiscalShock.Graphs;
using System.IO;

namespace FiscalShock.Pathfinding {
    public class AStar {
        /*
            Steps for Debugging this Class:
            1) Check the script execution order if obtaining null reference exceptions.
            2) Check if one of the data structures below is running out of nodes to visit prematurely.
        */

        private Delaunay navGraph;


        public AStar(Delaunay graph) {
            navGraph = graph;

            // DEBUG: Remove, or set debugging code.
            // Debug.Log("AStar is working. Graph: " + navGraph);
            // StreamWriter writer = new StreamWriter("/home/ybautista/Desktop/UnityOutput/cell_vertices.txt");

            // foreach (Vertex vertex in navGraph.vertices) {
            //     writer.Write("VERTEX: " + vertex.vector + "\n");

            //     foreach (Vertex v in vertex.neighborhood) {
            //         if (!v.toIgnore) {
            //             writer.Write("\tNEIGHBOR VERTEX: " + v.vector + "\n");
            //         }
            //     }
            // }

            // writer.Close();
        }

        public Stack<Vertex> findPath(Vertex lastVisitedNode, Vertex destination) {
            Debug.Log("DESTINATION: " + destination.vector);
            Stack<Vertex> path = new Stack<Vertex>();

            if (lastVisitedNode.Equals(destination)) {
                return path;
            }

            // Add starting node to the list, and set parent to null.
            LinkedList<VertexNode> open = new LinkedList<VertexNode>();
            open.AddFirst(new VertexNode(lastVisitedNode, 0, destination.getDistanceTo(lastVisitedNode)));
            open.First.Value.setLinkToPrevious(null);

            // Create the closed set.
            LinkedList<VertexNode> closed = new LinkedList<VertexNode>();

            // Set current node to starting point.
            LinkedListNode<VertexNode> currentNode = open.Last;

            // Placeholder variables for use inside while loop.
            double cost;
            LinkedListNode<VertexNode> openTemp, closedTemp;

            // Add neightbors to open list if not there or better f cost than before.
            while (!currentNode.Value.associatedLocation.Equals(destination)) {
                // Remove the value with the smallest f cost (should be at end of list) and close it.
                open.RemoveLast();
                closed.AddFirst(currentNode);

                // Check the node's neighbors to see which ones can be visited or revisited.
                foreach (Vertex neighbor in currentNode.Value.associatedLocation.neighborhood) {
                    // Neighboring vertices that are unnavigable are useless.
                    if (neighbor.toIgnore) {
                        continue;
                    }

                    cost = currentNode.Value.gCost + currentNode.Value.associatedLocation.getDistanceTo(neighbor);
                    VertexNode neighborNode = new VertexNode(neighbor);

                    // Find returns null in any case where the node doesn't exist.
                    // openTemp should never be filled at the same time as closedTemp.
                    // If node is in one of lists, gCost shouldn't be empty.
                    openTemp = open.Find(neighborNode);
                    if (openTemp != null && openTemp.Value.gCost > cost) {
                        open.Remove(openTemp);
                    }

                    closedTemp = closed.Find(neighborNode);
                    if (closedTemp != null && closedTemp.Value.gCost > cost) {
                        closed.Remove(closedTemp);
                    }

                    if (!open.Contains(neighborNode) && !closed.Contains(neighborNode)) {
                        // NeighborNode is a different instance of VertexNode and, thus,
                        // needs to be set to instance that already existed in lists.
                        if (openTemp != null) {
                            neighborNode = openTemp.Value;
                            neighborNode.changeGCost(cost);
                        }

                        else if (closedTemp != null) {
                            neighborNode = closedTemp.Value;
                            neighborNode.changeGCost(cost);
                        }

                        else {
                            neighborNode.setCosts(cost, destination.getDistanceTo(neighborNode.associatedLocation));
                        }

                        neighborNode.setLinkToPrevious(currentNode.Value);
                        sortedAdd(open, neighborNode);
                    }
                }

                currentNode = open.Last;
            }

            VertexNode node = currentNode.Value;

            while (node != null) {
                path.Push(node.associatedLocation);
                node = node.previousOnPath;
            }

            // DEBUG: Debug code. Remove or refactor for debugging mode.
            Debug.Log("TOTAL NODES PASSED: " + path.Count);

            return path;
        }

        // Will take the element to add it via insertion sort to correct position.
        // Yeah, this is an anti-pattern. But it was the best way to generalize it last minute.
        private void sortedAdd(LinkedList<VertexNode> list, VertexNode node) {
            LinkedListNode<VertexNode> currentNode = list.First;

            if (list.Count == 0) {
                list.AddFirst(node);
                return;
            }

            while(true) {
                if(currentNode.Value.fCost == node.fCost) {
                    if(node.hCost >= currentNode.Value.hCost) {
                        list.AddBefore(currentNode, node);
                        break;
                    }
                }

                if(node.fCost > currentNode.Value.fCost) {
                    list.AddBefore(currentNode, node);
                    break;
                }

                if(currentNode.Next == null) {
                    list.AddAfter(currentNode, node);
                    break;
                }

                currentNode = currentNode.Next;
            }
        }
    }
}
