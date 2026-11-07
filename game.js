// 게임 진행 로직 (Unity Assets/Scripts/Game/GameState.cs 를 그대로 옮김).
// 뽑기, 승급(자유 스탯), 무공 추출·장착, 덱 편성, 전투. 화면은 이 객체만 거쳐 저장 데이터를 바꾼다.
// 브라우저에서는 window.WuxiaGame, Node에서는 module.exports.
(function (root) {
  'use strict';
  const S = typeof module !== 'undefined' && module.exports ? require('../tools/web-sim/engine.js') : root.WuxiaSim;

  const POINTS_PER_PROMOTION = 10;
  const STAT_KEYS = ['ext', 'int', 'def', 'spd'];
  const STAT_NAMES = ['외공', '내공', '호신', '경공'];

  // 등급 (C# Catalog의 Stars와 같게 둔다. 없으면 3성) — test.js가 Catalog.cs와 같은지 확인한다
  const STARS = { dok: 5, cheol: 5, gongjin: 5, guyang: 5, jeokyeom: 5, sahyeol: 5, cheongheo: 6, moyong: 6, hwangboyeol: 4, muyeong: 4, jangwiwon: 4 };
  const stars = id => STARS[id] || 3;
  const rank = n => ({ 3: '삼류', 4: '이류', 5: '일류', 6: '절정' }[n] || '');

  // 뽑기에 나오는 무인. 6성 절정은 확장 패치 전까지 뺀다
  const DRAW_POOL = S.UNITS.filter(u => stars(u.id) <= 5);

  function newData() {
    return { units: [], skills: [], deck: { element: '', units: ['', '', ''] }, nextUid: 1, draws: 0 };
  }

  class GameState {
    constructor(data, rand) {
      this.data = data || newData();
      this.rand = rand || Math.random;
    }

    find(id) { return this.data.units.find(u => u.id === id) || null; }
    findSkill(uid) { return this.data.skills.find(s => s.uid === uid) || null; }
    pick(n) { return Math.floor(this.rand() * n); }

    // --- 뽑기 ---
    // 임시: 등급별 확률이 아직 없어서 뽑기 대상 무인 균등 확률
    draw(count) {
      const results = [];
      for (let i = 0; i < count; i++) {
        const def = DRAW_POOL[this.pick(DRAW_POOL.length)];
        const own = this.find(def.id);
        if (!own) this.data.units.push({ id: def.id, spare: 0, promotions: 0, freePoints: 0, added: [0, 0, 0, 0], equipped: [-1, -1] });
        else own.spare++;
        results.push({ unit: def.id, isNew: !own });
        this.data.draws++;
      }
      return results;
    }

    // --- 승급·추출 ---
    canPromote(u) { return u.spare > 0 && u.promotions < stars(u.id); }

    promote(u) {
      if (!this.canPromote(u)) throw new Error('승급할 수 없습니다.');
      u.spare--;
      u.promotions++;
      u.freePoints += POINTS_PER_PROMOTION;
    }

    addStat(u, k) {
      if (u.freePoints <= 0) throw new Error('자유 스탯이 없습니다.');
      u.freePoints--;
      u.added[k]++;
    }

    // 중복 카드 1장을 소모해 그 무인의 무공을 얻는다
    extract(u) {
      if (u.spare <= 0) throw new Error('중복 카드가 없습니다.');
      u.spare--;
      const s = { uid: this.data.nextUid++, skill: S.UNIT[u.id].skill };
      this.data.skills.push(s);
      return s;
    }

    // --- 무공 장착 ---
    equippedBy(uid) { return this.data.units.find(u => u.equipped.includes(uid)) || null; }

    // 장착할 수 없으면 이유, 할 수 있으면 null. 다른 무인이 장착 중이면 옮겨 온다
    equipError(u, slot, s) {
      if (s.skill === S.UNIT[u.id].skill) return '자기 무공과 같습니다.';
      const other = this.findSkill(u.equipped[1 - slot]);
      if (other && other.uid !== s.uid && other.skill === s.skill) return '다른 칸에 같은 무공이 있습니다.';
      return null;
    }

    equip(u, slot, s) {
      const err = this.equipError(u, slot, s);
      if (err) throw new Error(err);
      const holder = this.equippedBy(s.uid);
      if (holder) holder.equipped[holder.equipped.indexOf(s.uid)] = -1;
      u.equipped[slot] = s.uid;
    }

    unequip(u, slot) { u.equipped[slot] = -1; }

    // 자유 스탯을 더한 값 (화면 표시용). 전투에는 장비 칸(gear)으로 더해 넘긴다
    statValue(u, k) { return S.UNIT[u.id][STAT_KEYS[k]] + u.added[k]; }

    // --- 덱 편성 ---
    // pos 자리에 무인을 넣는다. 이미 다른 자리에 있으면 두 자리를 맞바꾼다
    setDeckUnit(pos, unitId) {
      const units = this.data.deck.units;
      const prev = units.indexOf(unitId);
      if (prev >= 0) units[prev] = units[pos];
      units[pos] = unitId;
    }

    buildTeam() {
      return {
        element: this.data.deck.element,
        slots: this.data.deck.units.map(id => {
          const u = this.find(id);
          if (!u) return { unit: null, skills: [] };
          // 자유 스탯은 무인 스탯에 더한 뒤 적성 배수를 곱한다 (C# EffectiveDef와 같은 결과)
          const gear = {};
          STAT_KEYS.forEach((k, i) => { if (u.added[i]) gear[k] = u.added[i]; });
          return { unit: id, skills: u.equipped.map(uid => this.findSkill(uid)?.skill).filter(Boolean), gear };
        }),
      };
    }

    validateDeck() { return S.validateTeam(this.buildTeam()); }

    // --- 전투 ---
    // 임시 상대: 무작위 무인 3명 + 무작위 속성, 추가 무공 없음
    randomOpponent() {
      const pool = S.UNITS.slice();
      const picks = [];
      for (let i = 0; i < 3; i++) picks.push(pool.splice(this.pick(pool.length), 1)[0]);
      return { element: S.ELEMENTS[this.pick(S.ELEMENTS.length)], slots: picks.map(d => ({ unit: d.id, skills: [] })) };
    }

    fight(opponent) {
      const errors = this.validateDeck();
      if (errors.length) throw new Error(errors.join(' '));
      return new S.Battle(this.buildTeam(), opponent, { seed: this.pick(2147483647), log: true }).run();
    }
  }

  const api = { GameState, newData, stars, rank, STARS, DRAW_POOL, STAT_NAMES, POINTS_PER_PROMOTION };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.WuxiaGame = api;
})(typeof self !== 'undefined' ? self : this);
