using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using static Mitochondrio_OpenGL.Scripts.Utils;

namespace Mitochondrio_OpenGL.Scripts
{
    internal class Mitochondrion : SceneObjectInstanced
    {
        // Static value, incremented on mito creation. Used for id numbers for comparison
        public static int numMitos = 0;

        // Used to prevent infinite looping
        private const int MaxChainLength = 100;

        // Reused buffer when picking the next segment at a junction
        private static readonly List<BranchNode> _neighbourScratch = new();

        // Unused currently
        public Vector3 lastPosition;

        // The spatial grid bucket this mito currently lives in. Maintained by SpatialGrid
        public Vector3i? gridCell;

        // the cell that this mito is on
        public Cell parentCell;

        // The nodes between which this mito is moving
        public BranchNode origin;
        public BranchNode target;

        // Chain links. parent == null means this mito is the head of its chain.
        public Mitochondrion? parent;
        public Mitochondrion? child;

        // Cooldowns to prevent fusion right after fission
        public float fusionTimeout = 0f;
        public bool turnLockout = false;

        // Lerping value describing where between origin and target this mito lies. only tracked for the head of a chain
        public float progressToNextNode = 0f;

        public float fusionProbability = 1.0f;
        public float fissionProbability = 0.15f;//0.001f;
        public float speed = 0.75f;

        // Chance multiplier for random mid-segment turns (per fraction of a segment travelled). 0 disables them.
        public float turnChance = 0.5f;

        // Radius of the sphere mesh in model units
        public static float MeshRadius = 0.5f;

        // 1 = chain members sit exactly touching, below 1 they begin to overlap
        public static float SpacingFactor = 1f;

        // Seconds that fusion is blocked after fission or a failed fusion roll. While it ticks down, a mito
        // bounces off whatever they touch instead of passing through
        public static float FusionCooldown = 2f;

        // Short lockout after a bounce so a chain squeezed between two blockers can't flip every frame
        private const float BounceCooldownTime = 0.5f;
        private float bounceCooldown = 0f;

        // A chain that reverses at a dead end twice within this many seconds is jammed (see ArriveAtTarget)
        private const float JamWindow = 1.5f;

        // A follower further than this many touching-distances from its parent lets go instead of chasing it
        private const float MaxLinkStretch = 2.5f;
        private float deadEndTimer = 0f;

        // A small cooldown after a random turn (not by hitting something) to prevent too much jittering
        private const float CalmAfterTurn = 0.1f;
        private float calmTimer = 0f;

        // If a mito is trying to move into something it can't fuse with too much, make it sit still temporarily
        private const float BlockedHold = 0.25f;
        private float holdTimer = 0f;

        // Reused buffer when finding blocking mitos that can't be fused with
        private static readonly List<Mitochondrion> _blockScratch = new();

        // How quickly the correction left by BecomeLeader fades out instead of snapping
        private const float SnapSmoothing = 10f;

        // A follower further than this from its cached segment gets a global re-search to find its new origin/target
        private const float RelocateFallbackDistance = 1.0f;

        // Difference between where a mito is drawn and where it is on the tube, to fade between locations when correcting instead of snapping
        private Vector3 snapOffset = Vector3.Zero;

        // World-space radius of this mito's sphere
        public float Radius => Scale.X * MeshRadius;

        public int mitoID;

        public HashSet<Mitochondrion> activeCollisions;

        public Vector3 Colour { get; set; } = new Vector3(0, 1, 0);

        public Mitochondrion Head => HeadOfChain();
        public Mitochondrion Tail => TailOfChain();

        public Mitochondrion(Cell cell, BranchNode origin, BranchNode target, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;

            lastPosition = position;

            parentCell = cell;
            this.origin = origin;
            this.target = target;

            // Uses a set of mitos in the case that there are multiple at once
            activeCollisions = new HashSet<Mitochondrion>();
        }

        // Copies the control panel's mitochondria settings onto this mito
        public void ApplySettings()
        {
            fusionProbability = Math.Clamp(SimSettings.FusionPercent, 0f, 100f) / 100f;
            fissionProbability = Math.Clamp(SimSettings.FissionPercent, 0f, 100f) / 100f;
            speed = SimSettings.MitoSpeed;

            // the panel's diameter is in world units, the mesh has radius MeshRadius at scale 1
            Scale = new Vector3(SimSettings.MitoDiameter / (2f * MeshRadius));
        }

        // Chain helpers (all pure: they never modify any links)

        public Mitochondrion HeadOfChain()
        {
            Mitochondrion current = this;
            for (int i = 0; i < MaxChainLength && current.parent != null; i++)
                current = current.parent;
            return current;
        }

        public Mitochondrion TailOfChain()
        {
            Mitochondrion current = this;
            for (int i = 0; i < MaxChainLength && current.child != null; i++)
                current = current.child;
            return current;
        }

        public int ChainLength()
        {
            int count = 0;
            Mitochondrion? current = HeadOfChain();
            while (current != null && count < MaxChainLength * 2)
            {
                count++;
                current = current.child;
            }
            return count;
        }

        public bool IsInChain(Mitochondrion other)
        {
            return HeadOfChain() == other.HeadOfChain();
        }

        // Flip every parent/child link in the chain that starts at this head
        private void ReverseLinks()
        {
            Mitochondrion? current = this;
            int guard = 0;
            while (current != null && guard++ < MaxChainLength * 2)
            {
                Mitochondrion? next = current.child;
                (current.parent, current.child) = (current.child, current.parent);
                current = next;
            }
        }

        // Update / movement

        public override void Update()
        {
            base.Update();

            if (fusionTimeout > 0f)
                fusionTimeout -= Scene.simDeltaTime;

            if (bounceCooldown > 0f)
                bounceCooldown -= Scene.simDeltaTime;

            if (deadEndTimer > 0f)
                deadEndTimer -= Scene.simDeltaTime;

            if (calmTimer > 0f)
                calmTimer -= Scene.simDeltaTime;

            // Chance per second that this mito lets go of its parent
            if (parent != null && fissionProbability > 0f
                && RandomRange(0f, 1f) < fissionProbability * Scene.simDeltaTime)
            {
                DoFission();
            }

            UpdatePosition();
        }

        public void UpdatePosition()
        {
            // followers are moved by their leader
            if (parent != null) return;

            if (holdTimer > 0f)
                holdTimer -= Scene.simDeltaTime;

            // remember where we were, so a step into something we can't fuse with can be undone
            Vector3 oldPosition = Position;
            BranchNode oldOrigin = origin;
            BranchNode oldTarget = target;
            float oldProgress = progressToNextNode;
            Vector3 oldSnap = snapOffset;
            bool oldLockout = turnLockout;

            Move();

            // If this mito became a follower during Move, and it's being blocked
            if (parent == null && BlockingMito(oldPosition) != null)
            {
                Position = oldPosition;
                origin = oldOrigin;
                target = oldTarget;
                progressToNextNode = oldProgress;
                snapOffset = oldSnap;
                turnLockout = oldLockout;

                if (holdTimer <= 0f)
                {
                    TurnAround();
                    holdTimer = BlockedHold;
                }
            }

            if (parent == null && child != null)
            {
                child.FollowerMove(0);
            }
        }

        // The mito in a different chain that our last step ran into and that cannot be fused with, if any
        private Mitochondrion? BlockingMito(Vector3 oldPosition)
        {
            if (Scene.partition == null) return null;

            _blockScratch.Clear();
            Scene.partition.QueryRadius(Position, Math.Max(1, (int)MathF.Ceiling(Radius * 2f)), _blockScratch);

            foreach (Mitochondrion other in _blockScratch)
            {
                if (other == this || other.HeadOfChain() == this) continue;

                float touch = (Radius + other.Radius) * 0.999f;
                float now = Vector3.DistanceSquared(Position, other.Position);

                if (now >= touch * touch) continue;
                if (now >= Vector3.DistanceSquared(oldPosition, other.Position) - 1e-9f) continue; // moving away

                if (fusionTimeout <= 0f && other.fusionTimeout <= 0f && CanFuse(other)) continue;

                return other;
            }

            return null;
        }

        // Moves a leader along the tube network by a fixed distance, changing branches if it reaches a junction
        public void Move()
        {
            lastPosition = Position;

            float travel = speed * Scene.simDeltaTime;
            int guard = 8; // to prevent infinite looping

            // Go until it reaches the fixed distance or exceeds maximum depth
            while (travel > 0f && guard-- > 0)
            {
                float segLen = Vector3.Distance(origin.Position, target.Position);
                float distLeft = (1f - progressToNextNode) * segLen;

                if (segLen > 1e-5f && travel < distLeft)
                {
                    progressToNextNode += travel / segLen;
                    travel = 0f;
                    break;
                }

                // Upon reaching a junction, commit the movement and ready the rest after changing targets
                travel -= distLeft;
                progressToNextNode = 1f;
                Position = target.Position;

                ArriveAtTarget();

                // In case the above method turned this mito around, stop doing movement since it's now a follower
                if (parent != null) return;
            }

            // If the position needs correcting, glide towards the target instead of snapping
            if (snapOffset.LengthSquared > 1e-8f)
                snapOffset *= MathF.Exp(-SnapSmoothing * Scene.simDeltaTime);
            else
                snapOffset = Vector3.Zero;

            Position = Vector3.Lerp(origin.Position, target.Position, Math.Clamp(progressToNextNode, 0f, 1f)) + snapOffset;

            // Chance of turning around, proportional to movement distance
            float currentLen = Vector3.Distance(origin.Position, target.Position);
            if (!turnLockout && calmTimer <= 0f && currentLen > 1e-5f)
            {
                float percentMovement = speed * Scene.simDeltaTime / currentLen;
                if (RandomRange(0f, 1f) < percentMovement * turnChance)
                {
                    TurnAround();
                }
            }
        }

        // Called when a head reaches its target (progress >= 1)
        private void ArriveAtTarget()
        {
            turnLockout = false;

            _neighbourScratch.Clear();
            foreach (BranchNode n in target.Neighbours)
            {
                // Possible moves are to every neighbour node except for the one it just arrives at
                if (n != origin) _neighbourScratch.Add(n);
            }

            if (_neighbourScratch.Count == 0)
            {
                // Split the chain in half if there is a jam
                if (child != null && deadEndTimer > 0f)
                {
                    Mitochondrion mid = this;
                    for (int i = ChainLength() / 2; i > 0 && mid.child != null; i--) mid = mid.child;
                    mid.DoFission();
                }

                // Reverse at a dead end
                TurnAround();
                if (parent == null) turnLockout = true;

                // Remember the timer for the new head
                HeadOfChain().deadEndTimer = JamWindow;
                return;
            }

            origin = target;
            target = _neighbourScratch[RandomRange(0, _neighbourScratch.Count)];
            progressToNextNode = 0f;
        }

        public void FollowerMove(int depth)
        {
            if (depth > MaxChainLength)
            {
                // Unbind if it reaches an infinite loop
                UnbindParent();
                return;
            }

            if (parent == null)
            {
                UpdatePosition();
                return;
            }

            // Reset the snap
            snapOffset = Vector3.Zero;

            // Space according to both radii, spacing factor multiplies this
            float minDist = (Radius + parent.Radius) * SpacingFactor;
            Vector3 toParent = parent.Position - Position;
            float dist = toParent.Length;

            // If this mito is too far from its parent, cut off the connection
            if (dist > minDist * MaxLinkStretch)
            {
                DoFission();
                UpdatePosition(); // now a leader (and it moves its own followers)
                return;
            }

            // Move as close as minDist will allow
            if (dist > minDist)
            {
                float step = Math.Min(2f * speed * Scene.simDeltaTime, dist - minDist);
                Position += toParent / dist * step;

                // Reduce corner cutting
                ConstrainToTube(parent.Position);
            }

            if (child != null && child != parent && child != this)
            {
                child.FollowerMove(depth + 1);
            }
        }

        // Become the head of the chain. Finds the mito's location along its segment, turns away from its new follower
        // (former parent), and glides towards its location if correction is needed
        private void BecomeLeader()
        {
            RelocateOnSegment();
            TurnAwayFromFollower();

            Vector3 a = origin.Position;
            Vector3 ab = target.Position - a;
            float lenSq = ab.LengthSquared;

            progressToNextNode = lenSq > 1e-8f
                ? Math.Clamp(Vector3.Dot(Position - a, ab) / lenSq, 0f, 1f)
                : 0f;

            snapOffset = Position - (a + ab * progressToNextNode);
            turnLockout = true;
        }

        // Puts this mito back onto its own segment (origin -> target). If it ran past either end it stops at that
        // node and switches to the neighbouring segment that gets closest to its parent, so at a junction
        // the follower takes the branch the chain actually went down.
        private void ConstrainToTube(Vector3 toward)
        {
            for (int iteration = 0; iteration < 4; iteration++)
            {
                Vector3 a = origin.Position;
                Vector3 ab = target.Position - a;
                float lenSq = ab.LengthSquared;

                BranchNode passed;
                if (lenSq > 1e-10f)
                {
                    float t = Vector3.Dot(Position - a, ab) / lenSq;
                    if (t >= 0f && t <= 1f)
                    {
                        Position = a + ab * t;
                        return;
                    }

                    passed = t > 1f ? target : origin;
                }
                else
                {
                    passed = origin; // zero-length segment
                }

                Position = passed.Position;

                BranchNode? best = null;
                float bestSq = float.MaxValue;
                foreach (BranchNode n in passed.Neighbours)
                {
                    Vector3 q = NearestPointOnLineSegment(passed.Position, n.Position, toward);
                    float sq = (toward - q).LengthSquared;
                    if (sq < bestSq)
                    {
                        bestSq = sq;
                        best = n;
                    }
                }

                if (best == null) return;

                origin = passed;
                target = best;
            }
        }

        // Finds approximately where along the current segment this mito is, as a follower. If it is past
        // the segment that it thinks it's on, find the segment that matches and move towards the target
        private void RelocateOnSegment()
        {
            Vector3 a = origin.Position;
            Vector3 ab = target.Position - a;
            float lenSq = ab.LengthSquared;

            if (lenSq > 1e-10f)
            {
                float t = Vector3.Dot(Position - a, ab) / lenSq;

                if (t < 0f || t > 1f)
                {
                    BranchNode passed = t > 1f ? target : origin; // the node we ran past

                    BranchNode? best = null;
                    float bestSq = float.MaxValue;
                    foreach (BranchNode n in passed.Neighbours)
                    {
                        Vector3 p = NearestPointOnLineSegment(passed.Position, n.Position, Position);
                        float sq = (Position - p).LengthSquared;
                        if (sq < bestSq)
                        {
                            bestSq = sq;
                            best = n;
                        }
                    }

                    if (best != null)
                    {
                        origin = passed;
                        target = best;
                    }
                }
            }

            // safety net: if we're nowhere near our segment, search the whole network
            Vector3 q = NearestPointOnLineSegment(origin.Position, target.Position, Position);
            if ((Position - q).LengthSquared > RelocateFallbackDistance * RelocateFallbackDistance)
            {
                var (_, o, t2) = NearestPointOnStructure(Position);
                if (o != null && t2 != null)
                {
                    origin = o;
                    target = t2;
                }
            }
        }

        // Unbinds this mito from its parent and becomes a head
        public void UnbindParent()
        {
            if (parent == null) return;

            if (parent.child == this)
            {
                parent.child = null;
            }

            parent = null;

            BecomeLeader();
        }

        // Turn away from its child. Finds the direction away from the child, then projects that direction onto the segment it's on
        public void TurnAwayFromFollower()
        {
            if (child == null)
                return;

            Vector3 toChild = child.Position - Position;
            Vector3 dirCurrent = target.Position - origin.Position;

            if (toChild.LengthSquared < 1e-10f || dirCurrent.LengthSquared < 1e-10f)
                return;

            // if our current heading points toward the follower, flip it
            if (Vector3.Dot(dirCurrent.Normalized(), toChild.Normalized()) > 0f)
            {
                (origin, target) = (target, origin);
            }
        }

        // Reverses the direction of travel of the whole chain this mito leads.
        public void TurnAround()
        {
            // only a head can decide to turn, route anything else to its head
            if (parent != null)
            {
                HeadOfChain().TurnAround();
                return;
            }

            // If this is a lone mito, just turn around on its segment
            if (child == null)
            {
                (origin, target) = (target, origin);
                progressToNextNode = 1f - progressToNextNode;
                calmTimer = CalmAfterTurn;
                return;
            }

            // Reverse all links down the chain
            Mitochondrion tail = TailOfChain();
            ReverseLinks();
            tail.BecomeLeader();
            tail.calmTimer = CalmAfterTurn;
        }

        // Nearest point on the whole cell's branch network
        public (Vector3?, BranchNode?, BranchNode?) NearestPointOnStructure(Vector3 point)
        {
            Vector3 nearestPoint = Vector3.Zero;
            BranchNode? start = null;
            BranchNode? end = null;
            float minSqrDist = float.MaxValue;

            foreach (Branch branch in parentCell.branches)
            {
                foreach (BranchNode A in branch.GetNodes())
                {
                    foreach (BranchNode B in A.Neighbours)
                    {
                        Vector3 candidate = NearestPointOnLineSegment(A.Position, B.Position, point);
                        float sqrDist = (point - candidate).LengthSquared;

                        if (sqrDist < minSqrDist)
                        {
                            minSqrDist = sqrDist;
                            start = A;
                            end = B;

                            nearestPoint = candidate;
                        }
                    }
                }
            }

            return (nearestPoint, start, end);
        }

        private static Vector3 NearestPointOnLineSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float lenSq = ab.LengthSquared;
            if (lenSq < 1e-10f) return a; // zero-length segment

            float t = Vector3.Dot(point - a, ab) / lenSq;
            t = Math.Clamp(t, 0, 1);
            return a + t * ab;
        }

        // Splits this mito and all of its children off from its parent, and sends the two chains apart
        public void DoFission()
        {
            if (parent == null) return; // a head has nothing to split from

            Mitochondrion oldParent = parent;

            // break both sides
            oldParent.child = null;
            parent = null;

            // The mito who does the fission is the new head
            BecomeLeader();

            // Make sure that the chains both go in opposite directions
            Vector3 heading = target.Position - origin.Position;
            if (Vector3.Dot(heading, oldParent.Position - Position) > 0f)
            {
                TurnAround();
            }

            // Neither chain may fuse (or pass through the other) until the cooldown runs out
            oldParent.StartChainCooldown();
            StartChainCooldown();
        }

        // Starts the fusion cooldown on every mito in this mito's chain, so it still applies after the chain
        // reverses and a different mito becomes the head, or in the case of fission
        private void StartChainCooldown()
        {
            Mitochondrion? current = HeadOfChain();
            int guard = 0;
            while (current != null && guard++ < MaxChainLength * 2)
            {
                current.fusionTimeout = FusionCooldown;
                current = current.child;
            }
        }

        // Fuses 2 chains/mitos together. Only fuses chains by their edges currently
        public void TryFusion(Mitochondrion other)
        {
            if (!CanFuse(other)) return;

            if (RandomRange(0f, 1f) >= fusionProbability)
            {
                // Cooldown to avoid immediate retries. OnCollision these will bounce off instead
                StartChainCooldown();
                other.StartChainCooldown();
                return;
            }

            // Cases for how the two chains collided, whether it be head to head, head to tail, or tail to tail
            bool thisIsHead = parent == null;
            bool otherIsHead = other.parent == null;

            if (thisIsHead && otherIsHead)
            {
                // Head to head
                // The shorter chain reverses, so the longer chain keeps its direction
                Mitochondrion keep = ChainLength() >= other.ChainLength() ? this : other;
                Mitochondrion rev = keep == this ? other : this;

                Mitochondrion revTail = rev.TailOfChain();
                rev.ReverseLinks();          // rev (old head) is now that chain's tail, revTail its head
                rev.child = keep;            // old head of rev sits directly in front of keep's head
                keep.parent = rev;

                // The old tail of rev leads the combined chain, facing away from the body
                revTail.BecomeLeader();
                revTail.calmTimer = CalmAfterTurn;
            }
            else if (thisIsHead || otherIsHead)
            {
                // Head to tail
                // The chain that touches the tail of the other is joined onto its end, no reversal needed
                Mitochondrion head = thisIsHead ? this : other;
                Mitochondrion tail = thisIsHead ? other : this;

                tail.child = head;
                head.parent = tail;
            }
            else
            {
                // Tail to tail
                // Reverse the second chain and ad the new head after reversal onto the first chain
                Mitochondrion keep = ChainLength() >= other.ChainLength() ? this : other;
                Mitochondrion rev = keep == this ? other : this;

                rev.HeadOfChain().ReverseLinks();
                keep.child = rev;
                rev.parent = keep;
            }
        }

        // Prevent mitos from fusing if they're close enough, but don't share a branch
        private bool IsTubeConnected(Mitochondrion other)
        {
            float touch = Radius + other.Radius;
            float limit = 2f * touch + 0.05f;
            return TubeGap(other, limit) <= limit;
        }

        private float TubeGap(Mitochondrion other, float limit)
        {
            // same segment (in either direction): plain distance
            if ((origin == other.origin && target == other.target) || (origin == other.target && target == other.origin))
                return Vector3.Distance(Position, other.Position);

            float best = float.MaxValue;
            TubeSearch(origin, null, Vector3.Distance(Position, origin.Position), limit, other, ref best);
            TubeSearch(target, null, Vector3.Distance(Position, target.Position), limit, other, ref best);
            return best;
        }

        // Depth-first walk along the tubes until limit has been used up
        private static void TubeSearch(BranchNode node, BranchNode? previous, float travelled, float limit, Mitochondrion other, ref float best)
        {
            if (travelled > limit) return;

            if (node == other.origin || node == other.target)
                best = Math.Min(best, travelled + Vector3.Distance(node.Position, other.Position));

            foreach (BranchNode n in node.Neighbours)
            {
                if (n != previous)
                    TubeSearch(n, node, travelled + Vector3.Distance(node.Position, n.Position), limit, other, ref best);
            }
        }

        public bool CanFuse(Mitochondrion other)
        {
            bool thisIsEnd = parent == null || child == null;
            bool otherIsEnd = other.parent == null || other.child == null;

            return other != this
                && thisIsEnd && otherIsEnd
                && !IsInChain(other)
                && fusionTimeout <= 0 && other.fusionTimeout <= 0
                && ChainLength() + other.ChainLength() <= MaxChainLength
                && IsTubeConnected(other);
        }




        // Collision events

        public void OnCollisionEnter(Mitochondrion other)
        {
            // Called once when collision starts
            OnCollision(other);
            //Colour = new Vector3(1, 0, 0); // Red
        }

        public void OnCollisionStay(Mitochondrion other)
        {
            // Called every frame while colliding. Retry fusion in case it wasn't possible on Enter.
            OnCollision(other);
            //Colour = new Vector3(0.2f, 0.2f, 1f); // Blue
        }

        public void OnCollisionExit(Mitochondrion other)
        {
            // Called once when collision ends
            //if (activeCollisions.Count == 0) Colour = new Vector3(0, 1, 0); // Green
        }

        // Update and trigger collision events. Events fire on both mitos of a pair.
        // Fusion is only attempted from the lower ID side (see OnCollision).
        public void CheckCollision(Mitochondrion other)
        {
            bool wasColliding = activeCollisions.Contains(other);
            bool isColliding = IsCollidingWith(other) && !IsInChain(other);

            if (isColliding && !wasColliding)
            {
                activeCollisions.Add(other);
                OnCollisionEnter(other);
            }
            else if (isColliding && wasColliding)
            {
                OnCollisionStay(other);
            }
            else if (!isColliding && wasColliding)
            {
                activeCollisions.Remove(other);
                OnCollisionExit(other);
            }
        }

        public bool IsCollidingWith(Mitochondrion other)
        {
            float radii = Radius + other.Radius;
            return (Position - other.Position).LengthSquared < radii * radii;
        }

        private void OnCollision(Mitochondrion other)
        {
            // Bounce off of anything that can't be fused with
            if (fusionTimeout > 0f || other.fusionTimeout > 0f || !CanFuse(other))
            {
                BounceFrom(other);
                return;
            }

            // Both sides get this event; only the lower ID rolls so a pair is only attempted once.
            if (mitoID > other.mitoID) return;

            TryFusion(other);
        }

        private void BounceFrom(Mitochondrion other)
        {
            if (parent != null || bounceCooldown > 0f) return;

            Vector3 heading = target.Position - origin.Position;
            if (Vector3.Dot(heading, other.Position - Position) <= 0f) return;

            TurnAround();

            // lockout on whoever leads now as well (TurnAround may have made a different mito the head)
            bounceCooldown = BounceCooldownTime;
            HeadOfChain().bounceCooldown = BounceCooldownTime;
        }
    }
}