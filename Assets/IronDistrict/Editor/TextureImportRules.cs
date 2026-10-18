#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IronDistrict.EditorTools
{
    /// <summary>Import rules for the generated PBR textures (albedo / _n normal / _r data).</summary>
    public class TextureImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Resources/Textures/")) return;

            var ti = (TextureImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 4;
            ti.maxTextureSize = 1024;

            if (name.EndsWith("_n"))
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.sRGBTexture = false;
            }
            else if (name.EndsWith("_r"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
            }
            else
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaIsTransparency = true;
            }
        }
    }

    /// <summary>Keeps runtime Shader.Find() calls working in builds.</summary>
    public static class ShaderGuard
    {
        public static readonly string[] Wanted =
        {
            "Standard",
            "Skybox/Procedural",
            "Legacy Shaders/Particles/Additive",
            "Legacy Shaders/Particles/Alpha Blended",
            "Sprites/Default",
            "Legacy Shaders/Transparent/Diffuse"
        };

        public static void EnsureAlwaysIncludedShaders()
        {
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
                if (assets == null || assets.Length == 0) return;

                var so = new SerializedObject(assets[0]);
                var arr = so.FindProperty("m_AlwaysIncludedShaders");
                if (arr == null) return;

                foreach (var shaderName in Wanted)
                {
                    var shader = Shader.Find(shaderName);
                    if (shader == null) continue;

                    bool present = false;
                    for (int i = 0; i < arr.arraySize; i++)
                    {
                        if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                        {
                            present = true;
                            break;
                        }
                    }
                    if (present) continue;

                    arr.InsertArrayElementAtIndex(arr.arraySize);
                    arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = shader;
                    Debug.Log("[IronDistrict] Shader добавлен в Always Included: " + shaderName);
                }
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[IronDistrict] ShaderGuard: " + e.Message);
            }
        }
    }
}
#endif
