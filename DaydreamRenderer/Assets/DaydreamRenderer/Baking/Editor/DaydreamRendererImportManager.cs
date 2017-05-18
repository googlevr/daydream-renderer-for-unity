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
using UnityEditorInternal;
using UnityEditor.SceneManagement;

namespace daydreamrenderer
{
    using MatEntry = DaydreamRendererMaterialHistory.Entry;

    class DaydreamRendererImportManager : EditorWindow
    {
        public static List<string> m_projectAssetAddDaydream = new List<string>();

        static Vector2 m_scrollPosConverted;
        static Vector2 m_scrollPosSplit;

        const string m_assetPathBackup = BakeData.kDaydreamPath + "Backup";
        const string m_assetPathSplit = BakeData.kDaydreamPath + "Materials/DaydreamStatic";

        static DaydreamRendererMaterialHistory s_staticMaterialHistory;
        static DaydreamRendererMaterialHistory s_dynamicMaterialHistory;
        static SerializedObject s_listContainerStaticMats;
        static SerializedObject s_listContainerDynamicMats;
        static SerializedProperty s_list;
        static int s_importType = 0;

        const string kDaydreamShader = "Daydream/Standard";
        static private bool s_gatherMetrics = true;
        static List<MaterialInfo> m_convertableMaterials = null;
        static int m_staticConvertableCount = 0;
        static int m_dynamicConvertableCount = 0;
        static int m_convertedCount = 0;
        static List<DaydreamRendererMaterialHistory.Entry> m_dynamRemoveList = new List<DaydreamRendererMaterialHistory.Entry>();
        static List<DaydreamRendererMaterialHistory.Entry> m_removeList = new List<DaydreamRendererMaterialHistory.Entry>();
        static List<DaydreamRendererMaterialHistory.Entry> m_removeSplitList = new List<DaydreamRendererMaterialHistory.Entry>();

        static class Styles
        {
            static public GUIStyle s_defaultLabel;

            public const int kSectionSpacing = 35;

            public static GUIContent importStaticLightingLabel = new GUIContent("Import daydream static lighting");
            public static GUIContent importStaticLightingButton = new GUIContent("Convert");
            public static GUIContent addComponents = new GUIContent("Add to Scene", "Adds Daydream lighting system components to scene objects");
            public static GUIContent addComponentsToProject = new GUIContent("Add to Project Assets", "Adds Daydream lighting components to models and prefabs in the project (assets must have daydream shaders)");
            public static GUIContent removeComponents = new GUIContent("Remove from Scene", "Removes all Daydream lighting system components from the scene objects");
            public static GUIContent removeComponentsFromProject = new GUIContent("Remove from Project Assets", "Removes all Daydream lighting system components from models and prefabs in the project");
            public static GUIContent toggleLightingSystem = new GUIContent("Enable Daydream Lighting System", "Daydream replaces the lighting system");
            public static GUIContent toggleComponents = new GUIContent("Auto add Daydream lighting components in Scene", "Lighting system components will be auto added to the scene (applies only to edit-time)");
            public const string kStaticConvertedMaterialListFrmt = "{0} Converted static lighting materials";
            public const string kDynamConvertedMaterialListFrmt = "{0} Converted dynamic lighting materials";
            public const string kDaydreamMaterialListFrmt = "{0} Daydream materials found";
            public const string kStaticSplitMaterialListFrmt = "{0} Split materials references. These materials were shared between a statically " +
                "lit and non-statically lit objects. The material has been duplicated and changes applied to the static version (used by static objects). " +
                "The duplicated material(s) can be found in Assets/Materials, appended with '_staticlit'.";
            public const string kWelcomeMsg = "Welcome to the Daydream Renderer Import Wizard. From here you can easily convert your scene to use Daydream static and dynamic materials and shaders." +
                " You can also control how the Daydream lighting system is applied to the scene and to project assets.";
            public const string kObjectsWaiting = "{0} dynamic and {1} static objects waiting for conversion to daydream lighting";
            public const string kToggleComponentsHelp = "Daydream Renderer can utilize a custom lighting system to provide lighting data to shaders. " + 
                "For the best work-flow experience enable this option when using Daydream Renderer static lighting.";
            public const string kEnlightenHelp = "Daydream Renderer static lighting depends on Unity's light states in order apply lighting to static lit objects correctly. " +
                "In order to have the light states apply to the scene you must bake the scene with the Enlighten at least once. Or, you can enable the Daydream lighting system";
            static Styles()
            {
                s_defaultLabel = new GUIStyle(EditorStyles.label);
                s_defaultLabel.fixedHeight = 18;
                s_defaultLabel.wordWrap = false;
                s_defaultLabel.stretchHeight = false;
            }

        }

        [InitializeOnLoad]
        public class FirstTimeLoader
        {
            static DateTime s_lastUpdate = DateTime.Now;
            static FirstTimeLoader()
            {
                EditorApplication.update += Update;
            }

            static void Update()
            {

                if (!string.IsNullOrEmpty(SceneManager.GetActiveScene().name) && SceneManager.GetActiveScene().isLoaded)
                {
                    if (DaydreamRendererImportSettings.FirstRun)
                    {
                        DaydreamRendererImportSettings.FirstRun = false;
                        DaydreamRendererImportManager.Init();
                    }
                }

                if(DaydreamRendererImportSettings.EnableLightingComponentsAutoAdd && DaydreamRendererImportSettings.DaydreamLightinSystemEnabled && (DateTime.Now - s_lastUpdate).TotalSeconds > 2)
                {
                    s_lastUpdate = DateTime.Now;
                    ApplyLightingComponents();
                }
            }
        }

        static class StaticSceneState
        {
            public static uint kNotStatic = 1;
            public static uint kStatic = 2;
            public static uint kMixed = 3;
        }

        public class MaterialInfo
        {
            public MaterialInfo() { }

            public MaterialInfo(Material material)
            {
                m_material = material;
            }

            public HashSet<Renderer> GetTargets()
            {
                if (m_material == null || !m_targets.ContainsKey(m_material.name))
                {
                    return null;
                }

                return m_targets[m_material.name];
            }

            public void AddTarget(Renderer target)
            {
                if (!m_targets.ContainsKey(m_material.name))
                {
                    m_targets.Add(m_material.name, new HashSet<Renderer>());
                }

                m_targets[m_material.name].Add(target);
            }

            public Material m_material;

            // targets container is shared across material infos to capture one material used by multiple targets
            public static Dictionary<string, HashSet<Renderer>> m_targets = new Dictionary<string, HashSet<Renderer>>();
        }


        [MenuItem("Window/Daydream Renderer/Import Wizard")]
        public static void Init()
        {
            Configure();
            DaydreamRendererImportManager window = EditorWindow.GetWindow<DaydreamRendererImportManager>("Import Wizard");
            window.Show();
        }


        [MenuItem("GameObject/Daydream Baker/Convert Static Materials", false, 0)]
        public static void ConvertStaticMaterials()
        {
            GatherMaterials(Selection.gameObjects);
            StaticLightingConversionHelper();
        }

        [MenuItem("GameObject/Daydream Baker/Convert Dynamic Materials", false, 0)]
        public static void ConvertDynamicMaterials()
        {
            GatherMaterials(Selection.gameObjects);
            DynamicLightingConversionHelper();
        }

        private static void GatherMaterials(GameObject[] gos)
        {
            Configure();

            m_staticConvertableCount = 0;
            m_dynamicConvertableCount = 0;
            m_convertedCount = 0;
            m_convertableMaterials = GatherMaterialsForConversion(gos);

            Dictionary<string, HashSet<Renderer>>.Enumerator dictIter = MaterialInfo.m_targets.GetEnumerator();
            while (dictIter.MoveNext())
            {
                HashSet<Renderer>.Enumerator iter = dictIter.Current.Value.GetEnumerator();
                while (iter.MoveNext())
                {
                    if(iter.Current == null) continue;

                    for (int i = 0, k = iter.Current.sharedMaterials.Length; i < k; ++i)
                    {
                        Material m = iter.Current.sharedMaterials[i];

                        if(m == null) continue;
                        
                        if (m.shader.name != kDaydreamShader)
                        {
                            if (IsStaticLit(iter.Current))
                            {
                                m_staticConvertableCount++;
                            }
                            else
                            {
                                m_dynamicConvertableCount++;
                            }
                        }
                        else
                        {
                            // already a daydream material
                            if (IsStaticLit(iter.Current))
                            {
                                m_convertedCount++;
                            }
                        }
                    }
                }
            }
        }

        void OnEnable()
        {
            EditorApplication.hierarchyWindowChanged += OnSceneChange;
        }

        public static void OnSceneChange()
        {
            MaterialInfo.m_targets.Clear();
            Configure();
        }

        static void Configure()
        {
            s_dynamicMaterialHistory = TypeExtensions.FindOrCreateScriptableAsset<DaydreamRendererMaterialHistory>(m_assetPathBackup, "dynamicmaterialhistory");
            s_listContainerDynamicMats = new SerializedObject(s_dynamicMaterialHistory);

            s_staticMaterialHistory = TypeExtensions.FindOrCreateScriptableAsset<DaydreamRendererMaterialHistory>(m_assetPathBackup, "materialhistory");
            s_listContainerStaticMats = new SerializedObject(s_staticMaterialHistory);
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(Styles.kWelcomeMsg, MessageType.Info);

            if (s_staticMaterialHistory == null || s_dynamicMaterialHistory == null)
            {
                Configure();
            }


            //---------------------------------------------------------------------//
            // Daydream lighting

            EditorGUI.BeginChangeCheck();
            GUILayout.Space(20);


            if (DaydreamRendererImportSettings.DaydreamLightinSystemEnabled)
            {
                EditorGUILayout.HelpBox(Styles.kToggleComponentsHelp, MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(Styles.kEnlightenHelp, MessageType.Warning);
            }

            EditorGUI.BeginChangeCheck();
            DaydreamRendererImportSettings.DaydreamLightinSystemEnabled = EditorGUILayout.BeginToggleGroup(Styles.toggleLightingSystem, DaydreamRendererImportSettings.DaydreamLightinSystemEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                if(!DaydreamRendererImportSettings.DaydreamLightinSystemEnabled)
                {
                    RemoveAllLightingComponents();
                }
                else if (EditorUtility.DisplayDialog("Daydream Lighting", "Would you like to add lighting to prefabs and models in your project folder? Only affects assets using Daydream Renderer shaders.", "Yes", "No"))
                {
                    ApplyLightingToProject();
                }

                DaydreamRenderer renderer = FindObjectOfType<DaydreamRenderer>();
                if (renderer)
                {
                    renderer.EnableEnlighten(!DaydreamRendererImportSettings.DaydreamLightinSystemEnabled);
                }
            }


            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            DaydreamRendererImportSettings.EnableLightingComponentsAutoAdd = EditorGUILayout.ToggleLeft(Styles.toggleComponents, DaydreamRendererImportSettings.EnableLightingComponentsAutoAdd);
            EditorGUILayout.EndHorizontal();
            if (!DaydreamRendererImportSettings.EnableLightingComponentsAutoAdd)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                if (GUILayout.Button(Styles.addComponents))
                {
                    ApplyLightingComponents();
                }
                if (GUILayout.Button(Styles.removeComponents))
                {
                    RemoveAllLightingComponents();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            if (GUILayout.Button(Styles.addComponentsToProject))
            {
                ApplyLightingToProject();
            }
            if (GUILayout.Button(Styles.removeComponentsFromProject))
            {
                RemoveAllLightingComponents();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndToggleGroup();

            //---------------------------------------------------------------------//
            // Daydream Material Wizard

            GUILayout.Space(Styles.kSectionSpacing);
            EditorGUILayout.HelpBox("The Material Wizard assists in converting an existing scene over to Daydream materials. It applies a conversion process to preserve all the feature selections of the original Unity shader but converted to the Daydream standard shader.", MessageType.Info);
            
            if (GUILayout.Button("Scan Materials") || m_convertableMaterials == null || m_convertableMaterials.Count == 0 || s_gatherMetrics)
            {
                List<GameObject> roots = Utilities.GetAllRoots();
                GatherMaterials(roots.ToArray());
            }

            EditorGUILayout.Separator();

            EditorGUILayout.HelpBox(String.Format(Styles.kObjectsWaiting, m_dynamicConvertableCount, m_staticConvertableCount), MessageType.Info);

            if (GUILayout.Button("Convert Materials Now"))
            {
                DoDynamicLightingConversion();
                DoStaticLightingConversion();
            }

            string[] text = new string[] { "Dynamic Converted Materials", "Static Converted Materials", "List All Daydream Materials" };
            s_importType = GUILayout.SelectionGrid(s_importType, text, 3, EditorStyles.radioButton);

            if (s_importType == 0)
            {
                DrawDynamicRevertMaterials();

            }
            else if (s_importType == 1)
            {

                DrawStaticRevertMaterials();

            }
            else if (m_convertedCount > 0)
            {
                DrawDaydreamMaterialList();
            }
        }

        static void DoDynamicLightingConversion()
        {
            // static lighting report
            if (m_dynamicConvertableCount > 0)
            {
                if (m_convertableMaterials.Count > 0)
                {
                    DynamicLightingConversionHelper();
                }
            }

        }

        static void DynamicLightingConversionHelper()
        {
            bool assetsDirty = false;

            HashSet<string> converted = new HashSet<string>();

            int index = 0;
            foreach (MaterialInfo matInfo in m_convertableMaterials)
            {
                Material material = matInfo.m_material;

                if (material == null) continue;

                assetsDirty = true;

                uint staticState = GetSceneStaticState(matInfo.GetTargets());

                AddVertexLightingComponent(matInfo.GetTargets());

                if (staticState == StaticSceneState.kNotStatic || staticState == StaticSceneState.kMixed)
                {
                    // dynamic conversion
                    // find existing entry in converted materials list
                    bool exists = s_dynamicMaterialHistory.m_convertedMaterials.Exists(delegate (DaydreamRendererMaterialHistory.Entry sm)
                        {
                            if (sm != null && sm.m_backupMaterial != null)
                            {
                                return (material.name + "bak") == sm.m_backupMaterial.name;
                            }
                            return false;
                        });

                    if (!exists && !converted.Contains(material.name))
                    {
                        converted.Add(material.name);

                        MatEntry entry = new MatEntry();

                        // load the backup material
                        entry.m_backupMaterial = MakeMaterialBackup(material);
                        // reload material and update shader and settings
                        entry.m_material = ConvertToDynamicLighting(material);

                        if (entry.m_material != null && entry.m_backupMaterial)
                        {
                            s_dynamicMaterialHistory.m_convertedMaterials.Add(entry);

                            // mark dirty for saving
                            EditorUtility.SetDirty(s_dynamicMaterialHistory);
                        }
                    }
                }

                EditorUtility.DisplayProgressBar("Import Wizard", "Converting To Daydream Materials", index / (float)m_convertableMaterials.Count);
                index++;
            }

            EditorUtility.ClearProgressBar();

            if (assetsDirty)
            {
                s_gatherMetrics = true;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

        }

        static void DoStaticLightingConversion()
        {
            // static lighting report
            if (m_staticConvertableCount > 0)
            {
                if (m_convertableMaterials.Count > 0)
                {
                    StaticLightingConversionHelper();

                    if (EditorUtility.DisplayDialog("Conversion Complete", "Would you like to open Vertex Lighting now?", "Yes", "No"))
                    {
                        DaydreamVertexLightingEditor.OpenWindow();
                    }
                }
            }

        }

        static void StaticLightingConversionHelper()
        {
            bool assetsDirty = false;
            bool materialsWereSplit = false;

            HashSet<string> converted = new HashSet<string>();

            int index = 0;
            foreach (MaterialInfo matInfo in m_convertableMaterials)
            {
                Material material = matInfo.m_material;

                if (material == null) continue;

                assetsDirty = true;

                uint staticState = GetSceneStaticState(matInfo.GetTargets());

                AddVertexLightingComponent(matInfo.GetTargets());

                if (staticState == 0) continue;

                if (staticState == StaticSceneState.kStatic)
                {
                    // find existing entry in converted materials list
                    bool exists = s_staticMaterialHistory.m_convertedMaterials.Exists(delegate (DaydreamRendererMaterialHistory.Entry sm)
                        {
                            if (sm != null && sm.m_backupMaterial != null)
                            {
                                return (material.name + "bak") == sm.m_backupMaterial.name;
                            }
                            return false;
                        });

                    if (!exists && !converted.Contains(material.name))
                    {
                        converted.Add(material.name);

                        MatEntry entry = new MatEntry();

                        // load the backup material
                        entry.m_backupMaterial = MakeMaterialBackup(material);
                        // reload material and update shader and settings
                        entry.m_material = ConvertToStaticLighting(material);

                        if (entry.m_material != null && entry.m_backupMaterial)
                        {
                            s_staticMaterialHistory.m_convertedMaterials.Add(entry);

                            // mark dirty for saving
                            EditorUtility.SetDirty(s_staticMaterialHistory);
                        }
                    }

                }
                else if (staticState == StaticSceneState.kMixed)
                {
                    // duplicate material and convert the duplicate
                    Material staticLitMat = MakeOrLoadSplitMaterial(material);

                    staticLitMat = ConvertToStaticLighting(staticLitMat);

                    // update all static target sites
                    HashSet<Renderer>.Enumerator iter = matInfo.GetTargets().GetEnumerator();

                    while (iter.MoveNext())
                    {

                        if (IsStaticLit(iter.Current))
                        {
                            // find the split material source and replace it with the split material
                            Material[] mats = iter.Current.sharedMaterials;
                            for (int i = 0, k = mats.Length; i < k; ++i)
                            {
                                if (mats[i].name == material.name)
                                {
                                    MatEntry entry = new MatEntry();

                                    // setup entry
                                    entry.m_material = mats[i];
                                    entry.m_splitMaterial = staticLitMat;
                                    entry.m_sourceScenePath = iter.Current.gameObject.GetPath();

                                    mats[i] = staticLitMat;

                                    // only add it if does not exist already
                                    s_staticMaterialHistory.m_splitMaterials.Add(entry);
                                    EditorUtility.SetDirty(s_staticMaterialHistory);
                                }
                            }

                            iter.Current.sharedMaterials = mats;

                            // dirty the mesh renderer
                            EditorUtility.SetDirty(iter.Current);

                            materialsWereSplit = true;
                        }
                    }
                }

                EditorUtility.DisplayProgressBar("Import Wizard", "Converting To Daydream Materials", index / (float)m_convertableMaterials.Count);
                index++;
            }

            EditorUtility.ClearProgressBar();

            if (assetsDirty)
            {
                s_gatherMetrics = true;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (materialsWereSplit)
            {
                EditorUtility.DisplayDialog("Conversion Notice!", "New materials created and added to " + m_assetPathSplit, "Ok");
            }

        }

        static void DrawDynamicRevertMaterials()
        {
            int convertedCount = s_dynamicMaterialHistory.m_convertedMaterials != null ? s_dynamicMaterialHistory.m_convertedMaterials.Count : 0;

            if (convertedCount == 0) return;

            foreach (DaydreamRendererMaterialHistory.Entry sm in m_dynamRemoveList)
            {
                s_dynamicMaterialHistory.m_convertedMaterials.Remove(sm);
            }
            m_dynamRemoveList.Clear();

            s_listContainerDynamicMats = new SerializedObject(s_dynamicMaterialHistory);

            // Draw list of converted materials
            SerializedProperty convertList = s_listContainerDynamicMats.FindProperty("m_convertedMaterials");

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField(String.Format(Styles.kDynamConvertedMaterialListFrmt, convertList.arraySize));
            EditorGUILayout.BeginVertical();
            EditorGUIUtility.labelWidth = 75f;

            m_scrollPosConverted = DrawRevertList(m_scrollPosConverted, convertList, 400, delegate (SerializedProperty matHistoryEntry, int elIndex)
                {
                // OnRevert Callback

                // trigger refresh of metrics
                s_gatherMetrics = true;

                    Material main = matHistoryEntry.FindPropertyRelative("m_material").objectReferenceValue as Material;
                    Material backup = matHistoryEntry.FindPropertyRelative("m_backupMaterial").objectReferenceValue as Material;

                    main.shader = backup.shader;
                    main.CopyPropertiesFromMaterial(backup);
                    EditorUtility.SetDirty(main);
                    AssetDatabase.SaveAssets();

                // TODO
                //List<GameObject> revertSites = FindAllMaterialSites(main.name);
                //foreach (GameObject go in revertSites)
                //{
                //    // foreach revert site do something
                //    // ...
                //}
                m_dynamRemoveList.Add(s_dynamicMaterialHistory.m_convertedMaterials[elIndex]);
                });

            EditorGUILayout.EndVertical();
        }

        static void DrawStaticRevertMaterials()
        {
            int convertedCount = s_staticMaterialHistory.m_convertedMaterials != null ? s_staticMaterialHistory.m_convertedMaterials.Count : 0;

            if (convertedCount == 0) return;

            // remove anything flagged for revert
            foreach (DaydreamRendererMaterialHistory.Entry sm in m_removeList)
            {
                s_staticMaterialHistory.m_convertedMaterials.Remove(sm);
            }
            m_removeList.Clear();

            foreach (DaydreamRendererMaterialHistory.Entry sm in m_removeSplitList)
            {
                s_staticMaterialHistory.m_splitMaterials.Remove(sm);
                s_listContainerStaticMats.Update();
            }
            m_removeSplitList.Clear();

            // Draw list of converted materials
            //s_listContainerStaticMats = new SerializedObject(s_staticMaterialHistory);
            s_listContainerStaticMats.Update();
            SerializedProperty convertList = s_listContainerStaticMats.FindProperty("m_convertedMaterials");

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField(String.Format(Styles.kStaticConvertedMaterialListFrmt, convertList.arraySize));
            EditorGUILayout.BeginVertical();
            EditorGUIUtility.labelWidth = 75f;

            m_scrollPosConverted = DrawRevertList(m_scrollPosConverted, convertList, 400, delegate (SerializedProperty matHistoryEntry, int elIndex)
                {
                // OnRevert Callback

                // trigger refresh of metrics
                s_gatherMetrics = true;

                    Material main = matHistoryEntry.FindPropertyRelative("m_material").objectReferenceValue as Material;
                    Material backup = matHistoryEntry.FindPropertyRelative("m_backupMaterial").objectReferenceValue as Material;

                    main.shader = backup.shader;
                    main.CopyPropertiesFromMaterial(backup);
                    EditorUtility.SetDirty(main);
                    AssetDatabase.SaveAssets();

                    List<GameObject> revertSites = FindAllMaterialSites(main.name);
                    foreach (GameObject go in revertSites)
                    {
                        DaydreamVertexLighting dvl = go.GetComponent<DaydreamVertexLighting>();
                        if (dvl != null)
                        {
                            MeshRenderer mr = dvl.GetComponent<MeshRenderer>();
                            if (mr != null)
                            {
                                mr.additionalVertexStreams = null;
                            }
                            DestroyImmediate(dvl);
                        }
                    }
                    m_removeList.Add(s_staticMaterialHistory.m_convertedMaterials[elIndex]);
                });

            SerializedProperty splitList = s_listContainerStaticMats.FindProperty("m_splitMaterials");

            if (splitList.arraySize > 0)
            {
                EditorGUILayout.HelpBox(String.Format(Styles.kStaticSplitMaterialListFrmt, splitList.arraySize), MessageType.Info);
            }
            // Revert list for split materials
            m_scrollPosSplit = DrawRevertList(m_scrollPosSplit, splitList, 400, delegate (SerializedProperty matHistoryEntry, int elIndex)
                {
                // OnRevert Callback

                // trigger refresh of metrics
                s_gatherMetrics = true;

                    Material main = matHistoryEntry.FindPropertyRelative("m_material").objectReferenceValue as Material;

                    Material split = matHistoryEntry.FindPropertyRelative("m_splitMaterial").objectReferenceValue as Material;
                    string path = matHistoryEntry.FindPropertyRelative("m_sourceScenePath").stringValue;

                    List<GameObject> gos = Utilities.FindAll(path);
                    foreach (GameObject go in gos)
                    {
                        if (go != null)
                        {
                            MeshRenderer mr = go.GetComponent<MeshRenderer>();
                            if (mr != null)
                            {
                                Material[] mats = mr.sharedMaterials;
                            //bool restored = false;
                            for (int midx = 0, k = mats.Length; midx < k; ++midx)
                                {
                                    if (mr.sharedMaterials[midx].name == split.name)
                                    {
                                    // restore the material
                                    //restored = true;
                                    mats[midx] = main;
                                        EditorUtility.SetDirty(mr);
                                        AssetDatabase.SaveAssets();
                                    }
                                }

                                mr.sharedMaterials = mats;
                            //if (restored)
                            {
                                    m_removeSplitList.Add(s_staticMaterialHistory.m_splitMaterials[elIndex]);

                                //// remove daydream component
                                //DaydreamVertexLighting dvl = mr.GetComponent<DaydreamVertexLighting>();
                                //if (dvl != null)
                                //{
                                //    // remove vertex stream
                                //    mr.additionalVertexStreams = null;
                                //    DestroyImmediate(dvl);
                                //}
                            }
                            }
                        }
                    }

                },
                // draw extra properties of the material-history serialized object
                new string[]
                {
                "m_sourceScenePath",
                }
            );// end function call

            EditorGUILayout.EndVertical();
        }

        static void DrawDaydreamMaterialList()
        {

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField(String.Format(Styles.kDaydreamMaterialListFrmt, m_convertableMaterials.Count));
            EditorGUILayout.BeginVertical();
            EditorGUIUtility.labelWidth = 75f;

            m_scrollPosConverted = DrawMaterialList(m_scrollPosConverted, m_convertableMaterials, 400);

            EditorGUILayout.EndVertical();
        }

        public delegate void RevertDelegate(SerializedProperty matHistoryEntry, int elIndex);
        public delegate void DrawAdditionalProperties(SerializedProperty matHistoryEntry, int elIndex);

        static Vector2 DrawRevertList(Vector2 scrollPosition, SerializedProperty list, int listHeight, RevertDelegate OnRevert, string[] drawPropertiesList = null)
        {
            if (list.arraySize == 0)
            {
                return scrollPosition;
            }
            // Split and converted materials
            int linesPerEntry = 1 + (drawPropertiesList != null ? drawPropertiesList.Length : 0);
            float height = Styles.s_defaultLabel.fixedHeight * linesPerEntry;

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(listHeight));

            scrollPosition.y = Mathf.Min(height * list.arraySize, scrollPosition.y);
            for (int i = 0, c = list.arraySize; i < c; i++)
            {
                // create a 'window' of items to actually fill in, otherwise rendering the list is too slow
                int topIndex = (int)(scrollPosition.y / height);
                int start = (int)(Mathf.Max(0f, topIndex - 4f));
                int end = (int)(Mathf.Min(list.arraySize - 1, topIndex + 40f));

                if (i < start || i > end)
                {
                    // fill this item with blank space to save time
                    GUILayout.Space(Styles.s_defaultLabel.fixedHeight * linesPerEntry);
                    continue;
                }

                SerializedProperty element = list.GetArrayElementAtIndex(i);


                EditorGUILayout.BeginHorizontal();

                // print index
                GUILayout.Label("" + i, Styles.s_defaultLabel, GUILayout.Width(20));

                EditorGUILayout.PropertyField(element.FindPropertyRelative("m_material"), GUILayout.Height(Styles.s_defaultLabel.fixedHeight));

                if (GUILayout.Button("Revert", GUILayout.Width(60), GUILayout.Height(Styles.s_defaultLabel.fixedHeight)))
                {
                    OnRevert(element, i);

                    // if last item is visible and we delete something Unity throws a repaint exception, trying to repaint a list item thats not there anymore
                    if (list.arraySize - 1 == end)
                    {
                        scrollPosition.y -= height;
                    }
                }

                EditorGUILayout.EndHorizontal();

                if (drawPropertiesList != null)
                {
                    for (int prop = 0; prop < drawPropertiesList.Length; ++prop)
                    {
                        EditorGUILayout.PropertyField(element.FindPropertyRelative("m_sourceScenePath"), GUILayout.Height(Styles.s_defaultLabel.fixedHeight));
                    }
                }
            }
            EditorGUILayout.EndScrollView();


            return scrollPosition;
        }

        static Vector2 DrawMaterialList(Vector2 scrollPosition, List<MaterialInfo> list, int listHeight)
        {
            // Split and converted materials
            int linesPerEntry = 1;
            float height = Styles.s_defaultLabel.fixedHeight * linesPerEntry;

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(listHeight));
            for (int i = 0, c = list.Count; i < c; i++)
            {
                // create a 'window' of items to actually fill in, otherwise rendering the list is too slow
                int topIndex = (int)(scrollPosition.y / height);
                int start = (int)(Mathf.Max(0f, topIndex - 4f));
                int end = (int)(Mathf.Min(list.Count - 1, topIndex + 40f));

                if (i < start || i > end)
                {
                    // fill this item with blank space to save time
                    GUILayout.Space(Styles.s_defaultLabel.fixedHeight * linesPerEntry);
                    continue;
                }

                MaterialInfo element = list[i];

                EditorGUILayout.BeginHorizontal();

                // print index
                GUILayout.Label("" + i, Styles.s_defaultLabel, GUILayout.Width(20));

                EditorGUILayout.ObjectField(element.m_material, typeof(Material), false, GUILayout.Height(Styles.s_defaultLabel.fixedHeight));

                EditorGUILayout.EndHorizontal();

            }
            EditorGUILayout.EndScrollView();

            return scrollPosition;
        }

        static List<MaterialInfo> GatherMaterialsForConversion()
        {
            List<GameObject> roots = Utilities.GetAllRoots();
            return GatherMaterialsForConversion(roots.ToArray());
        }

        static List<MaterialInfo> GatherMaterialsForConversion(GameObject[] roots)
        {
            List<MaterialInfo> materials = new List<MaterialInfo>();
            foreach (GameObject go in roots)
            {
                Renderer[] meshes = go.GetComponentsInChildren<Renderer>();
                foreach (Renderer mr in meshes)
                {
                    foreach (Material m in mr.sharedMaterials)
                    {
                        if (m != null)// && m.shader.name != kDaydreamShader)
                        {
                            MaterialInfo matInfo = new MaterialInfo(m);
                            // record scene path
                            matInfo.AddTarget(mr);

                            materials.Add(matInfo);

                        }
                    }

                }

            }

            return materials;
        }


        static uint GetSceneStaticState(HashSet<Renderer> renderers)
        {
            uint staticFlag = 0;
            HashSet<Renderer>.Enumerator iter = renderers.GetEnumerator();

            while (iter.MoveNext())
            {
                if (!IsStaticLit(iter.Current))
                {
                    staticFlag |= StaticSceneState.kNotStatic;
                }
                else
                {
                    staticFlag |= StaticSceneState.kStatic;
                }
            }

            return staticFlag;
        }

        static void AddVertexLightingComponent(HashSet<Renderer> renderers)
        {
            HashSet<Renderer>.Enumerator iter = renderers.GetEnumerator();

            while (iter.MoveNext())
            {
                if (IsStaticLit(iter.Current))
                {
                    if (iter.Current.gameObject.GetComponent<DaydreamVertexLighting>() == null)
                    {
                        iter.Current.gameObject.AddComponent<DaydreamVertexLighting>();
                    }
                }

            }
        }

        static bool IsStaticLit(Renderer renderer)
        {
            return ((int)StaticEditorFlags.LightmapStatic & (int)GameObjectUtility.GetStaticEditorFlags(renderer.gameObject)) == 1;
        }

        static bool IsDynamicLit(Renderer renderer)
        {
            return !renderer.sharedMaterial.shader.name.ToLower().Contains("unlit");
        }

        static Material MakeMaterialBackup(Material material)
        {
            if (!Directory.Exists(m_assetPathBackup))
            {
                Directory.CreateDirectory(m_assetPathBackup);
            }
            // create asset for backup
            Material copy = new Material(material);
            AssetDatabase.CreateAsset(copy, m_assetPathBackup + "/" + material.name + "bak.mat");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // load the backup material
            return AssetDatabase.LoadAssetAtPath<Material>(m_assetPathBackup + "/" + material.name + "bak.mat");
        }

        static Material MakeOrLoadSplitMaterial(Material material)
        {
            if (!Directory.Exists(m_assetPathSplit))
            {
                Directory.CreateDirectory(m_assetPathSplit);
            }

            if (!File.Exists(m_assetPathSplit + "/" + material.name + "_staticlit.mat"))
            {
                // create asset for backup
                Material copy = new Material(material);
                AssetDatabase.CreateAsset(copy, m_assetPathSplit + "/" + material.name + "_staticlit.mat");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // load the split material
            return AssetDatabase.LoadAssetAtPath<Material>(m_assetPathSplit + "/" + material.name + "_staticlit.mat");
        }

        static Material ConvertToStaticLighting(Material material)
        {
            Shader destShader = Shader.Find("Daydream/Standard");

            if (!DaydreamMenu.StandardToDaydreamSingleMaterial(material, material.shader, destShader))
            {
                Debug.LogWarning("No Conversion");
                material.shader = destShader;
            }

            // enable static vertex lighting
            material.EnableKeyword("STATIC_LIGHTING");
            material.DisableKeyword("LIGHTMAP");
            material.EnableKeyword("VERTEX_LIGHTING");

            EditorUtility.SetDirty(material);

            return material;
        }

        static Material ConvertToDynamicLighting(Material material)
        {
            // load main asset
            string path = AssetDatabase.GetAssetPath(material);
            Material mainMaterial = AssetDatabase.LoadMainAssetAtPath(path) as Material;

            if (mainMaterial == null)
            {
                return null;
            }

            Shader destShader = Shader.Find("Daydream/Standard");

            if (!DaydreamMenu.StandardToDaydreamSingleMaterial(material, material.shader, destShader))
            {
                Debug.Log("No Conversion for " + material.shader.name);
            }

            EditorUtility.SetDirty(material);

            return material;
        }

        static List<GameObject> FindAllMaterialSites(string materialName)
        {
            List<GameObject> roots = Utilities.GetAllRoots();

            List<GameObject> foundObjs = new List<GameObject>();
            for (int i = 0; i < roots.Count; ++i)
            {
                Renderer[] mrs = roots[i].transform.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer mr in mrs)
                {
                    if (mr != null)
                    {
                        foreach (Material m in mr.sharedMaterials)
                        {
                            if (m != null && m.name == materialName)
                            {
                                foundObjs.Add(mr.gameObject);
                                break;
                            }
                        }
                    }
                }
            }

            return foundObjs;
        }

        static void RemoveAllLightingComponents()
        {
            DaydreamLight[] lightComp = GameObject.FindObjectsOfType<DaydreamLight>();
            DaydreamMeshRenderer[] meshComp = GameObject.FindObjectsOfType<DaydreamMeshRenderer>();

            RemoveLightingComponentsFromAssets();

            foreach (var v in lightComp)
            {
                DestroyImmediate(v);
            }
            foreach (var v in meshComp)
            {
                DestroyImmediate(v);
            }
        }

        static void RemoveLightingComponentsFromAssets()
        {
            // find models and prefabs
            string[] modelAssets = AssetDatabase.FindAssets("t:Model t:prefab");

            // remove from prefabs and models in the project
            int count = modelAssets.Length;
            int index = 0;
            foreach(string guid in modelAssets)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                DaydreamMeshRenderer[] dmrs = model.GetComponentsInChildren<DaydreamMeshRenderer>();

                EditorUtility.DisplayProgressBar("Import Wizard", "Adding Component to Model " + path, index / (float)count);

                foreach(DaydreamMeshRenderer dmr in dmrs)
                {
                    GameObject.DestroyImmediate(dmr, true);
                }

                index++;
            }

            EditorUtility.ClearProgressBar();
        }

        static void ApplyLightingToProject()
        {
            // find models and prefabs
            string[] modelAssets = AssetDatabase.FindAssets("t:prefab");

            // Add daydream mesh renderer to objects in the project
            int count = modelAssets.Length;
            int index = 0;
            foreach(string guid in modelAssets)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Renderer[] mrs = model.GetComponentsInChildren<Renderer>();

                EditorUtility.DisplayProgressBar("Import Wizard", "Adding Component to Prefab " + path, index / (float)count);

                foreach(Renderer mr in mrs)
                {
                    if(mr != null && mr.gameObject != null && mr.gameObject.GetComponent<DaydreamMeshRenderer>() == null)
                    {
                        foreach(Material m in mr.sharedMaterials)
                        {
                            if(m != null && m.shader.name.ToLower().Contains("daydream"))
                            {
                                mr.gameObject.AddComponent<DaydreamMeshRenderer>();
                                break;
                            }
                        }
                    }
                }
            }

            // find models and prefabs
            modelAssets = AssetDatabase.FindAssets("t:Model");

            // Add daydream mesh renderer to objects in the project
            count = modelAssets.Length;
            index = 0;
            foreach(string guid in modelAssets)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Renderer[] mrs = model.GetComponentsInChildren<Renderer>();

                EditorUtility.DisplayProgressBar("Import Wizard", "Adding Component to Model " + path, index / (float)count);

                if(mrs.Length > 0)
                {
                    // check if daydream shader is used
                    bool imported = false;
                    foreach(Renderer r in mrs)
                    {
                        if(r != null && r.gameObject.GetComponent<DaydreamMeshRenderer>() == null)
                        {
                            foreach(Material m in r.sharedMaterials)
                            {
                                if(m != null && m.shader.name.ToLower().Contains("daydream"))
                                {
                                    AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
                                    imported = true;
                                    break;
                                }
                            }
                        }

                        if(imported)
                        {
                            break;
                        }
                    }
                }

                index++;
            }

            EditorUtility.ClearProgressBar();
        }

        static void ApplyLightingComponents()
        {
            // check renderers
            List<GameObject> roots = Utilities.GetAllRoots();
            List<GameObject> missingRenderer = new List<GameObject>();
            List<GameObject> missingDrLight = new List<GameObject>();

            for (int i = 0, k = roots.Count; i < k; ++i)
            {
                Renderer[] renderers = roots[i].GetComponentsInChildren<Renderer>();
                Light[] lights = roots[i].GetComponentsInChildren<Light>();

                for (int j = 0, n = renderers.Length; j < n; ++j)
                {
                    MeshRenderer mr = renderers[j] as MeshRenderer;
                    SkinnedMeshRenderer smr = renderers[j] as SkinnedMeshRenderer;

                    if (renderers[j] == null || (mr == null && smr == null))
                    {
                        continue;
                    }

                    bool isDaydreamRendered = false;
                    for (int m = 0; m < renderers[j].sharedMaterials.Length; ++m)
                    {
                        if (renderers[j].sharedMaterials[m] != null && renderers[j].sharedMaterials[m].shader.name.ToLower().Contains("daydream"))
                        {
                            isDaydreamRendered = true;
                            break;
                        }
                    }
                    if (renderers[j] != null && isDaydreamRendered)
                    {
                        DaydreamMeshRenderer[] dmrs = renderers[j].GetComponents<DaydreamMeshRenderer>();
                        if(null == dmrs || dmrs.Length == 0)
                        {
                            missingRenderer.Add(renderers[j].gameObject);
                        }
                        else if(dmrs.Length > 1)
                        {
                            // remove extra
                            for(int x = 1; x < dmrs.Length; ++x)
                            {
                                GameObject.DestroyImmediate(dmrs[x], true);
                            }
                        }
                        else
                        {
                            // update disable/enabled
                            dmrs[0].enabled = renderers[j].enabled;
                        }
                    }
                }
                for (int j = 0, n = lights.Length; j < n; ++j)
                {
                    if (lights[j] != null && null == lights[j].GetComponent<DaydreamLight>())
                    {
                        missingDrLight.Add(lights[j].gameObject);
                    }
                }
            }

            if (missingRenderer.Count != 0 || missingDrLight.Count != 0)
            {
                if(!Application.isPlaying)
                {
                    for (int i = 0, k = missingRenderer.Count; i < k; ++i)
                    {
                        if(missingRenderer[i] != null)
                        {
                            missingRenderer[i].AddComponent<DaydreamMeshRenderer>();
                        }
                    }
                    for (int i = 0, k = missingDrLight.Count; i < k; ++i)
                    {
                        if(missingDrLight[i] != null)
                        {
                            missingDrLight[i].AddComponent<DaydreamLight>();
                        }
                    }
                }else
                {
                    for (int i = 0, k = missingRenderer.Count; i < k; ++i)
                    {
                        if(missingRenderer[i] != null)
                        {
                            Debug.LogWarning(missingRenderer[i].GetPath() + ": DaydreamMeshRenderer must be added to MeshRenderer objects in order for Daydream Lighting system to function");
                        }
                    }
                    for (int i = 0, k = missingDrLight.Count; i < k; ++i)
                    {
                        if(missingDrLight[i] != null)
                        {
                            Debug.LogWarning(missingDrLight[i].GetPath() + ": DaydreamLight must be added to Light components \tin order for Daydream Lighting system to function");
                        }
                    }
                }
            }
        }

    }

}