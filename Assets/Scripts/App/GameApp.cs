using System;
using System.Collections.Generic;
using System.Linq;
using MartialTactics.Battle;
using MartialTactics.Game;
using UnityEngine;

namespace MartialTactics.App
{
    /// <summary>
    /// 게임 화면. 씬에 아무것도 없어도 Play하면 스스로 생긴다.
    /// 화면: 처음 → 뽑기 / 무인 목록 → 무인 상세 → 무공 고르기·승급/추출 / 덱 편성 → 무인 고르기 / 전투 준비 → 전투 → 결과
    /// 확정된 화면(무인 상세, 전투, 결과)은 UI Toolkit(UiRoot), 나머지는 아직 기능 확인용 IMGUI.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        enum Page { Home, Gacha, Units, UnitDetail, SkillPick, CardUse, Deck, DeckPick, Battle, BattlePlay, Result }

        const string Gold = "#e8c050", Red = "#e05050", Green = "#7fd07f", Gray = "#9a948a";
        static readonly Color Background = new Color(0.12f, 0.11f, 0.10f);

        static GameApp instance;

        GameState game;
        Page page;
        Vector2 scroll;
        bool confirmReset;

        string unitId;  // 무인 창에서 보는 무인
        int slot;       // 무공을 바꿀 칸 (0 = 2칸, 1 = 3칸)
        int deckPos;    // 덱에서 바꿀 자리
        List<DrawResult> lastDraw = new List<DrawResult>();
        Team opponent;
        BattleResult result;

        UiRoot ui;
        BattleScreen battle;
        bool uiDirty;

        GUIStyle label, small, title, button;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Background;
            }
            var go = new GameObject("GameApp");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameApp>();
        }

        void Awake()
        {
            game = new GameState(SaveStore.Load(), Environment.TickCount);
            ui = new UiRoot(transform);
        }

        void OnApplicationQuit() => ui.Shutdown();

        void Save() => SaveStore.Save(game.Data);

        // 누르면 상태를 바꾸고 이번 GUI 이벤트를 끝낸다 (같은 이벤트 안에서 화면 구성이 바뀌어 IMGUI 레이아웃이 어긋나지 않게)
        void Button(string text, Action onClick, params GUILayoutOption[] options)
        {
            if (!GUILayout.Button(text, button, options)) return;
            onClick();
            GUIUtility.ExitGUI();
        }

        void Go(Page p)
        {
            page = p;
            scroll = Vector2.zero;
            confirmReset = false;
            uiDirty = true;
        }

        void Update()
        {
            if (uiDirty) SyncUi();
            if (page == Page.BattlePlay) battle?.Tick(Time.deltaTime);
        }

        // 지금 페이지가 UI Toolkit 화면이면 새로 만들어 띄우고, 아니면 내린다
        void SyncUi()
        {
            uiDirty = false;
            battle = null;
            switch (page)
            {
                case Page.UnitDetail:
                    var u = game.Find(unitId);
                    if (u == null) { ui.Hide(); return; }
                    ui.Show(UnitDetailScreen.Build(game, u,
                        back: () => Go(Page.Units),
                        pickSkill: s => { slot = s; Go(Page.SkillPick); },
                        cardUse: () => Go(Page.CardUse)));
                    break;
                case Page.BattlePlay:
                    battle = new BattleScreen(result, () => Go(Page.Result));
                    ui.Show(battle.Root);
                    break;
                case Page.Result:
                    ui.Show(ResultScreen.Build(result, game.Data.Deck.Element, opponent.Element,
                        replay: () => Go(Page.BattlePlay),
                        confirm: () => { result = null; Go(Page.Battle); }));
                    break;
                default:
                    ui.Hide();
                    break;
            }
        }

        void OnGUI()
        {
            if (ui.Visible || uiDirty) return;
            EnsureStyles();
            float scale = Mathf.Min(Screen.width / 540f, Screen.height / 960f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float vw = Screen.width / scale, vh = Screen.height / scale, w = Mathf.Min(vw - 16, 640);

            // 씬 배경(하늘 등) 위에서도 글씨가 읽히게 화면 전체를 어둡게 깐다
            GUI.color = Background;
            GUI.DrawTexture(new Rect(0, 0, vw, vh), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect((vw - w) / 2, 8, w, vh - 16));
            if (page != Page.Home)
            {
                GUILayout.BeginHorizontal();
                Button("← 처음", () => Go(Page.Home), GUILayout.Width(110));
                GUILayout.Label(PageTitle(), title);
                GUILayout.EndHorizontal();
            }
            scroll = GUILayout.BeginScrollView(scroll);
            switch (page)
            {
                case Page.Home: HomePage(); break;
                case Page.Gacha: GachaPage(); break;
                case Page.Units: UnitsPage(); break;
                case Page.SkillPick: SkillPickPage(); break;
                case Page.CardUse: CardUsePage(); break;
                case Page.Deck: DeckPage(); break;
                case Page.DeckPick: DeckPickPage(); break;
                case Page.Battle: BattleReady(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        string PageTitle()
        {
            switch (page)
            {
                case Page.Gacha: return "뽑기";
                case Page.Units: return "무인";
                case Page.SkillPick: return $"무공 고르기 ({slot + 2}칸)";
                case Page.CardUse: return "승급 · 무공 추출";
                case Page.Deck: return "덱 편성";
                case Page.DeckPick: return $"무인 고르기 ({BattleRules.PosNames[deckPos]})";
                case Page.Battle: return "전투";
                default: return "";
            }
        }

        // --- 처음 ---

        void HomePage()
        {
            GUILayout.Label("무협 카드배틀", title);
            GUILayout.Label($"보유 무인 {game.Data.Units.Count}/{Catalog.Units.Count} · 추출 무공 {game.Data.Skills.Count}개 · 뽑기 {game.Data.Draws}회", small);
            GUILayout.Space(12);
            Button("뽑기", () => Go(Page.Gacha));
            Button("무인", () => Go(Page.Units));
            Button("덱 편성", () => Go(Page.Deck));
            Button("전투", () => { result = null; Go(Page.Battle); });
            GUILayout.Space(40);
            if (!confirmReset)
            {
                Button("저장 초기화", () => confirmReset = true);
            }
            else
            {
                GUILayout.Label($"<color={Red}>보유 무인, 무공, 덱이 모두 지워집니다.</color>", label);
                GUILayout.BeginHorizontal();
                Button("초기화", () =>
                {
                    SaveStore.Delete();
                    game = new GameState(new PlayerData(), Environment.TickCount);
                    lastDraw.Clear();
                    unitId = null;
                    opponent = null;
                    result = null;
                    confirmReset = false;
                });
                Button("취소", () => confirmReset = false);
                GUILayout.EndHorizontal();
            }
        }

        // --- 뽑기 ---

        void GachaPage()
        {
            GUILayout.Label("테스트 기간: 뽑기 무료", small);
            GUILayout.BeginHorizontal();
            Button("1회 뽑기", () => { lastDraw = game.Draw(1); Save(); });
            Button("10회 뽑기", () => { lastDraw = game.Draw(10); Save(); });
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            foreach (var r in lastDraw)
            {
                var d = Catalog.Unit[r.Unit];
                string what = r.New ? $"<color={Gold}>새 무인</color>" : $"<color={Gray}>중복 카드 +1</color>";
                GUILayout.Label($"{Stars(r.Unit, 0)} {d.Name} ({d.Title}) — {what}", label);
            }
        }

        // --- 무인 목록·무인 창 ---

        void UnitsPage()
        {
            if (game.Data.Units.Count == 0) GUILayout.Label("보유한 무인이 없습니다. 뽑기에서 무인을 얻으세요.", label);
            foreach (var u in game.Data.Units)
            {
                var d = Catalog.Unit[u.Id];
                string spare = u.Spare > 0 ? $"  <color={Gold}>중복 {u.Spare}</color>" : "";
                Button($"{Stars(u.Id, u.Promotions)} {d.Name}  <color={Gray}>{d.Title} · {d.Faction}</color>{spare}", () =>
                {
                    unitId = u.Id;
                    Go(Page.UnitDetail);
                });
            }
        }

        // 무인 상세의 "승급 / 무공 추출"과 "배분" 버튼이 오는 곳. 화면 설계가 아직 없어 기능 확인용으로 둔다
        void CardUsePage()
        {
            var u = game.Find(unitId);
            if (u == null) return;
            var d = Catalog.Unit[u.Id];
            int stars = GameState.Stars(u.Id);
            Button("← 무인 상세", () => Go(Page.UnitDetail));

            GUILayout.Label($"{Stars(u.Id, u.Promotions)}  <b>{d.Name}</b>  승급 {u.Promotions}/{stars}", label);
            int[] baseStats = { d.Ext, d.Int, d.Def, d.Spd };
            for (int k = 0; k < 4; k++)
            {
                GUILayout.BeginHorizontal();
                string added = u.Added[k] > 0 ? $" <color={Green}>+{u.Added[k]}</color>" : "";
                GUILayout.Label($"{GameState.StatNames[k]} {baseStats[k]}{added}", label);
                if (u.FreePoints > 0) Button("+1", () => { game.AddStat(u, (StatKind)k); Save(); }, GUILayout.Width(70));
                GUILayout.EndHorizontal();
            }
            if (u.FreePoints > 0) GUILayout.Label($"<color={Gold}>남은 자유 스탯 {u.FreePoints}</color>", label);

            GUILayout.Space(12);
            GUILayout.Label($"<b>중복 카드 {u.Spare}장</b> — 승급 또는 무공 추출에 씁니다", label);
            GUI.enabled = game.CanPromote(u);
            Button($"승급 (카드 1장, 자유 스탯 +{GameState.PointsPerPromotion})", () => { game.Promote(u); Save(); });
            GUI.enabled = u.Spare > 0;
            Button($"무공 추출: {Catalog.Skill[d.Skill].Name} (카드 1장 소모)", () => { game.Extract(u); Save(); });
            GUI.enabled = true;
            if (u.Promotions >= stars) GUILayout.Label($"<color={Gray}>최대 승급</color>", small);
        }

        void SkillPickPage()
        {
            var u = game.Find(unitId);
            if (u == null) return;
            Button("← 무인 상세", () => Go(Page.UnitDetail));
            if (game.FindSkill(u.Equipped[slot]) != null)
                Button("이 칸 비우기", () => { game.Unequip(u, slot); Save(); Go(Page.UnitDetail); });
            if (game.Data.Skills.Count == 0) GUILayout.Label("추출한 무공이 없습니다. 무인 상세에서 중복 카드로 무공을 추출하세요.", label);
            foreach (var s in game.Data.Skills)
            {
                var def = Catalog.Skill[s.Skill];
                var holder = game.EquippedBy(s.Uid);
                string err = game.EquipError(u, slot, s);
                string held = holder != null ? $"  <color={Gray}>{Catalog.Unit[holder.Id].Name} 장착 중</color>" : "";
                GUI.enabled = err == null;
                Button($"{def.Name} ({SkillTag(def)}){held}", () =>
                {
                    game.Equip(u, slot, s);
                    Save();
                    Go(Page.UnitDetail);
                });
                GUI.enabled = true;
                GUILayout.Label(err != null ? $"<color={Red}>{err}</color>" : def.Desc, small);
            }
        }

        static string SkillTag(SkillDef s) => BattleRules.TypeNames[s.Type] + (s.Rate > 0 ? $" {s.Rate}%" : "");

        // --- 덱 편성 ---

        void DeckPage()
        {
            var deck = game.Data.Deck;
            GUILayout.Label("<b>속성</b>", label);
            GUILayout.BeginHorizontal();
            foreach (var e in BattleRules.Elements)
                Button(deck.Element == e ? $"<color={Gold}>[{e}]</color>" : e, () => { deck.Element = e; Save(); });
            GUILayout.EndHorizontal();

            for (int pos = 0; pos < 3; pos++)
            {
                GUILayout.Space(8);
                var u = game.Find(deck.Units[pos]);
                GUILayout.Label($"<b>{BattleRules.PosNames[pos]}</b>", label);
                if (u != null)
                {
                    var d = Catalog.Unit[u.Id];
                    var skills = new[] { d.Skill }.Concat(u.Equipped.Select(uid => game.FindSkill(uid)?.Skill).Where(id => id != null));
                    GUILayout.Label($"{Stars(u.Id, u.Promotions)} {d.Name} · {d.Faction} · 적성 {Apt(d)}", label);
                    GUILayout.Label(string.Join(", ", skills.Select(id => Catalog.Skill[id].Name)), small);
                }
                else GUILayout.Label($"<color={Gray}>비어 있음</color>", label);
                GUILayout.BeginHorizontal();
                Button(u == null ? "넣기" : "바꾸기", () => { deckPos = pos; Go(Page.DeckPick); });
                if (u != null) Button("빼기", () => { deck.Units[pos] = ""; Save(); });
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(12);
            var errors = game.ValidateDeck();
            if (errors.Count == 0) GUILayout.Label($"<color={Green}>편성 완료</color>", label);
            foreach (var e in errors) GUILayout.Label($"<color={Red}>{e}</color>", label);
        }

        void DeckPickPage()
        {
            Button("← 덱 편성", () => Go(Page.Deck));
            if (game.Data.Units.Count == 0) GUILayout.Label("보유한 무인이 없습니다. 뽑기에서 무인을 얻으세요.", label);
            var deck = game.Data.Deck;
            foreach (var u in game.Data.Units)
            {
                var d = Catalog.Unit[u.Id];
                int at = Array.IndexOf(deck.Units, u.Id);
                string where = at >= 0 ? $"  <color={Gray}>{BattleRules.PosNames[at]}</color>" : "";
                Button($"{Stars(u.Id, u.Promotions)} {d.Name}  <color={Gray}>{d.Faction} · 적성 {Apt(d)}</color>{where}", () =>
                {
                    game.SetDeckUnit(deckPos, u.Id);
                    Save();
                    Go(Page.Deck);
                });
            }
        }

        string Apt(UnitDef d)
        {
            int ei = Array.IndexOf(BattleRules.Elements, game.Data.Deck.Element);
            return ei >= 0 ? d.Apt[ei].ToString() : "-";
        }

        // --- 전투 ---

        void BattleReady()
        {
            var errors = game.ValidateDeck();
            if (errors.Count > 0)
            {
                foreach (var e in errors) GUILayout.Label($"<color={Red}>{e}</color>", label);
                Button("덱 편성으로", () => Go(Page.Deck));
                return;
            }
            if (opponent == null) opponent = game.RandomOpponent();
            GUILayout.Label("<b>아군</b>", label);
            TeamLines(game.BuildTeam());
            GUILayout.Space(8);
            GUILayout.Label("<b>상대</b> (임시: 무작위 편성)", label);
            TeamLines(opponent);
            GUILayout.Space(12);
            Button("전투 시작", () =>
            {
                result = game.Fight(opponent);
                Go(Page.BattlePlay);
            });
            Button("새 상대", () => opponent = null);
        }

        void TeamLines(Team team)
        {
            GUILayout.Label($"속성 {team.Element}", small);
            for (int i = 0; i < team.Slots.Length; i++)
            {
                var s = team.Slots[i];
                var d = Catalog.Unit[s.Unit];
                var skills = new[] { d.Skill }.Concat(s.Skills.Where(id => !string.IsNullOrEmpty(id))).Select(id => Catalog.Skill[id].Name);
                GUILayout.Label($"{BattleRules.PosNames[i]} {d.Name} — {string.Join(", ", skills)}", small);
            }
        }

        // --- 표시 ---

        // 승급한 만큼 앞에서부터 붉은 별
        static string Stars(string unitId, int promotions)
        {
            int n = GameState.Stars(unitId);
            return string.Concat(Enumerable.Range(0, n).Select(i => $"<color={(i < promotions ? Red : Gold)}>★</color>"));
        }

        void EnsureStyles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true, richText = true };
            small = new GUIStyle(label) { fontSize = 16 };
            title = new GUIStyle(label) { fontSize = 26 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 20, richText = true, wordWrap = true, fixedHeight = 0, padding = new RectOffset(12, 12, 10, 10) };
        }
    }
}
