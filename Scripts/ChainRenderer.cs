using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Mitochondrio_OpenGL.Scripts
{
    // Used to render the "smoothing" effect for chains of mitochondria. renders a camera-facing quad towards the camera along
    // the diameter of one mito to that of its neighbour
    // Turned on and off by CellGenerator.smoothLines.
    //
    // Chains change every frame (mitos move, fuse, split and reverse), so the batch is rebuilt every frame.
    // Each ribbon blends from one mito's Colour to the next.
    internal class ChainRenderer
    {
        private const int MaxChainWalk = 512; // never loop forever on a corrupt chain

        private readonly LineRenderer lines = new LineRenderer();

        public void Render(List<SceneObjectInstanced> mitochondria, Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            lines.Clear();

            // Walk every chain once, starting from its head; lone mitos have no neighbour and draw nothing
            foreach (SceneObjectInstanced obj in mitochondria)
            {
                if (obj is not Mitochondrion head || head.parent != null || head.child == null) continue;

                Mitochondrion current = head;
                // Loop until we find the tail of the chain, or reach MaxChainWalk
                for (int guard = 0; current.child != null && guard < MaxChainWalk; guard++)
                {
                    Mitochondrion next = current.child;

                    // width = mito diameter (radius a + radius b is the average of the two diameters)
                    lines.AddSegment(current.Position, next.Position, current.Colour, next.Colour, current.Radius + next.Radius);

                    current = next;
                }
            }

            lines.Draw(view, projection, cameraPos);
        }
    }
}