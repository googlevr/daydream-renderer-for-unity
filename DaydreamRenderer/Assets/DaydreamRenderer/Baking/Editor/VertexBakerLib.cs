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
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using UnityEditor;
using System.IO;
using daydreamrenderer;
using FlatBuffers;
using System.Threading;
using UnityEngine.SceneManagement;
using System.Reflection;
using UnityEditor.SceneManagement;

namespace daydreamrenderer
{
    using BakeSettings = DDRSettings.BakeSettings;

    public class VertexBakerLib : NativeLib
    {

#if UNITY_EDITOR_OSX
        public const string LIBNAME = "daydreambaker";
#else
        public const string LIBNAME = "Assets\\Plugins\\DaydreamRenderer\\x86_64\\daydreambaker\\daydreambaker.dll";
#endif

        static public readonly string m_settingsFileName = "bakesettings";
        public static int s_logging = Logging.kMinimal;

        static readonly int SIZE_INT = Marshal.SizeOf(typeof(int));
        static readonly int SIZE_FLOAT = Marshal.SizeOf(typeof(float));
        

        public static class Logging
        {
            public const int kMinimal = 0;
            public const int kWarning = 1;
            public const int kError = 2;
            public const int kVerbose = 3;
            public const int kAssert = 4;
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

        public static void Log(string msg)
        {
            if (s_logging >= Logging.kVerbose)
            {
                Debug.Log(msg);
            }
        }

        public static void LogError(string msg)
        {
            if (s_logging >= Logging.kError)
            {
                Debug.LogError(msg);
            }
        }

        public static void LogWarning(string msg)
        {
            if (s_logging >= Logging.kWarning)
            {
                Debug.LogWarning(msg);
            }
        }

        public static void Assert(bool cond)
        {
            if (s_logging >= Logging.kAssert)
            {
                Debug.Assert(cond);
            }
        }

        public static void Assert(bool cond, string msg)
        {
            if (s_logging >= Logging.kAssert)
            {
                Debug.Assert(cond, msg);
            }
        }


        public static void AssertFormat(bool cond, string format, params object[] args)
        {
            if (s_logging >= Logging.kAssert)
            {
                Debug.AssertFormat(cond, format, args);
            }
        }
        
        public class Handle
        {
            public Handle(IntPtr handle, VertexBakerLib lib)
            {
                m_handle = handle;
                m_lib = lib;
            }
            public void FreeHandle()
            {
                if (m_lib.LibLoaded && m_handle != IntPtr.Zero)
                {
                    m_lib.FreeHandle(m_handle);
                    m_handle = IntPtr.Zero;
                }
            }
            public bool IsValid()
            {
                if (m_lib.LibLoaded && m_handle != IntPtr.Zero)
                {
                    return m_lib.ValidHandle(m_handle);
                }
                return false;
            }
            public IntPtr Ptr()
            {
                return m_handle;
            }
            ~Handle()
            {
                FreeHandle();
            }
            VertexBakerLib m_lib;
            IntPtr m_handle;
        }

        public class BVHHandle : Handle
        {
            public BVHHandle(MeshFilter source, IntPtr handle, VertexBakerLib lib) : base(handle, lib)
            {
                SourceMeshData = source;
            }

            public MeshFilter SourceMeshData {
                get {
                    return m_sourceMeshData;
                }

                set {
                    m_sourceMeshData = value;
                }
            }

            MeshFilter m_sourceMeshData;
        }

        private static VertexBakerLib s_instance;

        public static VertexBakerLib Instance {
            get {
                if (s_instance == null || !s_instance.LibLoaded)
                {
                    if (s_instance == null)
                    {
                        s_instance = new VertexBakerLib();
                    }
                    s_instance.LoadLib();
#if UNITY_EDITOR

#if DDR_RUNTIME_DLL_LINKING_
                    s_instance.StartFileWatcher();
#endif
                    s_instance.LoadSettings();
#endif
                }
                return s_instance;
            }
        }

        private VertexBakerLib() : base(LIBNAME)
        { }

        public delegate void BakeProgressUpdate(IntPtr data);

        #region _MemoryCopy
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _MemoryCopy(IntPtr dest, int destSize, IntPtr src, int byteCount);
        #endregion
        public int MemoryCopy(IntPtr dest, int destSize, IntPtr src, int byteCount)
        {
#if DDR_RUNTIME_DLL_LINKING_
        int errno = Invoke<int, _MemoryCopy>(dest, destSize, src, byteCount);
#else
            int errno = _MemoryCopy(dest, destSize, src, byteCount);
#endif
            if (errno != 0)
            {
                VertexBakerLib.LogError("Error copying data in memcpy_s, errno " + errno);
            }
            return errno == 0 ? byteCount : 0;
        }

        #region _CopyArray
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _CopyArray(IntPtr dest, int destSize, [In, Out] int[] src, int byteCount);
        #endregion
        public int CopyArray(IntPtr dest, int destSize, int[] src, int byteCount)
        {
#if DDR_RUNTIME_DLL_LINKING_
        int errno = Invoke<int, _CopyArray>(dest, destSize, src, byteCount);
#else
            int errno = _CopyArray(dest, destSize, src, byteCount);
#endif
            if (errno != 0)
            {
                VertexBakerLib.LogError("Error copying data in memcpy_s, errno " + errno);
            }
            return errno == 0 ? byteCount : 0;
        }

        #region _CopyUIntArray
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _CopyUIntArray(IntPtr dest, int destSize, [In, Out] uint[] src, int byteCount);
        #endregion
        public int CopyUIntArray(IntPtr dest, int destSize, uint[] src, int byteCount)
        {
#if DDR_RUNTIME_DLL_LINKING_
        int errno = Invoke<int, _CopyUIntArray>(dest, destSize, src, byteCount);
#else
            int errno = _CopyUIntArray(dest, destSize, src, byteCount);
#endif
            if (errno != 0)
            {
                VertexBakerLib.LogError("Error copying data in memcpy_s, errno " + errno);
            }
            return errno == 0 ? byteCount : 0;
        }


        #region _CopyFloatArray
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _CopyFloatArray(IntPtr dest, int destSize, [In, Out] float[] src, int byteCount);
        #endregion

        public int CopyFloatArray(IntPtr dest, int destSize, float[] src, int byteCount)
        {
#if DDR_RUNTIME_DLL_LINKING_
        int errno = Invoke<int, _CopyFloatArray>(dest, destSize, src, byteCount);
#else
            int errno = _CopyFloatArray(dest, destSize, src, byteCount);
#endif
            if (errno != 0)
            {
                VertexBakerLib.LogError("Error copying data in memcpy_s, errno " + errno);
            }
            return errno == 0 ? byteCount : 0;
        }

        #region _CopyVector2Array
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _CopyVector2Array(IntPtr dest, int destSize, [In, Out] Vector2[] src, int byteCount);
        #endregion
        public int CopyVector2Array(IntPtr dest, int destSize, Vector2[] src, int byteCount)
        {
#if DDR_RUNTIME_DLL_LINKING_
        int errno = Invoke<int, _CopyVector2Array>(dest, destSize, src, byteCount);
#else
            int errno = _CopyVector2Array(dest, destSize, src, byteCount);
#endif
            if (errno != 0)
            {
                VertexBakerLib.LogError("Error copying data in memcpy_s, errno " + errno);
            }
            return errno == 0 ? byteCount : 0;
        }

        #region _CopyVector3Array
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _CopyVector3Array(IntPtr dest, int destSize, [In, Out] Vector3[] src, int byteCount);
        #endregion
        public int CopyVector3Array(IntPtr dest, int destSize, Vector3[] src, int byteCount)
        {
#if DDR_RUNTIME_DLL_LINKING_
        int errno = Invoke<int, _CopyVector3Array>(dest, destSize, src, byteCount);
#else
            int errno = _CopyVector3Array(dest, destSize, src, byteCount);
#endif
            if (errno != 0)
            {
                VertexBakerLib.LogError("Error copying data in memcpy_s, errno " + errno);
            }
            return errno == 0 ? byteCount : 0;
        }

        #region _CopyVector4Array
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _CopyVector4Array(IntPtr dest, int destSize, [In] Vector4[] src, int byteCount);
        #endregion
        public int CopyVector4Array(IntPtr dest, int destSize, Vector4[] src, int byteCount)
        {
#if DDR_RUNTIME_DLL_LINKING_
        int errno = Invoke<int, _CopyVector4Array>(dest, destSize, src, byteCount);
#else
            int errno = _CopyVector4Array(dest, destSize, src, byteCount);
#endif
            if (errno != 0)
            {
                VertexBakerLib.LogError("Error copying data in memcpy_s, errno " + errno);
            }
            return errno == 0 ? byteCount : 0;
        }

        #region _CopyVector4Array
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _CopyColorArray(IntPtr dest, int destSize, [In] Color[] src, int byteCount);
        #endregion
        public int CopyColorArray(IntPtr dest, int destSize, Color[] src, int byteCount)
        {
#if DDR_RUNTIME_DLL_LINKING_
        int errno = Invoke<int, _CopyColorArray>(dest, destSize, src, byteCount);
#else
            int errno = _CopyColorArray(dest, destSize, src, byteCount);
#endif
            if (errno != 0)
            {
                VertexBakerLib.LogError("Error copying data in memcpy_s, errno " + errno);
            }
            return errno == 0 ? byteCount : 0;
        }

        #region _CopyVector4
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _CopyVector4(IntPtr dest, int destSize, ref Vector4 src, int byteCount);
        #endregion

        public int CopyVector4(IntPtr dest, int destSize, Vector4 src, int byteCount)
        {
#if DDR_RUNTIME_DLL_LINKING_
        int errno = Invoke<int, _CopyVector4>(dest, destSize, src, byteCount);
#else
            int errno = _CopyVector4(dest, destSize, ref src, byteCount);
#endif
            if (errno != 0)
            {
                VertexBakerLib.LogError("Error copying data in memcpy_s, errno " + errno);
            }
            return errno == 0 ? byteCount : 0;
        }



        #region _Alloc
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    IntPtr _Alloc(int size);
        #endregion
        public IntPtr Alloc(int size)
        {
#if DDR_RUNTIME_DLL_LINKING_
        IntPtr ptr = Invoke<IntPtr, _Alloc>(size);
#else
            IntPtr ptr = _Alloc(size);
#endif
            if (ptr == IntPtr.Zero)
            {
                VertexBakerLib.LogError("Alloc null");
            }

            return ptr;
        }

        #region _Free
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    void _Free([In, Out]IntPtr ptr);
        #endregion
        public void Free(IntPtr ptr)
        {
#if DDR_RUNTIME_DLL_LINKING_
        Invoke<_Free>(ptr);
#else
            _Free(ptr);
#endif
            ptr = IntPtr.Zero;

        }

        #region _FreeHandle
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    void _FreeHandle(IntPtr handle);
        #endregion
        public void FreeHandle(IntPtr handle)
        {
#if DDR_RUNTIME_DLL_LINKING_
        Invoke<_FreeHandle>(handle);
#else
            _FreeHandle(handle);
#endif
            handle = IntPtr.Zero;
        }

        #region _ValidHandle
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _ValidHandle(IntPtr handle);
        #endregion
        public bool ValidHandle(IntPtr handle)
        {
#if DDR_RUNTIME_DLL_LINKING_
        return Invoke<int, _ValidHandle>(handle) == 1;
#else
            return _ValidHandle(handle) == 1;
#endif
        }

        #region _GetLastError
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    IntPtr _GetLastError();
        #endregion
        public string GetLastError()
        {
#if DDR_RUNTIME_DLL_LINKING_
        return Marshal.PtrToStringAnsi(Invoke<IntPtr, _GetLastError>());
#else
            return Marshal.PtrToStringAnsi(_GetLastError());
#endif
        }

        #region _Triangle2LineSegmentIntersection
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    bool _Triangle2LineSegmentIntersection(ref Vector3 a, ref Vector3 b, ref Vector3 c, ref Vector3 startPoint, ref Vector3 endPoint, bool colBkFace);
        #endregion
        public bool Triangle2LineSegment(Vector3 a, Vector3 b, Vector3 c, Vector3 startPoint, Vector3 endPoint, bool colBkFace)
        {
#if DDR_RUNTIME_DLL_LINKING_
        return Invoke<bool, _Triangle2LineSegmentIntersection>(a, b, c, startPoint, endPoint, colBkFace);
#else
            return _Triangle2LineSegmentIntersection(ref a, ref b, ref c, ref startPoint, ref endPoint, colBkFace);
#endif
        }

        #region _Triangle2LineSegmentColPoint
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    bool _Triangle2LineSegmentColPoint(ref Vector3 a, ref Vector3 b, ref Vector3 c, ref Vector3 startPoint, ref Vector3 endPoint, bool colBkFace, [In, Out] Vector3[] colPoint);
        #endregion
        public bool Triangle2LineSegment(Vector3 a, Vector3 b, Vector3 c, Vector3 startPoint, Vector3 endPoint, bool colBkFace, ref float colX, ref float colY, ref float colZ)
        {
            Vector3[] outColPoint = new Vector3[1];
#if DDR_RUNTIME_DLL_LINKING_
        bool result = Invoke<bool, _Triangle2LineSegmentColPoint>(a, b, c, startPoint, endPoint, colBkFace, outColPoint);
#else
            bool result = _Triangle2LineSegmentColPoint(ref a, ref b, ref c, ref startPoint, ref endPoint, colBkFace, outColPoint);
#endif
            colX = outColPoint[0].x;
            colY = outColPoint[0].y;
            colZ = outColPoint[0].z;

            return result;
        }

        #region _GetErrorCount
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _GetErrorCount();
        #endregion
        public int GetErrorCount()
        {
#if DDR_RUNTIME_DLL_LINKING_
        return Invoke<int, _GetErrorCount>();
#else
            return _GetErrorCount();
#endif
        }

        public BakeContext m_baker = new BakeContext();

        public bool BakeInProgress()
        {
            return m_baker.m_run;
        }

        public bool BakeReset()
        {
            return m_baker.m_cancel = false;
        }

        public void Bake(List<MeshFilter> meshes, List<Light> lights, System.Action onFinished)
        {
            m_baker.Bake(meshes, lights, onFinished);
        }

        public int BakeFinish(BakeContext.OnFinishedUpdate onUpdate)
        {
            return m_baker.BakeFinish(onUpdate);
        }

        #region _BakeProgress
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    float _BakeProgress();
        #endregion
        public float BakeProgress()
        {
#if DDR_RUNTIME_DLL_LINKING_
        return Invoke<float, _BakeProgress>();
#else
            return _BakeProgress(); ;
#endif
        }

        #region _BakeCancel
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    float _BakeCancel();
        #endregion
        public void BakeCancel()
        {
            m_baker.m_cancel = true;
#if DDR_RUNTIME_DLL_LINKING_
        Invoke<_BakeCancel>();
#else
            _BakeCancel(); ;
#endif
        }

        #region _BuildBVH
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    IntPtr _BuildBVH(IntPtr meshIds, IntPtr vertexCounts, IntPtr triangleCountPtr, IntPtr matData
            , IntPtr mesh, IntPtr triangles, IntPtr bakeOptions, IntPtr layers, int meshCount, [In] string[] guids
            , [In] string[] sourcePaths, IntPtr settingsIndices, IntPtr[] settings, [In, Out] IntPtr[] outBVHHandle);
        #endregion

        public string BuildBVH(MeshFilter[] meshes)
        {
            BVHHandle[] handles = null;
            string ret = BuildBVH(meshes, ref handles);
            for (int i = 0; i < handles.Length; ++i)
            {
                FreeHandle(handles[i].Ptr());
            }
            return ret;
        }
        public string BuildBVH(MeshFilter[] meshes, ref BVHHandle[] bvhHandles)
        {

            m_baker.m_guids = new string[meshes.Length];
            m_baker.m_sourcePaths = new string[meshes.Length];
            m_baker.m_meshRenderers = new MeshRenderer[meshes.Length];
            for (int i = 0; i < meshes.Length; ++i)
            {
                m_baker.m_sourcePaths[i] = AssetDatabase.GetAssetPath(meshes[i].sharedMesh);
                m_baker.m_guids[i] = "" + meshes[i].GetUniqueId();
                m_baker.m_meshRenderers[i] = meshes[i].GetComponent<MeshRenderer>();
            }
            m_baker.m_meshCount = meshes.Length;

            BakeContext.BuildSceneContext(meshes, m_baker);

            IntPtr[] handles = new IntPtr[meshes.Length];
            for (int i = 0; i < handles.Length; ++i)
            {
                handles[i] = IntPtr.Zero;
            }
#if DDR_RUNTIME_DLL_LINKING_
        string msg = Marshal.PtrToStringAnsi(Invoke<IntPtr, _BuildBVH>(m_baker.m_meshIdsPtr, m_baker.m_vertexCountsPtr, m_baker.m_triangleCountPtr, m_baker.m_matDataPtr
            , m_baker.m_meshDataPtr, m_baker.m_triangleDataPtr, m_baker.m_bakeOptionsPtr, m_baker.m_layerPtr, m_baker.m_meshCount
            , m_baker.m_guids, m_baker.m_sourcePaths, m_baker.m_settingsIndicesPtr, m_baker.m_settingsPtrs, handles));
#else
            string msg = Marshal.PtrToStringAnsi(_BuildBVH(m_baker.m_meshIdsPtr, m_baker.m_vertexCountsPtr, m_baker.m_triangleCountPtr, m_baker.m_matDataPtr
                , m_baker.m_meshDataPtr, m_baker.m_triangleDataPtr, m_baker.m_bakeOptionsPtr, m_baker.m_layerPtr, m_baker.m_meshCount
                , m_baker.m_guids, m_baker.m_sourcePaths, m_baker.m_settingsIndicesPtr, m_baker.m_settingsPtrs, handles));
#endif
            m_baker.FreeContext();

            // output specialized handle
            bvhHandles = new BVHHandle[handles.Length];
            for (int i = 0; i < handles.Length; ++i)
            {
                bvhHandles[i] = new BVHHandle(meshes[i], handles[i], this);
            }

            return msg;
        }

        #region _RebuildBVH
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _RebuildBVH(IntPtr bvhHandle, IntPtr meshIds, IntPtr vertexCounts, IntPtr triangleCountPtr, IntPtr matData
            , IntPtr mesh, IntPtr triangles, IntPtr bakeOptions, IntPtr layers, int meshCount, [In] string[] guids, [In] string[] sourcePaths);
        #endregion

        public int RebuildBVH(BVHHandle bvhHandle)
        {
            // build baker context
            m_baker.m_sourcePaths = new string[] { AssetDatabase.GetAssetPath(bvhHandle.SourceMeshData.sharedMesh) };
            m_baker.m_guids = new string[] { "" + m_baker.m_meshes[0].GetUniqueId() };
            m_baker.m_meshRenderers = new MeshRenderer[] { bvhHandle.SourceMeshData.GetComponent<MeshRenderer>() };

            // parse data and fill in missing context
            BakeContext.BuildSceneContext(new MeshFilter[] { bvhHandle.SourceMeshData }, m_baker);

#if DDR_RUNTIME_DLL_LINKING_
        int err = Invoke<int, _RebuildBVH>(bvhHandle.Ptr(), m_baker.m_meshIdsPtr, m_baker.m_vertexCountsPtr, m_baker.m_triangleCountPtr
            , m_baker.m_matDataPtr, m_baker.m_meshDataPtr, m_baker.m_triangleDataPtr, m_baker.m_bakeOptionsPtr, m_baker.m_layerPtr, 1, m_baker.m_guids, m_baker.m_sourcePaths);
#else
            int err = _RebuildBVH(bvhHandle.Ptr(), m_baker.m_meshIdsPtr, m_baker.m_vertexCountsPtr, m_baker.m_triangleCountPtr
                , m_baker.m_matDataPtr, m_baker.m_meshDataPtr, m_baker.m_triangleDataPtr, m_baker.m_bakeOptionsPtr, m_baker.m_layerPtr, 1, m_baker.m_guids, m_baker.m_sourcePaths);
#endif

            m_baker.FreeContext();

            if (err != 0)
            {
                string error = GetLastError();
                VertexBakerLib.LogError(error);
            }

            return err;
        }


        #region _GetBVHBoundingBoxList
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    IntPtr _GetBVHBoundingBoxList(int meshId, [In, Out] ref Vector3[] center, [In, Out] ref Vector3[] size);
        #endregion

        public string GetBVHBoundingBoxList(MeshFilter mesh, ref Vector3[] center, ref Vector3[] size)
        {
#if DDR_RUNTIME_DLL_LINKING_
        string msg = Marshal.PtrToStringAnsi(Invoke<IntPtr, _GetBVHBoundingBoxList>(mesh.GetUniqueId(), center, size));
#else
            string msg = Marshal.PtrToStringAnsi(_GetBVHBoundingBoxList(mesh.GetUniqueId(), ref center, ref size));
#endif

            return msg;
        }


        #region _LoadBVH
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _LoadBVH(int meshId, [In, Out] IntPtr[] bvhHandle);
        #endregion

        public bool LoadBVH(MeshFilter mesh, ref BVHHandle bvhHandle)
        {
            IntPtr[] handle = new IntPtr[1];
#if DDR_RUNTIME_DLL_LINKING_
        int err = Invoke<int, _LoadBVH>(mesh.GetUniqueId(), handle);
#else
            int err = _LoadBVH(mesh.GetUniqueId(), handle);
#endif
            bvhHandle = new BVHHandle(mesh, handle[0], this);
            if (err != 0)
            {
                string error = GetLastError();
                VertexBakerLib.LogError(error);
                return false;
            }
            return true;
        }

        #region _IsValidBVH
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    bool _IsValidBVH(int meshId, IntPtr matData);
        #endregion

        public bool IsValidBVH(MeshFilter mesh)
        {
            IntPtr matDataPtr = Alloc(16 * SIZE_FLOAT);
            float[] matArr = new float[16];
            int matIndex = 0;
            AssignMat4(ref matArr, mesh.transform.localToWorldMatrix, ref matIndex); // 64 bytes
            Marshal.Copy(matArr, 0, matDataPtr, 16);
#if DDR_RUNTIME_DLL_LINKING_
        bool ret = Invoke<bool, _IsValidBVH>(mesh.GetUniqueId(), matDataPtr);
#else
            bool ret = _IsValidBVH(mesh.GetUniqueId(), matDataPtr);
#endif
            Free(matDataPtr);

            return ret;
        }

        #region _IsValidBVHFromHandle
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    bool _IsValidBVHFromHandle(IntPtr bvhHandle, IntPtr matData);
        #endregion

        public bool IsValidBVH(Handle bvhHandle, Transform world)
        {
            IntPtr matDataPtr = Alloc(16 * SIZE_FLOAT);
            float[] matArr = new float[16];
            int matIndex = 0;
            AssignMat4(ref matArr, world.localToWorldMatrix, ref matIndex); // 64 bytes
            Marshal.Copy(matArr, 0, matDataPtr, 16);
#if DDR_RUNTIME_DLL_LINKING_
        bool ret = Invoke<bool, _IsValidBVHFromHandle>(bvhHandle.Ptr(), matDataPtr);
#else
            bool ret = _IsValidBVHFromHandle(bvhHandle.Ptr(), matDataPtr);
#endif
            Free(matDataPtr);

            return ret;
        }

        #region _LoadBVHScene
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _LoadBVHScene(IntPtr meshIds, int meshCount, [In, Out] IntPtr[] bvhSceneHandle);
        #endregion

        public bool LoadBVHScene(List<MeshFilter> meshes, out Handle bvhHandle)
        {
            // filter items
            meshes.RemoveAll(delegate (MeshFilter mf)
            {
                BakeFilter bf = mf.GetComponent<BakeFilter>();
                if (bf != null)
                {
                    return bf.m_bakeFilter == BakeFilter.Filter.ExcludeFromBake;
                }

                return false;
            });

            // find BVH to rebuild
            List<MeshFilter> rebuildList = new List<MeshFilter>();
            foreach (MeshFilter mf in meshes)
            {
                if (!IsValidBVH(mf))
                {
                    rebuildList.Add(mf);
                }
            }

            // build BVH for missing/out-of-date BVH's
            if (rebuildList.Count > 0)
            {
                BuildBVH(rebuildList.ToArray());
            }

            // build id list
            IntPtr meshIdsPtr = Alloc(meshes.Count * SIZE_INT);
            int[] ids = new int[meshes.Count];
            for (int m = 0; m < meshes.Count; ++m)
            {
                ids[m] = meshes[m].GetUniqueId();
            }
            CopyArray(meshIdsPtr, meshes.Count * SIZE_INT, ids, meshes.Count * SIZE_INT);

            IntPtr[] handle = new IntPtr[1];
#if DDR_RUNTIME_DLL_LINKING_
        int err = Invoke<int, _LoadBVHScene>(meshIdsPtr, meshes.Count, handle);
#else
            int err = _LoadBVHScene(meshIdsPtr, meshes.Count, handle);
#endif

            Free(meshIdsPtr);

            bvhHandle = new Handle(handle[0], this);
            if (err != 0)
            {
                string error = GetLastError();
                VertexBakerLib.LogError(error);
                return false;
            }

            return true;
        }

        #region _BVHToLineSegment
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _BVHToLineSegment(IntPtr bvhSceneHandle, float[] startPoint, float[] endPoint, [In, Out] IntPtr[] outCenterSize, [In, Out] int[] outCount);
        #endregion

        public void BVHToLineSegment(Handle bvhSceneHandle, Vector3 startPoint, Vector3 endPoint, List<Vector3> outCenters, List<Vector3> outSizes)
        {

            IntPtr[] outCenterSizePtr = new IntPtr[1];
            outCenterSizePtr[0] = IntPtr.Zero;
            int[] outCount = new int[1];
            outCount[0] = 0;
#if DDR_RUNTIME_DLL_LINKING_
        int err = Invoke<int, _BVHToLineSegment>(bvhSceneHandle.Ptr(), ToFloatArray(startPoint), ToFloatArray(endPoint), outCenterSizePtr, outCount);
#else
            int err = _BVHToLineSegment(bvhSceneHandle.Ptr(), ToFloatArray(startPoint), ToFloatArray(endPoint), outCenterSizePtr, outCount);
#endif
            if (err == 0)
            {
                int count = outCount[0];
                IntPtr dataPtr = outCenterSizePtr[0];

                if (outCount[0] > 0 && dataPtr != IntPtr.Zero)
                {
                    float[] rawData = new float[outCount[0]];
                    Marshal.Copy(dataPtr, rawData, 0, outCount[0]);
                    for (int i = 0; i < count; i += 6)
                    {
                        outCenters.Add(new Vector3(rawData[i], rawData[i + 1], rawData[i + 2]));
                        outSizes.Add(new Vector3(rawData[i + 3], rawData[i + 4], rawData[i + 5]));
                    }

                    Free(dataPtr);
                }
            }
            else
            {
                string error = GetLastError();
                VertexBakerLib.LogError(error);
            }

        }


        #region _RayToIndex
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _RayToIndex(IntPtr bvhSceneHandle, float[] startPoint, float[] endPoint, [In, Out] int[] outIndex);
        #endregion

        public int RayToIndex(Handle bvhSceneHandle, Vector3 startPoint, Vector3 endPoint)
        {

            int[] outIndex = new int[1];
            outIndex[0] = -1;
#if DDR_RUNTIME_DLL_LINKING_
		int err = Invoke<int, _RayToIndex>(bvhSceneHandle.Ptr(), ToFloatArray(startPoint), ToFloatArray(endPoint), outIndex);
#else
            int err = _RayToIndex(bvhSceneHandle.Ptr(), ToFloatArray(startPoint), ToFloatArray(endPoint), outIndex);
#endif
            if (err != 0)
            {
                string error = GetLastError();
                VertexBakerLib.LogError(error);
            }

            return outIndex[0];
        }

        #region _TessellateTriangles
#if DDR_RUNTIME_DLL_LINKING_
        private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
        bool _TessellateTriangles(IntPtr bvhhandle, IntPtr meshId, IntPtr vertexCount, IntPtr triangleCount, int elementCount, [In] BakeContext.VertexElement[] vertFormat
                                ,int[] targetFaces, int facesCount,  IntPtr mat, IntPtr meshData, IntPtr triangles, IntPtr bakeOptions
                                , [In] string[] guid, [In] string[] sourcePaths, IntPtr[] settingsFB, [In, Out] IntPtr[] meshBufferHandle
                                , [In, Out] IntPtr[] outVertData, [In, Out] int[] outVertCount, [In, Out] IntPtr[] outTriData, [In, Out] int[] outTriCount);
        #endregion

        public bool TessellateTriangles(BVHHandle bvhHandle, List<int> facesToTessellate)
        {

            Mesh sourceMesh = bvhHandle.SourceMeshData.sharedMesh;

            List<MeshFilter> meshes = new List<MeshFilter>() { bvhHandle.SourceMeshData };
            List<List<int>> _subMeshTriangles = new List<List<int>>() { facesToTessellate };

            IntPtr[] meshBufferData = new IntPtr[1];
            IntPtr[] outVertData = new IntPtr[1];
            int[] outVertCount = new int[1];
            IntPtr[] outTriData = new IntPtr[1];
            int[] outTriCount = new int[1];
            meshBufferData[0] = IntPtr.Zero;
            outVertData[0] = IntPtr.Zero;
            outTriData[0] = IntPtr.Zero;
            outVertCount[0] = 0;
            outTriCount[0] = 0;

            m_baker.InitBakeContext(meshes, null);
                
            BakeContext.IVertex vertexFormat = new BakeContext.DefaultVertex();
            BakeContext.BuildSceneContext(meshes.ToArray(), m_baker, vertexFormat);

#if DDR_RUNTIME_DLL_LINKING_
            bool success = Invoke<bool, _TessellateTriangles>(
                bvhHandle.Ptr()
                , m_baker.m_meshIdsPtr
                , m_baker.m_vertexCountsPtr
                , m_baker.m_triangleCountPtr
                , m_baker.m_vertexEementCount
                , m_baker.m_vertexDefinition
                , _subMeshTriangles[0].ToArray()
                , _subMeshTriangles[0].Count
                , m_baker.m_matDataPtr
                , m_baker.m_meshDataPtr
                , m_baker.m_triangleDataPtr
                , m_baker.m_bakeOptionsPtr
                , m_baker.m_guids
                , m_baker.m_sourcePaths
                , m_baker.m_settingsPtrs
                , meshBufferData
                , outVertData
                , outVertCount
                , outTriData
                , outTriCount);
#else
            bool success = _TessellateTriangles(
                bvhHandle.Ptr()
                , m_baker.m_meshIdsPtr
                , m_baker.m_vertexCountsPtr
                , m_baker.m_triangleCountPtr
                , m_baker.m_vertexEementCount
                , m_baker.m_vertexDefinition
                , _subMeshTriangles[0].ToArray()
                , _subMeshTriangles[0].Count
                , m_baker.m_matDataPtr
                , m_baker.m_meshDataPtr
                , m_baker.m_triangleDataPtr
                , m_baker.m_bakeOptionsPtr
                , m_baker.m_guids
                , m_baker.m_sourcePaths
                , m_baker.m_settingsPtrs
                , meshBufferData
                , outVertData
                , outVertCount
                , outTriData
                , outTriCount);
#endif
            if (!success)
            {
                string error = GetLastError();
                VertexBakerLib.LogError(error);
            }

            Mesh mesh = new Mesh();

            if (outVertCount[0] == 0 || outTriCount[0] == 0)
            {
                Debug.LogError("Tessellation output data, vert count " + outVertCount[0] + " tri count " + outTriCount[0]);
            }
            else
            {
                float[] vertexData = IntPtrToFloatArray(outVertData[0], outVertCount[0] * vertexFormat.TotalComponentCount);
                int[] triData = IntPtrToIntArray(outTriData[0], outTriCount[0]);

                // rebuild mesh

                List<Vector2> vec2List = new List<Vector2>(outVertCount[0]);
                List<Vector3> vec3List = new List<Vector3>(outVertCount[0]);
                List<Vector4> vec4List = new List<Vector4>(outVertCount[0]);
                List<Color> colorList = new List<Color>(outVertCount[0]);

                int indexStart = 0;
                for (int i = 0; i < m_baker.m_vertexEementCount; ++i)
                {
                    BakeContext.VertexElement el = m_baker.m_vertexDefinition[i];
                    int componentCount = el.ComponentCount;
                    int floatCount = outVertCount[0] * componentCount;

                    switch (el.GetElType)
                    {
                        case BakeContext.VertexElementType.kPosition:
                            CopyFloatArrToVectorList(vertexData, indexStart, floatCount, ref vec3List);
                            mesh.SetVertices(vec3List);
                            break;
                        case BakeContext.VertexElementType.kNormal:
                            CopyFloatArrToVectorList(vertexData, indexStart, floatCount, ref vec3List);
                            mesh.SetNormals(vec3List);
                            break;
                        case BakeContext.VertexElementType.kUV0:
                            CopyFloatArrToVectorList(vertexData, indexStart, floatCount, ref vec2List);
                            mesh.SetUVs(0, vec2List);
                            break;
                        case BakeContext.VertexElementType.kTangent:
                            CopyFloatArrToVectorList(vertexData, indexStart, floatCount, ref vec4List);
                            mesh.SetTangents(vec4List);
                            break;
                        case BakeContext.VertexElementType.kColor:
                            CopyFloatArrToVectorList(vertexData, indexStart, floatCount, ref colorList);
                            mesh.SetColors(colorList);
                            break;
                        case BakeContext.VertexElementType.kUV1:
                            CopyFloatArrToVectorList(vertexData, indexStart, floatCount, ref vec4List);
                            mesh.SetUVs(1, vec4List);
                            break;
                        case BakeContext.VertexElementType.kUV2:
                            CopyFloatArrToVectorList(vertexData, indexStart, floatCount, ref vec4List);
                            mesh.SetUVs(2, vec4List);
                            break;
                        case BakeContext.VertexElementType.kUV3:
                            CopyFloatArrToVectorList(vertexData, indexStart, floatCount, ref vec4List);
                            mesh.SetUVs(3, vec4List);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }


                    indexStart += floatCount;
                }

                mesh.triangles = triData;
            }


            FreeHandle(meshBufferData[0]);

            m_baker.FreeContext();

            AssetDatabase.CreateAsset(mesh, BakeData.DataPath + "/TessMesh.asset");
            AssetDatabase.SaveAssets();

            return success;
        }

        #region _LightBlockersForVertex
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    int _LightBlockersAtVertex(IntPtr bvhhandle, int vertIndex, float[] wPosition, float[] wNormal, float[] wLightPos, float[] wLightDir
            , float[] lightColor, float[] lightRIAT, [In, Out] IntPtr[] startPoints, [In, Out] IntPtr[] endPoints, [In, Out] int[] pointCount, [In, Out] IntPtr[] colPoints, [In, Out] int[] colCount);
        #endregion

        public void LightBlockersAtVertex(Handle bvhhandle, int vertIndex, Vector3 worldPos, Vector3 worldNorm, Vector3 worldLightPos, Vector3 worldLightDir,
            Vector3 lightColor, Vector4 lightRIAT, ref List<Vector3> outStartPoints, ref List<Vector3> outEndPoints, ref List<Vector3> outColPoints)
        {

            IntPtr[] startPoints = new IntPtr[1];
            IntPtr[] endPoints = new IntPtr[1];
            int[] pointCount = new int[1];
            startPoints[0] = IntPtr.Zero;
            endPoints[0] = IntPtr.Zero;
            pointCount[0] = 0;

            IntPtr[] colPoints = new IntPtr[1];
            int[] colCount = new int[1];
            colPoints[0] = IntPtr.Zero;
            colCount[0] = 0;

            float[] wPosArr = ToFloatArray(worldPos);
            float[] wNormArr = ToFloatArray(worldNorm);
            float[] wLightPosArr = ToFloatArray(worldLightPos);
            float[] wLightDirArr = ToFloatArray(worldLightDir);
            float[] lightColorArr = ToFloatArray(lightColor);
            float[] lightRIATArr = ToFloatArray(lightRIAT);

#if DDR_RUNTIME_DLL_LINKING_
            int err = Invoke<int, _LightBlockersAtVertex>(bvhhandle.Ptr(), vertIndex, wPosArr, wNormArr, wLightPosArr, wLightDirArr
                , lightColorArr, lightRIATArr, startPoints, endPoints, pointCount, colPoints, colCount);
#else
            int err = _LightBlockersAtVertex(bvhhandle.Ptr(), vertIndex, wPosArr, wNormArr, wLightPosArr, wLightDirArr
                , lightColorArr, lightRIATArr, startPoints, endPoints, pointCount, colPoints, colCount);
#endif
            if (err == 0)
            {
                CopyFloatPtrToVectorList(startPoints[0], pointCount[0], ref outStartPoints);
                CopyFloatPtrToVectorList(endPoints[0], pointCount[0], ref outEndPoints);
                CopyFloatPtrToVectorList(colPoints[0], colCount[0], ref outColPoints);
            }
            else
            {
                string error = GetLastError();
                VertexBakerLib.LogError(error);
            }

        }

        #region _OccludersAtVertex
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
        int _OccludersAtVertex(IntPtr bvhHandle, int vertex, Vector3 worldPos, Vector3 worldNormal, Vector3 bentWorldNormal, [In, Out] IntPtr[] testPoints, [In, Out] int[] pointsCount
        , [In, Out] IntPtr[] colPoints, [In, Out] int[] colCount, [In, Out] float[] accessability, [In, Out] Vector3[] m_accumulatedRayDir);
        #endregion

        public void OccludersAtVertex(Handle bvhhandle, int vertIndex, Vector3 worldPos, Vector3 worldNorm, Vector3 bentWorldNormal
            , ref List<Vector3> testPoints, ref List<Vector3> colPoints, ref float accessability, ref Vector3 accumulatedRayDir)
        {

            // dest points data
            IntPtr[] testPointsPtr = new IntPtr[1];
            int[] testPointCount = new int[1];
            testPointsPtr[0] = IntPtr.Zero;
            testPointCount[0] = 0;

            // col Points data
            IntPtr[] colPointsPtr = new IntPtr[1];
            int[] colCount = new int[1];
            colPointsPtr[0] = IntPtr.Zero;
            colCount[0] = 0;

            // data to hold accessability
            float[] accessabilityOut = new float[1] { 0f };

            // data to hold accumulated ray dir
            Vector3[] accumulatedRayDirOut = new Vector3[1] { Vector3.zero };

#if DDR_RUNTIME_DLL_LINKING_
            int err = Invoke<int, _OccludersAtVertex>(bvhhandle.Ptr(), vertIndex, worldPos, worldNorm, bentWorldNormal, testPointsPtr, testPointCount, colPointsPtr, colCount, accessabilityOut, accumulatedRayDirOut);
#else
            int err = _OccludersAtVertex(bvhhandle.Ptr(), vertIndex, worldPos, worldNorm, bentWorldNormal, testPointsPtr, testPointCount, colPointsPtr, colCount, accessabilityOut, accumulatedRayDirOut);
#endif
            if (err == 0)
            {
                CopyFloatPtrToVectorList(testPointsPtr[0], testPointCount[0], ref testPoints);
                CopyFloatPtrToVectorList(colPointsPtr[0], colCount[0], ref colPoints);
                accessability = accessabilityOut[0];
                accumulatedRayDir = accumulatedRayDirOut[0];
            }
            else
            {
                string error = GetLastError();
                VertexBakerLib.LogError(error);
            }

        }

        #region _SaveSettings
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    void _SaveSettings();
        #endregion
        public void SaveSettings()
        {
#if DDR_RUNTIME_DLL_LINKING_
        Invoke<_SaveSettings>();
#else
            _SaveSettings();
#endif
        }

        #region _LoadSettings
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    void _LoadSettings();
        #endregion
        public void LoadSettings()
        {
            BakeData.Instance().GetBakeSettings();

        }

        #region _WriteSettings
#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    void _WriteSettings(IntPtr settings);
        #endregion
        public void WriteSettings()
        {
            BakeData.Instance().SaveBakeSettings();
        }
        
        private static void CopyFloatPtrToVectorList(IntPtr floatPtr, int count, ref List<Vector3> outList)
        {
            if (count > 0 && floatPtr != IntPtr.Zero && ((count % 3) == 0))
            {
                float[] rawData = new float[count];
                Marshal.Copy(floatPtr, rawData, 0, count);
                for (int i = 0; i < count; i += 3)
                {
                    outList.Add(new Vector3(rawData[i], rawData[i + 1], rawData[i + 2]));
                }
            }
        }

        private static void CopyFloatArrToVectorList(float[] rawData, int indexStart, int count, ref List<Vector2> outArr)
        {
            const int componentCount = 2;
            CopyFloatArrToVectorList<Vector2>(componentCount, rawData, indexStart, count, ref outArr);

        }

        private static void CopyFloatArrToVectorList(float[] rawData, int indexStart, int count, ref List<Vector3> outArr)
        {
            const int componentCount = 3;
            CopyFloatArrToVectorList<Vector3>(componentCount, rawData, indexStart, count, ref outArr);

        }

        private static void CopyFloatArrToVectorList(float[] rawData, int indexStart, int count, ref List<Vector4> outArr)
        {
            const int componentCount = 4;
            CopyFloatArrToVectorList<Vector4>(componentCount, rawData, indexStart, count, ref outArr);
        }

        private static void CopyFloatArrToVectorList(float[] rawData, int indexStart, int count, ref List<Color> outArr)
        {
            const int componentCount = 4;
            CopyFloatArrToVectorList<Color>(componentCount, rawData, indexStart, count, ref outArr);
        }

        private static void CopyFloatArrToVectorList<VEC>(int componentCount, float[] rawData, int indexStart, int count, ref List<VEC> outArr)
        {
            outArr.Clear();

            if(count > 0 && (count % componentCount) == 0)
            {
                List<Vector2> v2arr = outArr as List<Vector2>;
                List<Vector3> v3arr = outArr as List<Vector3>;
                List<Vector4> v4arr = outArr as List<Vector4>;
                List<Color> colorList = outArr as List<Color>;

                if(componentCount == 2 && v2arr != null)
                {
                    for(int i = 0; i < count; i += componentCount)
                    {
                        v2arr.Add(new Vector2(rawData[i], rawData[i + 1]));
                    }
                }
                else if(componentCount == 3 && v3arr != null)
                {
                    for(int i = 0; i < count; i += componentCount)
                    {
                        v3arr.Add(new Vector3(rawData[i], rawData[i + 1], rawData[i + 2]));
                    }
                }
                else
                {
                    if(v4arr != null)
                    {
                        for(int i = 0; i < count; i += componentCount)
                        {
                            v4arr.Add(new Vector4(rawData[i], rawData[i + 1], rawData[i + 2], rawData[i + 3]));
                        }
                    }
                    else if(colorList != null)
                    {
                        for(int i = 0; i < count; i += componentCount)
                        {
                            colorList.Add(new Color(rawData[i], rawData[i + 1], rawData[i + 2], rawData[i + 3]));
                        }
                    }
                }
            }
        }

        private static float[] IntPtrToFloatArray(IntPtr floatPtr, int count)
        {
            if (count > 0 && floatPtr != IntPtr.Zero)
            {
                float[] rawData = new float[count];
                Marshal.Copy(floatPtr, rawData, 0, count);
                return rawData;
            }

            return null;
        }

        private static int[] IntPtrToIntArray(IntPtr intPtr, int count)
        {
            if (count > 0 && intPtr != IntPtr.Zero)
            {
                int[] rawData = new int[count];
                Marshal.Copy(intPtr, rawData, 0, count);
                return rawData;
            }

            return null;
        }

        public static void AssignMat4(ref float[] data, Matrix4x4 src, ref int index)
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

        static float[] ToFloatArray(Vector2 vec)
        {
            return new float[] {
            vec.x,
            vec.y,
        };
        }

        static float[] ToFloatArray(Vector3 vec)
        {
            return new float[] {
            vec.x,
            vec.y,
            vec.z,
        };
        }

        static float[] ToFloatArray(Vector4 vec)
        {
            return new float[] {
            vec.x,
            vec.y,
            vec.z,
            vec.w,
        };
        }

        #region testcalls
#if UNITY_EDITOR

        [StructLayout(LayoutKind.Sequential)]
        public struct FloatArr
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public float[] arr;
        }

        // prototypes the signature of the method (return type arg types and so on)
        private delegate IntPtr _TestCall();
        private delegate IntPtr _TestArrayOfInts([In, Out] int[] arr, int size);
        private delegate IntPtr _TestRefArrayOfInts(ref IntPtr arr, ref int size);

        public string TestCall()
        {
            // Template argument is the return type
            // name is the method name in native to call
            // delegate provides the method details (arguments and such)
            return Marshal.PtrToStringAnsi(Invoke<IntPtr, _TestCall>());
        }

        public string TestArrayOfInts(Matrix4x4 matrix)
        {
            int[] matrixArr = new int[16];
            for (int i = 0; i < 16; ++i)
            {
                matrixArr[i] = (int)matrix[i];
            }
            int size = matrixArr.Length;

            string msg = Marshal.PtrToStringAnsi(Invoke<IntPtr, _TestArrayOfInts>(matrixArr, size));

            for (int i = 0; i < 16; ++i)
            {
                VertexBakerLib.Log("val " + matrixArr[i]);
            }

            return msg;
        }


        public string NativeAllocWithManagedCopyTest()
        {

            int[] test = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            IntPtr destPtr = Alloc(Marshal.SizeOf(typeof(int)) * test.Length * 2);
            IntPtr offset = new IntPtr(destPtr.ToInt64() + 10 * 4);

            Marshal.Copy(test, 0, offset, test.Length);

            int[] result = new int[10];
            Marshal.Copy(offset, result, 0, 10);

            Free(destPtr);


            string msg = "(NativeAllocWithManagedCopyTest) input was ";
            foreach (int i in test)
            {
                msg += i + ", ";
            }
            msg += "copied buffer contains ";
            foreach (int i in result)
            {
                msg += i + ", ";
            }

            return msg;
        }

        public string NativeMemoryCopyTest()
        {

            int[] test = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            IntPtr testPtr = Marshal.UnsafeAddrOfPinnedArrayElement(test, 0);

            int totalSize = Marshal.SizeOf(typeof(int)) * test.Length;

            IntPtr tempPtr = Marshal.AllocCoTaskMem(totalSize * 2);

            // copy to second half
            IntPtr destPtr = new IntPtr(tempPtr.ToInt64() + 10 * 4);
            MemoryCopy(destPtr, totalSize * 2, testPtr, totalSize);

            int[] result = new int[10];
            Marshal.Copy(destPtr, result, 0, 10);

            string msg = "(NativeMemoryCopyTest) input was ";
            foreach (int i in test)
            {
                msg += i + ", ";
            }
            msg += "copied buffer contains ";
            foreach (int i in result)
            {
                msg += i + ", ";
            }

            Marshal.FreeHGlobal(destPtr);

            return msg;
        }

        public string TestRefArrayOfInts(Matrix4x4 matrix)
        {
            int[] matrixArr = new int[16];
            for (int i = 0; i < 16; ++i)
            {
                matrixArr[i] = (int)matrix[i];
            }
            int size = matrixArr.Length;
            IntPtr buffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(int)) * matrixArr.Length);
            Marshal.Copy(matrixArr, 0, buffer, matrixArr.Length);

            string msg = Marshal.PtrToStringAnsi(Invoke<IntPtr, _TestRefArrayOfInts>(buffer, size));

            Marshal.FreeHGlobal(buffer);

            return msg;

        }

#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    IntPtr _TestVector(ref Vector3 vec);

        public string TestUnityVector()
        {
            Vector3 vec = new Vector3(1f, 2f, 3f);

#if DDR_RUNTIME_DLL_LINKING_
        string msg = Marshal.PtrToStringAnsi(Invoke<IntPtr, _TestVector>(vec));
#else
            string msg = Marshal.PtrToStringAnsi(_TestVector(ref vec));
#endif

            return msg;
        }

#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    IntPtr _TestVectorArray([In, Out] Vector3[] vec, int size);

        public string TestUnityVectorArray()
        {
            Vector3[] vec = new Vector3[] {
            new Vector3(1f, 2f, 3f),
            new Vector3(4f, 5f, 6f),
        };
#if DDR_RUNTIME_DLL_LINKING_
        string msg = Marshal.PtrToStringAnsi(Invoke<IntPtr, _TestVectorArray>(vec, 2));
#else
            string msg = Marshal.PtrToStringAnsi(_TestVectorArray(vec, 2));
#endif
            return msg;
        }

#if DDR_RUNTIME_DLL_LINKING_
    private delegate
#else
        [DllImport(LIBNAME)]
        private static extern
#endif
    bool _TestFileSystem();

        public void TestFileSystem()
        {
#if DDR_RUNTIME_DLL_LINKING_
        bool pass = Invoke<bool, _TestFileSystem>();
#else
            bool pass = _TestFileSystem();
#endif

            if (!pass)
            {
                VertexBakerLib.LogError("TestFileSystem failed");
            }
            else
            {
                VertexBakerLib.Log("TestFileSystem pass");
            }

        }

#endif
        #endregion

    }
}
