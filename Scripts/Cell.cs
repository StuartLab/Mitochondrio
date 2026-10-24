using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mitochondrio_OpenGL.Scripts
{
    internal class Cell
    {
        public List<Branch> branches;

        public BranchRenderer branchRenderer;   // the cytoskeleton
        public ChainRenderer chainRenderer; // the smoothing ribbons between chained mitos
        public ShaderGroup mitoShaderGroup;
        public MeshRenderer nucleusMeshRenderer;

        public List<SceneObjectInstanced> mitochondria;
        public SceneObject nucleus;

        public bool renderMitos = true;
        public bool renderCytoskeleton = true;
        public bool renderNucleus = true;

        public Cell()
        {
            branchRenderer = new BranchRenderer();
            chainRenderer = new ChainRenderer();

            mitochondria = new List<SceneObjectInstanced>();

            // setup shader group for the mitochondria
            Shader mitoShader = new Shader("Shaders/flat_instanced.vert", "Shaders/flat_instanced.frag");
            mitoShader.Use();

            (float[] mitoVertexData, int[] mitoIndices) = ObjLoader.GetVertexData("Meshes/Sphere.obj");
            mitoShaderGroup = new ShaderGroup(mitoVertexData, mitoIndices, mitoShader);

            // size the nucleus so its drawn radius equals CellGenerator.nucleusRadius (the radius the tubes are rooted on)
            float nucleusScale = CellGenerator.nucleusRadius / Mitochondrion.MeshRadius;
            nucleus = new SceneObject(Vector3.Zero, Quaternion.Identity, new Vector3(nucleusScale));

            // setup shader group for the nucleus
            Shader nucleusShader = new Shader("Shaders/shaded.vert", "Shaders/shaded.frag");
            nucleusShader.Use();

            (float[] nucleusVertexData, int[] nucleusIndices) = ObjLoader.GetVertexData("Meshes/Sphere.obj");
            nucleusMeshRenderer = new MeshRenderer(nucleusVertexData, nucleusIndices, nucleusShader);

            // no cytoskeleton until the user presses Create
            branches = new List<Branch>();
        }

        public void Render(Camera camera, Vector3 lightPosition)
        {
            Matrix4 viewMatrix = camera.GetViewMatrix();
            Matrix4 projectionMatrix = camera.GetProjectionMatrix();

            Vector3 cameraPosition = camera.Position;

            if (renderMitos)
            {
                mitoShaderGroup.Render(mitochondria, viewMatrix, projectionMatrix);

                // Creates ribbons to link the mitos from their centres, ribbon lines always face the camera
                if (CellGenerator.smoothLines)
                {
                    chainRenderer.Render(mitochondria, viewMatrix, projectionMatrix, cameraPosition);
                }
            }

            if (renderCytoskeleton)
            {
                branchRenderer.Render(branches, viewMatrix, projectionMatrix, cameraPosition);
            }

            if (renderNucleus)
            {
                nucleusMeshRenderer.Render(nucleus.GetTransformMatrix(), viewMatrix, projectionMatrix, lightPosition, cameraPosition);
            }
        }
    }
}