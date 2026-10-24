using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Mitochondrio_OpenGL.Scripts.Utils;

namespace Mitochondrio_OpenGL.Scripts
{
    internal struct CellData
    {
        public List<Branch> branches;

        public CellData()
        {
            branches = new List<Branch>();
        }
    }

    // Generates a cell based on the static parameters, adjusted by the control panel UI.
    // Settings that are a range are randomized according to a bell curve, with the value
    // between the min and max being the most common.
    internal static class CellGenerator
    {
        // Main branches (microtubules), actin filaments branch off of these
        public static int numMainBranches = 10;

        // Length in number of nodes
        public static int microtubuleLengthMin = 5;
        public static int microtubuleLengthMax = 15;

        // Spacing of nodes along branches
        public static float branchSegmentSize = 0.4f;

        // Number of actin filaments that branch off of the main branches
        public static int numActinsMin = 1;
        public static int numActinsMax = 3;

        // Length in number of nodes
        public static int actinLengthMin = 5;
        public static int actinLengthMax = 9;

        // Number of mitos to spawn per microtubule
        public static int mitosPerMicrotubuleMin = 3;
        public static int mitosPerMicrotubuleMax = 6;

        public static bool smoothLines = true;

        public static bool branchesEnabled = true;
        public static float nucleusRadius = 0.5f;
        public static Vector3 nucleusCenter = Vector3.Zero;
        public static bool useSphericalMicrotubules;

        public static Vector3 cytoDimensions = new Vector3(1, 1, 1);
        public static Vector3 cytoBranchJitter = new Vector3(0.2f, 0.05f, 0.2f);

        // Creates a brand new Cell (including its GPU resources) with a freshly generated cytoskeleton.
        public static Cell GenerateCell()
        {
            Cell cell = new Cell();
            Regenerate(cell);

            return cell;
        }

        // Rebuilds only the cytoskeleton of an existing cell using the current settings.
        // No new GPU resources are created
        public static void Regenerate(Cell cell)
        {
            CellData data = new CellData();
            CreateBranches(data);
            ApplyDimensions(data);

            cell.branches = data.branches;
        }

        // Stretches or flattens the generated cytoskeleton around the nucleus by cytoDimensions (x, y, z)
        private static void ApplyDimensions(CellData cellData)
        {
            if (cytoDimensions == Vector3.One) return;

            foreach (Branch branch in cellData.branches)
            {
                foreach (BranchNode node in branch.GetNodes())
                {
                    node.Position = nucleusCenter + (node.Position - nucleusCenter).Scaled(cytoDimensions);
                }
            }
        }

        // Generates all of the microtubule branches. Actin branches are built in CreateBranch.
        private static CellData CreateBranches(CellData cellData)
        {
            for (int branchNum = 0; branchNum < numMainBranches; branchNum++)
            {
                float branchAngle = MathF.PI * 2f * branchNum / (float)numMainBranches;

                Branch newBranch;
                if (!useSphericalMicrotubules)
                {
                    var rootX = nucleusCenter.X + nucleusRadius * MathF.Cos(branchAngle);
                    var rootZ = nucleusCenter.Z + nucleusRadius * MathF.Sin(branchAngle);

                    newBranch = CreateBranch(cellData, branchAngle, new Vector3(rootX, 0f, rootZ));
                }
                else
                {
                    Vector3 randomRoot = new Vector3(RandomGaussian(), RandomGaussian(), RandomGaussian()).Normalized() * nucleusRadius;

                    newBranch = CreateBranch(cellData, branchAngle, randomRoot + nucleusCenter);
                }
            }

            return cellData;
        }

        private static Branch CreateBranch(CellData cellData, float angle, Vector3 origin)
        {
            // One in four microtubules spawn slighty away from the nucleus, and at a slightly off angle.
            if (RandomRange(0f, 1f) < 0.25f)
            {
                // Scale the origin away from the centre of the nucleus
                float scaler = BellCurve(1.2f, 1.6f);
                origin = origin.Scaled(scaler);
                // Slightly skew angle
                angle += BellCurve(-MathF.PI / 3f, MathF.PI / 3f);
            }

            // MAIN BRANCH
            Branch newBranch = CreateBranchNodes(cellData, angle, origin, (int)MathF.Round(BellCurve(microtubuleLengthMin, microtubuleLengthMax)));

            int numBranches = (int)MathF.Round(BellCurve(numActinsMin, numActinsMax));
            numBranches = Math.Max(numBranches, 0);

            // MICROTUBULE SPLITS
            for (int i = 0; i < (int)MathF.Round(Math.Abs(BellCurve(-1f, 1f))); i++)
            {
                // 0-60 degrees for these splits
                Branch newChildBranch = CreateChildBranch(cellData, newBranch, BellCurve(0f, MathF.PI / 6f));

                newChildBranch.isMainBranch = true;
            }

            // ACTINS
            for (int i = 0; i < numBranches; i++)
            {
                // 0-90 degrees
                Branch newChildBranch = CreateChildBranch(cellData, newBranch, BellCurve(0f, MathF.PI));

                newChildBranch.isMainBranch = false;
            }

            //newBranch.UpdateBranchVisuals();

            return newBranch;
        }

        // Makes an actin filament that branches off of the microtubule parentBranch. The node off of which an actin branches
        // is randomized here, not in the input to this method
        private static Branch CreateChildBranch(CellData cellData, Branch parentBranch, float branchAngle)
        {
            // can't branch off of first or last nodes
            int nodeIndex = RandomRange(1, parentBranch.NodeCount() - 1);
            BranchNode newParentNode = parentBranch.GetNode(nodeIndex);

            // Find the angle that the parent branch is going at the given node, then make the actin brnach off 90 degrees left or right
            Vector2 segmentDirection = newParentNode.FlatDirection;

            float segmentAngle = MathF.Atan2(segmentDirection.Y, segmentDirection.X);
            float bonusAngle = branchAngle * RandomRange(-1, 2);
            float finalAngle = segmentAngle + bonusAngle;

            float x = MathF.Cos(finalAngle);
            float z = MathF.Sin(finalAngle);
            Vector3 d = new Vector3(x, 0, z).Normalized() * branchSegmentSize;

            Branch newChildBranch = CreateBranchNodes(cellData, finalAngle, newParentNode.Position + d, (int)MathF.Round(BellCurve(actinLengthMin, actinLengthMax)));
            newChildBranch.parentNode = newParentNode;

            // Now duct tape the child onto its parent node
            newParentNode.Neighbours.Add(newChildBranch.GetNode(0));
            newChildBranch.GetNode(0).Neighbours.Add(newParentNode);

            return newChildBranch;
        }

        // Creates a Branch object and its nodes, and adds it to the given Cell, and returns the Branch
        private static Branch CreateBranchNodes(CellData cellData, float angle, Vector3 origin, int length)
        {
            Branch branch = new Branch();

            Vector3 currentPosition = origin;

            // Convert the angle into a direction vector
            Vector3 currentDirection;
            if (!useSphericalMicrotubules)
            {
                float x = MathF.Cos(angle);
                float z = MathF.Sin(angle);
                currentDirection = new Vector3(x, 0, z).Normalized() * branchSegmentSize;
            }
            else
            {
                currentDirection = (origin - nucleusCenter).Normalized() * branchSegmentSize;
            }

            // Add the branch origin to start with
            branch.AddNode(CreateBranchNode(branch, origin, currentDirection));

            for (int i = 1; i < length; i++)
            {
                // get the new node position from adding the direction to the last node's position
                BranchNode lastNode = branch.GetNode(i - 1);
                Vector3 newPosition = lastNode.Position + currentDirection;


                // jitter the direction a little bit
                //currentDirection += new Vector3(
                //    Random.Range(-0.1f, 0.1f),
                //    Random.Range(-0.05f, 0.05f),
                //    Random.Range(-0.1f, 0.1f)
                //);

                // Jitter the direction vector randomly so the branch isn't straight
                currentDirection += Vector3.Lerp(-cytoBranchJitter, cytoBranchJitter, RandomRange(0f, 1f));
                currentDirection = currentDirection.Normalized() * branchSegmentSize;

                // If the next position is going to be inside of the centre nucleus, invert the direction
                if (Vector3.Distance(newPosition + currentDirection, nucleusCenter) < nucleusRadius ||
                    Vector3.Distance(newPosition + currentDirection, nucleusCenter) > cytoDimensions.Length * 500f)
                {
                    Vector3 toCenter = (nucleusCenter - newPosition).Normalized();
                    currentDirection = -toCenter * branchSegmentSize;
                }

                BranchNode newNode = CreateBranchNode(branch, newPosition, currentDirection);

                branch.AddNode(newNode);
            }

            cellData.branches.Add(branch);

            return branch;
        }

        // Returns a new node for the branch given a position and direction
        private static BranchNode CreateBranchNode(Branch branch, Vector3 origin, Vector3 currentDirection)
        {
            BranchNode branchNode = new BranchNode(origin);
            branchNode.Direction = currentDirection;
            branchNode.parentBranch = branch;

            return branchNode;
        }
    }
}