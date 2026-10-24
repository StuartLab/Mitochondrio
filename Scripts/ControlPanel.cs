using System;
using System.Numerics;
using ImGuiNET;

namespace Mitochondrio_OpenGL.Scripts
{
    // Everything the control panel edits that doesn't already live in CellGenerator.
    internal static class SimSettings
    {
        // Mitochondria tab
        public static float FissionPercent = 5f;   // chance per second that a chained mito detaches from its parent
        public static float FusionPercent = 100f;   // chance that two touching chain heads actually fuse
        public static float MitoDiameter = 0.2f;    // world units

        // Simulation tab
        public static float SliceDepth = 0.15f;     // (unused)
        public static float MitoSpeed = 0.75f;      // world units per second along the cytoskeleton
        public static float SimulationSpeed = 1f;   // time scale for the whole simulation (0 = paused)
        public static float BloomStrength = 1.6f;   // how strongly bright areas glow (0 = bloom off)

        // Whenever a mito setting changes, Scene pushes the new values to every mito and clears it
        public static bool MitoDirty = true;
    }

    // Camera tab. The sliders work in 0-100; the real values are mapped linearly onto these ranges.
    internal static class CameraSettings
    {
        // Movement speed using WASD / Arrow Keys (world units per second)
        public const float MoveSpeedMin = 0.5f, MoveSpeedMax = 3f, MoveSpeedDefault = 1.5f;

        // Middle-mouse panning
        public const float PanMin = 1f, PanMax = 15f, PanDefault = 5f;

        // Right-mouse looking
        public const float LookMin = 10f, LookMax = 30f, LookDefault = 20f;

        // Slider positions (0-100), starting at the defaults above
        public static float MoveSpeedPercent = ToPercent(MoveSpeedDefault, MoveSpeedMin, MoveSpeedMax);
        public static float PanPercent = ToPercent(PanDefault, PanMin, PanMax);
        public static float LookPercent = ToPercent(LookDefault, LookMin, LookMax);

        // the values the camera code actually uses
        public static float MoveSpeed => FromPercent(MoveSpeedPercent, MoveSpeedMin, MoveSpeedMax);
        public static float PanSensitivity => FromPercent(PanPercent, PanMin, PanMax);
        public static float LookSensitivity => FromPercent(LookPercent, LookMin, LookMax);

        public static void ResetSensitivities()
        {
            MoveSpeedPercent = ToPercent(MoveSpeedDefault, MoveSpeedMin, MoveSpeedMax);
            PanPercent = ToPercent(PanDefault, PanMin, PanMax);
            LookPercent = ToPercent(LookDefault, LookMin, LookMax);
        }

        private static float ToPercent(float value, float min, float max) => (value - min) / (max - min) * 100f;

        private static float FromPercent(float percent, float min, float max) =>
            min + Math.Clamp(percent, 0f, 100f) / 100f * (max - min);
    }

    // Draws the actual UI panel 
    internal static class ControlPanel
    {
        private static readonly string[] TabNames = { "Cytoskeleton", "Mitochondria", "Simulation", "Camera" };
        private static int _tab = 0;

        public static void Draw(Scene scene)
        {
            ImGui.SetNextWindowPos(new Vector2(16, 16), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(340, 540), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Controls"))
            {
                // the dropdown that picks which settings page is shown
                ImGui.SetNextItemWidth(-1f);
                ImGui.Combo("##tab", ref _tab, TabNames, TabNames.Length);
                ImGui.Separator();

                // settings page; leave room underneath for the buttons (and the density warning, if there is one)
                // Warns if the number of mitos called for is too dense for the microtubule length to prevent overlapping
                string? warning = DensityWarning();
                float reserved = ImGui.GetFrameHeightWithSpacing() * 2f
                               + (warning != null ? ImGui.GetTextLineHeightWithSpacing() * 4f : 0f);

                if (ImGui.BeginChild("##panel", new Vector2(0, -reserved)))
                {
                    switch (_tab)
                    {
                        case 0: DrawCytoskeletonTab(scene); break;
                        case 1: DrawMitochondriaTab(); break;
                        case 2: DrawSimulationTab(); break;
                        case 3: DrawCameraTab(scene); break;
                    }
                }
                ImGui.EndChild();

                // Clamp values once the user has stopped typing
                if (!ImGui.IsAnyItemActive())
                {
                    Sanitize();
                }

                ImGui.Separator();

                if (warning != null)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.75f, 0.2f, 1f));
                    ImGui.TextWrapped(warning);
                    ImGui.PopStyleColor();
                }

                float buttonWidth = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X * 2f) / 3f;
                if (ImGui.Button("Create", new Vector2(buttonWidth, 0)))
                {
                    scene.CreateCell();
                }

                // Restart and Clear only exist when a cell exists
                ImGui.BeginDisabled(!scene.HasCell);

                ImGui.SameLine();
                if (ImGui.Button("Restart", new Vector2(buttonWidth, 0)))
                {
                    scene.RestartMitos();
                }

                ImGui.SameLine();
                if (ImGui.Button("Clear", new Vector2(buttonWidth, 0)))
                {
                    scene.ClearCell();
                }

                ImGui.EndDisabled();
            }
            ImGui.End();
        }

        // ------------------------------------------------------------------
        // Tabs
        // ------------------------------------------------------------------

        private static void DrawCytoskeletonTab(Scene scene)
        {
            ImGui.Text("# Microtubules");
            ImGui.SetNextItemWidth(80f);
            ImGui.InputInt("##numMicrotubules", ref CellGenerator.numMainBranches, 0, 0);

            ImGui.Spacing();
            ImGui.Text("Cytoskeleton Dimensions");
            ImGui.PushID("dimensions");
            ImGui.SetNextItemWidth(60f);
            ImGui.InputFloat("##x", ref CellGenerator.cytoDimensions.X, 0f, 0f, "%.2f");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60f);
            ImGui.InputFloat("##y", ref CellGenerator.cytoDimensions.Y, 0f, 0f, "%.2f");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60f);
            ImGui.InputFloat("##z", ref CellGenerator.cytoDimensions.Z, 0f, 0f, "%.2f");
            ImGui.PopID();

            ImGui.Spacing();
            IntRange("Microtubule Length", ref CellGenerator.microtubuleLengthMin, ref CellGenerator.microtubuleLengthMax);

            ImGui.Spacing();
            IntRange("# Actin Filaments", ref CellGenerator.numActinsMin, ref CellGenerator.numActinsMax);

            ImGui.Spacing();
            IntRange("Actin Length", ref CellGenerator.actinLengthMin, ref CellGenerator.actinLengthMax);

            ImGui.Spacing();
            IntRange("# Mitos per Microtubule", ref CellGenerator.mitosPerMicrotubuleMin, ref CellGenerator.mitosPerMicrotubuleMax);

            ImGui.Spacing();
            ImGui.Checkbox("Cytoskeleton", ref scene.mainCell.renderCytoskeleton);
            ImGui.Checkbox("Nucleus", ref scene.mainCell.renderNucleus);
        }

        private static void DrawMitochondriaTab()
        {
            ImGui.Text("Fission probability (% per second):");
            ImGui.SetNextItemWidth(80f);
            if (ImGui.InputFloat("##fission", ref SimSettings.FissionPercent, 0f, 0f, "%.1f"))
                SimSettings.MitoDirty = true;

            ImGui.Spacing();
            ImGui.Text("Fusion probability (%):");
            ImGui.SetNextItemWidth(80f);
            if (ImGui.InputFloat("##fusion", ref SimSettings.FusionPercent, 0f, 0f, "%.1f"))
                SimSettings.MitoDirty = true;

            ImGui.Spacing();
            ImGui.Text("Diameter:");
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.SliderFloat("##diameter", ref SimSettings.MitoDiameter, 0.05f, 2f, "%.2f"))
                SimSettings.MitoDirty = true;

            ImGui.Spacing();
            ImGui.Checkbox("Toggle Smoothing", ref CellGenerator.smoothLines);
        }

        private static void DrawSimulationTab()
        {
            ImGui.Text("View Slice Depth:");
            ImGui.SetNextItemWidth(-1f);
            ImGui.SliderFloat("##slice", ref SimSettings.SliceDepth, 0f, 1f, "%.2f");

            ImGui.Spacing();
            ImGui.Text("Mitochondria Speed:");
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.SliderFloat("##mitoSpeed", ref SimSettings.MitoSpeed, 0f, 5f, "%.2f"))
                SimSettings.MitoDirty = true;

            ImGui.Spacing();
            ImGui.Text("Simulation Speed:");
            ImGui.SetNextItemWidth(-1f);
            ImGui.SliderFloat("##simSpeed", ref SimSettings.SimulationSpeed, 0f, 2f, "%.2f");

            ImGui.Spacing();
            ImGui.Text("Bloom Strength:");
            ImGui.SetNextItemWidth(-1f);
            ImGui.SliderFloat("##bloom", ref SimSettings.BloomStrength, 0f, 2f, "%.2f");
        }

        private static void DrawCameraTab(Scene scene)
        {
            ImGui.Text("Movement Speed:");
            ImGui.SetNextItemWidth(-1f);
            ImGui.SliderFloat("##camSpeed", ref CameraSettings.MoveSpeedPercent, 0f, 100f, "%.0f");

            ImGui.Spacing();
            ImGui.Text("Pan Sensitivity:");
            ImGui.SetNextItemWidth(-1f);
            ImGui.SliderFloat("##camPan", ref CameraSettings.PanPercent, 0f, 100f, "%.0f");

            ImGui.Spacing();
            ImGui.Text("Look Sensitivity:");
            ImGui.SetNextItemWidth(-1f);
            ImGui.SliderFloat("##camLook", ref CameraSettings.LookPercent, 0f, 100f, "%.0f");

            ImGui.Spacing();
            if (ImGui.Button("Reset Sensitivities", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
            {
                CameraSettings.ResetSensitivities();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("Reset View", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
            {
                scene.ResetCameraView();
            }
        }

        // "label" followed by "[min] - [max]" boxes
        private static void IntRange(string label, ref int min, ref int max)
        {
            ImGui.Text(label);
            ImGui.PushID(label);
            ImGui.SetNextItemWidth(70f);
            ImGui.InputInt("##min", ref min, 0, 0);
            ImGui.SameLine();
            ImGui.Text("-");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(70f);
            ImGui.InputInt("##max", ref max, 0, 0);
            ImGui.PopID();
        }

        // ------------------------------------------------------------------
        // Density warning
        // ------------------------------------------------------------------

        // Mitos are spawned at least one diameter apart, so a tube only holds floor(length / diameter) of them.
        // Warns (null = fine) when the shortest microtubule can't hold the largest requested number.
        // Tube length is estimated: segments are branchSegmentSize long and mostly run in the X/Z plane, so the
        // cytoskeleton dimensions stretch them by about the average of X and Z.
        private static string? DensityWarning()
        {
            float diameter = SimSettings.MitoDiameter;
            if (diameter <= 0f) return null;

            float stretch = (Math.Max(CellGenerator.cytoDimensions.X, 0.01f) + Math.Max(CellGenerator.cytoDimensions.Z, 0.01f)) * 0.5f;
            float shortest = (Math.Max(CellGenerator.microtubuleLengthMin, 2) - 1) * CellGenerator.branchSegmentSize * stretch;

            int capacity = (int)MathF.Floor(shortest / diameter);
            int requested = CellGenerator.mitosPerMicrotubuleMax;

            if (requested <= capacity) return null;

            return $"Too dense: the shortest microtubules (~{shortest:F1} long) fit about {capacity} mitos of this " +
                   $"diameter, but up to {requested} are requested. The extras won't be spawned.";
        }

        // Keeps every setting in a range the generator can handle. Also called right before Create/Restart,
        // in case the user clicked the button while still typing in a box.
        public static void Sanitize()
        {
            CellGenerator.numMainBranches = Math.Clamp(CellGenerator.numMainBranches, 1, 500);

            // microtubules need at least 3 nodes so a child branch has an interior node to grow from
            CellGenerator.microtubuleLengthMin = Math.Clamp(CellGenerator.microtubuleLengthMin, 3, 500);
            CellGenerator.microtubuleLengthMax = Math.Clamp(CellGenerator.microtubuleLengthMax, CellGenerator.microtubuleLengthMin, 500);

            CellGenerator.numActinsMin = Math.Clamp(CellGenerator.numActinsMin, 0, 50);
            CellGenerator.numActinsMax = Math.Clamp(CellGenerator.numActinsMax, CellGenerator.numActinsMin, 50);

            CellGenerator.actinLengthMin = Math.Clamp(CellGenerator.actinLengthMin, 2, 500);
            CellGenerator.actinLengthMax = Math.Clamp(CellGenerator.actinLengthMax, CellGenerator.actinLengthMin, 500);

            CellGenerator.mitosPerMicrotubuleMin = Math.Clamp(CellGenerator.mitosPerMicrotubuleMin, 0, 100);
            CellGenerator.mitosPerMicrotubuleMax = Math.Clamp(CellGenerator.mitosPerMicrotubuleMax, CellGenerator.mitosPerMicrotubuleMin, 100);

            CellGenerator.cytoDimensions.X = Math.Clamp(CellGenerator.cytoDimensions.X, 0.1f, 10f);
            CellGenerator.cytoDimensions.Y = Math.Clamp(CellGenerator.cytoDimensions.Y, 0.1f, 10f);
            CellGenerator.cytoDimensions.Z = Math.Clamp(CellGenerator.cytoDimensions.Z, 0.1f, 10f);

            float fission = Math.Clamp(SimSettings.FissionPercent, 0f, 100f);
            float fusion = Math.Clamp(SimSettings.FusionPercent, 0f, 100f);
            if (fission != SimSettings.FissionPercent || fusion != SimSettings.FusionPercent)
            {
                SimSettings.FissionPercent = fission;
                SimSettings.FusionPercent = fusion;
                SimSettings.MitoDirty = true;
            }
        }
    }
}