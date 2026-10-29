using System;
using MartialTactics.Battle;
using MartialTactics.Game;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>UI Toolkit 요소를 짧게 만드는 도우미. 클래스 이름은 Resources/UI/App.uss에 있다.</summary>
    static class Ui
    {
        /// <summary>수치가 아직 정해지지 않은 자리.</summary>
        public const string N = "[n]";

        // 장비 (기획서 2026-10-29): 칸마다 주 스탯 하나, 등급 7개(신화는 확장용), 특수효과 7종은 영웅부터
        public static readonly string[] GearSlots = { "무기", "옷", "신발", "옥패" };
        public static readonly string[] GearStats = { "외공", "호신", "경공", "내공" };
        public static readonly string[] GearGrades = { "하품", "중품", "상품", "희귀", "명품", "영웅", "전설" };
        public static readonly string[] GearEffects = { "치명타율", "치명타 피해", "무공 피해 증가", "받는 피해 감소", "회피", "제어 적중", "제어 저항" };

        /// <summary>재화 칩: 아이콘 + 수량.</summary>
        public static VisualElement Cur(Icon.Kind icon, string amount, params string[] classes)
        {
            var e = El(classes);
            e.AddToClassList("cur");
            e.Add(new Icon(icon, 16));
            e.Add(Lb(amount, "cur-n"));
            return e;
        }

        /// <summary>제목 띠: 먹 바탕에 한지색 글자, 오른쪽 끝을 주홍으로 끊는다.</summary>
        public static VisualElement Band(string text, params string[] classes)
        {
            var e = El(classes);
            e.AddToClassList("band");
            e.Add(Lb(text, "band-t"));
            return e;
        }

        /// <summary>그림 배경을 깐 요소. 그림이 없으면 자리 표시.</summary>
        public static VisualElement ArtBox(string art, string placeholder, params string[] classes)
        {
            var tex = Art.Load("Art/" + art);
            if (tex == null) return Placeholder(placeholder, -1, -1, classes);
            var e = El(classes);
            e.AddToClassList("artbox");
            e.style.backgroundImage = new StyleBackground(tex);
            return e;
        }

        public static VisualElement El(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static Label Lb(string text, params string[] classes)
        {
            var l = new Label(text) { enableRichText = true };
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        public static Button Btn(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = text, enableRichText = true };
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        public static VisualElement Add(this VisualElement parent, params VisualElement[] children)
        {
            foreach (var c in children) parent.Add(c);
            return parent;
        }

        public static VisualElement Placeholder(string text, float w, float h, params string[] classes)
        {
            var e = El(classes);
            e.AddToClassList("ph");
            if (w > 0) e.style.width = w;
            if (h > 0) e.style.height = h;
            e.Add(Lb(text));
            return e;
        }

        /// <summary>아이콘만 있는 버튼.</summary>
        public static Button IconBtn(Icon.Kind kind, Action onClick, float size, params string[] classes)
        {
            var b = new Button(onClick);
            foreach (var c in classes) b.AddToClassList(c);
            b.Add(new Icon(kind, size));
            return b;
        }

        /// <summary>무인 초상. 그림이 없으면 이름을 적은 자리 표시. face = 얼굴 쪽을 확대해 자른다 (작은 칸용).</summary>
        public static VisualElement Portrait(string unitId, bool face, params string[] classes)
        {
            var e = El(classes);
            e.AddToClassList("portrait");
            var tex = Art.Portrait(unitId);
            if (tex != null)
            {
                e.style.backgroundImage = new StyleBackground(tex);
                e.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                e.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top, Length.Percent(face ? 12 : 0));
                e.style.backgroundSize = face
                    ? new BackgroundSize(Length.Percent(170), Length.Auto())
                    : new BackgroundSize(BackgroundSizeType.Cover);
            }
            else
            {
                e.AddToClassList("ph");
                if (!string.IsNullOrEmpty(unitId) && Catalog.Unit.TryGetValue(unitId, out var d)) e.Add(Lb(d.Name));
            }
            return e;
        }

        /// <summary>체력 막대. ratio 0~1.</summary>
        public static VisualElement Bar(float ratio, bool foe, float width = -1)
        {
            var bar = El("bar");
            if (width > 0) bar.style.width = width;
            var fill = El("fill");
            if (foe) fill.AddToClassList("foe");
            fill.style.width = Length.Percent(Math.Max(0, Math.Min(1, ratio)) * 100);
            bar.Add(fill);
            return bar;
        }

        public static void SetRatio(VisualElement bar, float ratio)
        {
            bar[0].style.width = Length.Percent(Math.Max(0, Math.Min(1, ratio)) * 100);
        }

        // 승급한 만큼 앞에서부터 붉은 별
        public static string Stars(string unitId, int promotions)
        {
            int n = GameState.Stars(unitId);
            string s = "";
            for (int i = 0; i < n; i++) s += i < promotions ? "<color=#b33a2c>★</color>" : "★";
            return s;
        }
    }
}
