// 무협 카드배틀 전투 엔진 (기획서 초안 규칙 구현)
// 브라우저에서는 window.WuxiaSim, Node에서는 module.exports 로 쓴다.
(function (root) {
  'use strict';

  // ---------- 규칙 상수 (기획서 수치, 밸런싱 대상) ----------
  const CFG = {
    rounds: 8,
    baseDmg: 300,        // 피해식의 B
    statK: 2,            // 피해식의 k (공격 - 방어 1당)
    counterBonus: 0.15,  // 오행 상극 시 주는 피해 ±15%
    formationCounter: 0.10, // 진법: 상극 우위 피해 추가 +10%p
    factionBonus: 0.10,  // 같은 세력 3명 시 스탯 +10%
    critMult: 1.5,
    healBase: 150,       // 회복식 기본값
  };
  const POS_NAMES = ['주장', '부장', '부장'];
  const TARGET_WEIGHT = [20, 40, 40];
  const ELEMENTS = ['금', '목', '토', '수', '화'];
  const BEATS = { 금: '목', 목: '토', 토: '수', 수: '화', 화: '금' }; // 앞이 뒤를 이긴다
  const APT_MULT = { S: 1.2, A: 1.0, B: 0.85, C: 0.7 };
  const TYPE_NAMES = { formation: '진법', aura: '개전', active: '발동', chase: '연계', passive: '상시' };
  const STATUS_NAMES = {
    stun: '점혈', confuse: '혼란', silence: '봉인', disarm: '무장해제',
    taunt: '도발', poison: '중독', bleed: '출혈', injury: '내상', guard: '호위',
  };
  const DEBUFFS = ['stun', 'confuse', 'silence', 'disarm', 'poison', 'bleed', 'injury'];

  // ---------- 무공 ----------
  // 모든 무공은 어느 한 무인의 무공이다. 추출하면 다른 무인의 무공창 2·3칸에 넣을 수 있다.
  // 피해 종류: ext = 무기 피해(외공 vs 호신), int = 내공 피해(내공 vs 내공)
  // type: formation(주장만, 팀 속성이 element와 같을 때만), aura, active(rate%), chase(rate%), passive
  // setup(b, u): 전투 시작 시 1회. cast(b, u, target?): 발동 시.
  // 진법: 그 속성을 강화한다. 공통으로 아군 전체의 상극 우위 피해가 커지고, 진법마다 효과가 하나 더 붙는다.
  function formation(id, name, element, desc, extra) {
    return { id, name, type: 'formation', element,
      desc: `${element} 속성 강화: 아군 전체 상극 우위 피해 +${CFG.formationCounter * 100}%p, ${desc}`,
      setup: (b, u) => { b.allies(u).forEach(a => b.addMod(a, 'counter', CFG.formationCounter, Infinity, name)); extra(b, u); } };
  }

  const SKILLS = [
    { id: 'cheongun', name: '청운검기', type: 'active', rate: 35,
      desc: '적 전체에게 내공 피해 90%',
      cast: (b, u) => b.pickEnemies(u, 3).forEach(t => b.damage(u, t, 'int', 0.9, '청운검기')) },
    { id: 'saengsaeng', name: '생생조화신공', type: 'aura',
      desc: '1~4합 동안 매 합 시작 시 아군 전체 회복 (계수 50%)',
      setup: (b, u) => u.hooks.roundStart.push(() => {
        if (b.round <= 4) b.allies(u).forEach(a => b.heal(u, a, 0.5, '생생조화신공'));
      }) },
    { id: 'geumgang', name: '금강불괴', type: 'passive',
      desc: '점혈 면역, 받는 무기 피해 -20%',
      setup: (b, u) => { u.immune.add('stun'); b.addMod(u, 'takenExt', -0.2, Infinity, '금강불괴'); } },
    { id: 'hyeolma', name: '혈마천강', type: 'active', rate: 40, prep: true,
      desc: '1합 기를 모은 뒤 적 1명에게 무기 피해 260% + 출혈(2합)',
      cast: (b, u) => b.pickEnemies(u, 1).forEach(t => {
        b.damage(u, t, 'ext', 2.6, '혈마천강');
        b.addStatus(u, t, 'bleed', 2, { kind: 'ext', coef: 0.4 });
      }) },
    { id: 'mandok', name: '만독귀환', type: 'active', rate: 40,
      desc: '적 2명에게 중독(3합, 매 합 내공 피해 50%) + 내상(2합)',
      cast: (b, u) => b.pickEnemies(u, 2).forEach(t => {
        b.addStatus(u, t, 'poison', 3, { kind: 'int', coef: 0.5 });
        b.addStatus(u, t, 'injury', 2);
      }) },
    { id: 'cheolbyeok', name: '철벽마갑', type: 'aura',
      desc: '1~3합 동안 자신에게 도발, 받는 피해 -30%',
      setup: (b, u) => { b.addStatus(u, u, 'taunt', 3); b.addMod(u, 'taken', -0.3, 3, '철벽마갑'); } },
    { id: 'changgung', name: '창궁검법', type: 'chase', rate: 45,
      desc: '평타 후 같은 적에게 무기 피해 160%',
      cast: (b, u, t) => b.damage(u, t, 'ext', 1.6, '창궁검법') },
    { id: 'mancheon', name: '만천화우', type: 'active', rate: 30,
      desc: '무작위 적에게 3회 무기 피해 70%, 타격마다 30% 확률로 출혈(2합)',
      cast: (b, u) => { for (let i = 0; i < 3; i++) b.pickEnemies(u, 1).forEach(t => {
        b.damage(u, t, 'ext', 0.7, '만천화우');
        if (b.rand() < 0.3) b.addStatus(u, t, 'bleed', 2, { kind: 'ext', coef: 0.4 });
      }); } },
    { id: 'ildo', name: '일도양단', type: 'chase', rate: 35,
      desc: '평타 후 같은 적에게 무기 피해 200% + 무장해제(1합)',
      cast: (b, u, t) => { b.damage(u, t, 'ext', 2.0, '일도양단'); b.addStatus(u, t, 'disarm', 1); } },
    formation('bukdu', '천강북두진', '금', '아군 전체 주는 피해 +12%',
      (b, u) => b.allies(u).forEach(a => b.addMod(a, 'dealt', 0.12, Infinity, '천강북두진'))),
    formation('cheongryong', '청룡진', '목', '매 합 시작 시 아군 전체 회복 (계수 25%)',
      (b, u) => u.hooks.roundStart.push(() => b.allies(u).forEach(a => b.heal(u, a, 0.25, '청룡진')))),
    formation('samjae', '삼재진', '토', '아군 전체 호신 +15%',
      (b, u) => b.allies(u).forEach(a => b.addMod(a, 'def', 0.15, Infinity, '삼재진'))),
    formation('hyeonmu', '현무진', '수', '아군 전체 내공 +15%',
      (b, u) => b.allies(u).forEach(a => b.addMod(a, 'int', 0.15, Infinity, '현무진'))),
    formation('jujak', '주작진', '화', '아군 전체 무기 피해 치명타 확률 +15%',
      (b, u) => b.allies(u).forEach(a => b.addMod(a, 'crit', 0.15, Infinity, '주작진'))),
    { id: 'cheongsim', name: '청심결', type: 'passive',
      desc: '매 합 시작 시 상태이상에 걸린 아군 1명의 상태이상 1개 해제',
      setup: (b, u) => u.hooks.roundStart.push(() => {
        const sick = b.allies(u).filter(a => DEBUFFS.some(k => a.status[k]));
        if (!sick.length) return;
        const a = sick[Math.floor(b.rand() * sick.length)];
        const k = DEBUFFS.find(k => a.status[k]);
        delete a.status[k];
        b.log(`${b.tag(u)}의 청심결: ${b.tag(a)}의 ${STATUS_NAMES[k]} 해제`);
      }) },
    { id: 'jeomhyeol', name: '점혈수', type: 'active', rate: 35,
      desc: '적 1명 점혈(1합)',
      cast: (b, u) => b.pickEnemies(u, 1).forEach(t => b.addStatus(u, t, 'stun', 1)) },
    { id: 'naesang', name: '내상장', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 내공 피해 120% + 내상(2합)',
      cast: (b, u, t) => { b.damage(u, t, 'int', 1.2, '내상장'); b.addStatus(u, t, 'injury', 2); } },
    { id: 'saja', name: '사자후', type: 'active', rate: 35,
      desc: '자신에게 도발(2합), 적 전체에게 내공 피해 50%',
      cast: (b, u) => { b.addStatus(u, u, 'taunt', 2); b.pickEnemies(u, 3).forEach(t => b.damage(u, t, 'int', 0.5, '사자후')); } },
    { id: 'bantan', name: '반탄경', type: 'passive',
      desc: '평타에 맞으면 30% 확률로 반격 (무기 피해 80%)',
      setup: (b, u) => u.hooks.damaged.push((src, info) => {
        if (info.normal && !src.dead && b.rand() < 0.3) b.damage(u, src, 'ext', 0.8, '반탄경', { counter: true });
      }) },
    { id: 'sangong', name: '산공독', type: 'active', rate: 35,
      desc: '적 2명 봉인(1합)',
      cast: (b, u) => b.pickEnemies(u, 2).forEach(t => b.addStatus(u, t, 'silence', 1)) },
    { id: 'seophon', name: '섭혼대법', type: 'active', rate: 30,
      desc: '적 1명 혼란(2합)',
      cast: (b, u) => b.pickEnemies(u, 1).forEach(t => b.addStatus(u, t, 'confuse', 2)) },
    { id: 'hoechun', name: '회춘공', type: 'active', rate: 40,
      desc: '체력 비율이 가장 낮은 아군 1명 회복 (계수 120%)',
      cast: (b, u) => {
        const t = b.allies(u).sort((x, y) => x.hp / x.maxHp - y.hp / y.maxHp)[0];
        b.heal(u, t, 1.2, '회춘공');
      } },
    { id: 'jikdo', name: '직도황룡', type: 'active', rate: 35, prep: true,
      desc: '1합 기를 모은 뒤 적 주장에게 무기 피해 280%',
      cast: (b, u) => { const t = b.leaderTarget(u); if (t) b.damage(u, t, 'ext', 2.8, '직도황룡'); } },
    { id: 'talmyeong', name: '탈명연환', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 무기 피해 120%, 대상이 출혈 상태면 무기 피해 120% 한 번 더',
      cast: (b, u, t) => {
        const bleeding = !!t.status.bleed;
        b.damage(u, t, 'ext', 1.2, '탈명연환');
        if (bleeding) b.damage(u, t, 'ext', 1.2, '탈명연환 추가타');
      } },
    { id: 'geumna', name: '금나수', type: 'chase', rate: 35,
      desc: '평타 후 같은 적 무장해제(1합), 대상이 내상 상태면 점혈(1합)도 부여',
      cast: (b, u, t) => { b.addStatus(u, t, 'disarm', 1); if (t.status.injury) b.addStatus(u, t, 'stun', 1); } },
    { id: 'howi', name: '호위결', type: 'aura',
      desc: '1~4합 동안 아군 주장이 받는 피해의 30%를 대신 받음 (자신이 주장이면 효과 없음)',
      setup: (b, u) => {
        const leader = b.allies(u).find(a => a.pos === 0);
        if (!leader || leader === u) return;
        b.addStatus(u, leader, 'guard', 4);
        if (leader.status.guard) Object.assign(leader.status.guard, { by: u, ratio: 0.3 });
      } },
    { id: 'gwangpung', name: '광풍쾌검', type: 'passive',
      desc: '무기 피해 치명타 확률 +25% (치명타 시 1.5배)',
      setup: (b, u) => b.addMod(u, 'crit', 0.25, Infinity, '광풍쾌검') },
  ];
  const SKILL = Object.fromEntries(SKILLS.map(s => [s.id, s]));

  // ---------- 무인 ----------
  // hp = 체력, spd = 경공. apt: 금목토수화 순서의 적성. skill: 자기 무공(무공창 1칸)
  const UNITS = [
    { id: 'baek', name: '백무진', title: '청운검주', faction: '정파', hp: 3000, ext: 150, int: 205, def: 120, spd: 160, apt: 'SABAB', skill: 'cheongun' },
    { id: 'seol', name: '설지향', title: '화산의선', faction: '정파', hp: 2800, ext: 90, int: 190, def: 110, spd: 150, apt: 'ABBSA', skill: 'saengsaeng' },
    { id: 'hye', name: '혜법', title: '금강권승', faction: '정파', hp: 3600, ext: 170, int: 120, def: 200, spd: 95, apt: 'ABSCB', skill: 'geumgang' },
    { id: 'hyeol', name: '혈영마군', title: '혈마전인', faction: '마교', hp: 3000, ext: 220, int: 110, def: 130, spd: 140, apt: 'BASCS', skill: 'hyeolma' },
    { id: 'dok', name: '독수귀녀', title: '만독곡주', faction: '마교', hp: 2700, ext: 80, int: 200, def: 100, spd: 175, apt: 'CBASA', skill: 'mandok' },
    { id: 'cheol', name: '철갑마웅', title: '흑풍당주', faction: '마교', hp: 3800, ext: 160, int: 90, def: 210, spd: 85, apt: 'BASBA', skill: 'cheolbyeok' },
    { id: 'namgung', name: '남궁휘', title: '창궁검룡', faction: '세가', hp: 3000, ext: 195, int: 130, def: 140, spd: 170, apt: 'SABBA', skill: 'changgung' },
    { id: 'dang', name: '당소소', title: '암기천후', faction: '세가', hp: 2700, ext: 185, int: 140, def: 100, spd: 185, apt: 'ASBAB', skill: 'mancheon' },
    { id: 'nameless', name: '무명도객', title: '낭인', faction: '사파', hp: 3200, ext: 210, int: 80, def: 150, spd: 130, apt: 'SBABA', skill: 'ildo' },
    { id: 'cheongheo', name: '청허진인', title: '무당장문', faction: '정파', hp: 3200, ext: 110, int: 170, def: 170, spd: 120, apt: 'ABSAB', skill: 'samjae' },
    { id: 'jin', name: '진세악', title: '종남검수', faction: '정파', hp: 3000, ext: 175, int: 140, def: 140, spd: 150, apt: 'SABBA', skill: 'bukdu' },
    { id: 'jeongyeon', name: '정연', title: '아미신니', faction: '정파', hp: 2800, ext: 90, int: 185, def: 120, spd: 155, apt: 'BABSA', skill: 'cheongsim' },
    { id: 'gongjin', name: '공진', title: '나한당주', faction: '정파', hp: 3500, ext: 160, int: 150, def: 190, spd: 100, apt: 'BASCA', skill: 'saja' },
    { id: 'ma', name: '마천우', title: '귀영수', faction: '사파', hp: 2700, ext: 170, int: 130, def: 100, spd: 190, apt: 'ASBAB', skill: 'jeomhyeol' },
    { id: 'guyang', name: '구양철', title: '철장방주', faction: '사파', hp: 3200, ext: 190, int: 150, def: 150, spd: 120, apt: 'BAABS', skill: 'naesang' },
    { id: 'yeon', name: '연소백', title: '쾌검랑', faction: '사파', hp: 2900, ext: 200, int: 100, def: 120, spd: 185, apt: 'SABCB', skill: 'gwangpung' },
    { id: 'maeng', name: '맹악', title: '흑수장군', faction: '마교', hp: 3600, ext: 150, int: 110, def: 200, spd: 90, apt: 'BBSAB', skill: 'bantan' },
    { id: 'hahu', name: '하후빈', title: '산공노조', faction: '마교', hp: 2800, ext: 100, int: 190, def: 110, spd: 160, apt: 'CBASA', skill: 'sangong' },
    { id: 'so', name: '소요령', title: '섭혼마녀', faction: '마교', hp: 2700, ext: 90, int: 210, def: 100, spd: 170, apt: 'CBBSA', skill: 'seophon' },
    { id: 'songhak', name: '송학', title: '녹림군사', faction: '사파', hp: 3000, ext: 120, int: 175, def: 140, spd: 145, apt: 'ASBBC', skill: 'cheongryong' },
    { id: 'moyong', name: '모용상', title: '모용세가주', faction: '세가', hp: 3200, ext: 130, int: 180, def: 160, spd: 130, apt: 'BACSB', skill: 'hyeonmu' },
    { id: 'jeokyeom', name: '적염후', title: '주작당주', faction: '마교', hp: 3000, ext: 170, int: 160, def: 130, spd: 140, apt: 'BCBAS', skill: 'jujak' },
    { id: 'paeng', name: '팽무혁', title: '하북도왕', faction: '세가', hp: 3100, ext: 215, int: 90, def: 140, spd: 160, apt: 'SBABC', skill: 'jikdo' },
    { id: 'sahyeol', name: '사혈귀', title: '혈수당주', faction: '마교', hp: 2900, ext: 200, int: 100, def: 120, spd: 175, apt: 'BSACA', skill: 'talmyeong' },
    { id: 'jangsam', name: '장삼', title: '금나귀수', faction: '사파', hp: 3000, ext: 180, int: 120, def: 140, spd: 165, apt: 'ABSBA', skill: 'geumna' },
    { id: 'cheolwi', name: '철위강', title: '호위대장', faction: '세가', hp: 3800, ext: 140, int: 100, def: 210, spd: 90, apt: 'BBSAA', skill: 'howi' },
    { id: 'jegal', name: '제갈운', title: '신의', faction: '세가', hp: 2800, ext: 80, int: 190, def: 120, spd: 140, apt: 'ABASB', skill: 'hoechun' },
  ];
  const UNIT = Object.fromEntries(UNITS.map(u => [u.id, u]));

  // ---------- 편성 검증 ----------
  // team = { element: '금', slots: [{ unit, skills: [id, id] } x3] } (slots[0] = 주장)
  function validateTeam(team) {
    const errors = [];
    if (!ELEMENTS.includes(team.element)) errors.push('속성을 고르세요.');
    const units = team.slots.map(s => s.unit);
    if (units.some(id => !UNIT[id])) errors.push('빈 자리가 있습니다.');
    if (new Set(units).size !== units.length) errors.push('같은 무인은 한 팀에 한 명만 넣을 수 있습니다.');
    const skills = [];
    team.slots.forEach((s, i) => {
      if (UNIT[s.unit]) skills.push(UNIT[s.unit].skill);
      s.skills.filter(Boolean).forEach(id => {
        if (!SKILL[id]) errors.push(`${i + 1}번 자리(${POS_NAMES[i]}): 없는 무공입니다.`);
        skills.push(id);
      });
    });
    const dup = skills.filter((id, i) => skills.indexOf(id) !== i);
    if (dup.length) errors.push(`같은 무공은 한 팀에 하나만: ${[...new Set(dup)].map(id => SKILL[id].name).join(', ')}`);
    return errors;
  }

  // ---------- 전투 ----------
  function mulberry32(seed) {
    return function () {
      seed |= 0; seed = seed + 0x6D2B79F5 | 0;
      let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
      t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
      return ((t ^ t >>> 14) >>> 0) / 4294967296;
    };
  }

  class Battle {
    constructor(teamA, teamB, opts = {}) {
      this.rand = mulberry32(opts.seed ?? 1);
      this.logging = !!opts.log;
      this.lines = [];
      this.round = 0;
      this.turnSeq = 0;
      this.over = false;
      this.teams = { A: teamA, B: teamB };
      this.units = [...this.buildSide('A', teamA), ...this.buildSide('B', teamB)];
    }

    buildSide(side, team) {
      const defs = team.slots.map(s => UNIT[s.unit]);
      const sameFaction = defs.every(d => d.faction === defs[0].faction);
      const ei = ELEMENTS.indexOf(team.element);
      return team.slots.map((s, pos) => {
        const d = UNIT[s.unit];
        const aptMult = APT_MULT[d.apt[ei]];
        const mult = aptMult * (sameFaction ? 1 + CFG.factionBonus : 1);
        return {
          side, pos, def: d, name: d.name, maxHp: d.hp, hp: d.hp, dead: false,
          base: { ext: d.ext * mult, int: d.int * mult, def: d.def * mult, spd: d.spd * mult },
          apt: d.apt[ei],
          skills: [d.skill, ...s.skills.filter(Boolean)].map(id => SKILL[id]),
          status: {}, mods: [], immune: new Set(), prep: null,
          hooks: { roundStart: [], damaged: [] },
          rec: { dealt: 0, healed: 0, taken: 0, procs: {}, bySkill: {} },
        };
      });
    }

    // --- 조회 ---
    tag(u) { return `[${u.side === 'A' ? '아' : '적'}] ${u.name}`; }
    log(text) { if (this.logging) this.lines.push({ round: this.round, text }); }
    alive(side) { return this.units.filter(u => u.side === side && !u.dead); }
    allies(u) { return this.alive(u.side); }
    foes(u) { return this.alive(u.side === 'A' ? 'B' : 'A'); }
    mod(u, key) { return u.mods.reduce((s, m) => s + (m.key === key ? m.value : 0), 0); }
    stat(u, key) { return u.base[key] * Math.max(0.1, 1 + this.mod(u, key)); }

    weightedPick(pool) {
      const total = pool.reduce((s, u) => s + TARGET_WEIGHT[u.pos], 0);
      let r = this.rand() * total;
      for (const u of pool) { r -= TARGET_WEIGHT[u.pos]; if (r < 0) return u; }
      return pool[pool.length - 1];
    }

    // n명 고르기. 3 이상이면 적 전체. 혼란 상태면 단일 대상이 적·아군 중 무작위.
    pickEnemies(u, n) {
      const foes = this.foes(u);
      if (n >= 3) return foes;
      let pool = u.status.confuse ? this.units.filter(x => !x.dead && x !== u) : foes;
      const out = [];
      while (out.length < n && pool.length) {
        const t = this.weightedPick(pool);
        out.push(t);
        pool = pool.filter(x => x !== t);
      }
      return out;
    }

    // 적 주장을 노림. 혼란 상태면 무작위.
    leaderTarget(u) {
      if (u.status.confuse) return this.pickEnemies(u, 1)[0];
      const foes = this.foes(u);
      return foes.find(f => f.pos === 0) || this.weightedPick(foes);
    }

    attackTarget(u) {
      if (u.status.confuse) return this.pickEnemies(u, 1)[0];
      const foes = this.foes(u);
      const taunter = foes.find(f => f.status.taunt);
      return taunter || this.weightedPick(foes);
    }

    // --- 효과 ---
    addMod(u, key, value, rounds, label) {
      u.mods = u.mods.filter(m => m.label !== label || m.key !== key); // 같은 출처는 갱신
      u.mods.push({ key, value, rounds, label, since: this.turnSeq });
    }

    addStatus(src, dst, key, rounds, dot) {
      if (dst.dead) return;
      if (dst.immune.has(key)) { this.log(`${this.tag(dst)}: ${STATUS_NAMES[key]} 면역`); return; }
      if (dst.status[key]) return; // 같은 종류는 중첩되지 않음
      const entry = { rounds, since: this.turnSeq };
      if (dot) Object.assign(entry, { tick: Math.round(this.rawDamage(src, dst, dot.kind, dot.coef)), src, label: this.acting });
      dst.status[key] = entry;
      this.log(`${this.tag(dst)}: ${STATUS_NAMES[key]} (${rounds}합)`);
    }

    rawDamage(src, dst, kind, coef) {
      const atk = this.stat(src, kind === 'ext' ? 'ext' : 'int');
      const dfn = this.stat(dst, kind === 'ext' ? 'def' : 'int');
      let dmg = (CFG.baseDmg + Math.max(0, atk - dfn) * CFG.statK) * coef;
      const eA = this.teams[src.side].element, eB = this.teams[dst.side].element;
      if (src.side !== dst.side) {
        if (BEATS[eA] === eB) dmg *= 1 + CFG.counterBonus + this.mod(src, 'counter');
        else if (BEATS[eB] === eA) dmg *= 1 - CFG.counterBonus;
      }
      dmg *= 0.6 + 0.4 * src.hp / src.maxHp; // 체력 보정
      dmg *= Math.max(0, 1 + this.mod(src, 'dealt'));
      dmg *= Math.max(0.1, 1 + this.mod(dst, 'taken') + this.mod(dst, kind === 'ext' ? 'takenExt' : 'takenInt'));
      return dmg;
    }

    damage(src, dst, kind, coef, label, opts = {}) {
      if (dst.dead || this.over) return 0;
      let dmg = this.rawDamage(src, dst, kind, coef) * (0.9 + 0.2 * this.rand());
      let crit = false;
      if (kind === 'ext' && this.rand() < this.mod(src, 'crit')) { dmg *= CFG.critMult; crit = true; }
      dmg = Math.max(1, Math.round(dmg));
      const g = dst.status.guard; // 호위: 피해 일부를 호위하는 무인이 대신 받음
      if (g && src.side !== dst.side && !g.by.dead) {
        const share = Math.round(dmg * g.ratio);
        if (share > 0) {
          dmg -= share;
          this.applyLoss(g.by, share, src, label);
          this.log(`${this.tag(g.by)}의 호위: ${this.tag(dst)} 대신 ${share} 피해 (남은 체력 ${g.by.hp})`);
        }
      }
      this.applyLoss(dst, dmg, src, label);
      this.log(`${this.tag(src)}의 ${label}${crit ? ' (치명타)' : ''} → ${this.tag(dst)} ${dmg} 피해 (${kind === 'ext' ? '무기' : '내공'}, 남은 체력 ${dst.hp})`);
      if (!dst.dead && !opts.counter) dst.hooks.damaged.forEach(h => h(src, { normal: !!opts.normal, kind }));
      return dmg;
    }

    // 무공별 집계. '탈명연환 추가타'처럼 꾸밈말이 붙은 이름은 무공 이름으로 묶는다.
    record(u, label, key, amt) {
      const name = label.split(' ')[0];
      const r = u.rec.bySkill[name] || (u.rec.bySkill[name] = { dealt: 0, healed: 0 });
      r[key] += amt;
    }

    applyLoss(dst, dmg, src, label) {
      dmg = Math.min(dmg, dst.hp);
      dst.hp -= dmg;
      dst.rec.taken += dmg;
      if (src && src.side !== dst.side) { src.rec.dealt += dmg; if (label) this.record(src, label, 'dealt', dmg); }
      if (dst.hp <= 0) {
        dst.dead = true;
        this.log(`${this.tag(dst)} 쓰러짐`);
        if (dst.pos === 0) this.over = true;
      }
    }

    heal(src, dst, coef, label) {
      if (dst.dead) return;
      let amt = (CFG.healBase + this.stat(src, 'int')) * coef * (0.9 + 0.2 * this.rand());
      if (dst.status.injury) amt *= 0.5;
      amt = Math.min(Math.round(amt), dst.maxHp - dst.hp);
      if (amt <= 0) return;
      dst.hp += amt;
      src.rec.healed += amt;
      this.record(src, label, 'healed', amt);
      this.log(`${this.tag(src)}의 ${label} → ${this.tag(dst)} ${amt} 회복${dst.status.injury ? ' (내상으로 반감)' : ''}`);
    }

    proc(u, s) { u.rec.procs[s.id] = (u.rec.procs[s.id] || 0) + 1; this.acting = s.name; }

    // --- 진행 ---
    run() {
      this.log('준비 합');
      const order = this.speedOrder();
      for (const type of ['formation', 'aura', 'passive']) {
        for (const u of order) {
          u.skills.filter(s => s.type === type).forEach(s => {
            if (type === 'formation' && (u.pos !== 0 || s.element !== this.teams[u.side].element)) return; // 진법은 주장만, 같은 속성만
            this.log(`${this.tag(u)}: ${TYPE_NAMES[type]} ${s.name} 적용`);
            s.setup(this, u);
          });
        }
      }
      for (this.round = 1; this.round <= CFG.rounds && !this.over; this.round++) {
        this.log(`${this.round}합`);
        for (const u of this.speedOrder()) u.hooks.roundStart.forEach(h => !u.dead && h());
        for (const u of this.speedOrder()) {
          if (this.over) break;
          if (!u.dead) this.takeTurn(u);
        }
      }
      return this.result();
    }

    speedOrder() {
      const list = this.units.filter(u => !u.dead).map(u => ({ u, k: this.stat(u, 'spd') + this.rand() * 0.01 }));
      return list.sort((a, b) => b.k - a.k).map(x => x.u);
    }

    takeTurn(u) {
      const turn = ++this.turnSeq;
      for (const k of ['poison', 'bleed']) {
        const st = u.status[k];
        if (st && !u.dead) {
          this.applyLoss(u, st.tick, st.src, st.label); // 지속 피해는 건 무공의 피해로 집계
          this.log(`${this.tag(u)}: ${STATUS_NAMES[k]} ${st.tick} 피해 (남은 체력 ${u.hp})`);
        }
      }
      if (u.dead || this.over) return;

      if (u.status.stun) {
        this.log(`${this.tag(u)}: 점혈로 움직이지 못함`);
        if (u.prep) { this.log(`${this.tag(u)}: ${u.prep.name} 준비가 끊김`); u.prep = null; }
      } else {
        // 발동 무공
        let released = null;
        if (u.prep) {
          const s = released = u.prep; u.prep = null;
          if (u.status.silence) this.log(`${this.tag(u)}: 봉인으로 ${s.name} 준비가 끊김`);
          else { this.log(`${this.tag(u)}: 발동 무공 ${s.name}`); this.proc(u, s); s.cast(this, u); }
        }
        for (const s of u.skills.filter(s => s.type === 'active')) {
          if (this.over || u.status.silence) break;
          if (s === released || this.rand() * 100 >= s.rate) continue;
          if (s.prep) { u.prep = s; this.log(`${this.tag(u)}: ${s.name} 기를 모음`); continue; }
          this.log(`${this.tag(u)}: 발동 무공 ${s.name}`);
          this.proc(u, s);
          s.cast(this, u);
        }
        // 평타 + 연계 무공
        if (!this.over && !u.dead && !u.status.disarm) {
          const t = this.attackTarget(u);
          if (t) {
            this.damage(u, t, 'ext', 1.0, '평타', { normal: true });
            for (const s of u.skills.filter(s => s.type === 'chase')) {
              if (this.over || u.dead || this.rand() * 100 >= s.rate) continue;
              const tt = t.dead ? this.attackTarget(u) : t;
              if (!tt) break;
              this.log(`${this.tag(u)}: 연계 무공 ${s.name}`);
              this.proc(u, s);
              s.cast(this, u, tt);
            }
          }
        } else if (!this.over && u.status.disarm) {
          this.log(`${this.tag(u)}: 무장해제로 평타 불가`);
        }
      }
      // 자기 차례가 끝날 때 지속 시간 1 감소 (이번 차례에 걸린 것은 제외)
      for (const k of Object.keys(u.status)) {
        const st = u.status[k];
        if (st.since < turn && --st.rounds <= 0) delete u.status[k];
      }
      u.mods = u.mods.filter(m => !(m.rounds !== Infinity && m.since < turn && --m.rounds <= 0));
    }

    result() {
      const rounds = Math.min(this.round - 1, CFG.rounds);
      const leaderDead = { A: this.units[0].dead, B: this.units[3].dead };
      let winner = null, reason = 'draw';
      if (leaderDead.A !== leaderDead.B) { winner = leaderDead.A ? 'B' : 'A'; reason = 'leader'; }
      else {
        // 8합 종료: 팀의 남은 체력 비율(남은 체력 합 ÷ 최대 체력 합)이 높은 쪽 승리, 같으면 무승부
        const ratio = side => {
          const us = this.units.filter(u => u.side === side);
          return us.reduce((s, u) => s + u.hp, 0) / us.reduce((s, u) => s + u.maxHp, 0);
        };
        const a = ratio('A'), b = ratio('B');
        if (a !== b) { winner = a > b ? 'A' : 'B'; reason = 'hp'; }
      }
      return { winner, reason, rounds, units: this.units, log: this.lines };
    }
  }

  function simulate(teamA, teamB, n, opts = {}) {
    const agg = { A: 0, B: 0, draw: 0, byHp: 0, rounds: 0, units: [] };
    for (let i = 0; i < n; i++) {
      const r = new Battle(teamA, teamB, { seed: (opts.seed ?? 1) + i }).run();
      if (r.winner) agg[r.winner]++; else agg.draw++;
      if (r.reason === 'hp') agg.byHp++;
      agg.rounds += r.rounds;
      r.units.forEach((u, j) => {
        const a = agg.units[j] || (agg.units[j] = { side: u.side, pos: u.pos, name: u.name, dealt: 0, healed: 0, taken: 0, deaths: 0, procs: {}, bySkill: {} });
        a.dealt += u.rec.dealt; a.healed += u.rec.healed; a.taken += u.rec.taken; a.deaths += u.dead ? 1 : 0;
        for (const [k, v] of Object.entries(u.rec.procs)) a.procs[k] = (a.procs[k] || 0) + v;
        for (const [k, v] of Object.entries(u.rec.bySkill)) {
          const t = a.bySkill[k] || (a.bySkill[k] = { dealt: 0, healed: 0 });
          t.dealt += v.dealt; t.healed += v.healed;
        }
      });
    }
    agg.n = n;
    agg.avgRounds = agg.rounds / n;
    return agg;
  }

  const api = { CFG, POS_NAMES, ELEMENTS, BEATS, APT_MULT, TYPE_NAMES, STATUS_NAMES, SKILLS, SKILL, UNITS, UNIT, validateTeam, Battle, simulate };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.WuxiaSim = api;
})(typeof self !== 'undefined' ? self : this);
