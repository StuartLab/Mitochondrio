using System;
using System.Collections.Generic;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ImGuiNET;
using Mitochondrio_OpenGL.Scripts;
using Mitochondrio_OpenGL.Backends;
using static Mitochondrio_OpenGL.Scripts.Utils;

internal class Scene : GameWindow
{
    // real time between frames (camera, UI)
    public static float deltaTime;

    // simulation time between frames: real time scaled by the Simulation Speed slider
    public static float simDeltaTime;

    // Time smoothed out between the last two frames
    private float smoothDeltaTime = 0;

    public bool renderUI = true;

    Camera _camera = null!;
    private Vector2 _lastPos;

    public static SpatialGrid? partition;

    public Cell mainCell = null!;

    private Vector3 _lightPos = new Vector3(0, -50, 0);

    // where the camera starts, and where "Reset View" puts it
    private static readonly Vector3 DefaultCameraPosition = new Vector3(0, 4, 0);
    private const float DefaultCameraYaw = -90f;
    private const float DefaultCameraPitch = -89.9f; // looking straight down

    private ImGuiController _controller = null!;
    private Bloom _bloom = null!;

    // reused every frame so the collision pass doesn't allocate
    private readonly List<Mitochondrion> _nearby = new();
    private readonly List<Mitochondrion> _exited = new();

    public Scene() : base(GameWindowSettings.Default,
        new NativeWindowSettings()
        {
            ClientSize = new Vector2i(1280, 720),
            Title = "Mitochondrio",
            NumberOfSamples = 4
        })
    {
        GLFW.WindowHint(WindowHintBool.ScaleToMonitor, false);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, FramebufferSize.X, FramebufferSize.Y);

        if (_camera != null && ClientSize.Y > 0)
        {
            _camera.AspectRatio = (float)ClientSize.X / ClientSize.Y;
        }

        _controller?.WindowResized(ClientSize.X, ClientSize.Y);
        _bloom?.Resize(FramebufferSize.X, FramebufferSize.Y);
    }

    // GL METHODS
    protected override void OnLoad()
    {
        base.OnLoad();

        // ClientSize is the interior of the window, excluding the title bar
        _controller = new ImGuiController(ClientSize.X, ClientSize.Y);

        _camera = new Camera(DefaultCameraPosition, (float)ClientSize.X / ClientSize.Y);

        // Post-processing in framebuffer pixels
        _bloom = new Bloom(FramebufferSize.X, FramebufferSize.Y);
        //CursorState = CursorState.Grabbed;

        GL.Enable(EnableCap.Multisample);
        GL.Enable(EnableCap.DepthTest);

        partition = new SpatialGrid();

        mainCell = new Cell();
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        foreach (char c in e.AsString)
        {
            _controller.PressChar(c);
        }
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        // If bloom is on, render the scene to a frame buffer and apply the effect. Otherwise,
        // just draw the scene normally
        bool bloomOn = SimSettings.BloomStrength > 0.001f;

        if (bloomOn)
        {
            _bloom.StartScene();
        }
        else
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, FramebufferSize.X, FramebufferSize.Y);
        }

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _controller.Update(this, (float)e.Time);

        mainCell.Render(_camera, _lightPos);

        if (bloomOn)
        {
            _bloom.EndScene(SimSettings.BloomStrength); // draws scene + glow to the window
        }

        // the UI is drawn after bloom, straight onto the window
        if (renderUI)
        {
            ControlPanel.Draw(this);
            _controller.Render();
        }

        SwapBuffers();
    }

    // Called by the UI camera tab
    public void ResetCameraView()
    {
        _camera.Position = DefaultCameraPosition;
        _camera.Yaw = DefaultCameraYaw;
        _camera.Pitch = DefaultCameraPitch;
    }

    // ------------------------------------------------------------------
    // Create / Restart
    // ------------------------------------------------------------------

    // Makes a new cell to the specifications of the UI. Called by the "Create" button
    public void CreateCell()
    {
        ControlPanel.Sanitize();
        CellGenerator.Regenerate(mainCell);
        RestartMitos();
    }

    // True while a cell exists
    public bool HasCell => mainCell != null && mainCell.branches.Count > 0;

    // Clears the cytoskeleton and mitochondria of the cell
    public void ClearCell()
    {
        partition?.Clear();
        mainCell.mitochondria.Clear();
        Mitochondrion.numMitos = 0;

        mainCell.branches = new List<Branch>();
    }

    // Delete and respawn all mitochondria on the already existing cytoskeleton
    public void RestartMitos()
    {
        ControlPanel.Sanitize();

        if (partition == null) return;

        partition.Clear();
        mainCell.mitochondria.Clear();
        Mitochondrion.numMitos = 0;

        // microtubules only (mitos don't spawn on actins right now)
        int skipped = 0;
        foreach (Branch branch in mainCell.branches)
        {
            if (!branch.isMainBranch || branch.NodeCount() < 2) continue;

            int requested = RandomRange(CellGenerator.mitosPerMicrotubuleMin, CellGenerator.mitosPerMicrotubuleMax + 1);
            skipped += SpawnMitosOn(branch, requested);
        }

        if (skipped > 0)
        {
            Console.WriteLine($"Spawn: {skipped} mitochondria skipped (no room on the tube at diameter {SimSettings.MitoDiameter}).");
        }
    }

    // Spawns up to requested mitos on one branch so that none of them overlap. Returns how many did not fit.
    // Mitos are spread out enough to not overlap
    private int SpawnMitosOn(Branch branch, int requested)
    {
        if (requested <= 0) return 0;

        int nodes = branch.NodeCount();
        float d = SimSettings.MitoDiameter;

        // Segment length at every node
        float[] lengths = new float[nodes];
        for (int i = 1; i < nodes; i++)
        {
            lengths[i] = lengths[i - 1] + Vector3.Distance(branch.GetNode(i - 1).Position, branch.GetNode(i).Position);
        }

        float span = lengths[nodes - 1] - d;
        if (span < 0f) return requested;

        int capacity = (int)MathF.Floor(span / d) + 1;
        int count = Math.Min(requested, capacity);
        float slack = span - (count - 1) * d;

        float[] offsets = new float[count];
        for (int i = 0; i < count; i++) offsets[i] = RandomRange(0f, slack);
        Array.Sort(offsets);

        int skipped = requested - count;
        int seg = 0;
        for (int i = 0; i < count; i++)
        {
            float s = d * 0.5f + offsets[i] + i * d;

            // offsets are sorted, so the segment index only ever moves forward
            while (seg < nodes - 2 && lengths[seg + 1] < s) seg++;

            float segLen = lengths[seg + 1] - lengths[seg];
            float t = segLen > 1e-6f ? Math.Clamp((s - lengths[seg]) / segLen, 0f, 1f) : 0f;

            BranchNode a = branch.GetNode(seg);
            BranchNode c = branch.GetNode(seg + 1);
            Vector3 position = Vector3.Lerp(a.Position, c.Position, t);

            // spacing within the branch is guaranteed; this catches mitos from other branches (junctions, tubes
            // that run close together)
            if (OverlapsExistingMito(position, d))
            {
                skipped++;
                continue;
            }

            SpawnMitoAt(a, c, t, position);
        }

        return skipped;
    }

    // true if any existing mito is closer than one diameter to position
    private bool OverlapsExistingMito(Vector3 position, float diameter)
    {
        _nearby.Clear();
        partition!.QueryRadius(position, Math.Max(1, (int)MathF.Ceiling(diameter)), _nearby); // buckets are 1.0 wide

        float minSq = diameter * 0.999f * diameter * 0.999f;
        foreach (Mitochondrion other in _nearby)
        {
            if (Vector3.DistanceSquared(other.Position, position) < minSq) return true;
        }

        return false;
    }

    // Spawns a mito 't' distance between two nodes
    private void SpawnMitoAt(BranchNode a, BranchNode c, float t, Vector3 position)
    {
        var mito = new Mitochondrion(mainCell, a, c, position, Quaternion.Identity, Vector3.One);
        mito.ApplySettings();
        mito.progressToNextNode = t;
        mito.mitoID = Mitochondrion.numMitos++;

        mainCell.mitochondria.Add(mito);
        partition!.AddMito(mito);
    }

    // Spawn a single extra mito on a random microtubule, at a spot that isn't already occupied
    private void SpawnMito()
    {
        if (partition == null || mainCell.branches.Count == 0) return;

        for (int attempt = 0; attempt < 40; attempt++)
        {
            Branch branch = mainCell.branches[RandomRange(0, mainCell.branches.Count)];
            if (!branch.isMainBranch || branch.NodeCount() < 2) continue;

            int i = RandomRange(0, branch.NodeCount() - 1);
            BranchNode a = branch.GetNode(i);
            BranchNode c = branch.GetNode(i + 1);
            float t = RandomRange(0f, 1f);
            Vector3 position = Vector3.Lerp(a.Position, c.Position, t);

            if (OverlapsExistingMito(position, SimSettings.MitoDiameter)) continue;

            SpawnMitoAt(a, c, t, position);
            return;
        }
    }

    // Pushes the panel's mito settings to every mito (only when something changed)
    private void ApplyMitoSettings()
    {
        foreach (Mitochondrion mito in mainCell.mitochondria)
        {
            mito.ApplySettings();
        }

        SimSettings.MitoDirty = false;
    }



    // Update methods for OpenTK

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);

        deltaTime = (float)e.Time;

        // the 0.1 s cap keeps one long hitch from throwing mitos several nodes ahead
        simDeltaTime = Math.Min(deltaTime, 0.1f) * SimSettings.SimulationSpeed;

        // get average of last 2 frames' fps
        smoothDeltaTime += Math.Max(float.Epsilon, deltaTime);
        smoothDeltaTime /= 2f;
        Title = "FPS -- " + (1f / smoothDeltaTime).ToString("F0");

        if (SimSettings.MitoDirty)
        {
            ApplyMitoSettings();
        }

        // 1. move every mito
        foreach (Mitochondrion mito in mainCell.mitochondria)
        {
            mito.Update();
        }

        // 2. re-bucket EVERY mito now that movement is done
        if (partition != null)
        {
            foreach (Mitochondrion mito in mainCell.mitochondria)
            {
                partition.UpdateObject(mito);
            }

            UpdateCollisions();
            partition.ClearDirtyBuckets();
        }

        // 3. get mouse and keyboard inputs
        ImGuiIOPtr io = ImGui.GetIO();

        var mouse = MouseState;
        var deltaX = mouse.X - _lastPos.X;
        var deltaY = mouse.Y - _lastPos.Y;
        _lastPos = new Vector2(mouse.X, mouse.Y);

        if (!IsFocused) return;

        var input = KeyboardState;

        if (input.IsKeyDown(Keys.Escape))
        {
            Close();
        }

        // set on the Camera tab
        float cameraSpeed = CameraSettings.MoveSpeed;
        float panSensitivity = CameraSettings.PanSensitivity;
        float sensitivity = CameraSettings.LookSensitivity;

        // don't drive the camera while the UI is using the keyboard (e.g. typing in a text box)
        if (!io.WantCaptureKeyboard)
        {
            if (input.IsKeyDown(Keys.W))
            {
                _camera.Position += _camera.Front.Scaled(new Vector3(1, 0, 1)).Normalized() * cameraSpeed * deltaTime; // Forward
            }
            if (input.IsKeyDown(Keys.S))
            {
                _camera.Position -= _camera.Front.Scaled(new Vector3(1, 0, 1)).Normalized() * cameraSpeed * deltaTime; // Backwards
            }
            if (input.IsKeyDown(Keys.A))
            {
                _camera.Position -= _camera.Right * cameraSpeed * deltaTime; // Left
            }
            if (input.IsKeyDown(Keys.D))
            {
                _camera.Position += _camera.Right * cameraSpeed * deltaTime; // Right
            }
            if (input.IsKeyDown(Keys.Space))
            {
                _camera.Position += Vector3.UnitY * cameraSpeed * deltaTime; // Up
            }
            if (input.IsKeyDown(Keys.LeftShift))
            {
                _camera.Position -= Vector3.UnitY * cameraSpeed * deltaTime; // Down
            }

            if (input.IsKeyPressed(Keys.N))
            {
                // Used for testing, turned off
                //SpawnMito();
            }
        }

        // don't rotate/pan the camera while dragging a UI window
        if (!io.WantCaptureMouse)
        {
            if (mouse.IsButtonDown(MouseButton.Right))
            {
                _camera.Yaw += deltaX * sensitivity * deltaTime;
                _camera.Pitch -= deltaY * sensitivity * deltaTime;
            }
            if (mouse.IsButtonDown(MouseButton.Middle))
            {
                _camera.Position -= _camera.Right * panSensitivity * deltaX * deltaTime;
                _camera.Position += _camera.Up * panSensitivity * deltaY * deltaTime;
            }
        }
    }

    // Collision events for every mito against its neighbours
    private void UpdateCollisions()
    {
        if (partition == null) return;

        foreach (Mitochondrion mito in mainCell.mitochondria)
        {
            _nearby.Clear();
            partition.QueryRadius(mito.Position, 1, _nearby);

            foreach (Mitochondrion other in _nearby)
            {
                if (other != mito)
                {
                    mito.CheckCollision(other);
                }
            }

            // Check for mitos that are exiting collision this frame, trigger their OnExit events
            if (mito.activeCollisions.Count > 0)
            {
                _exited.Clear();
                foreach (Mitochondrion other in mito.activeCollisions)
                {
                    if (!_nearby.Contains(other))
                    {
                        _exited.Add(other);
                    }
                }

                foreach (Mitochondrion other in _exited)
                {
                    mito.CheckCollision(other);
                }
            }
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        _controller.MouseScroll(new Vector2(e.OffsetX, e.OffsetY));

        if (!ImGui.GetIO().WantCaptureMouse)
        {
            _camera.Position += _camera.Front * e.OffsetY * deltaTime * 35f;
        }
    }

    protected override void OnUnload()
    {
        base.OnUnload();
        _controller?.Dispose();
        _bloom?.Dispose();
    }
}

internal static class Program
{
    static void Main()
    {
        using var scene = new Scene();
        scene.Run();
    }
}