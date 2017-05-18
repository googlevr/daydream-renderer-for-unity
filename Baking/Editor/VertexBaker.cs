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
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.InteropServices;
using System.IO;
using System;

public class VertexBaker : Editor {

    static List<Vector3>[] m_basisValues = new List<Vector3>[3];
    
    static Vector3 m_cBasis0 = new Vector3(-0.40824829046386301636621401245098f, -0.70710678118654752440084436210485f, 0.57735026918962576450914878050195f);
    static Vector3 m_cBasis1 = new Vector3(-0.40824829046386301636621401245098f, 0.70710678118654752440084436210485f, 0.57735026918962576450914878050195f);
    static Vector3 m_cBasis2 = new Vector3(0.81649658092772603273242802490196f, 0.0f, 0.57735026918962576450914878050195f);

    [MenuItem("GameObject/Light/Vertex Bake", false, 0)]
   
    public static void BakeLights() {

        DateTime bakeStart = DateTime.Now;

        // current selection
        GameObject[] selection = Selection.gameObjects;

        List<MeshFilter> meshes = new List<MeshFilter>();
        // gather meshes in selection
        foreach (GameObject go in selection) {
            meshes.AddRange(go.GetComponentsInChildren<MeshFilter>());
        }
        if (meshes.Count == 0) {
            EditorUtility.DisplayDialog("Error", "No meshes in selection", "ok");
            return;
        }

        // mesh renderers allow access to additional mesh data
        List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
        // gather meshes in selection build 1 to 1 list of mesh filters to mesh renderers
        foreach (MeshFilter filter in meshes) {
            MeshRenderer mr = filter.GetComponent<MeshRenderer>();
            if (mr != null) {
                meshRenderers.Add(mr);
            }
        }

        if (meshes.Count != meshRenderers.Count) {
            EditorUtility.DisplayDialog("Error", "MeshRenderers are not 1 to 1 with Mesh Filters", "ok");
            return;
        }

        List<Light> lights = new List<Light>();
        // Gather lights
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject go in roots) {
            if (go.activeSelf) {
                lights.AddRange(go.GetComponentsInChildren<Light>());
            }
        }
        
        int workerThreadCount = Math.Max(7, Environment.ProcessorCount-1);

        ThreadPool.SetMaxThreads(workerThreadCount, workerThreadCount);
        ThreadPool.SetMinThreads(1, 1);

        // create new meshes with lights baked in
        for (int i = 0, count = meshes.Count; i < count; ++i) {
            
            Matrix4x4 worldM = meshes[i].gameObject.transform.localToWorldMatrix;

            // create new mesh to hold color data
            for (int c = 0; c < 3; ++c) {
                m_basisValues[c] = new List<Vector3>();
            }


            ManualResetEvent[] waitHandles = new ManualResetEvent[workerThreadCount];
            for(int e = 0; e < workerThreadCount; ++e) {
                waitHandles[e] = new ManualResetEvent(false);
            }
            
            int itemsPerThread = meshes[i].sharedMesh.vertexCount / workerThreadCount;
            int remainder = meshes[i].sharedMesh.vertexCount % workerThreadCount;

            // store basis results here then combine into final result
            List<List<Vector3>[]> tempBasisValues = new List<List<Vector3>[]>();

            for (int t = 0; t < workerThreadCount; ++ t) {

                tempBasisValues.Add(new List<Vector3>[3]);

                for (int c = 0; c < 3; ++c) {
                    tempBasisValues[t][c] = new List<Vector3>();
                }
                
                // create cache friendly lists of data
                SOAVertex verts = new SOAVertex(meshes[i].sharedMesh.vertices.Length);
                verts.vertices = meshes[i].sharedMesh.vertices;
                verts.normals = meshes[i].sharedMesh.normals;
                verts.tangents = meshes[i].sharedMesh.tangents;

                LightSOA lightsSoa = new LightSOA(lights);

                object context = new object[]{
                    waitHandles[t],
                    verts,
                    worldM,
                    lightsSoa,
                    tempBasisValues[t],         // output list
                    itemsPerThread,             // number of verts to process
                    itemsPerThread*t,           // vert offset
                    remainder,                  // last thread will process remainder
                    t == (workerThreadCount-1), // is last
                    t                           // id
                };

                ThreadPool.QueueUserWorkItem(ProcessVertex, context);
                //ProcessVertex(context);

            }

            // wait for jobs to finish
            WaitHandle.WaitAll(waitHandles);

            // collect basis value results
            for (int t = 0; t < workerThreadCount; ++t) {
                m_basisValues[0].AddRange(tempBasisValues[t][0]);
                m_basisValues[1].AddRange(tempBasisValues[t][1]);
                m_basisValues[2].AddRange(tempBasisValues[t][2]);
            }

            const int uvOffset = 1;
            // if vertices is not set than we cannot set UVS
            // these vertices seem to be used then in place of the primary meshes vertices
            Mesh colorData = new Mesh();
            colorData.vertices = meshes[i].sharedMesh.vertices;
            colorData.SetUVs(uvOffset + 0, m_basisValues[0]);
            colorData.SetUVs(uvOffset + 1, m_basisValues[1]);
            colorData.SetUVs(uvOffset + 2, m_basisValues[2]);
            colorData.UploadMeshData(true);


            string assetPath = "Assets/BakedLighting";

            if(!Directory.Exists(assetPath)) {
                Directory.CreateDirectory(assetPath);
            }

            AssetDatabase.CreateAsset(colorData, assetPath + "/" + meshRenderers[i].GetInstanceID() + ".asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            colorData = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath + "/" + meshRenderers[i].GetInstanceID() + ".asset");

            MeshDataContainer meshDataContainer = meshRenderers[i].GetComponent<MeshDataContainer>();
            
            if (meshDataContainer == null) {
                meshDataContainer = meshRenderers[i].gameObject.AddComponent<MeshDataContainer>();
            }
            
            meshDataContainer.m_mesh = colorData;
            
            meshRenderers[i].additionalVertexStreams = colorData;

            EditorUtility.SetDirty(meshDataContainer.m_mesh);
            EditorUtility.SetDirty(meshDataContainer);
            EditorUtility.SetDirty(meshRenderers[i].gameObject);
            AssetDatabase.SaveAssets();
        }

        Debug.Log("Bake time: " + (DateTime.Now - bakeStart).TotalSeconds + " seconds");
    }

    private static void ProcessVertex(object context) {

        object[] data = context as object[];
        int d = 0;
        ManualResetEvent waitHandle = data[d++] as ManualResetEvent;
        SOAVertex verts = (SOAVertex)data[d++];
        Matrix4x4 worldM = (Matrix4x4)data[d++];
        LightSOA lights = (LightSOA)data[d++];
        List<Vector3>[] basisValues = data[d++] as List<Vector3>[];
        int itemsPerThread = (int)data[d++];
        int offset = (int)data[d++];
        int remainder = (int)data[d++];
        bool isLast = (bool)data[d++];
        
        try {
            

            int vertCount = itemsPerThread;
            if (isLast) {
                vertCount += remainder;
            }


            int vertCount4 = 4*(vertCount / 4);

            // per vertex calculations
            for (int j = offset, k = offset + vertCount4; j < k; j+=4) {
                
                Vector3 worldPos0 = worldM * (verts.vertices[j].ToVector4(1f));  
                Vector3 worldPos1 = worldM * (verts.vertices[j+1].ToVector4(1f));
                Vector3 worldPos2 = worldM * (verts.vertices[j+2].ToVector4(1f));
                Vector3 worldPos3 = worldM * (verts.vertices[j+3].ToVector4(1f));

                Vector3 tangent0 = worldM * verts.tangents[j+0];
                Vector3 tangent1 = worldM * verts.tangents[j+1];
                Vector3 tangent2 = worldM * verts.tangents[j+2];
                Vector3 tangent3 = worldM * verts.tangents[j+3];

                Vector3 normal0 = worldM * verts.normals[j+0];
                Vector3 normal1 = worldM * verts.normals[j+1];
                Vector3 normal2 = worldM * verts.normals[j+2];
                Vector3 normal3 = worldM * verts.normals[j+3];

                tangent0.Normalize();
                tangent1.Normalize();
                tangent2.Normalize();
                tangent3.Normalize();

                normal0.Normalize();
                normal1.Normalize();
                normal2.Normalize();
                normal3.Normalize();

                Vector3 binormal0 = Vector3.Cross(tangent0, normal0) * verts.tangents[j + 0].w;
                Vector3 binormal1 = Vector3.Cross(tangent1, normal1) * verts.tangents[j + 1].w;
                Vector3 binormal2 = Vector3.Cross(tangent2, normal2) * verts.tangents[j + 2].w;
                Vector3 binormal3 = Vector3.Cross(tangent3, normal3) * verts.tangents[j + 3].w;
                
                Matrix4x4 worldToTangentM0 = new Matrix4x4 {
                    m00 = tangent0.x, m10 = binormal0.x, m20 = normal0.x, m30 = 0f,
                    m01 = tangent0.y, m11 = binormal0.y, m21 = normal0.y, m31 = 0f,
                    m02 = tangent0.z, m12 = binormal0.z, m22 = normal0.z, m32 = 0f,
                    m03 = 0f, m13 = 0f, m23 = 0f, m33 = 0f,
                };
                Matrix4x4 worldToTangentM1 = new Matrix4x4 {
                    m00 = tangent1.x, m10 = binormal1.x, m20 = normal1.x, m30 = 0f,
                    m01 = tangent1.y, m11 = binormal1.y, m21 = normal1.y, m31 = 0f,
                    m02 = tangent1.z, m12 = binormal1.z, m22 = normal1.z, m32 = 0f,
                    m03 = 0f, m13 = 0f, m23 = 0f, m33 = 0f,
                };
                Matrix4x4 worldToTangentM2 = new Matrix4x4 {
                    m00 = tangent2.x, m10 = binormal2.x, m20 = normal2.x, m30 = 0f,
                    m01 = tangent2.y, m11 = binormal2.y, m21 = normal2.y, m31 = 0f,
                    m02 = tangent2.z, m12 = binormal2.z, m22 = normal2.z, m32 = 0f,
                    m03 = 0f, m13 = 0f, m23 = 0f, m33 = 0f,
                };
                Matrix4x4 worldToTangentM3 = new Matrix4x4 {
                    m00 = tangent3.x, m10 = binormal3.x, m20 = normal3.x, m30 = 0f,
                    m01 = tangent3.y, m11 = binormal3.y, m21 = normal3.y, m31 = 0f,
                    m02 = tangent3.z, m12 = binormal3.z, m22 = normal3.z, m32 = 0f,
                    m03 = 0f, m13 = 0f, m23 = 0f, m33 = 0f,
                };

                // calculate light basis contributions

                // 0
                Vector3 basis0_0 = Vector3.zero;
                Vector3 basis1_0 = Vector3.zero;
                Vector3 basis2_0 = Vector3.zero;

                Vector3 basis0_1 = Vector3.zero;
                Vector3 basis1_1 = Vector3.zero;
                Vector3 basis2_1 = Vector3.zero;

                Vector3 basis0_2 = Vector3.zero;
                Vector3 basis1_2 = Vector3.zero;
                Vector3 basis2_2 = Vector3.zero;

                Vector3 basis0_3 = Vector3.zero;
                Vector3 basis1_3 = Vector3.zero;
                Vector3 basis2_3 = Vector3.zero;

                /// 0
                for (int l = 0, lk = lights.pos_range.Length; l < lk; ++l) {
                    Vector3 lightPos = lights.pos_range[l];
                    Vector3 lightDir = lights.dir_inten[l];
                    float lightRange = lights.pos_range[l].w;
                    float lightInten = lights.dir_inten[l].w;
                    float spotAngle = lights.spot_angle[l].w;
                    Vector3 lightColor = lights.color_types[l];
                    LightType lightType = (LightType)lights.color_types[l].w;
                    ComputeLightBasis(worldPos0, normal0, worldToTangentM0, lightPos, lightDir, lightColor, lightRange, lightInten, spotAngle, lightType, ref basis0_0, ref basis1_0, ref basis2_0);
                    ComputeLightBasis(worldPos1, normal1, worldToTangentM1, lightPos, lightDir, lightColor, lightRange, lightInten, spotAngle, lightType, ref basis0_1, ref basis1_1, ref basis2_1);
                    ComputeLightBasis(worldPos2, normal2, worldToTangentM2, lightPos, lightDir, lightColor, lightRange, lightInten, spotAngle, lightType, ref basis0_2, ref basis1_2, ref basis2_2);
                    ComputeLightBasis(worldPos3, normal3, worldToTangentM3, lightPos, lightDir, lightColor, lightRange, lightInten, spotAngle, lightType, ref basis0_3, ref basis1_3, ref basis2_3);
                }
                
                basisValues[0].Add(basis0_0);
                basisValues[1].Add(basis1_0);
                basisValues[2].Add(basis2_0);

                basisValues[0].Add(basis0_1);
                basisValues[1].Add(basis1_1);
                basisValues[2].Add(basis2_1);

                basisValues[0].Add(basis0_2);
                basisValues[1].Add(basis1_2);
                basisValues[2].Add(basis2_2);

                basisValues[0].Add(basis0_3);
                basisValues[1].Add(basis1_3);
                basisValues[2].Add(basis2_3);

                Thread.Sleep(1);
            }

           
            // remainder
            int remainderLoopCount = vertCount % 4;
            for (int j = vertCount4 + offset, k = vertCount4 + offset + remainderLoopCount; j < k; ++j) {
                
                Vector3 worldPos0 = worldM * (verts.vertices[j].ToVector4(1f));
                Vector3 tangent0 = worldM * verts.tangents[j];
                Vector3 normal0 = worldM * verts.normals[j];

                tangent0.Normalize();
                normal0.Normalize();

                Vector3 binormal0 = Vector3.Cross(tangent0, normal0) * verts.tangents[j].w;
                
                Matrix4x4 worldToTangentM0 = new Matrix4x4 {
                    m00 = tangent0.x, m10 = binormal0.x, m20 = normal0.x, m30 = 0f,
                    m01 = tangent0.y, m11 = binormal0.y, m21 = normal0.y, m31 = 0f,
                    m02 = tangent0.z, m12 = binormal0.z, m22 = normal0.z, m32 = 0f,
                    m03 = 0f, m13 = 0f, m23 = 0f, m33 = 0f,
                };
                

                // calculate light basis contributions

                // 0
                Vector3 basis0_0 = Vector3.zero;
                Vector3 basis1_0 = Vector3.zero;
                Vector3 basis2_0 = Vector3.zero;
                /// 0
                for (int l = 0, lk = lights.pos_range.Length; l < lk; ++l) {
                    Vector3 lightPos = lights.pos_range[l];
                    Vector3 lightDir = lights.dir_inten[l];
                    float lightRange = lights.pos_range[l].w;
                    float lightInten = lights.dir_inten[l].w;
                    float spotAngle = lights.spot_angle[l].w;
                    Vector3 lightColor = lights.color_types[l];
                    LightType lightType = (LightType)lights.color_types[l].w;
                    ComputeLightBasis(worldPos0, normal0, worldToTangentM0, lightPos, lightDir, lightColor, lightRange, lightInten, spotAngle, lightType, ref basis0_0, ref basis1_0, ref basis2_0);
                }
                basisValues[0].Add(basis0_0);
                basisValues[1].Add(basis1_0);
                basisValues[2].Add(basis2_0);
            }
            
            
        } finally {

            // release the wait on this handle
            waitHandle.Set();
        }
    }

    private static void ComputeLightBasis(Vector3 worldPos, Vector3 normal, Matrix4x4 worldToTangentM, Vector3 lightPos, Vector3 lightDir, Vector3 lightColor, float lightRange, float lightInten, float cutoffAngle, LightType lightType, ref Vector3 basis0, ref Vector3 basis1, ref Vector3 basis2) {
           
        // point light attenuation
        float atten = 0f;
        Vector3 finalDirection = Vector3.up;
        
        float kl = 25f / lightRange*0.5f;
        float kq = 25f / lightRange*0.5f;
        
        bool supported = true;
        if (lightType == LightType.Point || lightType == LightType.Area) {

            finalDirection = (lightPos - worldPos);
            finalDirection = worldToTangentM * (finalDirection.ToVector4(0f));

            if (lightType == LightType.Point) {
                atten += kq * finalDirection.sqrMagnitude;
            } else if (lightType == LightType.Area) {
                atten += kl * finalDirection.magnitude;
            }

            atten = 1f / (1f + atten);

        } else if (lightType == LightType.Directional) {
            atten = lightInten;
            finalDirection = worldToTangentM*lightDir;
            finalDirection *= -1f;
        } else if (lightType == LightType.Spot) {
            Vector3 toSpot = (1f / (lightRange)) * (lightPos - worldPos);
            Vector3 spotDir = lightDir * -1f;

            float dist = toSpot.sqrMagnitude;
            finalDirection = worldToTangentM * toSpot;

            toSpot.Normalize();
            spotDir.Normalize();
            
            float coneAngle = Mathf.Cos(cutoffAngle * Mathf.Deg2Rad);
            float spotAngle = Mathf.Max(0, Vector3.Dot(toSpot, spotDir));

            // to keep the smooth fall off needed by the basis lighting
            // take a harsh cone and a soft cone and blending them to keep falloff continuous looking
            float minFallOff = Mathf.Pow(spotAngle, 2f);
            float maxFallOff = Mathf.Pow(spotAngle, 32f);
            spotAngle = Mathf.Lerp(minFallOff, maxFallOff, (coneAngle / (spotAngle)));
            atten = spotAngle / (1f + kq * dist);

        } else {
            supported = false;
        }

        if (supported) {
            basis0 += atten * lightColor * Mathf.Max(0f, Vector3.Dot(m_cBasis0, finalDirection.normalized));
            basis1 += atten * lightColor * Mathf.Max(0f, Vector3.Dot(m_cBasis1, finalDirection.normalized));
            basis2 += atten * lightColor * Mathf.Max(0f, Vector3.Dot(m_cBasis2, finalDirection.normalized));
        }
    }
}
