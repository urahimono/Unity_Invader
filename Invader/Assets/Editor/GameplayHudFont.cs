using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityInvader.Editor
{
    // Original 5x7 HUD glyphs. No imported typeface or game assets.
    internal static class GameplayHudFont
    {
        private const string DirectoryPath = "Assets/Fonts/GameplayHud";
        private const string Characters = " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private static readonly string[] _rows =
        {
            "00000/00000/00000/00000/00000/00000/00000",
            "01110/10001/10001/11111/10001/10001/10001",
            "11110/10001/10001/11110/10001/10001/11110",
            "01111/10000/10000/10000/10000/10000/01111",
            "11110/10001/10001/10001/10001/10001/11110",
            "11111/10000/10000/11110/10000/10000/11111",
            "11111/10000/10000/11110/10000/10000/10000",
            "01111/10000/10000/10111/10001/10001/01111",
            "10001/10001/10001/11111/10001/10001/10001",
            "11111/00100/00100/00100/00100/00100/11111",
            "00111/00010/00010/00010/10010/10010/01100",
            "10001/10010/10100/11000/10100/10010/10001",
            "10000/10000/10000/10000/10000/10000/11111",
            "10001/11011/10101/10101/10001/10001/10001",
            "10001/11001/10101/10011/10001/10001/10001",
            "01110/10001/10001/10001/10001/10001/01110",
            "11110/10001/10001/11110/10000/10000/10000",
            "01110/10001/10001/10001/10101/10010/01101",
            "11110/10001/10001/11110/10100/10010/10001",
            "01111/10000/10000/01110/00001/00001/11110",
            "11111/00100/00100/00100/00100/00100/00100",
            "10001/10001/10001/10001/10001/10001/01110",
            "10001/10001/10001/10001/10001/01010/00100",
            "10001/10001/10001/10101/10101/11011/10001",
            "10001/10001/01010/00100/01010/10001/10001",
            "10001/10001/01010/00100/00100/00100/00100",
            "11111/00001/00010/00100/01000/10000/11111",
            "01110/10001/10011/10101/11001/10001/01110",
            "00100/01100/00100/00100/00100/00100/01110",
            "01110/10001/00001/00010/00100/01000/11111",
            "11110/00001/00001/01110/00001/00001/11110",
            "00010/00110/01010/10010/11111/00010/00010",
            "11111/10000/10000/11110/00001/00001/11110",
            "01110/10000/10000/11110/10001/10001/01110",
            "11111/00001/00010/00100/01000/01000/01000",
            "01110/10001/10001/01110/10001/10001/01110",
            "01110/10001/10001/01111/00001/00001/01110"
        };

        internal static Font Create()
        {
            Directory.CreateDirectory(DirectoryPath);
            string texturePath = DirectoryPath + "/Hud.png";
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color32[64 * 64];
            var characters = new CharacterInfo[Characters.Length];
            for (int i = 0; i < Characters.Length; i++)
            {
                int x = i % 8 * 8;
                int y = i / 8 * 8;
                string[] rows = _rows[i].Split('/');
                for (int row = 0; row < 7; row++)
                {
                    for (int column = 0; column < 5; column++)
                    {
                        if (rows[row][column] == '1')
                        {
                            pixels[(y + 6 - row) * 64 + x + column] = new Color32(255, 255, 255, 255);
                        }
                    }
                }
                characters[i] = new CharacterInfo
                {
                    index = Characters[i],
                    size = 8,
                    advance = 6,
                    minX = 0,
                    maxX = 5,
                    minY = -7,
                    maxY = 0,
                    uvBottomLeft = new Vector2(x / 64f, y / 64f),
                    uvBottomRight = new Vector2((x + 5) / 64f, y / 64f),
                    uvTopLeft = new Vector2(x / 64f, (y + 7) / 64f),
                    uvTopRight = new Vector2((x + 5) / 64f, (y + 7) / 64f)
                };
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            string fontPath = DirectoryPath + "/Hud.fontsettings";
            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                font = new Font("Gameplay HUD Pixel");
                AssetDatabase.CreateAsset(font, fontPath);
            }
            string materialPath = DirectoryPath + "/Hud.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("UI/Default"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            font.material = material;
            font.characterInfo = characters;
            // Unity's custom bitmap Font metrics: one font unit is one Canvas unit.
            var data = new SerializedObject(font);
            data.FindProperty("m_FontSize").floatValue = 8f;
            data.FindProperty("m_LineSpacing").floatValue = 8f;
            data.FindProperty("m_Ascent").floatValue = 0f;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(material);
            return font;
        }
    }
}
