using System;
using UnityEngine;

namespace TheLastWatch.Environment
{
    public sealed class KoiPondLibrary : ScriptableObject
    {
        [Serializable] public sealed class Part
        {
            public string name;
            public Mesh mesh;
            // Null on original pond parts; new catch models may use their own material.
            public Material material;
            public Vector3 pivot;
        }
        [Serializable] public sealed class Variety
        {
            public string name;
            public Part[] parts;
        }
        public Material material;
        public Variety[] varieties;
        public string sourceSha256;
    }
}
