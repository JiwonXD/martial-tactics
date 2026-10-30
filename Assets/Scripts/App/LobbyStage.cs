using System;
using System.Collections.Generic;
using System.Linq;
using MartialTactics.Battle;
using MartialTactics.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>
    /// 로비 무대: 덱 3명이 쉬지 않고 자동 대전을 하는 모습 (크럼블·캣히어로 로비처럼 늘 돌아가는 전투).
    /// 방치 스테이지 규칙이 아직 없어 상대는 대전과 같은 임시 무작위 편성이고, 결과는 저장하지 않는다.
    /// 전투는 미리 끝까지 계산해 두고 기록 한 줄씩 체력과 데미지 숫자를 보여 준다.
    /// </summary>
    sealed class LobbyStage
    {
        const float LineDelay = 0.35f;

        public readonly VisualElement Root;

        readonly GameState game;
        BattleResult result;
        int shown;
        float wait;
        readonly List<(VisualElement face, VisualElement bar)> views = new List<(VisualElement, VisualElement)>();
        readonly Floaters floaters = new Floaters();

        public LobbyStage(GameState game)
        {
            this.game = game;
            Root = Ui.El("lstage");
            if (game.ValidateDeck().Count > 0)
            {
                Root.Add(Ui.Lb("덱 편성", "lstage-empty"));
                return;
            }
            NewFight();
        }

        void NewFight()
        {
            result = game.Fight(game.RandomOpponent());
            shown = -1;
            wait = 0.6f;
            Root.Clear();
            views.Clear();
            var foe = Row(Side.B);
            var ally = Row(Side.A);
            foe.AddToClassList("foe");
            Root.Add(foe, ally);
        }

        VisualElement Row(Side side)
        {
            var row = Ui.El("ls-row");
            foreach (int pos in new[] { 0, 1, 2 }) // 왼쪽부터 주장 · 부장 · 부장
            {
                int i = result.Units.FindIndex(u => u.Side == side && u.Pos == pos);
                if (i < 0) continue;
                var u = result.Units[i];
                var col = Ui.El("ls-u");
                if (pos == 0) col.AddToClassList("leader");
                var face = Ui.Portrait(u.Source.Id, true, "ls-face");
                face.Add(Skin.CardFrame(pos == 0));
                var bar = Ui.Bar(1, side == Side.B);
                col.Add(face, bar);
                row.Add(col);
                while (views.Count <= i) views.Add((null, null));
                views[i] = (face, bar);
            }
            return row;
        }

        public void Tick(float dt)
        {
            floaters.Tick(dt);
            if (result == null) return;
            wait -= dt;
            if (wait > 0) return;
            if (shown >= result.Log.Count - 1)
            {
                NewFight();
                return;
            }
            shown++;
            wait = LineDelay;
            var line = result.Log[shown];
            var prev = shown > 0 ? result.Log[shown - 1].Hp : result.Units.Select(u => u.MaxHp).ToArray();
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                if (v.face == null) continue;
                Ui.SetRatio(v.bar, (float)line.Hp[i] / result.Units[i].MaxHp);
                v.face.parent.EnableInClassList("dead", line.Hp[i] <= 0);
                int delta = line.Hp[i] - prev[i];
                if (delta != 0) floaters.Spawn(v.face, delta);
            }
            if (shown >= result.Log.Count - 1) wait = 1.5f;
        }
    }

    /// <summary>데미지·회복 숫자를 초상 위에 띄워 올리며 사라지게 한다 (로비 무대·전투 공통).</summary>
    sealed class Floaters
    {
        const float Life = 0.9f;
        readonly List<(Label label, float t)> items = new List<(Label, float)>();

        public void Spawn(VisualElement over, int delta)
        {
            var l = Ui.Lb(delta > 0 ? $"+{delta}" : $"{-delta}", "dmg");
            if (delta > 0) l.AddToClassList("heal");
            l.pickingMode = PickingMode.Ignore;
            l.style.left = UnityEngine.Random.Range(-6f, 18f);
            over.Add(l);
            items.Add((l, 0));
        }

        public void Tick(float dt)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var (l, t) = items[i];
                t += dt;
                if (t >= Life || l.panel == null)
                {
                    l.RemoveFromHierarchy();
                    items.RemoveAt(i);
                    continue;
                }
                items[i] = (l, t);
                float k = t / Life;
                l.style.translate = new Translate(0, -28 * k);
                l.style.opacity = k < 0.6f ? 1 : 1 - (k - 0.6f) / 0.4f;
                float s = k < 0.15f ? 1.4f - k * 2.6f : 1;
                l.style.scale = new Scale(new Vector3(s, s, 1));
            }
        }
    }
}
