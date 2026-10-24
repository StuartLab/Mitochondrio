using System;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Mitochondrio_OpenGL.Scripts;

namespace Mitochondrio_OpenGL.Scripts
{
    // A 3D point that represents the points of a line for a Branch
    public class BranchNode
    {
        public Branch? parentBranch;
        private Vector3 position;
        private Vector3 direction;
        private List<BranchNode> neighbours;
        public string id;

        public Vector3 Position
        {
            get { return position; }
            set { position = value; }
        }
        
        public Vector3 Direction
        {
            get { return direction; }
            set { direction = value; }
        }

        public Vector2 FlatDirection
        {
            get { return new Vector2(direction.X, direction.Z); }
        }

        public List<BranchNode> Neighbours
        {
            get { return neighbours; }
        }

        public BranchNode(Vector3 position)
        {
            Position = position;

            neighbours = new List<BranchNode>();

            id = Guid.NewGuid().ToString();
        }
    }
}
