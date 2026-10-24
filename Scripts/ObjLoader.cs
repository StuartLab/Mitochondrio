using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Mitochondrio_OpenGL.Scripts
{
    // Loads vertex data from a .obj file, and returns the vertex data as an interleaved buffer, and the indices
    public static class ObjLoader
    {
        struct Triplet : IEquatable<Triplet>
        {
            public int v, t, n;

            public Triplet(int v, int t, int n)
            {
                this.v = v; this.t = t; this.n = n;
            }

            public bool Equals(Triplet other)
                => v == other.v && t == other.t && n == other.n;

            public override int GetHashCode()
                => HashCode.Combine(v, t, n);
        }

        public static (float[], int[]) GetVertexData(string path)
        {
            float[] VertexData; // pos(3), uv(2), normal(3)
            int[] Indices;

            List<float> tempVerts = new();
            List<float> tempUvs = new();
            List<float> tempNorms = new();

            List<float> outInterleaved = new();
            List<int> outIndices = new();

            Dictionary<Triplet, int> lookup = new();

            foreach (var raw in File.ReadAllLines(path))
            {
                
                if (raw.StartsWith("v ")) // Vertex position
                {
                    var p = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    tempVerts.Add(float.Parse(p[1], CultureInfo.InvariantCulture));
                    tempVerts.Add(float.Parse(p[2], CultureInfo.InvariantCulture));
                    tempVerts.Add(float.Parse(p[3], CultureInfo.InvariantCulture));
                }
                else if (raw.StartsWith("vt ")) // Vertex texture coordinates
                {
                    var p = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    tempUvs.Add(float.Parse(p[1], CultureInfo.InvariantCulture));
                    tempUvs.Add(1 - float.Parse(p[2], CultureInfo.InvariantCulture));
                }
                else if (raw.StartsWith("vn ")) // Vertex normals
                {
                    var p = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    tempNorms.Add(float.Parse(p[1], CultureInfo.InvariantCulture));
                    tempNorms.Add(float.Parse(p[2], CultureInfo.InvariantCulture));
                    tempNorms.Add(float.Parse(p[3], CultureInfo.InvariantCulture));
                }
                else if (raw.StartsWith("f ")) // Indices for the face
                {
                    var p = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    // OBJ faces are triangles here
                    for (int i = 1; i <= 3; i++)
                    {
                        var parts = p[i].Split('/');
                        int v = int.Parse(parts[0]) - 1;
                        int t = parts.Length > 1 && parts[1] != "" ? int.Parse(parts[1]) - 1 : -1;
                        int n = parts.Length > 2 ? int.Parse(parts[2]) - 1 : -1;

                        Triplet key = new(v, t, n);

                        if (!lookup.TryGetValue(key, out int finalIndex))
                        {
                            // pack new vertex
                            lookup[key] = finalIndex = outInterleaved.Count / 8;

                            // pos
                            outInterleaved.Add(tempVerts[v * 3 + 0]);
                            outInterleaved.Add(tempVerts[v * 3 + 1]);
                            outInterleaved.Add(tempVerts[v * 3 + 2]);

                            // uv
                            if (t >= 0)
                            {
                                outInterleaved.Add(tempUvs[t * 2 + 0]);
                                outInterleaved.Add(tempUvs[t * 2 + 1]);
                            }
                            else { outInterleaved.Add(0); outInterleaved.Add(0); }

                            // normal
                            if (n >= 0)
                            {
                                outInterleaved.Add(tempNorms[n * 3 + 0]);
                                outInterleaved.Add(tempNorms[n * 3 + 1]);
                                outInterleaved.Add(tempNorms[n * 3 + 2]);
                            }
                            else { outInterleaved.Add(0); outInterleaved.Add(0); outInterleaved.Add(1); }
                        }

                        outIndices.Add(finalIndex);
                    }
                }
            }

            VertexData = outInterleaved.ToArray();
            Indices = outIndices.ToArray();

            return (VertexData, Indices);
        }
    }
}