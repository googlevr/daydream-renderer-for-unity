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
using FlatBuffers;
using System.Reflection;
using daydreamrenderer;
using System.Collections.Generic;
using System;
using UnityEngine.SceneManagement;
namespace daydreamrenderer
{
    public class DDRSettings : ScriptableObject
    {

        const int BAKESETTINGS_VER = 1;

        public int m_selectedSettings = 0;
        public List<BakeSettings> m_settingsList = new List<BakeSettings>();

        public static class Constants
        {
            public const string kRoot = "-=Root=-";
            public const string kGroup = "-=Group=-";
        }

        [System.Serializable]
        public class LightEntry
        {
            public int m_idInFile;
            public string m_group;
            public LightEntry(Light light, string group)
            {
                m_group = group;
                if(light != null)
                {
                    m_idInFile = light.GetLocalIDinFile();
                }
            }
        }

        public class LightEntryCompare : IEqualityComparer<LightEntry>
        {
            public int GetHashCode(LightEntry le)
            {
                if (le == null)
                {
                    return 0;
                }
                return le.GetHashCode();
            }

            public bool Equals(LightEntry LightEntryA, LightEntry LightEntryB)
            {
                if (ReferenceEquals(LightEntryA, LightEntryB))
                {
                    return true;
                }

                if (ReferenceEquals(LightEntryA, null) ||
                    ReferenceEquals(LightEntryB, null))
                {
                    return false;
                }

                return LightEntryA.m_idInFile == LightEntryB.m_idInFile && LightEntryA.m_group == LightEntryB.m_group;
            }
        }

        public BakeSettings DefaultSettings {
            get {
                if (m_settingsList.Count == 0)
                {
                    m_settingsList.Add(BakeSettings.CreateDefaultSetting());

                }
                return m_settingsList[0];
            }
            set {
                if (m_settingsList.Count == 0)
                {
                    m_settingsList.Add(BakeSettings.CreateDefaultSetting());
                }
                m_settingsList[0] = value;
            }
        }

        public BakeSettings SelectedBakeSet {
            get {
                if (m_settingsList.Count == 0)
                {
                    m_settingsList.Add(BakeSettings.CreateDefaultSetting());
                }

                m_selectedSettings = Math.Max(0, Math.Min(m_selectedSettings, m_settingsList.Count - 1));

                return m_settingsList[m_selectedSettings];
            }
            set {
                if (m_settingsList.Count == 0)
                {
                    m_settingsList.Add(BakeSettings.CreateDefaultSetting());
                }

                m_selectedSettings = Math.Max(0, Math.Min(m_selectedSettings, m_settingsList.Count - 1));

                m_settingsList[m_selectedSettings] = value;
            }
        }

        public int GetBakeSetIndex()
        {
            return m_selectedSettings;
        }

        public void SetBakeSetIndex(int index)
        {
            if (index < 0) index = 0;

            m_selectedSettings = index;
        }

        public void AddBakeSettings(BakeSettings settings)
        {
            m_settingsList.Add(settings);
        }

        public void RemoveBakeSetting(int index)
        {
            if (index >= 0 && index < m_settingsList.Count)
            {
                m_settingsList.RemoveAt(index);
            }
        }

        public static void GatherLightGroups(ref Dictionary<string, List<Light>> groups)
        {
            List<GameObject> roots = Utilities.GetAllRoots();
            List<Light> lights = new List<Light>();

            // Lights grouped by parent name
            groups = new Dictionary<string, List<Light>>();

            // get list of lights
            for (int i = 0; i < roots.Count; ++i)
            {
                lights.AddRange(roots[i].GetComponentsInChildren<Light>());
            }

            // group by parents
            for (int i = 0; i < lights.Count; ++i)
            {
                string parent = lights[i].transform.parent == null ? Constants.kRoot : lights[i].transform.parent.gameObject.GetPath();
                if (!groups.ContainsKey(parent))
                {
                    groups.Add(parent, new List<Light>());
                }
                groups[parent].Add(lights[i]);
            }
        }

        // settings struct that mirrors struct in native
        [System.Serializable]
        public class BakeSettings
        {
            public string m_settingsId;
            public List<LightEntry> m_lightList = new List<LightEntry>();
            public bool m_activeSet = true;
            public bool m_forceAllLights = false;

            public static class ColorCubeFaces
            {
                public static int PosX = 0;
                public static int NegX = 1;
                public static int PosY = 2;
                public static int NegY = 3;
                public static int PosZ = 4;
                public static int NegZ = 5;
            }

            public enum AmbientColorMode
            {
                kColor,
                kColorGradient,
                kColorCube,
            }

            public static class GradientColorIndex
            {
                public static int Ground = 0;
                public static int Equator = 1;
                public static int Sky = 2;
            }

            public BakeSettings() { }
            public BakeSettings(string id)
            {
                m_settingsId = id;
            }

            public static BakeSettings CreateDefaultSetting()
            {
                BakeSettings def = new BakeSettings("default");
                def.m_forceAllLights = true;

                return def;
            }

            public Color GetColorCubeFace(int face)
            {
                if (m_colorCube == null || m_colorCube.Length < 24)
                {
                    m_colorCube = new float[]{
                    // 24 floats
                    Color.gray.r, Color.gray.g, Color.gray.b, 1f,
                    Color.gray.r, Color.gray.g, Color.gray.b, 1f,
                    Color.gray.r, Color.gray.g, Color.gray.b, 1f,
                    Color.gray.r, Color.gray.g, Color.gray.b, 1f,
                    Color.gray.r, Color.gray.g, Color.gray.b, 1f,
                    Color.gray.r, Color.gray.g, Color.gray.b, 1f,
                };
                }
                int offset = face * 4;
                return new Color(m_colorCube[offset], m_colorCube[offset + 1], m_colorCube[offset + 2], m_colorCube[offset + 3]);
            }

            public Color GetColorGradient(int gradientIndex)
            {
                if (m_colorGradient == null || m_colorGradient.Length < 12)
                {
                    m_colorGradient = new float[]{
                    // 12 floats
                    Color.gray.r, Color.gray.g, Color.gray.b, 1f,
                    Color.gray.r, Color.gray.g, Color.gray.b, 1f,
                    Color.gray.r, Color.gray.g, Color.gray.b, 1f,
                };
                }
                int offset = gradientIndex * 4;
                return new Color(m_colorGradient[offset], m_colorGradient[offset + 1], m_colorGradient[offset + 2], m_colorGradient[offset + 3]);
            }

            public void SetColorCubeFace(int face, Color color)
            {
                int offset = face * 4;
                m_colorCube[offset] = color.r;
                m_colorCube[offset + 1] = color.g;
                m_colorCube[offset + 2] = color.b;
                m_colorCube[offset + 3] = color.a;
            }

            public void SetColorGradient(int gradientIndex, Color color)
            {
                int offset = gradientIndex * 4;
                m_colorGradient[offset] = color.r;
                m_colorGradient[offset + 1] = color.g;
                m_colorGradient[offset + 2] = color.b;
                m_colorGradient[offset + 3] = color.a;
            }

            public Color GetColorSolid()
            {
                if (m_colorSolid == null || m_colorSolid.Length < 4)
                {
                    m_colorSolid = new float[4]{
                    Color.gray.r, Color.gray.g, Color.gray.b, 1f,
                };
                }
                return new Color(m_colorSolid[0], m_colorSolid[1], m_colorSolid[2], m_colorSolid[3]);
            }

            public void SetColorSolid(Color color)
            {
                m_colorSolid[0] = color.r;
                m_colorSolid[1] = color.g;
                m_colorSolid[2] = color.b;
                m_colorSolid[3] = color.a;
            }

            public int m_version = BAKESETTINGS_VER;

            public bool m_bakeAllLightSets = false;

            public bool m_shadowsEnabled = true;

            public bool m_ambientOcclusion = true;

            public AmbientColorMode m_colorMode = AmbientColorMode.kColorGradient;

            public float[] m_colorCube =
            {
            // 24 floats
            Color.gray.r, Color.gray.g, Color.gray.b, 1f,
            Color.gray.r, Color.gray.g, Color.gray.b, 1f,
            Color.gray.r, Color.gray.g, Color.gray.b, 1f,
            Color.gray.r, Color.gray.g, Color.gray.b, 1f,
            Color.gray.r, Color.gray.g, Color.gray.b, 1f,
            Color.gray.r, Color.gray.g, Color.gray.b, 1f,
        };

            public float[] m_colorGradient =
            {
            // 12 floats
            Color.gray.r, Color.gray.g, Color.gray.b, 1f,
            Color.gray.r, Color.gray.g, Color.gray.b, 1f,
            Color.gray.r, Color.gray.g, Color.gray.b, 1f,
        };

            public float[] m_colorSolid =
            {
            Color.gray.r, Color.gray.g, Color.gray.b, 1f,
        };

            // the distance away from the object to start a ray from
            public float m_rayStartOffset = 0.1f;

            // number of samples testing for occlusion
            public int m_occluderSearchSamples = 64;

            // force AO calculations to use double sided geometry
            public bool m_aoForceDoubleSidedGeo = true;

            public float m_occluderStartOffset = 0.1f;

            // number of samples per light for each vertex
            public int m_lightSamples = 64;

            // the amount of bent normal (the average of nearby normals) 0 = vertex normal, 1 = bent normal
            public float m_normalBend = 0.165f;

            // the length of the test ray used to test the accessibility of a vertex
            public float m_occlusionRayLength = 0.5f;

            // this is the Min ambient level, it is the lowest V in the HSV color that represents ambient
            public float m_ambientMin = 0.1f;

            // this is the Max ambient level, it is the highest V in the HSV color that represents ambient
            public float m_ambientMax = 0.5f;

            // simulate energy conservation for diffuse lighting 0 is stand diffuse lighting, 1 is full light wrapping
            public float m_diffuseEnergyConservation = 0.0f;

            public float m_debugSlider = 0.0f;

            public byte[] ToFlatbuffer()
            {
                FlatBufferBuilder builder = new FlatBufferBuilder(1);

                var colorCubeFBS = fbs_BakeSettings.CreateColorCubeVector(builder, m_colorCube);
                if (m_colorGradient == null)
                {
                    m_colorGradient = new float[12]
                    {
                    0.5f, 0.5f, 0.5f, 0.5f,
                    0.5f, 0.5f, 0.5f, 0.5f,
                    0.5f, 0.5f, 0.5f, 0.5f,
                    };
                }
                var colorGradientFBS = fbs_BakeSettings.CreateColorGradientVector(builder, m_colorGradient);

                if (m_colorSolid == null)
                {
                    m_colorSolid = new float[4]
                    {
                    0.5f, 0.5f, 0.5f, 0.5f,
                    };
                }
                var colorSolidFBS = fbs_BakeSettings.CreateColorSolidVector(builder, m_colorSolid);

                fbs_BakeSettings.Startfbs_BakeSettings(builder);

                // shadow settings
                fbs_BakeSettings.AddShadowsEnabled(builder, m_shadowsEnabled);
                fbs_BakeSettings.AddRayStartOffset(builder, m_rayStartOffset);
                fbs_BakeSettings.AddLightBlockerSamples(builder, m_lightSamples);

                // ambient occlusion settings
                fbs_BakeSettings.AddAmbientOcclusion(builder, m_ambientOcclusion);
                fbs_BakeSettings.AddNormalBend(builder, m_normalBend);
                fbs_BakeSettings.AddOccluderSearchSamples(builder, m_occluderSearchSamples);
                fbs_BakeSettings.AddOccluderRayLength(builder, m_occlusionRayLength);
                fbs_BakeSettings.AddOccluderStartOffset(builder, m_occluderStartOffset);
                fbs_BakeSettings.AddAoForceDoubleSidedGeo(builder, m_aoForceDoubleSidedGeo);

                // ambient
                fbs_BakeSettings.AddColorCube(builder, colorCubeFBS);
                fbs_BakeSettings.AddColorGradient(builder, colorGradientFBS);
                fbs_BakeSettings.AddColorSolid(builder, colorSolidFBS);
                fbs_BakeSettings.AddAmbientMin(builder, m_ambientMin);
                fbs_BakeSettings.AddAmbientMax(builder, m_ambientMax);
                fbs_BakeSettings.AddColorMode(builder, (int)m_colorMode);

                var settings = fbs_BakeSettings.Endfbs_BakeSettings(builder);
                builder.Finish(settings.Value);

                return builder.SizedByteArray();
            }

            public void FromFlatbuffer(byte[] data)
            {
                ByteBuffer bb = new ByteBuffer(data);
                fbs_BakeSettings settings = fbs_BakeSettings.GetRootAsfbs_BakeSettings(bb);

                m_version = settings.Version;

                // shadow settings
                m_shadowsEnabled = settings.ShadowsEnabled;
                m_rayStartOffset = settings.RayStartOffset;
                m_lightSamples = settings.LightBlockerSamples;

                // ambient occlusion
                m_ambientOcclusion = settings.AmbientOcclusion;
                m_normalBend = settings.NormalBend;
                m_occlusionRayLength = settings.OccluderRayLength;
                m_occluderStartOffset = settings.OccluderStartOffset;
                m_occluderSearchSamples = settings.OccluderSearchSamples;

                if (m_version > 0)
                {
                    m_aoForceDoubleSidedGeo = settings.AoForceDoubleSidedGeo;
                }

                // ambient
                m_colorCube = new float[settings.ColorCubeLength];
                for (int i = 0; i < settings.ColorCubeLength; i++)
                {
                    m_colorCube[i] = settings.GetColorCube(i);
                }
                m_colorGradient = new float[settings.ColorGradientLength];
                for (int i = 0; i < settings.ColorGradientLength; i++)
                {
                    m_colorGradient[i] = settings.GetColorGradient(i);
                }
                m_colorSolid = new float[settings.ColorSolidLength];
                for (int i = 0; i < settings.ColorSolidLength; i++)
                {
                    m_colorSolid[i] = settings.GetColorSolid(i);
                }
                m_ambientMin = settings.AmbientMin;
                m_ambientMax = settings.AmbientMax;
                m_colorMode = (AmbientColorMode)settings.ColorMode;
            }


            public void CopySettings(BakeSettings rhs)
            {
                FieldInfo[] fis = this.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                for (int i = 0; i < fis.Length; ++i)
                {
                    if (fis[i].FieldType == typeof(float[]))
                    {
                        float[] source = fis[i].GetValue(rhs) as float[];
                        float[] copy = new float[source.Length];
                        for (int j = 0; j < source.Length; ++j)
                        {
                            copy[j] = source[j];
                        }

                        fis[i].SetValue(this, copy);
                    }
                    else
                    {
                        fis[i].SetValue(this, fis[i].GetValue(rhs));
                    }
                }
            }

            public void CopyAmbient(BakeSettings rhs)
            {
                m_ambientMin = rhs.m_ambientMin;
                m_ambientMax = rhs.m_ambientMax;
                m_colorCube = new float[24];
                for (int i = 0; i < 24; ++i)
                {
                    m_colorCube[i] = rhs.m_colorCube[i];
                }
                m_colorGradient = new float[12];
                for (int i = 0; i < 12; ++i)
                {
                    m_colorGradient[i] = rhs.m_colorGradient[i];
                }
                m_colorSolid = new float[4];
                for (int i = 0; i < 4; ++i)
                {
                    m_colorSolid[i] = rhs.m_colorSolid[i];
                }
                m_colorMode = rhs.m_colorMode;
            }
        }

    }

}