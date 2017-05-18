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

namespace daydreamrenderer
{
    [InitializeOnLoad]
    public class DaydreamRendererImportSettings
    {
        public const string kFirstRun = "key.firstrun";
        public const string kDaydreamLightingSystem = "key.ddrlightingsystem";
        public const string kAutoAddLighting = "key.scene.autoaddlighting";

        private static bool m_firstRun = true;
        private static bool m_daydreamLightinSystemEnabled = true;
        private static bool m_enableLightingComponentsAutoAdd = true;
        private static bool m_bakingEnabledForScene = false;

        static DaydreamRendererImportSettings()
        {
            m_firstRun = EditorPrefs.GetBool(kFirstRun, m_firstRun);
            m_daydreamLightinSystemEnabled = EditorPrefs.GetBool(kDaydreamLightingSystem, m_daydreamLightinSystemEnabled);
            m_enableLightingComponentsAutoAdd = EditorPrefs.GetBool(kAutoAddLighting, m_enableLightingComponentsAutoAdd);
            EditorPrefs.DeleteKey("key.bakingenabled.test");
        }

        public static bool FirstRun 
        {
            get 
            {
                return m_firstRun;
            }
            set 
            {
                if(m_firstRun != value)
                {
                    m_firstRun = value;
                    EditorPrefs.SetBool(kFirstRun, m_firstRun);
                }
            }
        }

        public static bool DaydreamLightinSystemEnabled {
            get 
            {
                return m_daydreamLightinSystemEnabled;
            }
            set
            {
                if(m_daydreamLightinSystemEnabled != value)
                {
                    m_daydreamLightinSystemEnabled = value;
                    EditorPrefs.SetBool(kDaydreamLightingSystem, m_daydreamLightinSystemEnabled);
                }
            }
        }

        public static bool EnableLightingComponentsAutoAdd {
            get 
            {
                return m_enableLightingComponentsAutoAdd;
            }
            set 
            {
                if(m_enableLightingComponentsAutoAdd != value)
                {
                    m_enableLightingComponentsAutoAdd = value;
                    EditorPrefs.SetBool(kAutoAddLighting, m_enableLightingComponentsAutoAdd);
                }
            }
        }

        public static bool BakingEnabledForScene
        {
            get 
            {
                m_bakingEnabledForScene = EditorPrefs.GetBool(GetSceneKey(), false);
                return m_bakingEnabledForScene;
            }
            set 
            {
                if (m_bakingEnabledForScene != value)
                {
                    m_bakingEnabledForScene = value;
                    string key = GetSceneKey();
                    EditorPrefs.SetBool(key, m_bakingEnabledForScene);
                }
            }
        }

        private static string GetSceneKey()
        { 
            string name = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(name))
            {
                return "key.bakingenabled." + SceneManager.GetActiveScene().name;
            }

            return "";
        }
    }
}
