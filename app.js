// 웹게임 화면. Unity Assets/Scripts/App/ (GameApp · LobbyScreen · *Screen.cs)를 그대로 옮김.
// 로비(하단 바: 무인 · 성장 · 대장간 · 컨텐츠 · 뽑기) → 무인 상세 → 승급·추출 / 무공 고르기, 덱 편성, 대전 → 전투 → 결과.
// 수치가 아직 정해지지 않은 곳은 [n]. 진행 상황은 브라우저 localStorage에 저장.
(function () {
  'use strict';
  const S = window.WuxiaSim, G = window.WuxiaGame, icon = window.WuxiaIcon;
  const N = '[n]';
  const SAVE_KEY = 'martial-tactics/save';

  // 장비 (기획서 2026-10-29): 칸마다 주 스탯 하나, 등급 7개(신화는 확장용), 특수효과 7종은 영웅부터
  const GEAR_SLOTS = ['무기', '옷', '신발', '옥패'];
  const GEAR_STATS = ['외공', '호신', '경공', '내공'];
  const GEAR_GRADES = ['하품', '중품', '상품', '희귀', '명품', '영웅', '전설'];
  const GEAR_EFFECTS = ['치명타율', '치명타 피해', '무공 피해 증가', '받는 피해 감소', '회피', '제어 적중', '제어 저항'];
  const APT_ORDER = 'SABCD';

  // ---------- DOM 도우미 ----------
  function el(cls, ...kids) {
    const e = document.createElement('div');
    if (cls) e.className = cls;
    for (const k of kids.flat()) if (k != null && k !== false) e.append(k);
    return e;
  }
  function lb(text, cls) {
    const e = document.createElement('div');
    e.className = cls || '';
    e.textContent = text;
    return e;
  }
  function btn(content, onClick, cls) {
    const b = document.createElement('button');
    b.type = 'button';
    b.className = cls || '';
    if (typeof content === 'string') b.textContent = content;
    else if (content) b.append(...[content].flat());
    if (onClick) b.addEventListener('click', e => { e.stopPropagation(); onClick(e); });
    return b;
  }
  const iconBtn = (kind, onClick, size, cls) => btn(icon(kind, size), onClick, cls);
  const tap = (e, fn) => { e.style.cursor = 'pointer'; e.addEventListener('click', ev => { ev.stopPropagation(); fn(ev); }); return e; };

  // 그림 파일이 아직 없어 초상은 이름을 적은 자리 표시 (Unity도 그림이 없으면 같음)
  function portrait(unitId, cls) {
    const e = el('portrait ph ' + (cls || ''));
    if (unitId && S.UNIT[unitId]) e.append(lb(S.UNIT[unitId].name, 'ph-t'));
    return e;
  }
  const cardFrame = gold => el('cardedge' + (gold ? ' gold' : ''));
  function bar(ratio, foe) {
    const f = el('fill' + (foe ? ' foe' : ''));
    f.style.width = Math.max(0, Math.min(1, ratio)) * 100 + '%';
    return el('bar', f);
  }
  const setRatio = (b, r) => { b.firstChild.style.width = Math.max(0, Math.min(1, r)) * 100 + '%'; };
  // 승급한 만큼 앞에서부터 붉은 별
  function starsEl(unitId, promotions, cls) {
    const e = el(cls || 'sr');
    const n = G.stars(unitId);
    for (let i = 0; i < n; i++) { const s = document.createElement('span'); s.textContent = '★'; if (i < promotions) s.className = 'p'; e.append(s); }
    return e;
  }
  function cur(kind, amount) { return el('cur', icon(kind, 20), lb(amount, 'cur-n')); }
  function tile(kind, count, badge, size) {
    const s = size || 64;
    const t = el('tile-i', el('tile-gloss'), icon(kind, Math.round(s * 0.5)));
    t.style.width = t.style.height = s + 'px';
    if (count != null) t.append(lb(count, 'tile-count'));
    if (badge != null) t.append(lb(badge, 'tile-badge'));
    return t;
  }
  function costBtn(verb, kind, cost, pri, onClick) {
    return btn([lb(verb, 'cb-verb'), el('row cb-cost', icon(kind, 18), lb(cost, 'cb-n'))], onClick, 'btn costbtn' + (pri ? ' pri' : ''));
  }
  const band = (text, cls) => el('band ' + (cls || ''), lb(text, 'band-t'));
  const scroll = (cls) => el('pscroll ' + (cls || ''));
  const after = (ms, fn) => setTimeout(fn, ms);

  // ---------- 저장 ----------
  function load() {
    try { const raw = localStorage.getItem(SAVE_KEY); if (raw) return JSON.parse(raw); } catch (e) { /* 손상된 저장은 새로 시작 */ }
    return G.newData();
  }

  // ---------- 앱 상태 ----------
  const app = {
    game: new G.GameState(load()),
    page: 'lobby', tab: null, sub: 'list', content: 'match', starFilter: 0, sheet: null,
    unitId: null, slot: 0, deckPos: 0, opponent: null, result: null, lastDraw: [],
    keepGrade: 3, effectsOpen: false, gradeOpen: false, countOpen: false, effects: new Set(), researchNode: 0,
    equipSlot: 0, equipFromDetail: false,
    battle: null, stage: null,
  };
  const root = document.getElementById('app');

  function save() { try { localStorage.setItem(SAVE_KEY, JSON.stringify(app.game.data)); } catch (e) { toast('저장하지 못했습니다'); } }
  function go(page) { app.page = page; render(); }
  function openTab(tab) { app.tab = tab; if (tab !== 'gacha') app.lastDraw = []; go('lobby'); }
  function openSub(sub) { app.tab = 'units'; app.sub = sub; go('lobby'); }
  const closeTab = () => openTab(null);
  function openContent(c) { app.tab = 'content'; app.content = c; go('lobby'); }
  function openSheet(s) { app.sheet = s; render(); }
  const backToLobby = () => go('lobby');
  function openUnit(id) { app.unitId = id; go('unitDetail'); }
  const backToUnits = () => openSub('list');
  const backToDetail = () => go('unitDetail');
  const backToDeck = () => openSub('deck');
  function openDeckPick(pos) { app.deckPos = pos; go('deckPick'); }
  function openBattleReady() { app.result = null; go('battleReady'); }
  function newOpponent() { app.opponent = app.game.randomOpponent(); render(); }
  function startBattle() { app.result = app.game.fight(app.opponent); go('battlePlay'); }
  function openForgeResult() { app.tab = null; app.sheet = 'forgeResult'; go('lobby'); }
  function openForgeLevel() { app.sheet = null; go('forgeLevel'); }
  function openEquipSwap(unitId, slot, fromDetail) { app.unitId = unitId; app.equipSlot = slot; app.equipFromDetail = fromDetail; go('equipSwap'); }
  function backFromEquip() { if (app.equipFromDetail) go('unitDetail'); else openSub('gear'); }
  function resetSave() {
    try { localStorage.removeItem(SAVE_KEY); } catch (e) { /* 무시 */ }
    app.game = new G.GameState(G.newData());
    Object.assign(app, { lastDraw: [], unitId: null, opponent: null, result: null, sheet: null, tab: null, deckPos: 0 });
    go('lobby');
  }
  const refresh = () => render();
  const commit = () => { save(); render(); };

  // ---------- 겹쳐 띄우기 ----------
  function toast(text) {
    const t = el('toast', lb(text, 'toast-t'));
    root.append(t);
    after(1500, () => t.remove());
  }

  function popupFrame(title) {
    const dim = el('modal popup');
    const box = el('pop-box', el('pop-head', lb(title, 'pop-title')));
    const body = el('pop-body');
    box.append(body);
    dim.append(box);
    return { dim, box, body };
  }
  function confirmPopup(title, text, ok, onOk, cancel) {
    const { dim, box, body } = popupFrame(title);
    body.append(lb(text, 'pop-text'));
    box.append(el('pop-btns', btn(cancel || '취소', () => dim.remove(), 'btn'), btn(ok, () => { dim.remove(); onOk(); }, 'btn pri')));
    root.append(dim);
  }
  // 부족: 필요한 것(아이콘이 위 테두리에 걸침) + 가진/필요 수 + 얻는 곳 목록
  function lackPopup(item, kind, have, need, sources) {
    const { dim, box, body } = popupFrame(item + ' 부족');
    const hero = tile(kind, null, null, 72);
    hero.classList.add('pop-hero');
    box.prepend(hero);
    body.append(el('row pop-amount', lb(have, 'pop-have'), lb(' / ' + need, 'pop-need')));
    for (const [where, goTo] of sources)
      body.append(el('row pop-src', lb(where, 'b grow'), btn('바로 가기', () => { dim.remove(); goTo && goTo(); }, 'btn small')));
    box.append(el('pop-btns', btn('닫기', () => dim.remove(), 'btn')));
    root.append(dim);
  }
  // 보상 획득 연출: 어둡게 → 빛줄기 회전 → 제목 → 타일이 하나씩 튀어나옴 → 누르면 닫힘
  function rewardPopup(title, items) {
    const grid = el('reward-grid');
    items.forEach(([kind, count], i) => {
      const t = tile(kind, count, null, 72);
      grid.append(t);
      after(250 + i * 120, () => t.classList.add('in'));
    });
    const dim = el('modal reward', el('rays'), lb(title, 'reward-title'), grid, lb('터치하여 닫기', 'reward-hint'));
    dim.addEventListener('click', () => dim.remove());
    root.append(dim);
  }

  // ---------- 공통 틀 ----------
  const TABS = [['units', 'units', '무인'], ['growth', 'growth', '성장'], [null, 'close', ''], ['content', 'content', '컨텐츠'], ['gacha', 'gacha', '뽑기']];
  const SUBS = [['list', '목록'], ['levelUp', '레벨업'], ['gear', '장비'], ['deck', '덱 편성']];

  // 하단 바 가운데 칸 (크럼블): 로비 = 대장간, 메뉴 1단 = 닫기 X, 그 안으로 더 들어가면 = 한 단계 뒤로 ↩
  function navBar(center, back) {
    const nav = el('nav');
    for (const [tab, kind, name] of TABS) {
      if (tab === null) {
        const c = el('tb center');
        if (center === 'close') c.append(iconBtn('close', closeTab, 26, 'x'));
        else if (center === 'back') c.append(iconBtn('back', back, 26, 'x back-x'));
        nav.append(c);
        continue;
      }
      const on = app.tab === tab && app.page === 'lobby';
      const name_ = lb(name, 'tbn');
      if (!on) name_.style.visibility = 'hidden';
      nav.append(btn([el('medal', icon(kind, 40)), name_], () => openTab(tab), 'tb' + (on ? ' on' : '')));
    }
    return nav;
  }

  // 위 띠 (모든 화면 같음): 왼쪽 프로필 묶음(주장 얼굴 · Lv · 전투력), 오른쪽 재화와 설정
  function topBand() {
    const lead = app.game.data.deck.units[0];
    return el('ltop',
      el('profile', portrait(app.game.find(lead) ? lead : null, 'avatar'), el('pinfo', lb('Lv ' + N, 'plv'), lb('전투력 ' + N, 'ppow'))),
      el('grow'),
      el('curs', cur('book', N), cur('coin', N), cur('ticket', N)),
      iconBtn('gear', () => openSheet('settings'), 20, 'sq'));
  }
  const titleBand = title => el('title-band', lb(title, 'tb-title'));

  // 메뉴 안쪽 화면을 위 띠 + 제목 띠 + 내용 + 하단 바(가운데 ↩)로 감싼다
  function shell(title, content, back) {
    const s = el('screen shell', topBand());
    if (title) s.append(titleBand(title));
    content.classList.add('shell-body');
    s.append(content, navBar('back', back));
    if (app.sheet === 'settings') s.append(settingsSheet());
    return s;
  }

  function sheet(title, bottom) {
    const dim = el('modal' + (bottom ? ' bottom' : ''));
    dim.addEventListener('click', e => { if (e.target === dim) openSheet(null); });
    const body = el('sheet-body');
    const sh = el('sheet', el('sheet-head', lb(title, 'sheet-title'), iconBtn('close', () => openSheet(null), 18, 'sheet-x')), body);
    dim.append(sh);
    return { dim, body };
  }

  function settingsSheet() {
    const { dim, body } = sheet('설정');
    body.append(btn('저장 초기화', () => confirmPopup('저장 초기화', '보유 무인 · 무공 · 덱 삭제', '초기화', resetSave), 'btn'));
    body.append(lb('진행 상황은 이 브라우저에만 저장됩니다.', 'desc center-t'));
    return dim;
  }

  // ---------- 로비 ----------
  function lobby() {
    const s = el('screen lobby');
    if (!app.tab) { s.append(idle(), forgePanel()); }
    else panel(s);
    s.append(navBar(app.tab ? 'close' : 'forge'));
    if (!app.tab) s.append(forgeObject());
    if (app.sheet === 'settings') s.append(settingsSheet());
    if (app.sheet === 'idleReward') s.append(idleRewardSheet());
    if (app.sheet === 'forgeAuto') s.append(forgeAutoSheet());
    if (app.sheet === 'forgeResult') s.append(forgeResultSheet());
    return s;
  }

  function idle() {
    const stage = el('stage', topBand());
    // 무대 위 HUD 띠: 단계 진행 + 단계 도전. 상대는 아직 임시(무작위)라 대전 준비로 이어 둔다
    const sb = bar(0.6, false); sb.classList.add('sh-bar');
    stage.append(el('stage-hud', el('sh-prog', lb(N + '단계', 'sh-stage'), sb), btn(lb('단계 도전', 'ch-t'), openBattleReady, 'challenge')));
    app.stage = new LobbyStage();
    stage.append(app.stage.root);
    const reward = btn([icon('chest', 48), el('dot')], () => openSheet('idleReward'), 'round-btn');
    stage.append(el('side-col', reward));
    return stage;
  }

  // 대장간 패널 (크럼블 오븐 패널 자리): 가운데 대장간, 왼쪽 주장, 오른쪽 부장 둘
  function forgePanel() {
    return el('forge-panel', el('fp-side', deckCard(0)), el('fp-mid'), el('fp-side', deckCard(1), deckCard(2)));
  }

  // 대장간: 누르면 한 번 뽑기, 위 Lv = 레벨 창, 오른쪽 위 = 남은 철광석, 왼쪽 = 쌓인 장비, 왼쪽 아래 Auto = 자동 뽑기.
  // 장비 데이터가 아직 없어 뽑기는 아무것도 하지 않는다
  function forgeObject() {
    const box = el('forge-box',
      btn(el('forge-disc', icon('forge', 64)), () => toast('장비 데이터 준비 중'), 'forge-art'),
      btn('Lv ' + N, openForgeLevel, 'forge-lv'),
      el('forge-ore', icon('ore', 16), lb(N, 'fo-n')),
      btn(N + '개', openForgeResult, 'forge-stack'),
      btn(lb('Auto', 'fa-t'), () => openSheet('forgeAuto'), 'forge-auto'));
    return el('forge-wrap', box);
  }

  function deckCard(pos) {
    const u = app.game.find(app.game.data.deck.units[pos]);
    const col = el('pt' + (pos === 0 ? ' leader' : ''));
    let card;
    if (u) {
      card = portrait(u.id, 'pcard');
      card.append(lb('Lv ' + N, 'lv'), cardFrame(pos === 0));
      col.append(card, bar(1, false));
    } else {
      card = el('pcard empty', icon('plus', 24));
      col.append(card);
    }
    card.append(lb(S.POS_NAMES[pos], 'pos-seal'));
    return tap(col, () => u ? openUnit(u.id) : openDeckPick(pos));
  }

  // 방치 보상 (기획서 2026-10-28~29): 레벨업 재화 · 골드 · 뽑기권 · 장비 강화 재료 · 철광석 · 단약
  const IDLE_ITEMS = [['book', '레벨업 재화'], ['coin', '골드'], ['ticket', '뽑기권'], ['gear', '강화 재료'], ['ore', '철광석'], ['pill', '단약']];
  function idleRewardSheet() {
    const { dim, body } = sheet('방치 보상');
    body.append(el('idle-time', icon('clock', 26), lb(N + ':' + N, 'it-n'), el('grow'), lb('최대 ' + N + '시간', 'lb')));
    body.append(el('rewards', IDLE_ITEMS.map(([k, name]) => el('rw', tile(k, N), lb(name, 'rw-name')))));
    const get = btn('받기', () => { openSheet(null); rewardPopup('보상 획득!', IDLE_ITEMS.map(([k]) => [k, N])); }, 'btn pri');
    get.style.marginTop = '14px';
    body.append(get);
    return dim;
  }

  // ---------- 탭 패널 ----------
  function panel(s) {
    const title = app.tab === 'growth' ? '폐관수련' : TABS.find(t => t[0] === app.tab)[2];
    s.append(topBand(), titleBand(title));
    const body = el('pbody');
    if (app.tab === 'units') ({ list: unitList, deck: deckEdit, levelUp: levelUp, gear: gearList })[app.sub](body);
    else if (app.tab === 'growth') research(body);
    else if (app.tab === 'content') (app.content === 'match' ? matchList : dungeons)(body);
    else if (app.tab === 'gacha') gacha(body);
    s.append(body);
    // 하위 탭은 하단 바 바로 위 줄 (크럼블)
    if (app.tab === 'growth') s.append(subBar([['폐관수련', true, () => {}]]));
    if (app.tab === 'gacha') s.append(subBar([['무인 뽑기', true, () => {}]]));
    if (app.tab === 'units') s.append(subBar(SUBS.map(([k, n]) => [n, app.sub === k, () => openSub(k)])));
    if (app.tab === 'content') s.append(subBar([['대전', app.content === 'match', () => openContent('match')], ['일일 던전', app.content === 'dungeon', () => openContent('dungeon')]]));
  }
  const subBar = items => el('subs', items.map(([name, on, fn]) => btn(on ? `▸ ${name} ◂` : name, fn, 'sub' + (on ? ' on' : ''))));

  // 무인 목록: 3열 카드. 카드 구석 숫자 = 중복 카드 수
  function unitList(body) {
    const units = app.game.data.units;
    const flt = el('flt');
    for (const st of [0, 5, 4, 3]) flt.append(btn(st === 0 ? '전체' : '★' + st, () => { app.starFilter = st; refresh(); }, 'chip' + (app.starFilter === st ? ' on' : '')));
    flt.append(el('grow'), lb(`${units.length} / ${S.UNITS.length}`, 'count'));
    body.append(flt);
    const sv = scroll();
    if (!units.length) sv.append(el('empty-box', btn('뽑기', () => openTab('gacha'), 'btn pri')));
    const deck = app.game.data.deck.units;
    const grid = el('grid');
    units.filter(x => !app.starFilter || G.stars(x.id) === app.starFilter)
      .sort((a, b) => G.stars(b.id) - G.stars(a.id) || b.promotions - a.promotions)
      .forEach(u => {
        const card = unitCard(u.id, u.promotions, u.spare);
        const at = deck.indexOf(u.id);
        if (at >= 0) card.querySelector('.portrait').append(lb(S.POS_NAMES[at], 'inDeck'));
        grid.append(el('cell3', tap(card, () => openUnit(u.id))));
      });
    sv.append(grid);
    body.append(sv);
  }

  // 무인 카드: 초상 + 테두리 + 별·이름. 주장·5성 이상만 금색 (등급 색은 미정)
  function unitCard(id, promotions, spare) {
    const st = G.stars(id);
    const pic = portrait(id, 'upic');
    pic.append(lb('Lv ' + N, 'lv'));
    if (spare > 0) pic.append(lb('+' + spare, 'spare'));
    pic.append(cardFrame(st >= 5));
    return el('ucard s' + st, pic, el('ucap', starsEl(id, promotions), lb(S.UNIT[id].name, 'un')));
  }

  // 덱 편성: 속성 + 세 칸 (왼쪽부터 주장 · 부장 · 부장) + 아래 보유 무인 격자
  function deckEdit(body) {
    const deck = app.game.data.deck;
    const sv = scroll();
    // 엔트리(덱 여러 개) 수는 미정이라 덱 탭은 자리만
    sv.append(el('decktabs', [0, 1, 2].map(i => { const b = btn('덱 ' + (i + 1), null, 'chip' + (i === 0 ? ' on' : '')); b.disabled = i > 0; return b; })));
    sv.append(el('elements', S.ELEMENTS.map(e => btn(e, () => { deck.element = e; commit(); }, 'elem' + (deck.element === e ? ' on' : '')))));
    const ei = S.ELEMENTS.indexOf(deck.element);
    const slots = el('slots');
    for (let pos = 0; pos < 3; pos++) {
      const u = app.game.find(deck.units[pos]);
      const col = el('dslot' + (pos === 0 ? ' leader' : '') + (pos === app.deckPos ? ' sel' : ''), lb(S.POS_NAMES[pos], 'dpos'));
      if (u) {
        const d = S.UNIT[u.id];
        const pic = portrait(u.id, 'dpic');
        pic.append(cardFrame(pos === 0));
        if (ei >= 0) pic.append(lb(d.apt[ei], 'apt-badge'));
        const p = pos;
        pic.append(iconBtn('close', () => { deck.units[p] = ''; commit(); }, 14, 'dremove'));
        col.append(pic, starsEl(u.id, u.promotions), lb(d.name, 'un'));
        [d.skill, ...u.equipped.map(uid => app.game.findSkill(uid)?.skill).filter(Boolean)].forEach(s => col.append(lb(S.SKILL[s].name, 'dskill')));
      } else col.append(el('dpic empty', icon('plus', 30)));
      const p = pos;
      tap(col, () => { app.deckPos = p; refresh(); }); // 고른 칸에 아래 보유 무인을 눌러 넣는다
      slots.append(col);
    }
    sv.append(slots);
    // 팀 속성 적성 높은 순
    const owned = el('grid own-grid');
    app.game.data.units.slice()
      .sort((a, b) => (ei >= 0 ? APT_ORDER.indexOf(S.UNIT[a.id].apt[ei]) - APT_ORDER.indexOf(S.UNIT[b.id].apt[ei]) : 0) || G.stars(b.id) - G.stars(a.id))
      .forEach(o => {
        const face = portrait(o.id, 'own-face');
        if (ei >= 0) face.append(lb(S.UNIT[o.id].apt[ei], 'apt-badge'));
        const at = deck.units.indexOf(o.id);
        if (at >= 0) face.append(lb(S.POS_NAMES[at], 'inDeck'));
        face.append(cardFrame(at === 0));
        tap(face, () => { app.game.setDeckUnit(app.deckPos, o.id); commit(); });
        owned.append(el('cell5', face, lb(S.UNIT[o.id].name, 'dn')));
      });
    const errors = app.game.validateDeck();
    const foot = el('deckfoot', errors.map(e => lb(e, 'err')));
    const goBtn = btn('대전', openBattleReady, 'btn pri');
    goBtn.disabled = errors.length > 0;
    foot.append(goBtn);
    sv.append(owned, foot);
    body.append(sv);
  }

  // 무인 레벨업: 레벨업 재화로 올리고 기본 스탯이 자동으로 오른다. 덱 무인을 위에 (수치 미정)
  function levelUp(body) {
    const sv = scroll();
    const deck = app.game.data.deck.units;
    const inDeck = deck.map(id => app.game.find(id)).filter(Boolean);
    if (inDeck.length) { sv.append(band('덱', 'sub-band')); inDeck.forEach(u => sv.append(levelRow(u, deck.indexOf(u.id)))); }
    sv.append(band('다른 무인', 'sub-band'));
    app.game.data.units.filter(x => !inDeck.includes(x)).sort((a, b) => G.stars(b.id) - G.stars(a.id)).forEach(u => sv.append(levelRow(u, -1)));
    body.append(sv);
  }
  function levelRow(u, pos) {
    const face = tap(portrait(u.id, 'uface'), () => openUnit(u.id));
    if (pos >= 0) face.append(lb(S.POS_NAMES[pos], 'pos-seal'));
    return el('urow', face, el('grow', starsEl(u.id, u.promotions, 'sr left'), lb(S.UNIT[u.id].name, 'b'), lb('Lv ' + N, 'lb')), costBtn('레벨업', 'book', N, false, null));
  }

  // 장비: 무인마다 4칸. 칸을 누르면 장비 바꾸기
  function gearList(body) {
    const sv = scroll();
    const deck = app.game.data.deck.units;
    const order = u => { const i = deck.indexOf(u.id); return i >= 0 ? i : 9; };
    app.game.data.units.slice().sort((a, b) => order(a) - order(b) || G.stars(b.id) - G.stars(a.id)).forEach(u => {
      const pos = deck.indexOf(u.id);
      sv.append(el('gear-box',
        el('row', portrait(u.id, 'holder'), lb(S.UNIT[u.id].name, 'b grow'), pos >= 0 ? lb(S.POS_NAMES[pos], 'tag') : null, lb('전투력 ' + N, 'lb')),
        gearRow(s => openEquipSwap(u.id, s, false))));
    });
    body.append(sv);
  }
  function gearRow(onTap) {
    const g = el('grid5');
    g.style.marginTop = '6px';
    GEAR_SLOTS.forEach((name, i) => g.append(tap(el('cell eq' + (i === 0 ? ' first' : ''), lb(name, 'b'), lb(GEAR_STATS[i], 'lb')), () => onTap(i))));
    return g;
  }

  // 폐관수련 (크럼블 노움 연구소 참고): 위에 노드 나무, 아래 고른 노드 상세. 수치 미정
  const BASE_NODES = ['외공', '내공', '호신', '경공', '체력'];
  function research(body) {
    const sv = scroll();
    sv.append(el('row gwan', iconBtn('prev', null, 18, 'sq small'), lb('제 1관', 'gwan-t'), iconBtn('next', null, 18, 'sq small')));
    const tree = el('tree');
    BASE_NODES.forEach((name, i) => {
      const side = el('t-side');
      if (i === 1 || i === 3) side.append(el('t-hline'), researchTile(i === 1 ? 5 : 6, i === 1 ? '방치 시간' : '방치 보상', true));
      tree.append(el('t-row', el('t-side'), researchTile(i, name, false), side), el('t-vline'));
    });
    tree.append(el('t-els', S.ELEMENTS.map((e, i) => researchTile(7 + i, e, false, true))));
    sv.append(tree);
    body.append(sv, researchDetail());
  }
  function researchTile(index, name, special, small) {
    return btn([lb(name, 'tile-name'), el('tile-prog', lb(`${N}/${N}`, 'tile-n'))], () => { app.researchNode = index; refresh(); },
      'tile' + (special ? ' special' : '') + (small ? ' small' : '') + (app.researchNode === index ? ' on' : ''));
  }
  function researchDetail() {
    const n = app.researchNode;
    let name, effect;
    if (n < 5) { name = BASE_NODES[n] + ' 수련'; effect = BASE_NODES[n] + ' 증가'; }
    else if (n < 7) { name = n === 5 ? '방치 시간' : '방치 보상'; effect = n === 5 ? '최대 시간' : '보상 양'; }
    else { const e = S.ELEMENTS[n - 7]; name = e + ' 갈래'; effect = e + ' 덱 4스탯 · 상극 우위'; }
    const start = costBtn('수련 시작', 'pill', N, true, null);
    start.classList.add('dp-start');
    return el('detail-panel',
      el('row', el('dp-icon', lb(name[0], 'dp-glyph')), el('grow', lb(name, 'dp-name'), el('row dp-eff', lb(effect, 'dp-eff-t grow'), lb(N + '%', 'dp-now'), icon('next', 14), lb(N + '%', 'dp-next')))),
      el('row dp-bottom', el('dp-time', lb('수련 시간', 'dp-time-l'), el('row', icon('clock', 18), lb(`${N}:${N}:${N}`, 'dp-time-n'))), start));
  }

  // ---------- 컨텐츠 ----------
  function banner(name, sub, open) {
    const b = el('banner-card' + (open ? '' : ' locked'), lb('[배너 그림]', 'banner-ph'), el('banner-title', lb(name, 'bt-name'), sub ? lb(sub, 'bt-sub') : null));
    if (open) tap(b, open);
    return b;
  }
  function matchList(body) {
    body.append(el('pscroll', banner('대전', null, openBattleReady), banner('엔트리 순위전', '준비 중', null), banner('위너스 연전', '준비 중', null)));
  }
  // 일일 던전 (크럼블 던전 목록): 배너 카드 + 입장권 충전 시간
  function dungeons(body) {
    const b = banner('단약 던전', N + '단계', () => go('dungeonEntry'));
    b.append(el('banner-count', lb(`${N}/${N}`, 'bc-t')), el('banner-icon', icon('pill', 34)));
    body.append(el('pscroll', b), el('charge', icon('clock', 16), lb('입장권 충전까지 ' + N, 'charge-t')));
  }
  // 단약 던전 입장 (크럼블 연구석 던전). 수치·전투 규칙 미정이라 버튼은 자리만
  function dungeonEntry() {
    const hero = el('entry-art', lb('[단약 던전 그림]', 'banner-ph'),
      el('ribbon', lb('단약 던전', 'ribbon-t')),
      el('stage-pick', lb('추천 전투력 ' + N, 'sp-pow'), lb('단계 선택', 'sp-label'), el('row sp-step', btn('−', null, 'sp-btn minus'), lb(N, 'sp-n'), btn('+', null, 'sp-btn'))),
      el('entry-reward', icon('pill', 40), lb(N, 'er-n')),
      btn([icon('chest', 34), lb('달성 보상', 'goal-t')], null, 'goal-btn'));
    const deck = el('ep-deck');
    for (let i = 0; i < 3; i++) {
      const id = app.game.data.deck.units[i];
      const has = !!app.game.find(id);
      const card = has ? portrait(id, 'ep-card') : el('ep-card empty');
      if (has) card.append(lb('Lv ' + N, 'lv'), cardFrame(i === 0));
      card.append(lb(S.POS_NAMES[i], 'pos-seal'));
      if (i === 0) card.classList.add('leader');
      deck.append(card);
    }
    const panel_ = el('entry-panel',
      el('row ep-head', lb('전투력 ' + N, 'ep-pow'), el('grow'), btn('편성하기', backToDeck, 'btn sec small')),
      deck,
      el('row ep-btns', btn('소탕', null, 'btn sec'), btn([lb('도전하기!', 'go-t'), lb(`${N}/${N}`, 'go-n')], () => toast('던전 규칙 준비 중'), 'btn pri go'), btn([el('checkbox'), lb('연속 도전', 'chk-t')], null, 'check-btn')));
    return el('screen', hero, panel_);
  }

  // ---------- 뽑기 ----------
  // 무인 뽑기 (크럼블 쿠키 뽑기): 위에 확률 정보 · 뽑기 레벨, 아래 1회 / 10회. 테스트 기간이라 뽑기는 무료
  function gacha(body) {
    const gb = bar(0.4, false); gb.classList.add('gl-bar');
    body.append(el('gacha-art', lb('[무인 뽑기 그림]', 'ph-mid'),
      el('row gacha-tools', btn('확률 정보', () => go('gachaRates'), 'chip dark')),
      el('gacha-lv', lb('무인 뽑기 레벨', 'gl-label'), el('row', lb('Lv ' + N, 'gl-lv'), gb))));
    const draw = n => { app.lastDraw = app.game.draw(n); commit(); };
    const one = costBtn('1회', 'ticket', N, false, () => draw(1));
    const ten = costBtn('10회', 'ticket', N, true, () => draw(10));
    one.classList.add('drawbtn'); ten.classList.add('drawbtn');
    body.append(el('gacha-panel', el('gacha-btns', one, ten), lb('테스트 기간: 뽑기 무료', 'free-note')));
    if (app.lastDraw.length) body.append(drawResult());
  }

  // 뽑기 결과는 그림 위에 겹치는 창. 산문이 가운데에서 좌우로 열리고 그 뒤로 결과가 떠오른다
  function drawResult() {
    const grid = el('drawgrid', app.lastDraw.map(r => {
      const card = portrait(r.unit, 'dcard');
      card.append(lb(r.isNew ? 'NEW' : '+1', r.isNew ? 'new' : 'dup'), cardFrame(G.stars(r.unit) >= 5));
      return el(app.lastDraw.length === 1 ? 'cell-one' : 'cell5', card, lb(S.UNIT[r.unit].name, 'dn'));
    }));
    const sh = el('sheet reveal', el('sheet-head', lb('뽑기 결과', 'sheet-title')), grid,
      el('sheet-foot', btn('확인', () => { app.lastDraw = []; refresh(); }, 'btn pri wide')));
    const gate = el('gate', el('gate-half gate-l'), el('gate-half gate-r'), el('gate-light'));
    const dim = el('modal', sh, gate);
    if (!app.drawShown) {
      after(250, () => gate.classList.add('open'));
      after(650, () => sh.classList.add('in'));
      after(1200, () => gate.remove());
      app.drawShown = true;
    } else { sh.classList.add('in'); gate.remove(); }
    return dim;
  }

  // 확률 정보 (크럼블 확률표): 위에 등급별 합계, 아래 무인별 (확률 미정)
  function gachaRates() {
    const sv = scroll();
    sv.append(el('grid5 rate-sum', [5, 4, 3].map((st, i) => el('cell apt' + (i === 0 ? ' first' : ''), lb('★' + st, 'b'), lb(N + '%', 'v')))));
    for (const st of [5, 4, 3]) {
      sv.append(band(`★${st} ${G.rank(st)}`, 'sub-band'));
      sv.append(el('box rate-box', G.DRAW_POOL.filter(d => G.stars(d.id) === st).map(d => el('tr', lb(d.name, 'c1'), lb(N + '%', 'cn')))));
    }
    return el('screen', sv, el('row lvnav', iconBtn('prev', null, 20, 'sq'), lb('Lv ' + N, 'lv-big'), iconBtn('next', null, 20, 'sq')));
  }

  // ---------- 대장간 (장비 데이터·수치 미정: 화면만) ----------
  function dropdown(label, value, suffix, open, options, selected, toggle, pick) {
    const box = el('dd', lb(label, 'dd-label'), el('row', btn([lb(value, 'dd-t'), el('grow'), icon(open ? 'up' : 'down', 14)], toggle, 'dropdown'), suffix ? lb(suffix, 'dd-suffix') : null));
    if (open) box.append(el('dd-list', options.map(([key, text]) => btn(text, () => pick(key), 'dd-item' + (key === selected ? ' on' : '')))));
    return box;
  }
  // 자동 뽑기 (크럼블 자동 열기): 남길 등급 아래와 고른 특수효과가 없는 장비(영웅 이상)는 바로 분해 → 불씨
  function forgeAutoSheet() {
    const { dim, body } = sheet('자동 뽑기', true);
    const grades = GEAR_GRADES.map((g, i) => [i, g]).reverse();
    body.append(el('dd-pair',
      dropdown('장비 등급', GEAR_GRADES[app.keepGrade], '이상', app.gradeOpen, grades, app.keepGrade,
        () => { app.gradeOpen = !app.gradeOpen; app.countOpen = false; refresh(); }, g => { app.keepGrade = g; app.gradeOpen = false; refresh(); }),
      dropdown('한 번에 뽑는 개수', N, null, app.countOpen, [[0, N]], 0,
        () => { app.countOpen = !app.countOpen; app.gradeOpen = false; refresh(); }, () => { app.countOpen = false; refresh(); })));
    if (app.effectsOpen) {
      body.append(el('checks', GEAR_EFFECTS.map((name, k) => {
        const on = app.effects.has(k);
        return btn([el('checkbox' + (on ? ' on' : ''), on ? icon('check', 14) : null), lb(name, 'chk-t')], () => { if (!app.effects.delete(k)) app.effects.add(k); refresh(); }, 'checkrow');
      })));
    }
    body.append(el('sheet-foot', el('foot-side'), btn('시작', openForgeResult, 'btn pri wide'),
      el('foot-side right', btn([lb('특수효과 설정', 'link-t'), icon(app.effectsOpen ? 'down' : 'up', 12)], () => { app.effectsOpen = !app.effectsOpen; refresh(); }, 'link'))));
    return dim;
  }
  // 자동 뽑기 결과 (크럼블 결과 창): 뽑은 장비 격자 + 골라보기 / 모두 분해. 장착은 무인 상세 > 장비 칸
  function forgeResultSheet() {
    const { dim, body } = sheet('자동 뽑기 결과', true);
    body.append(el('gear-grid', Array.from({ length: 12 }, () => tile('forge', '+' + N, 'Lv ' + N, 70))));
    body.append(el('sheet-foot', el('foot-side', btn([icon('content', 22), lb('골라보기', 'it-t')], null, 'icon-text')), btn('모두 분해', null, 'btn pri wide'), el('foot-side right', cur('flame', '+' + N))));
    return dim;
  }
  // 대장간 레벨: 등급마다 지금/다음 레벨 확률, 불씨로 채우는 성장 게이지
  function forgeLevel() {
    const sv = scroll();
    sv.append(el('lvhead', lb('Lv ' + N, 'lv-big'), icon('next', 20), lb('Lv ' + N, 'lv-big next')));
    const table = el('box section', el('tr', lb('등급', 'lb c1'), lb('지금', 'lb cn'), lb('다음', 'lb cn')));
    for (let i = GEAR_GRADES.length - 1; i >= 0; i--) table.append(el('tr', lb(GEAR_GRADES[i], 'c1 b' + (i >= 5 ? ' red' : '')), lb(N + '%', 'cn'), lb(N + '%', 'cn b')));
    const gb = bar(0.35, true); gb.classList.add('thick');
    sv.append(table, el('box section', el('row', lb('성장', 'b grow'), lb(`${N} / ${N}`, 'lb')), gb));
    const grow = costBtn('대장간 성장', 'flame', N, true, () => lackPopup('불씨', 'flame', N, N, [['장비 분해', openForgeResult]]));
    grow.style.flexGrow = '1';
    return el('screen', sv, el('footer static', grow));
  }
  // 장비 바꾸기: 그 칸에 낄 수 있는 장비를 이 무인 기준 전투력 ±로 (장비 데이터 없음)
  function equipSwap(u) {
    const slot = app.equipSlot;
    const sv = scroll();
    for (let i = 0; i < 3; i++)
      sv.append(el('skrow off', el('row', tile('forge', '+' + N, null, 52), el('grow', lb('[장비] · ' + N, 'b'), lb(`${GEAR_STATS[slot]} +${N}`, 'desc')), lb('+' + N, 'delta'))));
    return el('screen',
      el('cu-head', portrait(u.id, 'cu-face'), el('grow', lb(S.UNIT[u.id].name, 'uname'), lb(`${GEAR_SLOTS[slot]} · ${GEAR_STATS[slot]}`, 'lb')), el('spare-box', lb('전투력', 'lb'), lb(N, 'spare-n'))),
      el('box section', lb('지금', 'lb'), lb('비어 있음', 'b')),
      sv);
  }

  // ---------- 무인 상세 ----------
  function skillTag(s) {
    const type = S.TYPE_NAMES[s.type];
    return lb(s.type === 'formation' ? `${type} · ${s.element}` : type, 'tag');
  }
  function skillLine(def, own, onTap) {
    const row = el('sk-line' + (own ? ' own' : '') + (onTap ? ' tap' : ''));
    if (!def) {
      row.classList.add('empty');
      row.append(el('sk-dot plus', icon('plus', 16)), lb('장착 가능', 'sk-empty'));
    } else {
      row.append(el('sk-dot', lb(S.TYPE_NAMES[def.type][0], 'sk-dot-t')), lb(def.name, 'sk-name'), skillTag(def), el('grow'));
      if (own) row.append(lb('자기 무공', 'lb'));
    }
    if (onTap) tap(row, onTap);
    return row;
  }
  function statCell(name, value, added, first) {
    return el('cell' + (first ? ' first' : ''), lb(name, 'lb'), lb(String(value), 'v'), added > 0 ? lb('+' + added, 'plus') : null);
  }

  // 무인 상세 A안: 큰 초상(아래로 어두워짐), 왼쪽 세력 세로 깃발, Lv · 승급 배지, 이름 · 별 · 전투력
  function unitDetail(u) {
    const d = S.UNIT[u.id];
    const st = G.stars(u.id);
    const head = el('dh', portrait(u.id, 'illust'), el('illust-shade'),
      el('sect-flag', [...d.faction].map(ch => lb(ch, 'sf-ch'))),
      lb('Lv ' + N, 'dh-lv'), lb(`승급 ${u.promotions}/${st}`, 'dh-promo'),
      el('dh-name', starsEl(u.id, u.promotions, 'stars'), lb(d.name, 'uname'), lb(`${d.title} · ${G.rank(st)}`, 'lb')),
      el('dh-pow', lb('전투력', 'lb'), lb(N, 'dh-pow-n')));
    const body = el('detail-body');
    const stats = el('grid5', statCell('체력', d.hp, 0, true));
    G.STAT_NAMES.forEach((name, k) => stats.append(statCell(name, app.game.statValue(u, k), u.added[k], false)));
    body.append(stats);
    if (u.freePoints > 0) {
      const t = document.createElement('div'); t.className = 'grow'; t.innerHTML = `남은 자유 스탯 <b>${u.freePoints}</b>`;
      body.append(el('row section', t, btn('배분 ›', () => go('cardUse'), 'btn sm')));
    }
    body.append(lb('속성 적성', 'lb section'));
    const apt = el('grid5', S.ELEMENTS.map((e, i) => el('cell apt' + (i === 0 ? ' first' : ''), lb(e, 'lb'), lb(d.apt[i], 'b'))));
    apt.style.marginTop = '6px';
    body.append(apt);
    // 무공 3칸: 칸마다 둥근 표식(종류 첫 글자) + 이름. 빈 칸은 + 장착 가능
    body.append(lb('무공', 'lb section'), skillLine(S.SKILL[d.skill], true, null));
    for (let i = 0; i < 2; i++) {
      const s = app.game.findSkill(u.equipped[i]);
      const slot = i;
      body.append(skillLine(s ? S.SKILL[s.skill] : null, false, () => { app.slot = slot; go('skillPick'); }));
    }
    // 장비 4칸: 장비 데이터가 아직 없어 빈 칸, 누르면 장비 바꾸기
    body.append(lb('장비', 'lb section'), gearRow(i => openEquipSwap(u.id, i, true)));
    const use = btn('승급 / 무공 추출', () => go('cardUse'), 'btn pri');
    use.disabled = u.spare <= 0; // 승급·추출 둘 다 중복 카드가 있어야 한다
    const sv = scroll();
    sv.append(head, body);
    return el('screen detail', sv, el('bottombar', el('', lb('중복 카드', 'lb'), lb(u.spare + '장', 'b')), el('grow'), use));
  }

  // 중복 카드 쓰기 (승급 / 무공 추출) + 자유 스탯 배분
  function cardUse(u) {
    const d = S.UNIT[u.id];
    const st = G.stars(u.id);
    const g = app.game;
    const sv = scroll();
    sv.append(el('cu-head', portrait(u.id, 'cu-face'), el('grow', starsEl(u.id, u.promotions), lb(d.name, 'uname'), lb(`승급 ${u.promotions} / ${st}`, 'lb')), el('spare-box', lb('중복 카드', 'lb'), lb(String(u.spare), 'spare-n'))));
    const pb = btn(u.promotions >= st ? '최대' : '승급', () => { g.promote(u); commit(); }, 'btn pri');
    pb.disabled = !g.canPromote(u);
    const eb = btn('추출', () => { const s = g.extract(u); save(); render(); toast(`${S.SKILL[s.skill].name} 추출`); }, 'btn');
    eb.disabled = u.spare <= 0;
    sv.append(el('uses',
      el('use', lb('승급', 'use-name'), lb(`자유 스탯 +${G.POINTS_PER_PROMOTION}`, 'lb'), pb),
      el('use', lb('추출', 'use-name'), lb(S.SKILL[d.skill].name, 'lb'), eb)));
    const alloc = el('box section', el('row', lb('자유 스탯', 'b grow'), lb(String(u.freePoints), 'free-n')));
    G.STAT_NAMES.forEach((name, k) => {
      const add = btn('+1', () => { g.addStat(u, k); commit(); }, 'btn sm');
      add.disabled = u.freePoints <= 0;
      alloc.append(el('srow', lb(name, 'sname'), lb(String(g.statValue(u, k)), 'sval'), lb(u.added[k] > 0 ? '+' + u.added[k] : '', 'plus grow'), add));
    });
    sv.append(alloc);
    return el('screen', sv);
  }

  // 무공 고르기 (장착): 추출해 둔 무공 중에서 무공창 2·3칸에 낄 것을 고른다
  function skillPick(u) {
    const g = app.game, slot = app.slot;
    const sv = scroll();
    if (g.findSkill(u.equipped[slot])) {
      const clear = btn('비우기', () => { g.unequip(u, slot); save(); backToDetail(); }, 'btn sm');
      sv.append(el('row', el('grow'), clear));
      sv.firstChild.style.padding = '8px 12px 0';
    }
    if (!g.data.skills.length) sv.append(el('ph empty-ph', lb('[추출한 무공 없음]', 'ph-t')));
    for (const s of g.data.skills) {
      const def = S.SKILL[s.skill];
      const holder = g.equippedBy(s.uid);
      const err = g.equipError(u, slot, s);
      const line = el('row', lb(def.name, 'b'), skillTag(def), el('grow'));
      if (holder) line.append(portrait(holder.id, 'holder'), lb(S.UNIT[holder.id].name, 'lb'));
      const row = el('skrow' + (err ? ' off' : ''), line, lb(err || def.desc, err ? 'err' : 'desc'));
      if (!err) tap(row, () => { g.equip(u, slot, s); save(); backToDetail(); });
      sv.append(row);
    }
    return el('screen', sv);
  }

  // 무인 고르기 (덱 칸을 누름): 팀 속성 적성이 높은 순
  function deckPick() {
    const deck = app.game.data.deck;
    const ei = S.ELEMENTS.indexOf(deck.element);
    const grid = el('grid');
    app.game.data.units.slice()
      .sort((a, b) => (ei >= 0 ? APT_ORDER.indexOf(S.UNIT[a.id].apt[ei]) - APT_ORDER.indexOf(S.UNIT[b.id].apt[ei]) : 0) || G.stars(b.id) - G.stars(a.id))
      .forEach(u => {
        const card = unitCard(u.id, u.promotions, 0);
        const pic = card.querySelector('.portrait');
        if (ei >= 0) pic.append(lb(S.UNIT[u.id].apt[ei], 'apt-badge'));
        const at = deck.units.indexOf(u.id);
        if (at >= 0) { pic.append(lb(S.POS_NAMES[at], 'inDeck')); if (at === app.deckPos) card.classList.add('picked'); }
        grid.append(el('cell3', tap(card, () => { app.game.setDeckUnit(app.deckPos, u.id); save(); backToDeck(); })));
      });
    const sv = scroll();
    if (!app.game.data.units.length) sv.append(el('empty-box', btn('뽑기', () => openTab('gacha'), 'btn pri')));
    sv.append(grid);
    return el('screen', sv);
  }

  // ---------- 대전 준비 ----------
  // 위 패널에 양 팀 전투력 한 번, 그 아래 상대·아군 줄. 상대는 아직 임시(무작위 3명 + 무작위 속성)
  function battleReady() {
    const g = app.game;
    const errors = g.validateDeck();
    const side = (who, element, foe) => el('rs-side' + (foe ? ' foe' : ''), el('row', lb(who, 'rs-who'), lb(element || '-', 'elem on small')), lb('전투력 ' + N, 'rs-pow'));
    const field = el('ready-field', readyRow(app.opponent, true));
    if (!errors.length) field.append(readyRow(g.buildTeam(), false));
    else field.append(el('ready-err', errors.map(e => lb(e, 'err')), btn('덱 편성', backToDeck, 'btn')));
    const start = btn('전투 시작', startBattle, 'btn pri second');
    start.disabled = errors.length > 0;
    return el('screen ready',
      el('row ready-sum', side('아군', g.data.deck.element, false), lb(':', 'rs-colon'), side('상대', app.opponent.element, true)),
      field,
      el('footer static', btn('새 상대', newOpponent, 'btn'), start));
  }
  function readyRow(team, foe) {
    const row = el('rrow');
    for (let i = 0; i < 3; i++) {
      const col = el('rcol' + (i === 0 ? ' leader' : ''), lb(S.POS_NAMES[i], 'role-band'));
      const s = team.slots[i];
      if (s && s.unit) {
        const pic = portrait(s.unit, 'rpic');
        pic.append(lb('Lv ' + N, 'lv'), cardFrame(i === 0));
        col.append(pic, lb(S.UNIT[s.unit].name, 'un'));
      } else col.append(el('rpic silhouette', icon('units', 56)), lb(' ', 'un'));
      row.append(col);
    }
    return el('rteam' + (foe ? ' foe' : ''), row);
  }

  // ---------- 전투 ----------
  // 전투 A안: 위 연출 + 아래 고정 기록, 누르면 전체 기록. 전투는 미리 끝까지 계산해 두고 기록 한 줄씩 그 시점의 체력·상태를 보여 준다
  const isHeader = l => l.text === '준비 합' || l.text === `${l.round}합`;
  // "[적] 독수귀녀: 발동 무공 산공독" → ["상대 발동", "독수귀녀 · 산공독"]
  function parseCast(text) {
    for (const kind of ['발동', '연계']) {
      const mark = `: ${kind} 무공 `;
      const at = text.indexOf(mark);
      if (at < 0 || text.length < 4) continue;
      return [(text.startsWith('[아]') ? '아군 ' : '상대 ') + kind, `${text.substring(4, at)} · ${text.substring(at + mark.length)}`];
    }
    return null;
  }
  function spawnFloater(over, delta) {
    const l = lb(delta > 0 ? '+' + delta : String(-delta), 'dmg' + (delta > 0 ? ' heal' : ''));
    l.style.left = (Math.random() * 24 - 6) + 'px';
    l.addEventListener('animationend', () => l.remove());
    over.append(l);
  }

  class BattleScreen {
    constructor(result, finished) {
      this.LINE = 0.45; this.LOG_LINES = 11;
      this.r = result; this.finished = finished;
      this.maxHp = result.units.map(u => u.maxHp);
      this.shown = -1; this.speed = 1; this.full = false; this.done = false;
      this.views = [];
      const r = result;

      // 머리줄: 아군 체력 비율 · 합 · 상대 체력 비율
      this.allyBar = bar(1, false); this.allyBar.style.flexGrow = '1'; this.allyBar.style.marginRight = '6px';
      this.foeBar = bar(1, true); this.foeBar.style.flexGrow = '1'; this.foeBar.style.marginLeft = '6px';
      this.allyPct = lb('', 'b'); this.foePct = lb('', 'b');
      this.roundL = lb('', 'round');
      const hud = el('hud',
        el('hud-side', this.minis('A'), el('row', this.allyBar, this.allyPct)),
        el('hud-mid', el('round-medal', this.roundL)),
        el('hud-side', this.minis('B'), el('row', this.foePct, this.foeBar)));

      // 연출: 위 상대, 가운데 발동 배너, 아래 아군. 주장이 크게
      this.bannerSide = lb('', 'bs'); this.bannerText = lb('', 'bt');
      this.banner = el('banner', this.bannerSide, this.bannerText);
      this.speedBtn = btn('×1', () => { this.speed = this.speed === 1 ? 2 : 1; this.speedBtn.textContent = '×' + this.speed; }, 'btn sm');
      this.arena = el('arena', this.teamRow('B'), this.banner, this.teamRow('A'), el('speed', this.speedBtn, btn('건너뛰기', () => this.skip(), 'btn sm')));

      // 아래 고정 기록
      this.logRound = lb('', 'lb'); this.logRound.style.marginLeft = '8px';
      this.fullBtn = btn('전체 보기 ▴', () => this.toggleFull(), 'btn sm flat');
      this.logBody = el('logbody');
      this.logPanel = el('logpanel', el('loghead', lb('전투 기록', 'b'), this.logRound, el('grow'), this.fullBtn), this.logBody);

      this.root = el('screen battle', hud, this.arena, this.logPanel, this.vsCut());
      this.step();
      this.wait = 1.4; // VS 컷인이 끝난 뒤 진행
    }

    // 위 띠의 팀 초상 3장 (왼쪽부터 주장 · 부장 · 부장)
    minis(side) {
      const row = el('row minis' + (side === 'B' ? ' foe' : ''));
      for (const pos of [0, 1, 2]) {
        const u = this.r.units.find(x => x.side === side && x.pos === pos);
        if (!u) continue;
        const f = portrait(u.def.id, 'mini');
        f.append(cardFrame(pos === 0));
        row.append(f);
      }
      return row;
    }

    teamRow(side) {
      const row = el('team ' + (side === 'A' ? 'ally' : 'foe'));
      for (const pos of [0, 1, 2]) {
        const i = this.r.units.findIndex(u => u.side === side && u.pos === pos);
        if (i < 0) continue;
        const u = this.r.units[i];
        const leader = pos === 0;
        const face = portrait(u.def.id, 'face' + (leader ? ' leader' : ''));
        face.append(cardFrame(leader));
        const b = bar(1, side === 'B');
        b.style.width = (leader ? 92 : 76) + 'px'; b.style.marginTop = '3px';
        const chips = el('chips');
        const name = lb(leader ? `${u.name} · 주장` : u.name, 'nm' + (leader ? ' b' : ''));
        // 상대 줄은 얼굴 아래 끝을 주장에 맞춘다
        const v = el('u', side === 'B' ? el('facebox', face) : face, name, b, chips);
        this.views[i] = { root: v, face, bar: b, chips };
        row.append(v);
      }
      return row;
    }

    flash(e, ms) {
      e.classList.add('flash');
      this.arena.append(e);
      after(20, () => e.classList.add('in'));
      after(ms - 200, () => e.classList.remove('in'));
      after(ms, () => e.remove());
    }

    // 대전 시작 VS 컷인: 양 주장이 사선으로 들어오고 가운데 붓 획 "對"가 찍힌 뒤 걷힌다
    vsCut() {
      const a = this.r.units.find(u => u.side === 'A' && u.pos === 0);
      const b = this.r.units.find(u => u.side === 'B' && u.pos === 0);
      const l = portrait(a.def.id, 'vs-l'); l.append(lb(a.name, 'vs-name'));
      const rr = portrait(b.def.id, 'vs-r'); rr.append(lb(b.name, 'vs-name'));
      const mark = lb('對', 'vs-mark');
      const cut = el('vs-cut', l, rr, mark);
      after(30, () => cut.classList.add('in'));
      after(380, () => mark.classList.add('in'));
      after(1150, () => cut.classList.add('out'));
      after(1500, () => cut.remove());
      return cut;
    }

    tick(dt) {
      if (this.done) return;
      this.wait -= dt * this.speed;
      if (this.wait > 0) return;
      if (this.shown >= this.r.log.length - 1) { this.done = true; this.finished(); return; }
      this.step();
    }

    step() {
      const line = this.r.log[++this.shown];
      this.wait = isHeader(line) ? this.LINE * 2 : this.LINE;
      const prev = this.shown > 0 ? this.r.log[this.shown - 1].hp : this.maxHp;
      this.views.forEach((v, i) => {
        if (!v) return;
        const hp = line.hp[i];
        setRatio(v.bar, hp / this.maxHp[i]);
        v.root.classList.toggle('dead', hp <= 0);
        v.chips.replaceChildren(...line.status[i].map(k => lb(S.STATUS_NAMES[k], 'sta')));
        const delta = hp - prev[i];
        if (delta) spawnFloater(v.face, delta); // 데미지 숫자를 초상 위에 띄운다
      });
      const a = this.ratio(line, 'A'), b = this.ratio(line, 'B');
      setRatio(this.allyBar, a); setRatio(this.foeBar, b);
      this.allyPct.textContent = Math.round(a * 100) + '%';
      this.foePct.textContent = Math.round(b * 100) + '%';
      this.roundL.textContent = line.round === 0 ? '준비' : `${line.round} / ${S.CFG.rounds}`;
      this.logRound.textContent = line.round === 0 ? '준비 합' : line.round + '합';

      // 발동·연계 무공이 나오면 배너. 새 합이 시작되면 내리고 가운데에 큰 "N합"
      if (isHeader(line)) {
        this.banner.style.visibility = 'hidden';
        if (line.round > 0) this.flash(lb(line.round + '합', 'round-call'), 800);
      }
      const cast = parseCast(line.text);
      if (cast) {
        this.bannerSide.textContent = cast[0];
        this.bannerText.textContent = cast[1];
        this.banner.style.visibility = 'visible';
        // 주장의 무공은 초상 + 붓 글씨 무공 이름 컷인
        const [who, skill] = cast[1].split(' · ');
        const leader = this.r.units.find(u => u.pos === 0 && u.name === who);
        if (leader) this.flash(el('skill-cut ' + (leader.side === 'A' ? 'ally' : 'foe'), portrait(leader.def.id, 'sc-face'), lb(skill, 'sc-name')), 900);
      }
      this.renderLog();
    }

    renderLog() {
      let lines = this.r.log.slice(0, this.shown + 1);
      if (!this.full) lines = lines.filter(l => !isHeader(l)).slice(-this.LOG_LINES);
      this.logBody.replaceChildren(...lines.map(l => {
        let cls = 'ln';
        if (l.text.startsWith('[아]')) cls += ' ally'; else if (l.text.startsWith('[적]')) cls += ' foe'; // 아군 비취 · 상대 주홍
        if (isHeader(l)) cls += ' head'; else if (parseCast(l.text)) cls += ' hl';
        return lb(l.text, cls);
      }));
      if (this.full) this.logBody.scrollTop = this.logBody.scrollHeight;
    }

    toggleFull() {
      this.full = !this.full;
      this.logPanel.classList.toggle('full', this.full);
      this.fullBtn.textContent = this.full ? '접기 ▾' : '전체 보기 ▴';
      this.renderLog();
    }

    skip() { if (this.done) return; this.done = true; this.finished(); }

    ratio(line, side) {
      let hp = 0, max = 0;
      this.r.units.forEach((u, i) => { if (u.side === side) { hp += Math.max(0, line.hp[i]); max += this.maxHp[i]; } });
      return max ? hp / max : 0;
    }
  }

  // ---------- 결과 ----------
  // 승패와 이유, 전보식 팀 카드(무인별 남은 체력 · 발동 · 피해 · 회복), 아군 무공 통계, 전보(전체 기록)
  function teamRatio(r, side) {
    const us = r.units.filter(u => u.side === side);
    return us.reduce((s, u) => s + Math.max(0, u.hp), 0) / us.reduce((s, u) => s + u.maxHp, 0);
  }
  function reasonText(r) {
    if (r.reason === 'leader') return r.winner === 'A' ? '적 주장 격파' : '아군 주장이 쓰러짐';
    const pct = x => Math.round(x * 100) + '%';
    return `${r.rounds}합 종료 · 남은 체력 비율 ${pct(teamRatio(r, 'A'))} : ${pct(teamRatio(r, 'B'))}`;
  }
  function resultScreen(r, allyEl, foeEl) {
    const win = r.winner === 'A';
    const head = el('res-head');
    if (win) head.append(el('rays'));
    head.append(lb(win ? '勝' : r.winner === 'B' ? '敗' : '和', 'verdict' + (win ? ' win' : '')), lb(win ? '승리' : r.winner === 'B' ? '패배' : '무승부', 'verdict-sub'), lb(reasonText(r), 'lb'));

    const summary = el('res-scroll', repTeam(r, 'A', '아군 · 속성 ' + allyEl), repTeam(r, 'B', '상대 · 속성 ' + foeEl), skillStats(r));
    const log = el('res-scroll', r.log.map(l => lb(l.text, 'ln' + (isHeader(l) ? ' head' : ''))));
    const sumTab = btn('요약 · 통계', () => select(false), 'tabbtn');
    const logTab = btn('전보 (전체 기록)', () => select(true), 'tabbtn');
    function select(showLog) {
      summary.style.display = showLog ? 'none' : 'flex';
      log.style.display = showLog ? 'flex' : 'none';
      sumTab.classList.toggle('on', !showLog);
      logTab.classList.toggle('on', showLog);
    }
    select(false);
    return el('screen', head, el('tabs', sumTab, logTab), summary, log,
      el('footer', btn('다시 보기', () => go('battlePlay'), 'btn'), btn('확인', openBattleReady, 'btn pri second')));
  }
  function repTeam(r, side, title) {
    const row = el('row rep-row');
    r.units.filter(u => u.side === side).sort((a, b) => a.pos - b.pos).forEach(u => {
      const face = portrait(u.def.id, 'rep-face');
      face.append(lb(S.POS_NAMES[u.pos], 'pos-seal'), cardFrame(u.pos === 0));
      const procs = Object.values(u.rec.procs).reduce((s, v) => s + v, 0);
      const stat = (name, v) => el('row rep-stat', lb(name, 'lb grow'), lb(v > 0 ? String(v) : '–', 'rep-v'));
      const card = el('rep-card' + (u.pos === 0 ? ' leader' : '') + (u.dead ? ' dead' : ''), face, lb(u.name, 'un'), bar(Math.max(0, u.hp) / u.maxHp, side === 'B'),
        el('rep-stats', stat('발동', procs), stat('피해', u.rec.dealt), stat('회복', u.rec.healed)));
      if (side === 'A') card.append(lb('경험치 ▲' + N, 'rep-exp'));
      row.append(card);
    });
    return el('rep-team' + (side === 'B' ? ' foe' : ''), lb(title, 'rep-title'), row);
  }
  // 전투 엔진이 이미 내는 집계(발동 횟수, 무공별 피해·회복)를 아군 무인별로
  function skillStats(r) {
    const row = (name, p, d, h, header) => el('tr', lb(name, 'c1' + (header ? ' lb' : '')), ...[p, d, h].map(v => lb(v, 'cn' + (header ? ' lb' : ''))));
    const box = el('box', lb('무공 통계 · 아군', 'b'), row('무공', '발동', '피해', '회복', true));
    r.units.filter(u => u.side === 'A').sort((a, b) => a.pos - b.pos).forEach(u => u.skills.forEach(s => {
      const procs = u.rec.procs[s.id] || 0;
      const t = u.rec.bySkill[s.name] || { dealt: 0, healed: 0 };
      if (!procs && !t.dealt && !t.healed) return;
      const f = v => v > 0 ? String(v) : '–';
      box.append(row(`${s.name} · ${u.name}`, f(procs), f(t.dealt), f(t.healed), false));
    }));
    return box;
  }

  // ---------- 로비 무대: 덱 3명이 쉬지 않고 자동 대전 (결과는 저장하지 않음) ----------
  class LobbyStage {
    constructor() {
      this.root = el('lstage');
      if (app.game.validateDeck().length) { this.root.append(lb('덱 편성', 'lstage-empty')); this.result = null; return; }
      this.newFight();
    }
    newFight() {
      this.result = app.game.fight(app.game.randomOpponent());
      this.shown = -1; this.wait = 0.6; this.views = [];
      const foe = this.row('B'); foe.classList.add('foe');
      this.root.replaceChildren(foe, this.row('A'));
    }
    row(side) {
      const row = el('ls-row');
      for (const pos of [0, 1, 2]) {
        const i = this.result.units.findIndex(u => u.side === side && u.pos === pos);
        if (i < 0) continue;
        const face = portrait(this.result.units[i].def.id, 'ls-face');
        face.append(cardFrame(pos === 0));
        const b = bar(1, side === 'B');
        const col = el('ls-u' + (pos === 0 ? ' leader' : ''), face, b);
        this.views[i] = { col, face, bar: b };
        row.append(col);
      }
      return row;
    }
    tick(dt) {
      if (!this.result) return;
      this.wait -= dt;
      if (this.wait > 0) return;
      if (this.shown >= this.result.log.length - 1) { this.newFight(); return; }
      const line = this.result.log[++this.shown];
      this.wait = 0.35;
      const prev = this.shown > 0 ? this.result.log[this.shown - 1].hp : this.result.units.map(u => u.maxHp);
      this.views.forEach((v, i) => {
        if (!v) return;
        setRatio(v.bar, line.hp[i] / this.result.units[i].maxHp);
        v.col.classList.toggle('dead', line.hp[i] <= 0);
        const delta = line.hp[i] - prev[i];
        if (delta) spawnFloater(v.face, delta);
      });
      if (this.shown >= this.result.log.length - 1) this.wait = 1.5;
    }
  }

  // ---------- 그리기 ----------
  function render() {
    app.battle = null;
    app.stage = null;
    if (app.page !== 'lobby' || app.tab !== 'gacha') app.drawShown = false;
    const u = app.game.find(app.unitId);
    let screen;
    switch (app.page) {
      case 'lobby': screen = lobby(); break;
      case 'unitDetail': if (!u) return backToUnits(); screen = shell(null, unitDetail(u), backToUnits); break;
      case 'cardUse': if (!u) return backToUnits(); screen = shell('승급 · 추출', cardUse(u), backToDetail); break;
      case 'skillPick': if (!u) return backToUnits(); screen = shell(`무공 ${app.slot + 2}칸`, skillPick(u), backToDetail); break;
      case 'deckPick': screen = shell(S.POS_NAMES[app.deckPos], deckPick(), backToDeck); break;
      case 'battleReady':
        if (!app.opponent) app.opponent = app.game.randomOpponent();
        screen = shell('대전', battleReady(), closeTab); break;
      case 'battlePlay':
        app.battle = new BattleScreen(app.result, () => go('result'));
        screen = app.battle.root; break;
      case 'result': screen = resultScreen(app.result, app.game.data.deck.element, app.opponent.element); break;
      case 'forgeLevel': screen = shell('대장간 레벨', forgeLevel(), backToLobby); break;
      case 'equipSwap': if (!u) return backToUnits(); screen = shell(`${GEAR_SLOTS[app.equipSlot]} 바꾸기`, equipSwap(u), backFromEquip); break;
      case 'dungeonEntry': screen = shell(null, dungeonEntry(), () => openContent('dungeon')); break;
      case 'gachaRates': screen = shell('확률 정보', gachaRates(), () => openTab('gacha')); break;
    }
    root.replaceChildren(screen);
  }

  let last = performance.now();
  function frame(now) {
    const dt = Math.min(0.1, (now - last) / 1000);
    last = now;
    if (app.page === 'battlePlay' && app.battle) app.battle.tick(dt);
    if (app.page === 'lobby' && !app.tab && app.stage) app.stage.tick(dt);
    requestAnimationFrame(frame);
  }

  render();
  requestAnimationFrame(frame);
  window.__app = app; // 확인용
})();
