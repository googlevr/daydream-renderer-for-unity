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
using System.Reflection;

public static class TypeExtensions {

    public static Vector3 ToVector3(this Color color) {
        return new Vector3(color.r, color.g, color.b);
    }

    public static Vector4 ToVector4(this Color color) {
        return new Vector4(color.r, color.g, color.b, color.a);
    }

    public static Vector4 ToVector4(this Color color, float w) {
        return new Vector4(color.r, color.g, color.b, w);
    }

    public static Vector4 ToVector4(this Vector3 v, float w = 0f) {
        return new Vector4(v.x, v.y, v.z, w);
    }

    //public static Vector4 ToVector4 (this Vector2 v, float z = 0f, float w = 0f) {
    //    return new Vector4(v.x, v.y, z, w);
    //}

    public static Vector4 ToVector4(this float v, float y = 0f, float z = 0f, float w = 0f) {
        return new Vector4(v, y, z, w);
    }

    public static Vector3 ToVector3(this Vector4 v) {
        return new Vector3(v.x, v.y, v.z);
    }

    public static Vector3 ToVector3(this Vector2 v, float z = 0f) {
        return new Vector3(v.x, v.y, z);
    }

    public static Vector3 ToVector3(this float v, float y = 0f, float z = 0f) {
        return new Vector3(v, y, z);
    }

    public static Vector2 ToVector2(this Vector3 v) {
        return new Vector2(v.x, v.y);
    }

    public static Vector2 ToVector2(this Vector4 v) {
        return new Vector2(v.x, v.y);
    }

    public static Vector2 ToVector2(this float v, float y = 0f) {
        return new Vector2(v, y);
    }
}
