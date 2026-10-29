using UnityEngine;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>선으로 그리는 아이콘 (24×24 격자). 색은 글자색(color)을 따른다.</summary>
    sealed class Icon : VisualElement
    {
        public enum Kind { Units, Growth, Content, Gacha, Forge, Gear, Close, Chest, Back, Plus }

        readonly Kind kind;

        public Icon(Kind kind, float size = 24)
        {
            this.kind = kind;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            AddToClassList("icon");
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
        }

        void Draw(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            float s = contentRect.width / 24f;
            if (s <= 0) return;
            var c = resolvedStyle.color;
            p.strokeColor = c;
            p.fillColor = c;
            p.lineWidth = 1.8f * s;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            Vector2 P(float x, float y) => new Vector2(x * s, y * s);
            void Line(params float[] xy)
            {
                p.BeginPath();
                p.MoveTo(P(xy[0], xy[1]));
                for (int i = 2; i < xy.Length; i += 2) p.LineTo(P(xy[i], xy[i + 1]));
                p.Stroke();
            }
            void Shape(bool fill, params float[] xy)
            {
                p.BeginPath();
                p.MoveTo(P(xy[0], xy[1]));
                for (int i = 2; i < xy.Length; i += 2) p.LineTo(P(xy[i], xy[i + 1]));
                p.ClosePath();
                if (fill) p.Fill(); else p.Stroke();
            }
            void Circle(float x, float y, float r)
            {
                p.BeginPath();
                p.Arc(P(x, y), r * s, Angle.Degrees(0), Angle.Degrees(360));
                p.ClosePath();
                p.Stroke();
            }

            switch (kind)
            {
                case Kind.Units:
                    Circle(12, 7.5f, 4);
                    p.BeginPath();
                    p.MoveTo(P(4, 21));
                    p.BezierCurveTo(P(4, 16.6f), P(7.6f, 14), P(12, 14));
                    p.BezierCurveTo(P(16.4f, 14), P(20, 16.6f), P(20, 21));
                    p.Stroke();
                    break;
                case Kind.Growth: // 산과 해: 폐관수련
                    Line(3, 20, 21, 20);
                    Line(5, 20, 10, 10, 13.5f, 15.5f, 15.5f, 12.5f, 19, 20);
                    Circle(17, 6, 2);
                    break;
                case Kind.Content: // 엇갈린 두 검
                    Line(5, 4, 15, 14);
                    Line(12.5f, 16.5f, 17.5f, 11.5f);
                    Line(15.5f, 15.5f, 19.5f, 19.5f);
                    Line(19, 4, 9, 14);
                    Line(11.5f, 16.5f, 6.5f, 11.5f);
                    Line(8.5f, 15.5f, 4.5f, 19.5f);
                    break;
                case Kind.Gacha: // 항아리
                    Line(8, 3, 16, 3);
                    Shape(false, 7, 6, 17, 6, 18, 17, 15, 21, 9, 21, 6, 17);
                    Line(6.6f, 12, 17.4f, 12);
                    break;
                case Kind.Forge: // 모루와 망치
                    Shape(true, 3, 12, 21, 12, 18, 15.5f, 15, 15.5f, 16, 19, 18.5f, 19, 18.5f, 21, 5.5f, 21, 5.5f, 19, 8, 19, 9, 15.5f, 6, 15.5f);
                    p.lineWidth = 2.6f * s;
                    Line(13, 9, 19, 3);
                    p.lineWidth = 1.8f * s;
                    Shape(true, 16, 1.5f, 21.5f, 7, 20, 8.5f, 14.5f, 3);
                    break;
                case Kind.Gear:
                    Circle(12, 12, 3);
                    Line(12, 2, 12, 5); Line(12, 19, 12, 22); Line(2, 12, 5, 12); Line(19, 12, 22, 12);
                    Line(4.9f, 4.9f, 7, 7); Line(17, 17, 19.1f, 19.1f); Line(4.9f, 19.1f, 7, 17); Line(17, 7, 19.1f, 4.9f);
                    break;
                case Kind.Close:
                    p.lineWidth = 2.4f * s;
                    Line(6, 6, 18, 18);
                    Line(18, 6, 6, 18);
                    break;
                case Kind.Chest: // 방치 보상
                    Shape(false, 3, 10, 21, 10, 21, 20, 3, 20);
                    Line(3, 10, 5, 5, 19, 5, 21, 10);
                    Shape(false, 10, 13, 14, 13, 14, 16, 10, 16);
                    break;
                case Kind.Back:
                    p.lineWidth = 2.2f * s;
                    Line(15, 5, 8, 12, 15, 19);
                    break;
                case Kind.Plus:
                    p.lineWidth = 2f * s;
                    Line(12, 5, 12, 19);
                    Line(5, 12, 19, 12);
                    break;
            }
        }
    }
}
