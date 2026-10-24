using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Mitochondrio_OpenGL.Scripts
{
    // A batch of flat ribbons (thick lines) that always turn to face the camera, drawn with line.vert / line.frag.
    //
    // Nothing is sent to the GPU until Draw, and only if the contents changed since the last Draw, so a batch that
    // never changes (the cytoskeleton) costs nothing per frame after the first one, while one that is rebuilt every
    // frame (the mitochondria chains) just calls Clear() and AddSegment() again
    public class LineRenderer
    {
        private const int FloatsPerVertex = 11;      // position(3) otherEnd(3) colour(3) thickness(1) side(1)
        private const int FloatsPerSegment = 6 * FloatsPerVertex;

        // Quad layout: vertices 0,1,3 sit on the start point, 2,4,5 on the end point.
        // line.vert pushes a vertex sideways along cross(direction toward the other end, direction to the camera).
        // That direction is reversed at the end point's vertices, so their side flags are reversed too. With the
        // same flags at both ends the ribbon twists into a bow-tie and only covers half its width mid-way.
        private static readonly float[] Sides = { -1, 1, -1, -1, -1, 1 };
        private static readonly bool[] IsStart = { true, true, false, true, false, false };

        private readonly int VAO, VBO;
        private readonly Shader lineShader;

        private float[] _data = new float[FloatsPerSegment * 256];
        private int _floatCount;
        private int _bufferCapacity;   // size of the GPU buffer, in floats
        private bool _dirty = true;

        public int SegmentCount => _floatCount / FloatsPerSegment;

        public LineRenderer()
        {
            lineShader = new Shader("Shaders/line.vert", "Shaders/line.frag");

            VAO = GL.GenVertexArray();
            VBO = GL.GenBuffer();

            GL.BindVertexArray(VAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);

            _bufferCapacity = _data.Length;
            GL.BufferData(BufferTarget.ArrayBuffer, _bufferCapacity * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);

            int stride = FloatsPerVertex * sizeof(float);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);                    // position
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));    // other end
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));    // colour
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, stride, 9 * sizeof(float));    // thickness
            GL.EnableVertexAttribArray(3);
            GL.VertexAttribPointer(4, 1, VertexAttribPointerType.Float, false, stride, 10 * sizeof(float));   // side
            GL.EnableVertexAttribArray(4);

            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        // Removes every segment. The GPU copy is replaced on the next Draw call
        public void Clear()
        {
            _floatCount = 0;
            _dirty = true;
        }

        // Overload that simply uses the same colour for both ends
        public void AddSegment(Vector3 start, Vector3 end, Vector3 colour, float thickness)
        {
            AddSegment(start, end, colour, colour, thickness);
        }

        // A ribbon from start to end, thickness wide (world units), blending from one colour to the other
        public void AddSegment(Vector3 start, Vector3 end, Vector3 startColour, Vector3 endColour, float thickness)
        {
            if (_floatCount + FloatsPerSegment > _data.Length)
            {
                Array.Resize(ref _data, _data.Length * 2);
            }

            int at = _floatCount;

            for (int i = 0; i < 6; i++)
            {
                bool atStart = IsStart[i];

                Vector3 position = atStart ? start : end;
                Vector3 otherEnd = atStart ? end : start;
                Vector3 colour = atStart ? startColour : endColour;

                _data[at++] = position.X;
                _data[at++] = position.Y;
                _data[at++] = position.Z;
                _data[at++] = otherEnd.X;
                _data[at++] = otherEnd.Y;
                _data[at++] = otherEnd.Z;
                _data[at++] = colour.X;
                _data[at++] = colour.Y;
                _data[at++] = colour.Z;
                _data[at++] = thickness;
                _data[at++] = Sides[i];
            }

            _floatCount = at;
            _dirty = true;
        }

        public void Draw(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            if (_floatCount == 0) return;

            // Rebuild/buffer the GPU resources if it's considered dirty
            if (_dirty)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);

                if (_floatCount > _bufferCapacity)
                {
                    _bufferCapacity = Math.Max(_floatCount, _bufferCapacity * 2);
                    GL.BufferData(BufferTarget.ArrayBuffer, _bufferCapacity * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);
                }

                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, _floatCount * sizeof(float), _data);
                _dirty = false;
            }

            lineShader.Use();
            lineShader.SetMatrix4("view", view);
            lineShader.SetMatrix4("projection", projection);
            lineShader.SetVector3("cameraPos", cameraPos);

            bool cullWasOn = GL.IsEnabled(EnableCap.CullFace);
            GL.Disable(EnableCap.CullFace);

            GL.BindVertexArray(VAO);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _floatCount / FloatsPerVertex);
            GL.BindVertexArray(0);

            if (cullWasOn) GL.Enable(EnableCap.CullFace);
        }
    }
}