using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Mitochondrio_OpenGL.Scripts;
using System.Drawing;
using System.Collections;

namespace Mitochondrio_OpenGL.Scripts
{
    // A list of nodes that form a line
    public class Branch
    {
        // Cytoskeleton drawing parameters
        private const float LineThickness = 0.008f;
        private static readonly Vector3 MicrotubuleColour = new Vector3(0f, 0f, 1f);
        private static readonly Vector3 ActinColour = new Vector3(0.5f, 0.5f, 0.5f);

        // The list of nodes that make up this branch
        private List<BranchNode> nodes;

        // the node off of which this branch stems, if any
        public BranchNode? parentNode;

        // if there is no parent, it's a main branch
        public bool isMainBranch = true;

        public Branch()
        {
            nodes = new List<BranchNode>();
        }


        // Method to get the node at a specific index
        public BranchNode GetNode(int index)
        {
            return nodes[index];
        }

        public List<BranchNode> GetNodes()
        {
            return nodes;
        }

        public void AddNode(BranchNode node)
        {
            if (nodes.Count > 0)
            {
                BranchNode lastNode = nodes[^1];

                // Links between nodes are bidirectional
                node.Neighbours.Add(lastNode);

                lastNode.Neighbours.Add(node);
            }

            // Add the node to the list
            nodes.Add(node);
        }

        public int NodeCount()
        {
            return nodes.Count;
        }

        // Adds one line segment per piece of this branch to a LineRenderer batch (see BranchRenderer).
        public void AddSegments(LineRenderer lines)
        {
            if (nodes.Count < 2) return;

            Vector3 colour = isMainBranch ? MicrotubuleColour : ActinColour;

            // the link back to the branch this one stems from
            if (parentNode != null) lines.AddSegment(parentNode.Position, nodes[0].Position, colour, LineThickness);

            // a segment for each pair of consecutive nodes
            for (int n = 0; n < nodes.Count - 1; n++)
            {
                lines.AddSegment(nodes[n].Position, nodes[n + 1].Position, colour, LineThickness);
            }
        }

        public void UpdateBranchVisuals()
        {
            // Ff there is a parent node, add an extra position to the line so it is connected to the main branch
            int offset = parentNode == null ? 0 : 1;
            int nodeCount = nodes.Count + offset;
            Vector3[] nodePositions = new Vector3[nodeCount];

            if (parentNode != null && offset == 1) nodePositions[0] = parentNode.Position;

            for (int i = 0; i < nodes.Count; i++)
            {
                BranchNode node = nodes[i];

                nodePositions[i + offset] = node.Position;
            }

            //lineRenderer.sharedMaterial.SetColor("_Color", isMainBranch ? UnityEngine.Color.blue : UnityEngine.Color.gray);
        }
    }
}