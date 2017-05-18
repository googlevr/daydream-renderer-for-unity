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


#if DDR_RUNTIME_DLL_LINKING && !UNITY_EDITOR_OSX
#define DDR_RUNTIME_DLL_LINKING_
#endif
using UnityEngine;
using System.Collections;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using System.IO;
using UnityEditor.SceneManagement;
using System.Linq;

namespace daydreamrenderer
{
    public class BakeContext  {

        public enum VertexElementType
        {
            kPosition,
            kNormal,
            kTangent,
            kColor,
            kUV0,
            kUV1,
            kUV2,
            kUV3,
        }

        public interface IVertex
        {
            List<VertexElement> Definition { get; }
            int ElementCount { get; }
            int VertexSize { get; }
            int TotalComponentCount { get; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VertexElement
        {
            public int m_elType;
            public int m_count;
            public int m_bytes;

            public VertexElement(VertexElementType elType, int count, int bytes)
            {
                m_elType = (int)elType;
                m_count = count;
                m_bytes = bytes;
            }

            public int TotalByteSize
            {
                get { return m_count * m_bytes; }
            }

            public int ComponentCount
            {
                get { return m_count; }
            }

            public int ComponentSizeBytes 
            {
                get { return m_bytes; }
            }

            public VertexElementType GetElType 
            {
                get { return (VertexElementType)m_elType; }
            }

            public void GetAsVec2(Mesh mesh, ref Vector2[] outData)
            {
                GetAsVec2(mesh, null, ref outData);
            }
            public void GetAsVec2(Mesh mesh, List<int> subMeshIndices, ref Vector2[] outData)
            {
                VertexElementType elType = (VertexElementType)m_elType;
                Vector2[] data = null;
                switch (elType)
                {
                    case VertexElementType.kPosition:
                        VertexBakerLib.LogError("Type Not Supported: " + elType);
                        break;
                    case VertexElementType.kNormal:
                        VertexBakerLib.LogError("Type Not Supported: " + elType);
                        break;
                    case VertexElementType.kTangent:
                        VertexBakerLib.LogError("Type Not Supported: " + elType);
                        break;
                    case VertexElementType.kColor:
                        VertexBakerLib.LogError("Type Not Supported: " + elType);
                        break;
                    case VertexElementType.kUV0:
                        data = mesh.uv;
                        break;
                    case VertexElementType.kUV1:
                        data = mesh.uv2;
                        break;
                    case VertexElementType.kUV2:
                        data = mesh.uv3;
                        break;
                    case VertexElementType.kUV3:
                        data = mesh.uv4;
                        break;

                    default:
                        VertexBakerLib.LogError("Type Not Supported: " + elType);
                        outData = null;
                        break;

                }

                if (subMeshIndices != null)
                {
                    outData = new Vector2[subMeshIndices.Count];
                    for (int i = 0, k = subMeshIndices.Count; i < k; ++i)
                    {
                        outData[i] = data[subMeshIndices[i]];
                    }
                }
                else
                {
                    outData = data;
                }
            }

            public void GetAsVec3(Mesh mesh,  ref Vector3[] outData)
            {
                GetAsVec3(mesh, null, ref outData);
            }

            public void GetAsVec3(Mesh mesh, List<int> subMeshIndices, ref Vector3[] outData)
            {
                VertexElementType elType = (VertexElementType)m_elType;
                Vector3[] data = null;
                switch (elType)
                {
                    case VertexElementType.kPosition:
                        data = mesh.vertices;
                        break;
                    case VertexElementType.kNormal:
                        data = mesh.normals;
                        break;
                    case VertexElementType.kTangent:
                    case VertexElementType.kColor:
                        VertexBakerLib.LogError("Type Not Supported: " + elType);
                        break;
                    case VertexElementType.kUV0:
                        {
                            List<Vector3> v = new List<Vector3>();
                            mesh.GetUVs(0, v);
                            data = v.ToArray();
                            break;
                        }
                    case VertexElementType.kUV1:
                        {
                            List<Vector3> v = new List<Vector3>();
                            mesh.GetUVs(1, v);
                            data = v.ToArray();
                            break;
                        }
                    case VertexElementType.kUV2:
                        {
                            List<Vector3> v = new List<Vector3>();
                            mesh.GetUVs(2, v);
                            data = v.ToArray();
                            break;
                        }
                    case VertexElementType.kUV3:
                        {
                            List<Vector3> v = new List<Vector3>();
                            mesh.GetUVs(3, v);
                            data = v.ToArray();
                            break;
                        }
                    default:
                        VertexBakerLib.LogError("Type Not Supported: " + elType);
                        data = null;
                        break;

                }

                if (subMeshIndices != null)
                {
                    outData = new Vector3[subMeshIndices.Count];
                    for (int i = 0, k = subMeshIndices.Count; i < k; ++i)
                    {
                        outData[i] = data[subMeshIndices[i]];
                    }
                }
                else
                {
                    outData = data;
                }
            }

            public void GetAsVec4(Mesh mesh, ref Vector4[] outData)
            {
                GetAsVec4(mesh, null, ref outData);
            }

            public void GetAsVec4(Mesh mesh, List<int> subMeshIndices, ref Vector4[] outData)
            {
                Vector4[] data = null;
                VertexElementType elType = (VertexElementType)m_elType;
                switch (elType)
                {
                    case VertexElementType.kPosition:
                        VertexBakerLib.LogError("Type Not Supported: " + elType);
                        break;
                    case VertexElementType.kNormal:
                        VertexBakerLib.LogError("Type Not Supported: " + elType);
                        break;
                    case VertexElementType.kTangent:
                        data = mesh.tangents;
                            break;
                    case VertexElementType.kColor:
                    case VertexElementType.kUV0:
                        {
                            List<Vector4> v = new List<Vector4>();
                            mesh.GetUVs(0, v);
                            data = v.ToArray();
                            break;
                        }
                    case VertexElementType.kUV1:
                        {
                            List<Vector4> v = new List<Vector4>();
                            mesh.GetUVs(1, v);
                            data = v.ToArray();
                            break;
                        }
                    case VertexElementType.kUV2:
                        {
                            List<Vector4> v = new List<Vector4>();
                            mesh.GetUVs(2, v);
                            data = v.ToArray();
                            break;
                        }
                    case VertexElementType.kUV3:
                        {
                            List<Vector4> v = new List<Vector4>();
                            mesh.GetUVs(3, v);
                            data = v.ToArray();
                            break;
                        }
                    default:
                        VertexBakerLib.LogError("Type Not Supported: " + elType);
                        data = null;
                        break;

                }
                if (subMeshIndices != null)
                {
                    outData = new Vector4[subMeshIndices.Count];
                    for (int i = 0, k = subMeshIndices.Count; i < k; ++i)
                    {
                        outData[i] = data[subMeshIndices[i]];
                    }
                }
                else
                {
                    outData = data;
                }
            }

            public void GetAsColor(Mesh mesh, ref Color[] outData)
            {
                GetAsColor(mesh, null, ref outData);
            }

            public void GetAsColor(Mesh mesh, List<int> subMeshIndices, ref Color[] outData)
            {
                Color[] data = null;
                VertexElementType elType = (VertexElementType)m_elType;
                switch (elType)
                {
                    case VertexElementType.kPosition:
                        VertexBakerLib.LogError("Type Not Supported: " + elType);
                        break;
                    case VertexElementType.kNormal:
                        VertexBakerLib.LogError("Type Not Supported: " + elType);
                        break;
                    case VertexElementType.kTangent:
                        VertexBakerLib.LogError("Type Not Supported: " + elType);
                        break;
                    case VertexElementType.kColor:
                        data = mesh.colors;
                        break;
                    case VertexElementType.kUV0:
                    case VertexElementType.kUV1:
                    case VertexElementType.kUV2:
                    case VertexElementType.kUV3:
                    default:
                        VertexBakerLib.LogError("Type Not Supported: " + elType);
                        data = null;
                        break;

                }
                if (subMeshIndices != null)
                {
                    outData = new Color[subMeshIndices.Count];
                    for (int i = 0, k = subMeshIndices.Count; i < k; ++i)
                    {
                        outData[i] = data[subMeshIndices[i]];
                    }
                }
                else
                {
                    outData = data;
                };
            }
        }

        public class DefaultVertex : IVertex
        {
            List<VertexElement> m_definition = new List<VertexElement>()
            {
                {new VertexElement(VertexElementType.kPosition, 3, Marshal.SizeOf(typeof(float)))},
                {new VertexElement(VertexElementType.kNormal, 3, Marshal.SizeOf(typeof(float)))},
                {new VertexElement(VertexElementType.kTangent, 4, Marshal.SizeOf(typeof(float)))},
            };

            int m_vertexSize = 0;
            int m_totalComponentCount = 0;

            public List<VertexElement> Definition
            {
                get
                {
                    return m_definition;
                }
            }

            public int ElementCount
            {
                get
                {
                    return m_definition.Count;
                }
            }

            public int VertexSize 
            {
                get 
                {
                    if(m_vertexSize == 0)
                    {
                        var iter = m_definition.GetEnumerator();
                        while(iter.MoveNext())
                        {
                            m_vertexSize += iter.Current.m_bytes;
                        }
                    }

                    return m_vertexSize;
                }
            }

            public int TotalComponentCount {
                get {
                    if (m_totalComponentCount == 0)
                    {
                        for(int i = 0, k = m_definition.Count; i < k; ++i)
                        {
                            m_totalComponentCount += m_definition[i].ComponentCount;
                        }
                    }

                    return m_totalComponentCount;
                }
            }
        }

        public static class BakeOptions
        {
            // mesh generation options
            public const uint kCalcNormals = (1 << 0);
            public const uint kCalcTangents = (1 << 1);
            public const uint kMeshCalcMask = (kCalcTangents - 1u);
            // shadow casting options
            public const uint kShadowsOff = (1 << 2);
            public const uint kShadowsOn = (1 << 3);
            public const uint kTwoSided = (1 << 4);
            public const uint kShadowsOnly = (1 << 5);
            public const uint kShadCastMask = ((kShadowsOnly - 1u) & ~kMeshCalcMask);
            // shadow receive
            public const uint kReceiveShadow = (1 << 6);
            public const uint kRecShadMask = ((kReceiveShadow - 1u) & ~kShadCastMask);

        }

        static readonly int SIZE_LONG = Marshal.SizeOf(typeof(long));
        static readonly int SIZE_INT = Marshal.SizeOf(typeof(int));
        static readonly int SIZE_FLOAT = Marshal.SizeOf(typeof(float));

        public Thread m_thread;
        public volatile bool m_cancel;
        public volatile bool m_run;
        public System.Action m_callback;
        public int m_result;

        public DateTime m_bakeStart;

        public List<MeshFilter> m_meshes;
        public List<Light> m_lights;

        // out handles
        public IntPtr[] m_outBasis0 = new IntPtr[] { IntPtr.Zero };
        public IntPtr[] m_outBasis1 = new IntPtr[] { IntPtr.Zero };
        public IntPtr[] m_outBasis2 = new IntPtr[] { IntPtr.Zero };

        // pointers to data
        public IntPtr m_meshIdsPtr = IntPtr.Zero;
        public IntPtr m_vertexCountsPtr = IntPtr.Zero;
        public IntPtr m_triangleCountPtr = IntPtr.Zero;
        public IntPtr m_matDataPtr = IntPtr.Zero;
        public IntPtr m_meshDataPtr = IntPtr.Zero;
        public IntPtr m_triangleDataPtr = IntPtr.Zero;
        public IntPtr m_lightsDataPtr = IntPtr.Zero;
        public IntPtr m_lightsOptPtr = IntPtr.Zero;
        public IntPtr m_bakeOptionsPtr = IntPtr.Zero;
        public IntPtr m_layerPtr = IntPtr.Zero;
        public IntPtr m_settingsIndicesPtr = IntPtr.Zero;
        public IntPtr[] m_settingsPtrs = new IntPtr[0];
        public int m_lightCount;
        public int m_meshCount;
        public int[] m_vertCounts;
        public string[] m_guids;
        public string[] m_sourcePaths;

        // vertex
        public int m_vertexEementCount;
        public VertexElement[] m_vertexDefinition;

        public MeshRenderer[] m_meshRenderers;
        public DaydreamVertexLighting[] m_lightBakers;

        public void InitBakeContext(List<MeshFilter> meshes, List<Light> lights)
        {
            BakeData.Instance().GetBakeSettings();

            // mark time
            m_bakeStart = DateTime.Now;

            string[] guids = new string[meshes.Count];
            string[] sourcePaths = new string[meshes.Count];
            m_meshRenderers = new MeshRenderer[meshes.Count];
            m_lightBakers = new DaydreamVertexLighting[meshes.Count];
            for (int i = 0; i < meshes.Count; ++i)
            {
                sourcePaths[i] = AssetDatabase.GetAssetPath(meshes[i].sharedMesh);
                //guids[i] = AssetDatabase.AssetPathToGUID(sourcePaths[i]);
                guids[i] = "" + meshes[i].GetUniqueId();
                m_meshRenderers[i] = meshes[i].GetComponent<MeshRenderer>();

                // check for daydream
                DaydreamVertexLighting bakerComp = m_meshRenderers[i].GetComponent<DaydreamVertexLighting>();
                if (bakerComp == null)
                {
                    bakerComp = m_meshRenderers[i].gameObject.AddComponent<DaydreamVertexLighting>();
                }
                m_lightBakers[i] = bakerComp;
            }

            m_meshes = meshes;
            m_lights = lights;

            // counts
            m_meshCount = meshes.Count;
            m_lightCount = lights != null ? lights.Count : 0;
            // other book-keeping data
            m_guids = guids;
            m_sourcePaths = sourcePaths;
        }

        public void Bake(List<MeshFilter> meshes, List<Light> lights, System.Action onFinished)
        {
            // refresh settings
            InitBakeContext(meshes, lights);

            // Create bake context
            // context for lights
            MeshFilter[] meshArr = meshes.ToArray();
            BuildLightContext(meshArr, lights, this);

            // build the rest of the scene
            BuildSceneContext(meshArr, this, new DefaultVertex());
            
            // on finished
            m_callback = onFinished;

            if (m_cancel) return;

            // start bake thread
            Start();

        }

        public delegate void OnFinishedUpdate(string message, float complete);

        public int BakeFinish(OnFinishedUpdate onUpdate)
        {
            VertexBakerLib.Assert(this != null && !m_run, "BakeFinished called but bake is still in process");

            string outputPath = BakeData.DataPath;

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            if (m_result != 0)
            {
                string error = VertexBakerLib.Instance.GetLastError();
                VertexBakerLib.LogError(error);
            }
            else if (!m_cancel && m_outBasis0[0] != IntPtr.Zero && m_outBasis1[0] != IntPtr.Zero && m_outBasis2[0] != IntPtr.Zero)
            {
                string bakeSetId = BakeData.Instance().GetBakeSettings().SelectedBakeSet.m_settingsId;

                BakeSets bakeSets = BakeData.Instance().GetBakeSets();
                MeshContainer meshContainer = BakeData.Instance().GetMeshContainer(bakeSetId);

                EditorUtility.SetDirty(meshContainer);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                AssetDatabase.StartAssetEditing();
                try
                {
                    int ptrOffset = 0;
                    int meshOffset = 0;
                    for (int m = 0; m < m_meshes.Count; ++m)
                    {
                        int count = m_vertCounts[m];
                        int floatCount = count * 3;

                        // the ID used to look up this mesh later
                        string objectId = "" + m_lightBakers[m].GetUniqueId();

                        m_lightBakers[m].m_currentContainer = meshContainer;


                        Mesh outputMesh = meshContainer.m_list.Find(delegate (Mesh mesh)
                            {
                                if (mesh != null)
                                {
                                    return mesh.name == bakeSetId + "_" + objectId;
                                }
                                return false;
                            });

                        if (outputMesh == null)
                        {

                            if (m_lightBakers[m].VertexLighting != null)
                            {
                                // if we are here than the mesh name may have changed, try and remove the stale data
                                string oldName = m_lightBakers[m].VertexLighting.name;
                                Mesh found = meshContainer.m_list.Find(delegate (Mesh mesh)
                                    {
                                        // remove the old reference
                                        if (mesh != null)
                                        {
                                            return mesh.name == oldName;
                                        }
                                        // remove null mesh
                                        return false;
                                    });

                                if (found != null)
                                {
                                    GameObject.DestroyImmediate(found, true);
                                }
                            }

                            // if no mesh exists for this target create it here
                            outputMesh = new Mesh();

                            BakeData.Instance().AddToMeshContainer(meshContainer, outputMesh);

                            //meshContainer.m_list.Add(outputMesh);
                            //// add to the container asset
                            //string outputFileName = bakeSetId + "_lighting";
                            //AssetDatabase.AddObjectToAsset(outputMesh, outputPath + "/" + outputFileName + ".asset");
                        }

                        outputMesh.name = bakeSetId + "_" + objectId;

                        // HACK: Work around to make Unity happy. If vertices are not found the additional vertex stream fails
                        //outMeshes[m].vertices = m_meshes[m].sharedMesh.vertices;
                        outputMesh.vertices = m_meshes[m].sharedMesh.vertices;

                        // 3 floats per vector
                        float[] rawData0 = new float[floatCount];
                        float[] rawData1 = new float[floatCount];
                        float[] rawData2 = new float[floatCount];

                        // offset pointer to next mesh
                        IntPtr basis0 = new IntPtr(m_outBasis0[0].ToInt64() + ptrOffset * SIZE_FLOAT);
                        IntPtr basis1 = new IntPtr(m_outBasis1[0].ToInt64() + ptrOffset * SIZE_FLOAT);
                        IntPtr basis2 = new IntPtr(m_outBasis2[0].ToInt64() + ptrOffset * SIZE_FLOAT);
                        ptrOffset += floatCount;

                        // marshal data into float arrays
                        Marshal.Copy(basis0, rawData0, 0, floatCount);
                        Marshal.Copy(basis1, rawData1, 0, floatCount);
                        Marshal.Copy(basis2, rawData2, 0, floatCount);

                        // lists to hold output vectors
                        List<Color> colorList0 = new List<Color>();
                        colorList0.Resize(count, Color.black);
                        List<Vector3> colorList1 = new List<Vector3>();
                        colorList1.Resize(count, Vector3.zero);
                        List<Vector3> colorList2 = new List<Vector3>();
                        colorList2.Resize(count, Vector3.zero);

                        // copy float arrays into mesh data
                        for (int i = 0; i < count; ++i)
                        {
                            int idx = i * 3;
                            colorList0[i] = new Color(rawData0[idx], rawData0[idx + 1], rawData0[idx + 2], 1.0f);
                            colorList1[i] = new Vector3(rawData1[idx], rawData1[idx + 1], rawData1[idx + 2]);
                            colorList2[i] = new Vector3(rawData2[idx], rawData2[idx + 1], rawData2[idx + 2]);
                        }

                        // this offset is target uv sets 1, 2, and 3 for data destination
                        const int uvOffset = 1;

                        outputMesh.SetColors(colorList0);
                        outputMesh.SetUVs(uvOffset + 1, colorList1);
                        outputMesh.SetUVs(uvOffset + 2, colorList2);
                        //outputMesh.UploadMeshData(true);
                        meshOffset += count;

                        EditorUtility.SetDirty(meshContainer);
                        m_meshRenderers[m].additionalVertexStreams = outputMesh;
                        m_lightBakers[m].m_bakeSets = bakeSets;
                        m_lightBakers[m].VertexLighting = outputMesh;
                        m_lightBakers[m].m_bakeId = objectId;

                        EditorUtility.SetDirty(m_lightBakers[m]);

                        onUpdate("Uploading Mesh Data", m_meshCount / (float)m);
                    }

                    // remove any null slots
                    meshContainer.m_list.RemoveAll(delegate (Mesh m)
                        {
                            return m == null;
                        });

                    // aggregate containers under one super container
                    int existingIdx = bakeSets.m_containers.FindIndex(delegate(MeshContainer mc) { return mc.name == meshContainer.name; });
                    if(existingIdx != -1)
                    {
                        // replace existing entry
                        bakeSets.m_containers[existingIdx] = meshContainer;
                    }
                    else
                    {
                        bakeSets.m_containers.Add(meshContainer);
                    }

                    BakeSetsInspector.CleanupStaleReferences(bakeSets);
                    EditorUtility.SetDirty(bakeSets);
                    AssetDatabase.SaveAssets();
                }
                finally
                {
                    onUpdate("Uploading Mesh Data", 1f);
                    AssetDatabase.StopAssetEditing();
                }

            }
            else
            {
                VertexBakerLib.LogWarning("Bake completed successfully but there was no output data available");
            }

            // free data
            FreeContext(true);

            // since basis memory was allocated in one chunk
            // freeing this handle frees all basis memory
            VertexBakerLib.Instance.Free(m_outBasis0[0]);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            VertexBakerLib.Log("Bake time: " + (DateTime.Now - m_bakeStart).TotalSeconds + " seconds");

            while (VertexBakerLib.Instance.GetErrorCount() > 0)
            {
                string err = VertexBakerLib.Instance.GetLastError();
                VertexBakerLib.LogError(err);
            }

            GC.Collect();

            return m_result;
        }
        
        public void Start()
        {
            if (m_thread != null)
            {
                if (m_run)
                {
                    m_run = false;
                    m_thread.Abort();
                }
            }

            m_run = true;
            m_thread = new Thread(Bake);
            m_thread.Start();
        }

        public void Stop()
        {
            m_run = false;
        }

        #region _Bake
        #if DDR_RUNTIME_DLL_LINKING_
        private delegate
        #else
        [DllImport(VertexBakerLib.LIBNAME)]
        private static extern
        #endif
        int _Bake(IntPtr meshIds, IntPtr vertexCounts, IntPtr triangleCountPtr, IntPtr matData
            , [In, Out] IntPtr mesh, IntPtr triangles, [In, Out] IntPtr bakeOptions, IntPtr layers, int meshCount, int vertElementCount
            , BakeContext.VertexElement[] vertexFormat, [In] string[] guids, [In] string[] sourcePaths, IntPtr settingsIndicesPtr, IntPtr[] settingsPtr
            , [In, Out] IntPtr lightDataPtr, [In, Out] IntPtr lightsOptPtr, int lightCount, [In, Out] IntPtr[] outBasis0, [In, Out] IntPtr[] outBasis1, [In, Out] IntPtr[] outBasis2);
        #endregion
        public void Bake()
        {
            try
            {
#if DDR_RUNTIME_DLL_LINKING_
                m_result = VertexBakerLib.Instance.InvokeAsync<int, _Bake>(m_meshIdsPtr, m_vertexCountsPtr, m_triangleCountPtr, m_matDataPtr
                , m_meshDataPtr, m_triangleDataPtr, m_bakeOptionsPtr, m_layerPtr, m_meshCount, m_vertexEementCount, m_vertexDefinition, m_guids
                , m_sourcePaths, m_settingsIndicesPtr, m_settingsPtrs, m_lightsDataPtr, m_lightsOptPtr
                , m_lightCount, m_outBasis0, m_outBasis1, m_outBasis2);
#else
                m_result = _Bake(m_meshIdsPtr, m_vertexCountsPtr, m_triangleCountPtr, m_matDataPtr
                , m_meshDataPtr, m_triangleDataPtr, m_bakeOptionsPtr, m_layerPtr, m_meshCount, m_vertexEementCount, m_vertexDefinition, m_guids
                , m_sourcePaths, m_settingsIndicesPtr, m_settingsPtrs, m_lightsDataPtr, m_lightsOptPtr
                , m_lightCount, m_outBasis0, m_outBasis1, m_outBasis2);
#endif
                m_run = false;
                m_callback();
            }
            catch (Exception e)
            {

                VertexBakerLib.LogError(e.Message);
                VertexBakerLib.LogError(e.StackTrace);
            }
        }

        public void FreeContext(bool freeLights = false)
        {
            VertexBakerLib.Instance.Free(m_meshIdsPtr);
            VertexBakerLib.Instance.Free(m_vertexCountsPtr);
            VertexBakerLib.Instance.Free(m_triangleCountPtr);
            VertexBakerLib.Instance.Free(m_matDataPtr);
            VertexBakerLib.Instance.Free(m_meshDataPtr);
            VertexBakerLib.Instance.Free(m_triangleDataPtr);
            VertexBakerLib.Instance.Free(m_bakeOptionsPtr);
            VertexBakerLib.Instance.Free(m_layerPtr);
            VertexBakerLib.Instance.Free(m_settingsIndicesPtr);
            for (int i = 0; i < m_settingsPtrs.Length; ++i)
            {
                VertexBakerLib.Instance.Free(m_settingsPtrs[i]);
            }
            m_settingsPtrs = new IntPtr[0]; // clear

            if (freeLights)
            {
                VertexBakerLib.Instance.Free(m_lightsDataPtr);
                VertexBakerLib.Instance.Free(m_lightsOptPtr);
                m_lightsOptPtr = IntPtr.Zero;
                m_lightsDataPtr = IntPtr.Zero;
            }

            m_meshIdsPtr = IntPtr.Zero;
            m_vertexCountsPtr = IntPtr.Zero;
            m_triangleCountPtr = IntPtr.Zero;
            m_matDataPtr = IntPtr.Zero;
            m_meshDataPtr = IntPtr.Zero;
            m_triangleDataPtr = IntPtr.Zero;
            m_bakeOptionsPtr = IntPtr.Zero;
            m_settingsIndicesPtr = IntPtr.Zero;
        }


        static void BuildLightContext(MeshFilter[] meshes, List<Light> lights, BakeContext ctx)
        {
            int lightCount = lights.Count;

            // light data size
            const int lightPosSize = 3; // padded with extra float
            const int LightDirSize = 3; // padded with extra float
            const int lightColorSize = 3; // padded with extra float (don't need alpha)
            const int lightRIATSize = 4; // range, intensity, angle, type

            int totalLightSize = lightCount * SIZE_FLOAT * (lightPosSize + LightDirSize + lightColorSize + lightRIATSize);

            VertexBakerLib instance = VertexBakerLib.Instance;
            ctx.m_lightsDataPtr = instance.Alloc(totalLightSize);
            ctx.m_lightsOptPtr = instance.Alloc(lightCount * SIZE_LONG);
            int lightDestOffset = 0;
            float[] riat = new float[4];
            long[] data = new long[lightCount];

            // light layout
            for (int l = 0; l < lightCount; ++l)
            {
                Light light = lights[l];
                riat[0] = light.range;
                riat[1] = light.intensity;
                riat[2] = light.spotAngle;
                riat[3] = (float)light.type;

                // position data
                IntPtr lightPosPtr = new IntPtr(ctx.m_lightsDataPtr.ToInt64() + lightDestOffset * SIZE_FLOAT);
                instance.CopyVector4(lightPosPtr, lightPosSize * SIZE_FLOAT, light.transform.position.ToVector4(1f), lightPosSize * SIZE_FLOAT);
                lightDestOffset += lightPosSize;

                // direction data
                IntPtr lightDirPtr = new IntPtr(ctx.m_lightsDataPtr.ToInt64() + lightDestOffset * SIZE_FLOAT);
                instance.CopyVector4(lightDirPtr, LightDirSize * SIZE_FLOAT, light.transform.forward.ToVector4(0f), LightDirSize * SIZE_FLOAT);
                lightDestOffset += LightDirSize;

                // color data
                IntPtr lightColorPtr = new IntPtr(ctx.m_lightsDataPtr.ToInt64() + lightDestOffset * SIZE_FLOAT);
                instance.CopyVector4(lightColorPtr, lightColorSize * SIZE_FLOAT, light.color.ToVector4(1f), lightColorSize * SIZE_FLOAT);
                lightDestOffset += LightDirSize;

                // IRAT data
                IntPtr lightIRATPtr = new IntPtr(ctx.m_lightsDataPtr.ToInt64() + lightDestOffset * SIZE_FLOAT);
                instance.CopyFloatArray(lightIRATPtr, lightRIATSize * SIZE_FLOAT, riat, lightRIATSize * SIZE_FLOAT);
                lightDestOffset += lightRIATSize;

                // set lighting options
                ulong dataValue = 0;
                // 3 bits
                dataValue |= (ulong)lights[l].shadows;
                // 32 bits max
                dataValue |= (ulong)(((long)lights[l].cullingMask) << 3);

                data[l] |= (long)dataValue;
            }

            Marshal.Copy(data, 0, ctx.m_lightsOptPtr, lightCount);

        }

        // Helper method for marshaling mesh data
        public static void BuildSceneContext(MeshFilter[] meshes, BakeContext ctx, IVertex vertex = null)
        {
            BuildSceneContext(meshes, null, null, ctx, vertex);
        }

        // Helper method for marshaling mesh data with sub mesh definition
        public static void BuildSceneContext(MeshFilter[] meshes, List<List<int>> subMeshIndices, List<List<int>> subMeshTriangleIndices, BakeContext ctx, IVertex vertex = null)
        {

            if(vertex == null)
            {
                vertex = new DefaultVertex();
            }

            ctx.m_vertexEementCount = vertex.ElementCount;
            ctx.m_vertexDefinition = vertex.Definition.ToArray();
            
            int totalVertCount = 0;
            int totalTriCount = 0;
            int meshCount = meshes.Length;

            // extract mesh renderer options
            MeshRenderer[] renderer = ctx.m_meshRenderers;

            // calculate mesh data size
            for (int i = 0; i < meshCount; ++i)
            {
                // if a sub mesh is defined use it for vert count
                if(subMeshIndices != null && subMeshIndices[i] != null)
                {
                    totalVertCount += subMeshIndices[i].Count;
                }
                else
                {
                    totalVertCount += meshes[i].sharedMesh.vertices.Length;
                }

                // if a sub mesh is defined use it for triangle index count
                if (subMeshTriangleIndices != null && subMeshTriangleIndices[i] != null)
                {
                    totalTriCount += subMeshTriangleIndices[i].Count;
                }
                else
                {
                    totalTriCount += meshes[i].sharedMesh.triangles.Length;
                }
            }

            // data size
            const int triangleSize = 3;
            const int matSize = 16;

            int totalMatrixDataSize = matSize * meshCount * SIZE_FLOAT;
            // mesh size depends on vertex definition
            int totalMeshDataSize = totalVertCount * SIZE_FLOAT * (vertex.VertexSize + meshCount); 
            int totalTriangleDataSize = totalTriCount * triangleSize * SIZE_INT;

            VertexBakerLib instance = VertexBakerLib.Instance;
            ctx.m_meshIdsPtr = instance.Alloc(meshCount * SIZE_INT);
            ctx.m_vertexCountsPtr = instance.Alloc(meshCount * SIZE_INT);
            ctx.m_triangleCountPtr = instance.Alloc(meshCount * SIZE_INT);
            ctx.m_matDataPtr = instance.Alloc(totalMatrixDataSize);
            ctx.m_meshDataPtr = instance.Alloc(totalMeshDataSize);
            ctx.m_triangleDataPtr = instance.Alloc(totalTriangleDataSize);
            ctx.m_settingsIndicesPtr = instance.Alloc(meshCount * SIZE_INT);
            ctx.m_bakeOptionsPtr = instance.Alloc(meshCount * SIZE_INT);
            ctx.m_layerPtr = instance.Alloc(meshCount * SIZE_INT);

            // temp buffer for matrix
            float[] matArr = new float[16];

            int matDestOffset = 0;
            int meshDestOffset = 0;
            int triangleDestOffset = 0;

            int[] vertexCounts = new int[meshCount];
            int[] triangleCounts = new int[meshCount];
            int[] ids = new int[meshCount];
            uint[] perMeshBakeOpt = new uint[meshCount];
            uint[] layerMask = new uint[meshCount];

            // data for settings
            int[] settingsIdx = new int[meshCount];
            List<IntPtr> settingsList = new List<IntPtr>();

            // global settings
            int globalSettingsIdx = 0;
            IntPtr globalSettings = SettingsToIntPtr(BakeData.Instance().GetBakeSettings().SelectedBakeSet);
            settingsList.Add(globalSettings);

            for (int m = 0; m < meshCount; ++m)
            {
                bool processSubMesh = false;

                // assume sub mesh
                if(subMeshIndices != null && subMeshIndices[m] != null
                    && subMeshTriangleIndices != null && subMeshTriangleIndices[m] != null)
                {
                    processSubMesh = true;
                }
                // setup settings
                settingsIdx[m] = globalSettingsIdx;
                // check for override settings
                VertexLightingOverride ovrdSettings = meshes[m].GetComponent<VertexLightingOverride>();
                if (ovrdSettings != null)
                {
                    // point at this overrides index
                    settingsIdx[m] = settingsList.Count;
                    // ensure ambient settings (copy from global which contains the valid ambient settings for now)
                    ovrdSettings.m_bakeSettingsOverride.CopyAmbient(BakeData.Instance().GetBakeSettings().SelectedBakeSet);
                    IntPtr settingsPtr = SettingsToIntPtr(ovrdSettings.m_bakeSettingsOverride);
                    settingsList.Add(settingsPtr);
                }

                Mesh mesh = meshes[m].sharedMesh;
                ids[m] = meshes[m].GetUniqueId();

                // layer mask
                layerMask[m] = (uint)(1 << meshes[m].gameObject.layer);

                // clear data
                perMeshBakeOpt[m] = 0;

                // if mesh has no normals or tangents flag them for generation
                // should calculate normals
                if (meshes[m].sharedMesh.normals.Length == 0)
                {
                    // set bit for normals
                    perMeshBakeOpt[m] |= BakeOptions.kCalcNormals;
                }
                // should calculate tangents
                if (meshes[m].sharedMesh.tangents.Length == 0)
                {
                    // set bit for tangents
                    perMeshBakeOpt[m] |= BakeOptions.kCalcTangents;
                }

                // extract shadowing options from renderer
                switch (renderer[m].shadowCastingMode)
                {
                    case UnityEngine.Rendering.ShadowCastingMode.Off:
                        {
                            perMeshBakeOpt[m] |= BakeOptions.kShadowsOff;
                        }
                        break;
                    case UnityEngine.Rendering.ShadowCastingMode.TwoSided:
                        {
                            perMeshBakeOpt[m] |= BakeOptions.kTwoSided;
                        }
                        break;
                    case UnityEngine.Rendering.ShadowCastingMode.On:
                        {
                            perMeshBakeOpt[m] |= BakeOptions.kShadowsOn;
                        }
                        break;
                    case UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly:
                        {
                            perMeshBakeOpt[m] |= BakeOptions.kShadowsOnly;
                        }
                        break;
                    default:
                        break;
                }

                if (renderer[m].receiveShadows)
                {
                    perMeshBakeOpt[m] |= BakeOptions.kReceiveShadow;
                }
                else
                {
                    perMeshBakeOpt[m] &= ~BakeOptions.kReceiveShadow;
                }

                // use the list of unique indices of the sub mesh to find the count here unless its null then assume all vertices are being processed
                int vertexCount = processSubMesh ? subMeshIndices[m].Count : mesh.vertices.Length;

                // use the list of triangles from the sub mesh to find the count here unless its null then assume all triangles are being processed
                int triangleCount = processSubMesh ? subMeshTriangleIndices[m].Count : mesh.triangles.Length;

                vertexCounts[m] = vertexCount;
                triangleCounts[m] = triangleCount;

                // copy mesh data into mesh buffer starting with world matrix
                int matIndex = 0;
                AssignMat4(ref matArr, meshes[m].transform.localToWorldMatrix, ref matIndex); // 64 bytes

                IntPtr matDestPtr = new IntPtr(ctx.m_matDataPtr.ToInt64() + matDestOffset * SIZE_FLOAT);
                Marshal.Copy(matArr, 0, matDestPtr, 16);
                matDestOffset += 16;

                if (processSubMesh)
                {
                    // build sub mesh
                    BuildMesh(ctx, meshes[m].sharedMesh, vertex, subMeshIndices[m], ref meshDestOffset);
                }
                else
                {
                    // build entire mesh
                    BuildMesh(ctx, meshes[m].sharedMesh, vertex, ref meshDestOffset);
                }

                // triangles
                IntPtr indexPtr = new IntPtr(ctx.m_triangleDataPtr.ToInt64() + triangleDestOffset * SIZE_INT);
                if(processSubMesh)
                {
                    // copy sub mesh triangle list
                    Marshal.Copy(subMeshTriangleIndices[m].ToArray(), 0, indexPtr, triangleCount);
                }
                else
                {
                    // copy entire triangle list
                    Marshal.Copy(mesh.triangles, 0, indexPtr, triangleCount);
                }

                triangleDestOffset += triangleCount;
            }

            // copy the mesh into pointer
            instance.CopyArray(ctx.m_meshIdsPtr, meshCount * SIZE_INT, ids, meshCount * SIZE_INT);
            instance.CopyArray(ctx.m_vertexCountsPtr, meshCount * SIZE_INT, vertexCounts, meshCount * SIZE_INT);
            instance.CopyArray(ctx.m_triangleCountPtr, meshCount * SIZE_INT, triangleCounts, meshCount * SIZE_INT);
            instance.CopyUIntArray(ctx.m_bakeOptionsPtr, meshCount * SIZE_INT, perMeshBakeOpt, meshCount * SIZE_INT);
            instance.CopyUIntArray(ctx.m_layerPtr, meshCount * SIZE_INT, layerMask, meshCount * SIZE_INT);
            instance.CopyArray(ctx.m_settingsIndicesPtr, meshCount * SIZE_INT, settingsIdx, meshCount * SIZE_INT);

            ctx.m_settingsPtrs = settingsList.ToArray();
            ctx.m_vertCounts = vertexCounts;
        }


        // Prepare mesh data in a flexible way based on vertex definition
        static void BuildMesh(BakeContext ctx, Mesh mesh, IVertex vertex, ref int ptrOffset)
        {
            BuildMesh(ctx, mesh, vertex, null, ref ptrOffset);
        }

        // Prepare sub-mesh data in a flexible way based on vertex definition
        static void BuildMesh(BakeContext ctx, Mesh mesh, IVertex vertex, List<int> optSubMeshIndices, ref int ptrOffset)
        {
            int vertexCount = optSubMeshIndices == null ? mesh.vertices.Length : optSubMeshIndices.Count;

            var vertElementIter = vertex.Definition.GetEnumerator();
            while (vertElementIter.MoveNext())
            {
                VertexElementType elType = vertElementIter.Current.GetElType;
                VertexElement elDef = vertElementIter.Current;
                int componentCount = elDef.ComponentCount;

                // pointer to new data
                IntPtr dataPtr = new IntPtr(ctx.m_meshDataPtr.ToInt64() + ptrOffset * SIZE_FLOAT);

                if(elType == VertexElementType.kTangent && mesh.tangents.Length == 0)
                {
                    // If the data type is kTangent and there is not tangent data, copy UV0 data into the first half of the tangent
                    // data buffer, leave the second half empty. The UV0 data will be used by the baker to generate the tangent data, which
                    // will then be re-written into the tangent buffer.
                    VertexElement uvElement = new VertexElement(VertexElementType.kUV0, 2, SIZE_FLOAT);
                    CopyVectorArrayFromMesh(dataPtr, vertexCount * uvElement.TotalByteSize, vertexCount * uvElement.TotalByteSize, mesh, uvElement);
                }
                else if(elType == VertexElementType.kNormal && mesh.normals.Length == 0)
                {
                    // Do Nothing!
                    // If the data type is kNormal and there is no data skip this step but still increment pointer to leave a 'hole' in the buffer
                    // that he baker can fill with dynamically generated normals. This statement is just here for reader clarity.
                }
                else
                {
                    // if here just copy data of the element size into the dataPtr (the buffer)
                    CopyVectorArrayFromMesh(dataPtr, vertexCount * elDef.TotalByteSize, vertexCount * elDef.TotalByteSize, mesh, elDef, optSubMeshIndices);
                }

                // regardless of which 'if-statement' we fall, in always increment the pointer by the full amount
                ptrOffset += vertexCount * componentCount;

            }
        }

        // helper method to copy vector array data in a somewhat generic fashion
        static void CopyVectorArrayFromMesh(IntPtr dest, int destSize, int byteCount, Mesh mesh, VertexElement element)
        {
            CopyVectorArrayFromMesh(dest, destSize, byteCount, mesh, element, null);
        }

        static void CopyVectorArrayFromMesh(IntPtr dest, int destSize, int byteCount, Mesh mesh, VertexElement element, List<int> subMeshIndices)
        {
            switch (element.ComponentCount)
            {
                case 2:
                    {
                        Vector2[] data = null;
                        element.GetAsVec2(mesh, subMeshIndices, ref data);
                        VertexBakerLib.Instance.CopyVector2Array(dest, destSize, data, byteCount);
                    }
                    break;
                case 3:
                    {
                        Vector3[] data = null;
                        element.GetAsVec3(mesh, subMeshIndices, ref data);
                        VertexBakerLib.Instance.CopyVector3Array(dest, destSize, data, byteCount);
                    }
                    break;
                case 4:
                    if (element.GetElType == VertexElementType.kColor)
                    {
                        Color[] data = null;
                        element.GetAsColor(mesh, subMeshIndices, ref data);
                        VertexBakerLib.Instance.CopyColorArray(dest, destSize, data, byteCount);
                    }
                    else
                    {
                        Vector4[] data = null;
                        element.GetAsVec4(mesh, subMeshIndices, ref data);
                        VertexBakerLib.Instance.CopyVector4Array(dest, destSize, data, byteCount);
                    }
                    break;
                default:
                    break;
            }
        }

        static IntPtr SettingsToIntPtr(DDRSettings.BakeSettings settings)
        {
            byte[] data = settings.ToFlatbuffer();
            IntPtr ptr = VertexBakerLib.Instance.Alloc(Marshal.SizeOf(data[0]) * data.Length);
            Marshal.Copy(data, 0, ptr, data.Length);
            return ptr;
        }

        static void AssignMat4(ref float[] data, Matrix4x4 src, ref int index)
        {
            data[index++] = src.m00;
            data[index++] = src.m01;
            data[index++] = src.m02;
            data[index++] = src.m03;

            data[index++] = src.m10;
            data[index++] = src.m11;
            data[index++] = src.m12;
            data[index++] = src.m13;

            data[index++] = src.m20;
            data[index++] = src.m21;
            data[index++] = src.m22;
            data[index++] = src.m23;

            data[index++] = src.m30;
            data[index++] = src.m31;
            data[index++] = src.m32;
            data[index++] = src.m33;
        }

    }

}
