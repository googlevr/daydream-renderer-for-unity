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
using System.Runtime.InteropServices;
using System.Collections.Generic;

[StructLayout(LayoutKind.Sequential, Size = 16)]
public struct SOAVertex
{
    public Vector3[] vertices;
    public Vector3[] normals;
    public Vector4[] tangents;
    public SOAVertex(int size) {
        vertices = new Vector3[size];
        normals = new Vector3[size];
        tangents = new Vector4[size];
    }
}

[StructLayout(LayoutKind.Sequential, Size=16)]
public struct LightSOA {
    public Vector4[] pos_range;
    public Vector4[] dir_inten;
    public Vector4[] spot_angle;
    public Vector4[] color_types;

    public LightSOA(List<Light> lights) {
        int size = lights.Count;
        pos_range = new Vector4[size];
        dir_inten = new Vector4[size];
        spot_angle = new Vector4[size];
        color_types = new Vector4[size];
        for (int i = 0; i < size; ++i) {
            pos_range[i] = lights[i].transform.position.ToVector4(lights[i].range);
            dir_inten[i] = lights[i].transform.forward.ToVector4(lights[i].intensity);
            spot_angle[i] = new Vector4(0f, 0f, 0f, lights[i].spotAngle);
            color_types[i] = lights[i].color.ToVector4((int)lights[i].type);
        }
    }
}


