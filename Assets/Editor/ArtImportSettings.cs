using UnityEditor;

namespace MartialTactics.EditorTools
{
    /// <summary>
    /// Resources/Portraits · Art · UI 그림 가져오기 설정. 기본값은 2의 거듭제곱이 아닌 그림(초상 416×512)을 512×512로 늘려
    /// 화면에서 찌그러져 보이므로 원래 크기를 지킨다.
    /// </summary>
    sealed class ArtImportSettings : AssetPostprocessor
    {
        static readonly string[] Folders = { "Assets/Resources/Portraits/", "Assets/Resources/Art/", "Assets/Resources/UI/" };

        void OnPreprocessTexture()
        {
            bool ours = false;
            foreach (var f in Folders) ours |= assetPath.StartsWith(f);
            if (!ours) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.npotScale = TextureImporterNPOTScale.None;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
