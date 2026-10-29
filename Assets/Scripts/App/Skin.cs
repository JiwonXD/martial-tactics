using UnityEngine;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>
    /// 그림 부품(Resources/UI/Art)을 9칸 늘이기로 입힌다. 그림이 없으면 USS의 단색 모양 그대로 둔다.
    /// plate = 금테 두른 검은 옻칠 이름판, panel = 청동 꺾쇠 나무 틀, frame_card / frame_gold = 카드 틀.
    /// </summary>
    static class Skin
    {
        /// <summary>화면을 띄울 때 한 번: 주 버튼과 이름판 줄에 plate, 창에 panel.</summary>
        public static void Apply(VisualElement root)
        {
            root.Query(className: "pri").ForEach(e => Set(e, "plate", 70, 40, 0.34f, false));
            root.Query(className: "btn").ForEach(e =>
            {
                if (e.ClassListContains("pri") || e.ClassListContains("flat") || e.ClassListContains("danger")) return;
                Set(e, "plate_light", 56, 40, 0.34f, false);
            });
            root.Query(className: "slot").ForEach(e => Set(e, e.ClassListContains("locked") ? "plate" : "plate_light", 56, 40, 0.38f, false));
            root.Query(className: "plate-row").ForEach(e => Set(e, "plate", 70, 40, 0.34f, false));
            root.Query(className: "panel-skin").ForEach(e => Set(e, "panel", 46, 46, 0.5f, true));
        }

        public static bool Set(VisualElement e, string art, int sliceX, int sliceY, float scale, bool keepBackground)
        {
            var tex = Art.Load("UI/Art/" + art);
            if (tex == null) return false;
            e.style.backgroundImage = new StyleBackground(tex);
            e.style.unitySliceLeft = sliceX;
            e.style.unitySliceRight = sliceX;
            e.style.unitySliceTop = sliceY;
            e.style.unitySliceBottom = sliceY;
            e.style.unitySliceScale = scale;
            e.style.borderLeftWidth = 0;
            e.style.borderRightWidth = 0;
            e.style.borderTopWidth = 0;
            e.style.borderBottomWidth = 0;
            if (!keepBackground) e.style.backgroundColor = Color.clear;
            e.AddToClassList("skinned");
            return true;
        }

        /// <summary>카드 위에 겹치는 틀. 그림이 없으면 선으로 그린 틀(Frame).</summary>
        public static VisualElement CardFrame(bool gold, float scale = 0.27f)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList("cardframe");
            if (!Set(e, gold ? "frame_gold" : "frame_card", 92, 92, scale, false))
                e.Add(new Frame(new Color(1, 1, 1, 0.5f), gold ? Frame.Vermilion : Frame.Paper, 3, 8));
            return e;
        }
    }
}
