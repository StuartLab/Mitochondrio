using System;
using OpenTK.Graphics.OpenGL4;

namespace Mitochondrio_OpenGL.Scripts
{
    // Post-processing bloom adds a glow effect around bright areas
    //
    //   BeginScene()  - the scene is drawn into an offscreen multisampled HDR buffer instead of the window
    //   EndScene()    - that buffer is resolved, bright areas are extracted and blurred over several half-sized
    //                   levels, and scene + glow is drawn to the window
    //
    // UI is drawn after the bloom is applied
    internal class Bloom : IDisposable
    {
        // The threshold past which colour channels add the glow
        public static float Threshold = 0.7f;
        // Softens the cutoff point of threshold, so that channels within Knee of Threshold generate more or less glow
        public static float Knee = 0.3f;

        // How wide the glow reaches: each level is half the size of the one before, and their blurs are summed
        private const int MaxLevels = 6;
        private const int Samples = 4;

        private readonly Shader downShader;
        private readonly Shader upShader;
        private readonly Shader compositeShader;
        private readonly int emptyVao; // core profile needs a VAO bound even when drawing without vertex data

        private int width, height;
        private bool created;

        // multisampled target the scene is drawn into
        private int msaaFbo, msaaColour, msaaDepth;

        // single-sample copy of it, which the bloom passes can read
        private int sceneFbo, sceneTex;

        // the blur chain: level 0 is half the window size, level 1 a quarter, ...
        private int levels;
        private readonly int[] mipFbo = new int[MaxLevels];
        private readonly int[] mipTex = new int[MaxLevels];
        private readonly int[] mipW = new int[MaxLevels];
        private readonly int[] mipH = new int[MaxLevels];

        public Bloom(int width, int height)
        {
            // Applies the glow and downsamples the base render
            downShader = new Shader("Shaders/fullscreen.vert", "Shaders/bloom_downsample.frag");
            // Upsamples the downsampled texture to blur it
            upShader = new Shader("Shaders/fullscreen.vert", "Shaders/bloom_upsample.frag");
            // Adds together the base render of the scene and the bloom effect FrameBuffer
            compositeShader = new Shader("Shaders/fullscreen.vert", "Shaders/bloom_composite.frag");

            emptyVao = GL.GenVertexArray();

            Resize(width, height);
        }

        // Resized when the screen is resized in Program.cs
        public void Resize(int newWidth, int newHeight)
        {
            if (newWidth < 1 || newHeight < 1) return;
            if (created && newWidth == width && newHeight == height) return;

            Destroy();

            width = newWidth;
            height = newHeight;

            Create();
        }

        // Binds the target that the base scene is drawn into before any post-processing
        public void StartScene()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, msaaFbo);
            GL.Viewport(0, 0, width, height);
        }

        // Finish the scene: resolve, bloom, and draw the result to the window
        public void EndScene(float strength)
        {
            // resolve the multisampled image into a texture
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, msaaFbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, sceneFbo);
            GL.BlitFramebuffer(0, 0, width, height, 0, 0, width, height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

            bool cullWasOn = GL.IsEnabled(EnableCap.CullFace);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);

            // Find bright spots to glow, shrink on repeat
            downShader.Use();
            downShader.SetInt("srcTex", 0);
            downShader.SetFloat("threshold", Threshold);
            downShader.SetFloat("knee", Math.Max(Knee, 0.001f));

            for (int i = 0; i < levels; i++)
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, mipFbo[i]);
                GL.Viewport(0, 0, mipW[i], mipH[i]);

                // On the first iteration, find the bright spots. That part can be reused by every further downsample
                downShader.SetInt("prefilter", i == 0 ? 1 : 0);
                BindTexture(i == 0 ? sceneTex : mipTex[i - 1]);
                DrawFullscreen();
            }

            // Blur every level and add it onto the larger one below it
            upShader.Use();
            upShader.SetInt("srcTex", 0);
            upShader.SetFloat("radius", 1f);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);

            for (int i = levels - 1; i >= 1; i--)
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, mipFbo[i - 1]);
                GL.Viewport(0, 0, mipW[i - 1], mipH[i - 1]);

                BindTexture(mipTex[i]);
                DrawFullscreen();
            }

            GL.Disable(EnableCap.Blend);

            // Add the bloom texture to the base scene render
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, width, height);

            compositeShader.Use();
            compositeShader.SetInt("sceneTex", 0);
            compositeShader.SetInt("bloomTex", 1);
            compositeShader.SetFloat("strength", strength);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, mipTex[0]);
            BindTexture(sceneTex); // leaves texture unit 0 active

            // Draw the composite image to the screen
            DrawFullscreen();

            // Set values back to what the rest of the render expects
            GL.BindVertexArray(0);
            GL.Enable(EnableCap.DepthTest);
            if (cullWasOn) GL.Enable(EnableCap.CullFace);
        }

        private void BindTexture(int texture)
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture);
        }

        private void DrawFullscreen()
        {
            GL.BindVertexArray(emptyVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }



        // OpenGL resources

        // Creates the framebuffers for the scene, and sample levels for the downsample layers
        private void Create()
        {
            // Color and depth buffers that the base scene is drawn into
            msaaFbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, msaaFbo);

            msaaColour = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, msaaColour);
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, Samples, RenderbufferStorage.Rgba16f, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, msaaColour);

            msaaDepth = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, msaaDepth);
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, Samples, RenderbufferStorage.DepthComponent24, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, msaaDepth);

            CheckComplete("multisampled scene");

            // The texture that the scene is resolved into
            sceneTex = CreateTexture(width, height);
            sceneFbo = CreateFramebuffer(sceneTex, "resolved scene");

            // Blur chain: half size, quarter size, ... as far as the window is big enough
            levels = 1;
            while (levels < MaxLevels && (width >> (levels + 1)) >= 4 && (height >> (levels + 1)) >= 4)
            {
                levels++;
            }

            for (int i = 0; i < levels; i++)
            {
                mipW[i] = Math.Max(1, width >> (i + 1));
                mipH[i] = Math.Max(1, height >> (i + 1));
                mipTex[i] = CreateTexture(mipW[i], mipH[i]);
                mipFbo[i] = CreateFramebuffer(mipTex[i], $"bloom level {i}");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

            created = true;
        }

        // Generates a texture reference to specified width and height, in rgba format with linear filtering
        private static int CreateTexture(int w, int h)
        {
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, w, h, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            return texture;
        }

        // Generates a framebuffer reference, used for the base scene render and the bloom texture
        private static int CreateFramebuffer(int texture, string name)
        {
            int fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);
            CheckComplete(name);
            return fbo;
        }

        // Verify if the framebuffer is completely rendered or not 
        private static void CheckComplete(string name)
        {
            FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                Console.WriteLine($"Bloom: framebuffer '{name}' is incomplete ({status})");
            }
        }

        private void Destroy()
        {
            if (!created) return;

            GL.DeleteFramebuffer(msaaFbo);
            GL.DeleteRenderbuffer(msaaColour);
            GL.DeleteRenderbuffer(msaaDepth);
            GL.DeleteFramebuffer(sceneFbo);
            GL.DeleteTexture(sceneTex);

            for (int i = 0; i < levels; i++)
            {
                GL.DeleteFramebuffer(mipFbo[i]);
                GL.DeleteTexture(mipTex[i]);
            }

            created = false;
        }

        public void Dispose()
        {
            Destroy();
            GL.DeleteVertexArray(emptyVao);
        }
    }
}