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


namespace Mitochondrio_OpenGL.Scripts
{
    // instance data must be sequential to be packed properly for the shaders
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct InstanceData
    {
        public Matrix4 Transform;
        public Vector3 Color;
        public float Padding;
    }

    // A class that groups together objects that are drawn by the same instanced shader, for batch rendering
    internal class ShaderGroup
    {
        private float[] VertexData; // float3 pos, float2 uv, float3 norm
        private int[] Indices;
        public int indexCount;

        public int VAO;
        public int VBO;
        public int EBO;

        // The starting buffer size, which is automatically grown if needed
        public int instanceCapacity = 1000;
        public int InstanceVBO;

        public Shader shader;

        public ShaderGroup(float[] verts, int[] indices, Shader shader)
        {
            VertexData = verts;
            Indices = indices;
            indexCount = indices.Length;

            this.shader = shader;

            SetupVAO();
        }

        public void SetupVAO()
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
            int eboStride = 8 * sizeof(float);

            // Position
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(
                0, 3, VertexAttribPointerType.Float, false, eboStride, 0);

            // UV
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(
                1, 2, VertexAttribPointerType.Float, false, eboStride, 3 * sizeof(float));

            // Normal
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(
                2, 3, VertexAttribPointerType.Float, false, eboStride, 5 * sizeof(float));



            // instance VBO to be updated when instance transforms are passed in
            InstanceVBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, InstanceVBO);
            GL.BufferData(
                BufferTarget.ArrayBuffer,
                instanceCapacity * 80,      // 64 bytes per mat4 + 16 for colour
                IntPtr.Zero,
                BufferUsageHint.DynamicDraw
            );

            int locationStartPoint = 3;
            int vec4Size = 4 * sizeof(float);
            int mat4Size = 4 * vec4Size;
            int instanceStride = 20 * sizeof(float); // 16 for matrix + 3 for colour + 1 padding

            // Pass in each row of the transform matrix, takes up 4 locations
            for (int i = 0; i < 4; i++)
            {
                GL.EnableVertexAttribArray(locationStartPoint + i);
                GL.VertexAttribPointer(
                    locationStartPoint + i,
                    4,
                    VertexAttribPointerType.Float,
                    false,
                    instanceStride,
                    i * vec4Size
                    );

                GL.VertexAttribDivisor(locationStartPoint + i, 1);
            }

            // Instance color (location 7)
            GL.EnableVertexAttribArray(7);
            GL.VertexAttribPointer(7, 3, VertexAttribPointerType.Float, false, instanceStride, 16 * sizeof(float));
            GL.VertexAttribDivisor(7, 1);
        }

        public void UpdateInstanceVBO(List<SceneObjectInstanced> instances)
        {
            GL.BindVertexArray(VAO);

            int count = instances.Count;

            // grow buffer if needed
            if (count > instanceCapacity)
            {
                instanceCapacity = Math.Max(count, instanceCapacity * 2);

                GL.BindBuffer(BufferTarget.ArrayBuffer, InstanceVBO);
                GL.BufferData(
                    BufferTarget.ArrayBuffer,
                    instanceCapacity * sizeof(float) * 20, // 16 for mat4 + 3 for vec3 color + 1 padding
                    IntPtr.Zero,
                    BufferUsageHint.DynamicDraw
                );
            }


            InstanceData[] instanceData = new InstanceData[count];

            for (int i = 0; i < count; i++)
            {
                instanceData[i].Transform = instances[i].GetTransformMatrix();
                instanceData[i].Color = ((Mitochondrion)instances[i]).Colour;
                instanceData[i].Padding = 0f;
            }


            GL.BindBuffer(BufferTarget.ArrayBuffer, InstanceVBO);
            // buffer sub data overwrites previous data
            GL.BufferSubData(BufferTarget.ArrayBuffer,
                IntPtr.Zero,
                count * sizeof(float) * 20,
                instanceData);
        }


        // The shader is flat shaded, so it only needs the view and projection matrices
        public void Render(List<SceneObjectInstanced> objects, Matrix4 view, Matrix4 projection)
        {
            if (objects.Count == 0) return;

            shader.Use();
            GL.BindVertexArray(VAO);

            UpdateInstanceVBO(objects);

            shader.SetMatrix4("view", view);
            shader.SetMatrix4("projection", projection);

            GL.DrawElementsInstanced(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, IntPtr.Zero, objects.Count);
            //GL.DrawElements(PrimitiveType.Triangles, mitoShaderGroup.indexCount, DrawElementsType.UnsignedInt, 0);
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(VAO);
            GL.DeleteBuffer(VBO);
            GL.DeleteBuffer(EBO);
        }
    }
}