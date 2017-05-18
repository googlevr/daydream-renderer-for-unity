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


[ExecuteInEditMode]
[RequireComponent(typeof(MeshRenderer))]
public class MeshDataContainer : MonoBehaviour
{
    public Mesh m_mesh;
    public MeshRenderer m_mr;

    public void Awake()
    {
    }

    public void Start()
    {
        LoadLightingMesh();
    }

    public void Update()
    {
        if (!Application.isPlaying)
        {
            m_mr = GetComponent<MeshRenderer>();
            if (m_mr.additionalVertexStreams == null)
            {
                LoadLightingMesh();
            }
        }
    }

    public void LoadLightingMesh()
    {

        Debug.Assert(m_mesh != null, name + " missing baked lighting mesh");
        if (m_mr == null)
        {
            m_mr = GetComponent<MeshRenderer>();
        }
        m_mesh.UploadMeshData(true);
        m_mr.additionalVertexStreams = m_mesh;
    }
}
