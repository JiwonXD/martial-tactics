using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>
    /// 공통 팝업 (플레이 영상 리뷰 2026-10-30): 확인 · 부족 · 보상. 새 화면은 이 중 하나를 쓴다.
    /// 지금 화면 위에 겹치고, 화면을 다시 그리면 함께 사라진다. 틀 하나씩 깨기: 대표 아이콘을 위 테두리에, 주 버튼을 아래 테두리에 걸친다.
    /// </summary>
    static class Popup
    {
        static VisualElement Frame(string title, out VisualElement body, out VisualElement dim)
        {
            var d = Ui.El("modal", "popup");
            var box = Ui.El("pop-box");
            var head = Ui.El("pop-head");
            head.Add(Ui.Lb(title, "pop-title"));
            box.Add(head);
            var b = Ui.El("pop-body");
            box.Add(b);
            d.Add(box);
            body = b;
            dim = d;
            return box;
        }

        static void Close(VisualElement dim) => dim.RemoveFromHierarchy();

        /// <summary>확인: 문장 + 버튼 둘 (왼쪽 보조, 오른쪽 주).</summary>
        public static void Confirm(string title, string text, string ok, Action onOk, string cancel = "취소")
        {
            var box = Frame(title, out var body, out var dim);
            body.Add(Ui.Lb(text, "pop-text"));
            var row = Ui.El("row", "pop-btns");
            row.Add(Ui.Btn(cancel, () => Close(dim), "btn"), Ui.Btn(ok, () => { Close(dim); onOk(); }, "btn", "pri"));
            box.Add(row);
            UiRoot.Current?.Overlay(dim);
        }

        /// <summary>부족: 필요한 것(아이콘이 위 테두리에 걸침) + 가진/필요 수 + 얻는 곳 목록. 막다른 길로 두지 않는다.</summary>
        public static void Lack(string item, Icon.Kind icon, string have, string need, (string where, Action go)[] sources)
        {
            var box = Frame($"{item} 부족", out var body, out var dim);
            var hero = Ui.Tile(null, icon, null, -1, null, 72);
            hero.AddToClassList("pop-hero");
            box.Insert(0, hero);
            var amount = Ui.El("row", "pop-amount");
            amount.Add(Ui.Lb(have, "pop-have"), Ui.Lb($" / {need}", "pop-need"));
            body.Add(amount);
            foreach (var s in sources)
            {
                var row = Ui.El("row", "pop-src");
                row.Add(Ui.Lb(s.where, "b", "grow"));
                var go = s.go;
                row.Add(Ui.Btn("바로 가기", () => { Close(dim); go?.Invoke(); }, "btn", "small"));
                body.Add(row);
            }
            var foot = Ui.El("row", "pop-btns");
            foot.Add(Ui.Btn("닫기", () => Close(dim), "btn"));
            box.Add(foot);
            UiRoot.Current?.Overlay(dim);
        }

        /// <summary>보상 획득 연출: 어둡게 → 가운데 빛줄기 회전 → 제목 → 타일이 하나씩 튀어나옴 → 누르면 닫힘.</summary>
        public static void Reward(string title, (string art, Icon.Kind icon, string count)[] items, Action closed = null)
        {
            var dim = Ui.El("modal", "reward");
            var rays = new Rays();
            dim.Add(rays);
            var t = Ui.Lb(title, "reward-title");
            dim.Add(t);
            var grid = Ui.El("reward-grid");
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var tile = Ui.Tile(it.art, it.icon, it.count, -1, null, 72);
                tile.style.opacity = 0;
                tile.style.scale = new Scale(new Vector3(0.4f, 0.4f, 1));
                tile.AddToClassList("pop-in");
                grid.Add(tile);
                int delay = 250 + i * 120;
                tile.schedule.Execute(() => { tile.style.opacity = 1; tile.style.scale = new Scale(Vector3.one); }).StartingIn(delay);
            }
            dim.Add(grid);
            dim.Add(Ui.Lb("터치하여 닫기", "reward-hint"));
            dim.RegisterCallback<ClickEvent>(_ => { Close(dim); closed?.Invoke(); });
            UiRoot.Current?.Overlay(dim);
        }

        /// <summary>천천히 도는 빛줄기 (Painter2D 부채꼴 12개).</summary>
        internal sealed class Rays : VisualElement
        {
            float angle;

            public Rays()
            {
                pickingMode = PickingMode.Ignore;
                AddToClassList("rays");
                generateVisualContent += Draw;
                schedule.Execute(() => { angle += 1.2f; MarkDirtyRepaint(); }).Every(33);
            }

            void Draw(MeshGenerationContext mgc)
            {
                var p = mgc.painter2D;
                var r = contentRect;
                var c = r.center;
                float len = Mathf.Max(r.width, r.height);
                p.fillColor = new Color(0.95f, 0.77f, 0.36f, 0.16f);
                for (int i = 0; i < 12; i++)
                {
                    float a0 = (angle + i * 30) * Mathf.Deg2Rad, a1 = a0 + 12 * Mathf.Deg2Rad;
                    p.BeginPath();
                    p.MoveTo(c);
                    p.LineTo(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * len);
                    p.LineTo(c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * len);
                    p.ClosePath();
                    p.Fill();
                }
            }
        }
    }
}
