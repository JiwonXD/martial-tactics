using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>
    /// 카드 테두리. 2026-10-29 크럼블 짜임으로 바꾸며 그림 틀·이름판은 걷어내고, 둥근 테두리 한 겹으로 그린다.
    /// 등급 색은 아직 정하지 않아 굵기만 다르고, 주장·5성 이상만 금색.
    /// </summary>
    static class Skin
    {
        public static void Apply(VisualElement root) { }

        public static VisualElement CardFrame(bool gold, float scale = 0)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList("cardedge");
            if (gold) e.AddToClassList("gold");
            return e;
        }
    }
}
