using System.Collections.Generic;
using UnityEngine;

namespace MartialTactics.App
{
    /// <summary>
    /// 화면에 쓰는 그림. Resources/Portraits/&lt;무인 id&gt;, Resources/Art/&lt;이름&gt;에서 읽고, 없으면 null(화면은 자리 표시로 그린다).
    /// 생성 그림은 확인 전까지 커밋하지 않으므로 두 폴더는 저장소에 없을 수 있다.
    /// </summary>
    static class Art
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Portrait(string unitId) => string.IsNullOrEmpty(unitId) ? null : Load("Portraits/" + unitId);

        static Texture2D pattern;

        /// <summary>바탕 무늬: 옅은 붓 사선 두 줄과 점. 코드로 만들어 그림 파일이 필요 없다.</summary>
        public static Texture2D Pattern()
        {
            if (pattern != null) return pattern;
            const int n = 48;
            pattern = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int d = (x + y) % n;
                    byte a = 0;
                    if (d < 2) a = 14;                      // 사선
                    else if (d == 24 || d == 25) a = 8;     // 가는 사선
                    if ((x - 36) * (x - 36) + (y - 12) * (y - 12) <= 2) a = 18; // 점
                    px[y * n + x] = new Color32(160, 220, 205, a);
                }
            pattern.SetPixels32(px);
            pattern.Apply();
            return pattern;
        }

        public static Texture2D Load(string path)
        {
            if (!cache.TryGetValue(path, out var tex))
            {
                tex = Resources.Load<Texture2D>(path);
                cache[path] = tex;
            }
            return tex;
        }
    }
}
