using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Mitochondrio_OpenGL.Scripts
{
    // Draws the cytoskeleton. The branches only change when a cell is created or cleared, so the line batch is built
    // once per cell and then just drawn every frame.
    public class BranchRenderer
    {
        private readonly LineRenderer lines = new LineRenderer();

        // which list (and how many branches) the batch was built from
        private List<Branch>? _builtFor;
        private int _builtCount = -1;

        // Forces a rebuild on the next Render
        public void Invalidate()
        {
            _builtFor = null;
        }

        public void Render(List<Branch> branches, Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            if (branches.Count == 0) return;

            // Create and Clear both assign a new list, so a changed list (or length) means the cell changed
            if (!ReferenceEquals(branches, _builtFor) || branches.Count != _builtCount)
            {
                lines.Clear();

                foreach (Branch branch in branches)
                {
                    branch.AddSegments(lines);
                }

                _builtFor = branches;
                _builtCount = branches.Count;
            }

            lines.Draw(view, projection, cameraPos);
        }
    }
}