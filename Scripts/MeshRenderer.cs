using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Mitochondrio_OpenGL.Scripts
{
    // Draws a mesh, loaded in from ObjLoader
    internal class MeshRenderer
    {
        private float[] VertexData; // float3 pos, float2 uv, float3 norm
        private int[] Indices;
        public int indexCount;
        public int VAO;
        public int VBO;
        public int EBO;
        public Shader shader;

        public MeshRenderer(float[] verts, int[] indices, Shader shader)
        {
            VertexData = verts;
            Indices = indices;
            indexCount = indices.Length;
            this.shader = shader;
            SetupVAO();
        }

        private void SetupVAO()
        {
            VAO = GL.GenVertexArray();
            GL.BindVertexArray(VAO);

            // Create VBO (single interleaved buffer)
            VBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
            GL.BufferData(BufferTarget.ArrayBuffer,
                VertexData.Length * sizeof(float),
                VertexData,
                BufferUsageHint.StaticDraw); 

            // Create EBO
            EBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer,
                Indices.Length * sizeof(int),
                Indices,
                BufferUsageHint.StaticDraw);

            // Attribute layout: pos (3), uv (2), normal (3)
            int stride = 8 * sizeof(float);

            // Position
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(
                0, 3, VertexAttribPointerType.Float, false, stride, 0);

            // UV
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(
                1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));

            // Normal
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(
                2, 3, VertexAttribPointerType.Float, false, stride, 5 * sizeof(float));

            // Unbind
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        public void Render(Matrix4 model, Matrix4 view, Matrix4 projection, Vector3 lightPosition, Vector3 cameraPosition)
        {
            shader.Use();
            GL.BindVertexArray(VAO);

            shader.SetMatrix4("model", model);
            shader.SetMatrix4("view", view);
            shader.SetMatrix4("projection", projection);

            shader.SetVector3("lightPos", lightPosition);
            shader.SetVector3("viewPos", cameraPosition);

            GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0);
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(VAO);
            GL.DeleteBuffer(VBO);
            GL.DeleteBuffer(EBO);
        }
    }
}