﻿using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public class MaterialProcessor : AssetPostprocessor
{
    public void OnPreprocessModel()
    {
        // Make sure that we only use local files, not files found elsewhere!
        ModelImporter importer = assetImporter as ModelImporter;
        if (importer != null && assetPath.ToLowerInvariant().EndsWith(".obj"))
            importer.materialSearch = ModelImporterMaterialSearch.Local;
    }

    public void OnPostprocessModel(GameObject obj)
    { 
        if (!assetPath.ToLowerInvariant().EndsWith(".obj"))
            return;
        HashSet<Material> mats = new HashSet<Material>();
        MeshRenderer[] mshRnds = obj.GetComponentsInChildren<MeshRenderer>();
        foreach(MeshRenderer rnd in mshRnds)
        {
            mats.Add(rnd.sharedMaterial);
        }

        Dictionary<Material, Material> toReplace = new Dictionary<Material, Material>();
        foreach(Material mat in mats)
        {
            // Rename if necessary
            if (!mat.name.StartsWith(obj.name + "_MAT_"))
            {
                string newName = obj.name + "_MAT_" + mat.name;
                // Rename asset, and switch to old asset if it already exists.
                Material prevAsset = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GetAssetPath(mat).Replace(mat.name, newName));
                if (prevAsset != null)
                    toReplace.Add(mat, prevAsset);
                else
                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(mat), newName);
            }
        }

        foreach(MeshRenderer rnd in mshRnds)
        {
            if (toReplace.ContainsKey(rnd.sharedMaterial))
                rnd.sharedMaterial = toReplace[rnd.sharedMaterial];
        }

        foreach(Material mat in toReplace.Keys)
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(mat));

        ProcessMtlFile(assetPath.Replace(".obj", ".mtl"));
    }

    public void ProcessMtlFile(string fileLocation)
    {
        string matDirectory = fileLocation.Insert(fileLocation.LastIndexOf('/'), "/Materials").Replace(".mtl", "_MAT_");
        string fullPath = Path.Combine(Application.dataPath, fileLocation.Substring(7));
        System.IO.StreamReader reader = new System.IO.StreamReader(fullPath);
        string contents = reader.ReadToEnd();
        Match m = Regex.Match(contents, "newmtl\\s+(?<matname>\\S+).*?((?=\\s*newmtl)|\\s*\\z)", RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.Singleline);
        while (m.Success)
        {
            HandleMaterial(matDirectory + m.Groups["matname"].Value + ".mat", m.Value);
            m = m.NextMatch();
        }
    }

    public void HandleMaterial(string materialLocation, string mtlFileContents)
    {
        string texDirectory = materialLocation.Remove(materialLocation.LastIndexOf("Materials/"));
        string fullPath = Path.Combine(Application.dataPath, materialLocation.Substring(7));
        Material m = AssetDatabase.LoadAssetAtPath<Material>(materialLocation);

        string testString = null;
        float testVal = 0.0f;
        int testInt = 0;
        // First replace the shader to the specular one, if necessary
        testString = LookUpMtlPrefix("illum", mtlFileContents);
        bool isSpecularSetup = (testString != null && int.TryParse(testString, out testInt) && testInt == 2);
        m.shader = Shader.Find(isSpecularSetup ? "Standard (Specular setup)" : "Standard");
        AssetDatabase.SaveAssets();

        // Set the mode to transparency if necessary
        testString = LookUpMtlPrefix("d", mtlFileContents);
        if (testString != null && float.TryParse(testString, out testVal) && testVal < 1f)
        {
            SetShaderTagToTransparent(fullPath);
            AssetDatabase.ImportAsset(materialLocation);
            m = AssetDatabase.LoadAssetAtPath<Material>(materialLocation);
        }

        // Set all the texture maps and colors as needed
        TrySetTexMap("map_Kd", "_MainTex", m, texDirectory, mtlFileContents);
        TrySetTexMap("map_Ks", "_SpecGlossMap", m, texDirectory, mtlFileContents);
        TrySetTexMap("map_bump", "_ParallaxMap", m, texDirectory, mtlFileContents);
        TrySetTexMap("bump", "_ParallaxMap", m, texDirectory, mtlFileContents);
        TrySetColor("Kd", "d", "_Color", m, mtlFileContents);
        TrySetColor("Ks", null, "_SpecColor", m, mtlFileContents);
        testString = LookUpMtlPrefix("Ns", mtlFileContents);
        if (testString != null && float.TryParse(testString, out testVal))
            m.SetFloat("_Glossiness", testVal * 0.001f);
        AssetDatabase.SaveAssets();
    }

    private void TrySetTexMap(string mtlKey, string shaderKey, Material mat, string texDirectory, string mtlContents)
    {
        string texName = LookUpMtlPrefix(mtlKey, mtlContents);
        if (texName != null)
        {
            Texture asset = AssetDatabase.LoadAssetAtPath<Texture>(texDirectory + texName);
            if (asset != null)
                mat.SetTexture(shaderKey, asset);
        }
    }

    private void TrySetColor(string mtlKey, string mtlAlphaKey, string shaderKey, Material mat, string mtlContents)
    {
        Match m = Regex.Match(mtlContents, string.Format("{0}\\s+(?<r>\\d*\\.?\\d+)\\s+(?<g>\\d*\\.?\\d+)\\s+(?<b>\\d*\\.?\\d+)\\b", mtlKey));
        if (!m.Success)
            return;
        
        Color newColor = new Color(float.Parse(m.Groups["r"].Value), float.Parse(m.Groups["g"].Value), float.Parse(m.Groups["b"].Value), 1f);
        if (mtlAlphaKey != null)
        {
            string alphaString = LookUpMtlPrefix("mtlAlphaKey", mtlContents);
            if (alphaString != null)
                float.TryParse(alphaString, out newColor.a);
        }
        mat.SetColor(shaderKey, newColor);
    }

    private string LookUpMtlPrefix(string mtlKey, string mtlContents)
    {
        Match m = Regex.Match(mtlContents, string.Format("{0}\\s+(?<val>\\S+)\\b", mtlKey));
        return m.Success ? m.Groups["val"].Value : null;
    }

    private void SetShaderTagToTransparent(string fullMaterialPath)
    {
        System.IO.StreamReader reader = new System.IO.StreamReader(fullMaterialPath);
        string contents = reader.ReadToEnd();
        reader.Close();
        Regex.Match(contents, "stringTagMap: \\{\\}");
        contents = Regex.Replace(contents, "m_ShaderKeywords:(?!\\s*_ALPHAPREMULTIPLY_ON)", "m_ShaderKeywords: _ALPHAPREMULTIPLY_ON");
        contents = Regex.Replace(contents, "stringTagMap: \\{\\}", "stringTagMap:\n    RenderType: Transparent");
        contents = Regex.Replace(contents, "m_CustomRenderQueue:\\s*[-\\d]+\\w", "m_CustomRenderQueue: 3000");
        contents = Regex.Replace(contents, "name:\\s*_DstBlend\\s+second:\\s*0", "name: _DstBlend\n        second: 10");
        contents = Regex.Replace(contents, "name:\\s*_ZWrite\\s+second:\\s*1", "name: _ZWrite\n        second: 0");
        contents = Regex.Replace(contents, "name:\\s*_Mode\\s+second:\\s*0", "name: _Mode\n        second: 3");
        System.IO.StreamWriter writer = new System.IO.StreamWriter(fullMaterialPath, false);
        writer.Write(contents);
        writer.Close();
    }
}
