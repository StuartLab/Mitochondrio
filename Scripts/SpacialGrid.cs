using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mitochondrio_OpenGL.Scripts
{
    // A grid along 3D space that stores buckets of data (mitos and branch nodes that lie within its space).
    // Used for fast proximity checks for collision, ie. not having to check every mito against every other mito
    // each frame
    internal class SpatialGrid
    {
        private Dictionary<Vector3i, GridBucket> grid;
        public HashSet<Vector3i> dirtyBuckets; // Track buckets where data changed
        private float bucketSize;

        public SpatialGrid(float bucketSize = 1.0f)
        {
            this.bucketSize = bucketSize;
            grid = new Dictionary<Vector3i, GridBucket>();
            dirtyBuckets = new HashSet<Vector3i>();
        }

        // Get the coordinates (grid key) for any point in space
        private Vector3i GetBucketCoords(Vector3 position)
        {
            return new Vector3i(
                (int)MathF.Floor(position.X / bucketSize),
                (int)MathF.Floor(position.Y / bucketSize),
                (int)MathF.Floor(position.Z / bucketSize)
            );
        }

        // Returns the bucket from a key, instantiates a new one if it doesn't already exist
        public GridBucket GetOrCreateBucket(Vector3i coords)
        {
            if (!grid.TryGetValue(coords, out GridBucket? bucket))
            {
                bucket = new GridBucket(coords);
                grid[coords] = bucket;
                // new bucket receives an update to initialize
                dirtyBuckets.Add(coords);
            }
            return bucket;
        }

        // Every mito remembers which bucket it is in (mito.gridCell), so removal never has to guess
        // from a position that may be stale.
        public void AddMito(Mitochondrion mito)
        {
            AddMito(mito, GetBucketCoords(mito.Position));
        }

        public void AddMito(Mitochondrion mito, Vector3i bucketPosition)
        {
            if (mito.gridCell != null) RemoveMito(mito);

            GetOrCreateBucket(bucketPosition).AddMito(mito);
            mito.gridCell = bucketPosition;
            dirtyBuckets.Add(bucketPosition);
        }

        public void RemoveMito(Mitochondrion mito)
        {
            if (mito.gridCell is Vector3i cell)
            {
                if (grid.TryGetValue(cell, out GridBucket? bucket))
                {
                    bucket.RemoveMito(mito);
                }
                dirtyBuckets.Add(cell);
                mito.gridCell = null;
            }
        }

        // Move a mito to the bucket matching its current position
        public void UpdateObject(Mitochondrion mito)
        {
            Vector3i cell = GetBucketCoords(mito.Position);

            if (mito.gridCell == null)
            {
                AddMito(mito, cell);
                return;
            }

            if (mito.gridCell == cell) return;

            Vector3i oldCell = mito.gridCell.Value;
            if (grid.TryGetValue(oldCell, out GridBucket? oldBucket))
            {
                oldBucket.RemoveMito(mito);
            }

            GetOrCreateBucket(cell).AddMito(mito);
            mito.gridCell = cell;

            dirtyBuckets.Add(cell);
            dirtyBuckets.Add(oldCell);
        }


        // Allocation-free query: appends every mito in the (2r+1)^3 block of buckets around position.
        // Each mito lives in exactly one bucket, so there are no duplicates and no HashSet is needed.
        // Adds into the results buffer
        public void QueryRadius(Vector3 position, int cellRadius, List<Mitochondrion> results)
        {
            Vector3i centerCell = GetBucketCoords(position);

            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                for (int y = -cellRadius; y <= cellRadius; y++)
                {
                    for (int z = -cellRadius; z <= cellRadius; z++)
                    {
                        Vector3i cellCoords = centerCell + new Vector3i(x, y, z);

                        if (grid.TryGetValue(cellCoords, out GridBucket? cell))
                        {
                            results.AddRange(cell.Mitos);
                        }
                    }
                }
            }
        }

        // Get nearby objects from world position point
        public IEnumerable<Mitochondrion> QueryRadius(Vector3 position, int cellRadius)
        {
            List<Mitochondrion> results = new List<Mitochondrion>();
            QueryRadius(position, cellRadius, results);
            return results;
        }

        // Find the nearest mito that a given mito is colliding with
        public Mitochondrion? NearestCollision(Mitochondrion collider)
        {
            Mitochondrion? best = null;
            float bestSq = float.MaxValue;

            foreach (Mitochondrion m in QueryRadius(collider.Position, 1))
            {
                if (m == collider) continue;

                float d = Vector3.DistanceSquared(collider.Position, m.Position);
                if (d < bestSq)
                {
                    bestSq = d;
                    best = m;
                }
            }

            if (best != null)
            {
                float radii = collider.Scale.X + best.Scale.X;
                if (bestSq < radii * radii) return best;
            }

            return null;
        }

        // Query outwards until a mito is found, returning all in the ring where any are found
        public IEnumerable<Mitochondrion> FindNearestShell(Vector3 position, float maxRadius)
        {
            int maxCellRadius = (int)MathF.Ceiling(maxRadius / bucketSize);
            Vector3i centerBucket = GetBucketCoords(position);

            // Spiral outward from center
            for (int radius = 0; radius <= maxCellRadius; radius++)
            {
                HashSet<Mitochondrion> results = new HashSet<Mitochondrion>();

                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        for (int z = -radius; z <= radius; z++)
                        {
                            // Only check the shell
                            if (Math.Abs(x) == radius || Math.Abs(y) == radius || Math.Abs(z) == radius)
                            {
                                Vector3i bucketCoords = centerBucket + new Vector3i(x, y, z);

                                if (grid.TryGetValue(bucketCoords, out GridBucket? bucket))
                                {
                                    foreach (var obj in bucket.Mitos)
                                    {
                                        results.Add(obj);
                                    }
                                }
                            }
                        }
                    }
                }

                // Return as soon as we find any objects
                if (results.Count > 0)
                {
                    return results;
                }
            }

            return Enumerable.Empty<Mitochondrion>();
        }

        // Query specific cell by world position
        public GridBucket? GetCell(Vector3 position)
        {
            Vector3i coords = GetBucketCoords(position);
            grid.TryGetValue(coords, out GridBucket? cell);
            return cell;
        }

        // Get all dirty cells
        public HashSet<Vector3i> GetDirtyBuckets()
        {
            HashSet<Vector3i> results = new HashSet<Vector3i>(dirtyBuckets);
            return results;
        }

        public void Clear()
        {
            foreach (GridBucket bucket in grid.Values)
            {
                foreach (Mitochondrion mito in bucket.Mitos)
                {
                    mito.gridCell = null;
                }
            }

            grid.Clear();
            dirtyBuckets.Clear();
        }

        public void ClearDirtyBuckets()
        {
            dirtyBuckets.Clear();
        }

        // Debug info
        public int BucketCount => grid.Count;
        public int DirtyBucketCount => dirtyBuckets.Count;
    }






    // Spatial bucket that stores all of the mitos and branch nodes that are in its area
    internal class GridBucket
    {
        public Vector3i Position { get; }
        public List<Mitochondrion> Mitos { get; private set; }
        public List<BranchNode> BranchNodes { get; private set; }

        public GridBucket(Vector3i coords)
        {
            Position = coords;
            Mitos = new List<Mitochondrion>();
            BranchNodes = new List<BranchNode>();
        }



        public void AddMito(Mitochondrion mito)
        {
            Mitos.Add(mito);
        }

        public void RemoveMito(Mitochondrion mito)
        {
            Mitos.Remove(mito);
        }

        public void AddBranchNode(BranchNode node)
        {
            BranchNodes.Add(node);
        }

        public void RemoveBranchNode(BranchNode node)
        {
            BranchNodes.Remove(node);
        }



        public List<Mitochondrion> GetMitos()
        {
            return new List<Mitochondrion>(Mitos);
        }


        public bool IsEmpty => Mitos.Count == 0;
    }
}