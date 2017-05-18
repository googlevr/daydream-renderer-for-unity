///////////////////////////////////////////////////////////////////////////////
//Copyright 2017 Google Inc.
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.
///////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Collections.Generic;
using System;
using UnityEditor.SceneManagement;

namespace daydreamrenderer
{
    [CustomEditor(typeof(TessellateTest), true)]
    public class TessellateTestInspector : DaydreamInspector
    {
        static class Styles
        {
            public static readonly GUIContent m_tesselateButton = new GUIContent("Tessellate");
        }


        public void OnEnable()
        {
            TessellateTest source = target as TessellateTest;
            MeshFilter meshFilter = source.GetComponent<MeshFilter>();

            Init(meshFilter);
        }

        public override void OnInspectorGUI()
        {
            TessellateTest source = target as TessellateTest;

            base.OnInspectorGUI();

            if (GUILayout.Button(Styles.m_tesselateButton))
            {
                Tessellate(source.m_sourceMesh);
            }

        }

        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        static void DrawGizmos(BVHTest source, GizmoType gizmoType)
        {

        }

        class Edge {
            public int m_index;
            public Vector3 m_point = Vector3.zero;
        }

        class MeshData {
            public List<Vector3> m_position = new List<Vector3>();
            public List<Vector2> m_uv = new List<Vector2>();
            public List<Vector3> m_normal = new List<Vector3>();
            public List<Vector4> m_tangent = new List<Vector4>();

            public void Resize(int size){
                m_position.Resize(size);
                m_uv.Resize(size);
                m_normal.Resize(size);
                m_tangent.Resize(size);
            }

            public Mesh GenerateMesh(){
                Mesh m = new Mesh();
                m.vertices = new Vector3[m_position.Count];
                m.SetVertices(m_position);
                m.SetUVs(0, m_uv);
                m.SetNormals(m_normal);
                m.SetTangents(m_tangent);
                m.UploadMeshData(true);
                return m;
            }
        }

        class Vertex
        {
            public Vector3 m_position = Vector3.zero;
            public Vector3 m_normal = Vector3.zero;
            public Vector2 m_uv = Vector2.zero;
            public Vector4 m_tangent = Vector4.zero;

            public Vertex(){}

            public void Set(MeshData mesh, int index)
            {
                m_position = mesh.m_position[index];
                m_normal = mesh.m_normal[index];
                m_uv = mesh.m_uv[index];
                m_tangent = mesh.m_tangent[index];
            }

            public void Add(MeshData mesh, int index)
            {
                m_position += mesh.m_position[index];
                m_normal += mesh.m_normal[index];
                m_uv += mesh.m_uv[index];
                m_tangent += mesh.m_tangent[index];
                m_tangent.w = mesh.m_tangent[index].w;
            }

            public void ScalarMult(float v)
            {
                m_position *= v;
                m_normal *= v;
                m_uv *= v;
                float w = m_tangent.w;
                m_tangent *= v;
                m_tangent.w = w;
            }
        }

        // Tessallate using loops subdivision
        public void Tessellate(MeshFilter meshFilter)
        {
            Mesh mesh = meshFilter.sharedMesh;

            MeshData tessMeshData = new MeshData();

            int triangleCount = mesh.triangles.Length / 3;
            int newTriCount = triangleCount*4;

            Dictionary<long, Edge> edgeMap = new Dictionary<long, Edge>();

            // track the which vertices are adjacent to the original vertex
            // the original vertex position will be recomputed with weights to
            // control the shape of the tessellated object a weight of 1 is a hard shell
            // min wieght of 3/8 is a flexible shell
            Dictionary<int, HashSet<int>> controlPointAdj = new Dictionary<int, HashSet<int>>();

            // new triangle list
            int[] triangleList = new int[newTriCount*3];

            // new point list add 3 new points per original triangle
            tessMeshData.Resize(mesh.vertices.Length);

            // for each face
            for(int i = 0; i < mesh.triangles.Length; i+=3)
            {
                // for each original point in the triangle
                for(int j = 0; j < 3; ++j)
                {
                    int triIdx0 = i + j;
                    int triIdx1 = i + ((j+1)%3);
                    int triIdx2 = i + ((j+2)%3);

                    int index0 = mesh.triangles[triIdx0];
                    int index1 = mesh.triangles[triIdx1];
                    int index2 = mesh.triangles[triIdx2];

                    // copy original point
                    CopyVertex(tessMeshData, mesh, index0);
                    CopyVertex(tessMeshData, mesh, index1);
                    CopyVertex(tessMeshData, mesh, index2);

                    // create or get edges always min/max the indices so we refer to the edges
                    // in a consistent manner
                    // edge 0 to 1
                    long edgeId0 = (Math.Min(index0, index1) << 16) + Math.Max(index0, index1);
                    // edge 0 to 2
                    long edgeId1 = (Math.Min(index0, index2) << 16) + Math.Max(index0, index2);

                    Edge edge0 = null;
                    if(!edgeMap.TryGetValue(edgeId0, out edge0))
                    {
                        edge0 = new Edge();
                        edge0.m_index = CreateVertex(tessMeshData, edge0.m_index, mesh, index0, index1, 0.5f);
                        edgeMap.Add(edgeId0, edge0);
                    }

                    Edge edge1 = null;
                    if(!edgeMap.TryGetValue(edgeId1, out edge1))
                    {
                        edge1 = new Edge();
                        edge1.m_index = CreateVertex(tessMeshData, edge1.m_index, mesh, index0, index2, 0.5f);
                        edgeMap.Add(edgeId1, edge1);
                    }

                    // create triangle using the same winding order as original triangle
                    triangleList[i*4 + j*3] = index0;
                    triangleList[i*4 + j*3+1] = edge0.m_index;
                    triangleList[i*4 + j*3+2] = edge1.m_index;

                    if(!controlPointAdj.ContainsKey(index0))
                    {
                        controlPointAdj.Add(index0, new HashSet<int>());
                    }

                    var hashSet = controlPointAdj[index0];
                    hashSet.Add(edge0.m_index);
                    hashSet.Add(edge1.m_index);

                }

//                Debug.Log("index " + i/3 + " of " + mesh.triangles.Length/3 +  ", added index"
//                    + (newIndexOffset) + " verts total now " + (tessMeshData.m_position.Count));

                // Loops subdivision is 1:4 tessellation, the previous loop created 3 new triangles from 1, now add the fourth
                {
                    long index0 = mesh.triangles[i];
                    long index1 = mesh.triangles[i+1];
                    long index2 = mesh.triangles[i+2];
                    // edge 0 to 1
                    long edgeId0 = (Math.Min(index0, index1) << 16) + Math.Max(index0, index1);
                    // edge 0 to 2
                    long edgeId1 = (Math.Min(index0, index2) << 16) + Math.Max(index0, index2);
                    // edge 1 to 2
                    long edgeId2 = (Math.Min(index1, index2) << 16) + Math.Max(index1, index2);

                    triangleList[i*4 + 9] = edgeMap[edgeId0].m_index;
                    triangleList[i*4 + 10] = edgeMap[edgeId2].m_index;
                    triangleList[i*4 + 11] = edgeMap[edgeId1].m_index;
                }
                
            }

            // using the new points adjacent to the control point (the original vertex) compute the control points new position
            Vertex avgVert = new Vertex();
            var controlIter = controlPointAdj.GetEnumerator();
            while(controlIter.MoveNext())
            {
                // if adjacencies are less than 6 we assume this control is  on an edge
                // to preserve the shape of the object we do not reposition this point
                if(controlIter.Current.Value.Count < 6)
                    continue;
                
                int ctlIdx = controlIter.Current.Key;
                int[] adj = new int[controlIter.Current.Value.Count];

                controlIter.Current.Value.CopyTo(adj);

                float controlWieght = (3f/8f) + Mathf.Pow((3f/8f) + 0.25f*Mathf.Cos(2f*Mathf.PI/(float)adj.Length), 2f);
//                float controlWieght = (5f/8f);
                float influenceWeight = (1f - controlWieght);

                avgVert.Set(tessMeshData, adj[0]);
                for(int i = 1, k = adj.Length; i < k; ++i)
                {
                    avgVert.Add(tessMeshData, adj[i]);
                }

                avgVert.ScalarMult(1f/adj.Length);

                InterpolatedVertex(tessMeshData, ctlIdx, avgVert, influenceWeight);
            }

            // output temporary mesh
            Mesh tessMesh = tessMeshData.GenerateMesh();
            tessMesh.name = "test";
            tessMesh.SetTriangles(triangleList, 0);

            AssetDatabase.CreateAsset(tessMesh, BakeData.kDaydreamPath + "/tessTestMesh.asset");
            AssetDatabase.SaveAssets();
            tessMesh = AssetDatabase.LoadAssetAtPath<Mesh>(BakeData.kDaydreamPath + "/tessTestMesh.asset");

            GameObject go = new GameObject("TessMesh");

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.mesh = tessMesh;

            go.AddComponent<MeshRenderer>();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }


        static void CopyVertex(MeshData dest, Mesh source, int index)
        {
            dest.m_position[index] = source.vertices[index];
            dest.m_uv[index] = source.uv[index];
            dest.m_normal[index] = (source.normals[index]);
            dest.m_tangent[index] =(source.tangents[index]);
        }

        // creates a new vertex at destIndex that is interpolated between A and B in the source
        static int CreateVertex(MeshData dest, int destIndex, Mesh source, int indexA, int indexB, float percentOfB)
        {
            dest.m_position.Add(Vector3.Lerp(source.vertices[indexA], source.vertices[indexB], percentOfB));
            dest.m_uv.Add(Vector2.Lerp(source.uv[indexA], source.uv[indexB], percentOfB));
            dest.m_normal.Add(Vector3.Lerp(source.normals[indexA], source.normals[indexB], percentOfB).normalized);

            Vector3 tan = Vector3.Lerp(source.tangents[indexA], source.tangents[indexB], percentOfB).normalized;
            dest.m_tangent.Add(new Vector4(tan.x, tan.y, tan.z, source.tangents[indexA].w));

            return dest.m_position.Count - 1;
        }

        // creates a new vertex at destIndex that is interpolated between A and B in the source
        static int InterpolatedVertex(MeshData dest, int index, Vertex sourceB, float alpha)
        {
            dest.m_position[index] = (Vector3.Lerp(dest.m_position[index], sourceB.m_position, alpha));
            dest.m_uv[index] = (Vector2.Lerp(dest.m_uv[index], sourceB.m_uv, alpha));
            dest.m_normal[index] = (Vector3.Lerp(dest.m_normal[index], sourceB.m_normal, alpha).normalized);

            Vector3 tan = Vector3.Lerp(dest.m_tangent[index], sourceB.m_tangent, alpha).normalized;
            dest.m_tangent[index] = (new Vector4(tan.x, tan.y, tan.z, dest.m_tangent[index].w));

            return dest.m_position.Count - 1;
        }

    }
}