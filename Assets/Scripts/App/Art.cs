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
