using System;
using System.Collections.Generic;
using MartialTactics.Battle;
using MartialTactics.Game;
using UnityEngine;

namespace MartialTactics.App
{
    public enum LobbyTab { None, Units, Growth, Content, Gacha }
    public enum UnitsSub { List, LevelUp, Gear, Deck }

    /// <summary>
    /// 게임 화면. 씬에 아무것도 없어도 Play하면 스스로 생긴다. 모든 화면은 UI Toolkit(UiRoot).
    /// 로비(하단 바: 무인·성장·대장간·컨텐츠·뽑기) → 무인 상세 → 승급·추출 / 무공 고르기, 덱 칸 → 무인 고르기, 대전 → 전투 → 결과
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        enum Page { Lobby, UnitDetail, CardUse, SkillPick, DeckPick, BattleReady, BattlePlay, Result }

        static readonly Color Background = new Color(0.937f, 0.910f, 0.847f); // 한지 #efe8d8

        static GameApp instance;

        internal GameState Game { get; private set; }
        Page page;
        bool dirty;

        // 로비 상태
        internal LobbyTab Tab { get; private set; }
        internal UnitsSub Sub { get; private set; }
        internal int StarFilter;  // 무인 목록 등급 거르기 (0 = 전체)
        internal bool SettingsOpen, ConfirmReset;
        internal List<DrawResult> LastDraw = new List<DrawResult>();

        internal string UnitId;   // 무인 상세에서 보는 무인
        internal int Slot;        // 무공을 바꿀 칸 (0 = 2칸, 1 = 3칸)
        internal int DeckPos;     // 덱에서 바꿀 자리
        internal Team Opponent;
        BattleResult result;

        UiRoot ui;
        BattleScreen battle;

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
            Game = new GameState(SaveStore.Load(), Environment.TickCount);
            ui = new UiRoot(transform);
            dirty = true;
        }

        internal void Save() => SaveStore.Save(Game.Data);

        /// <summary>상태를 바꾼 뒤 지금 화면을 다시 그린다.</summary>
        internal void Refresh() => dirty = true;

        void Go(Page p)
        {
            page = p;
            dirty = true;
        }

        // --- 로비 ---

        internal void OpenTab(LobbyTab tab)
        {
            Tab = tab;
            if (tab != LobbyTab.Gacha) LastDraw.Clear();
            Go(Page.Lobby);
        }

        internal void OpenSub(UnitsSub sub)
        {
            Tab = LobbyTab.Units;
            Sub = sub;
            Go(Page.Lobby);
        }

        internal void CloseTab() => OpenTab(LobbyTab.None);

        internal void ResetSave()
        {
            SaveStore.Delete();
            Game = new GameState(new PlayerData(), Environment.TickCount);
            LastDraw.Clear();
            UnitId = null;
            Opponent = null;
            result = null;
            SettingsOpen = ConfirmReset = false;
            Tab = LobbyTab.None;
            Go(Page.Lobby);
        }

        // --- 무인 ---

        internal void OpenUnit(string id)
        {
            UnitId = id;
            Go(Page.UnitDetail);
        }

        internal void BackToUnits() => OpenSub(UnitsSub.List);
        internal void BackToDetail() => Go(Page.UnitDetail);

        internal void OpenDeckPick(int pos)
        {
            DeckPos = pos;
            Go(Page.DeckPick);
        }

        internal void BackToDeck() => OpenSub(UnitsSub.Deck);

        // --- 전투 ---

        internal void OpenBattleReady()
        {
            result = null;
            Go(Page.BattleReady);
        }

        internal void NewOpponent()
        {
            Opponent = Game.RandomOpponent();
            Refresh();
        }

        internal void StartBattle()
        {
            result = Game.Fight(Opponent);
            Go(Page.BattlePlay);
        }

        void Update()
        {
            if (dirty) Sync();
            if (page == Page.BattlePlay) battle?.Tick(Time.deltaTime);
        }

        void Sync()
        {
            dirty = false;
            battle = null;
            var u = Game.Find(UnitId);
            switch (page)
            {
                case Page.Lobby:
                    ui.Show(LobbyScreen.Build(this));
                    break;
                case Page.UnitDetail:
                    if (u == null) { BackToUnits(); return; }
                    ui.Show(UnitDetailScreen.Build(Game, u,
                        back: BackToUnits,
                        pickSkill: s => { Slot = s; Go(Page.SkillPick); },
                        cardUse: () => Go(Page.CardUse)));
                    break;
                case Page.CardUse:
                    if (u == null) { BackToUnits(); return; }
                    ui.Show(CardUseScreen.Build(this, u));
                    break;
                case Page.SkillPick:
                    if (u == null) { BackToUnits(); return; }
                    ui.Show(SkillPickScreen.Build(this, u, Slot));
                    break;
                case Page.DeckPick:
                    ui.Show(DeckPickScreen.Build(this));
                    break;
                case Page.BattleReady:
                    if (Opponent == null) Opponent = Game.RandomOpponent();
                    ui.Show(BattleReadyScreen.Build(this));
                    break;
                case Page.BattlePlay:
                    battle = new BattleScreen(result, () => Go(Page.Result));
                    ui.Show(battle.Root);
                    break;
                case Page.Result:
                    ui.Show(ResultScreen.Build(result, Game.Data.Deck.Element, Opponent.Element,
                        replay: () => Go(Page.BattlePlay),
                        confirm: OpenBattleReady));
                    break;
            }
        }
    }
}
