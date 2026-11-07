using UnityEngine;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>
    /// 카드·패널 안쪽에 겹쳐 그리는 장식 틀: 안쪽 가는 선 + 네 모서리 꺾쇠. 부모를 꽉 채우고 누르기를 막지 않는다.
    /// </summary>
    sealed class Frame : VisualElement
    {
        public static readonly Color Ink = new Color(0.12f, 0.11f, 0.10f);
        public static readonly Color Paper = new Color(0.965f, 0.945f, 0.902f);
        public static readonly Color Vermilion = new Color(0.70f, 0.23f, 0.17f);
        public static readonly Color Bronze = new Color(0.55f, 0.42f, 0.25f);

        readonly Color line, corner;
        readonly float inset, arm;

        public Frame(Color line, Color corner, float inset = 4, float arm = 10)
        {
            this.line = line;
            this.corner = corner;
            this.inset = inset;
            this.arm = arm;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            generateVisualContent += Draw;
        }

        /// <summary>요소에 틀을 덧붙이고 그 요소를 돌려준다.</summary>
        public static T On<T>(T e, Color line, Color corner, float inset = 4, float arm = 10) where T : VisualElement
        {
            e.Add(new Frame(line, corner, inset, arm));
            return e;
        }

        void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width < inset * 4 || r.height < inset * 4) return;
            var p = mgc.painter2D;
            float l = inset, t = inset, rr = r.width - inset, b = r.height - inset;

            p.strokeColor = line;
            p.lineWidth = 1;
            p.BeginPath();
            p.MoveTo(new Vector2(l, t));
            p.LineTo(new Vector2(rr, t));
            p.LineTo(new Vector2(rr, b));
            p.LineTo(new Vector2(l, b));
            p.ClosePath();
            p.Stroke();

            // 모서리 꺾쇠: 바깥으로 한 번 더 꺾인 운문(雲紋) 느낌
            p.strokeColor = corner;
            p.lineWidth = 2;
            p.lineCap = LineCap.Butt;
            void Corner(float x, float y, float dx, float dy)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(x + dx * arm, y));
                p.LineTo(new Vector2(x, y));
                p.LineTo(new Vector2(x, y + dy * arm));
                p.Stroke();
                p.BeginPath();
                p.MoveTo(new Vector2(x + dx * 3, y + dy * 6));
                p.LineTo(new Vector2(x + dx * 3, y + dy * 3));
                p.LineTo(new Vector2(x + dx * 6, y + dy * 3));
                p.Stroke();
            }
            Corner(l, t, 1, 1);
            Corner(rr, t, -1, 1);
            Corner(l, b, 1, -1);
            Corner(rr, b, -1, -1);
        }
    }
}
