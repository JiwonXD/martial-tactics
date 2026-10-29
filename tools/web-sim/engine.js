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
    taunt: '도발', poison: '중독', bleed: '출혈', injury: '내상', guard: '호위', stealth: '은신',
  };
  const DEBUFFS = ['stun', 'confuse', 'silence', 'disarm', 'poison', 'bleed', 'injury'];
  const CONTROLS = ['stun', 'confuse', 'silence', 'disarm']; // 두전성이가 되돌리는 제어, 제어 적중·저항이 걸리는 상태
  // 장비 특수효과 (값은 비율, 0.05 = 5%). 장비 스탯 ext·int·def·spd는 무인 스탯에 더한 뒤 적성 배수를 곱한다.
  const GEAR_EFFECTS = { crit: '치명타율', critDmg: '치명타 피해', skillDmg: '무공 피해 증가', dmgReduce: '받는 피해 감소', evade: '회피', ctrlHit: '제어 적중', ctrlRes: '제어 저항' };

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
    { id: 'dujeon', name: '두전성이', type: 'passive',
      desc: '자신이 주장이면, 적이 아군에게 점혈·혼란·봉인·무장해제를 걸 때 건 적에게도 똑같이 건다',
      setup: (b, u) => { if (u.pos === 0) u.reflect = true; } },
    { id: 'sajung', name: '사중구활', type: 'passive',
      desc: '매 합 시작 시 아군 체력 비율이 적보다 낮으면 아군 전체 주는 피해 +15%, 높으면 아군 전체 받는 피해 -10% (그 합 동안)',
      setup: (b, u) => u.hooks.roundStart.push(() => {
        const mine = b.teamRatio(u.side), theirs = b.teamRatio(u.side === 'A' ? 'B' : 'A');
        b.allies(u).forEach(a => { a.mods = a.mods.filter(m => m.label !== '사중구활'); }); // 지난 합 효과 해제
        if (mine === theirs) return;
        const [key, value, text] = mine < theirs ? ['dealt', 0.15, '주는 피해 +15%'] : ['taken', -0.1, '받는 피해 -10%'];
        b.allies(u).forEach(a => b.addMod(a, key, value, Infinity, '사중구활'));
        b.log(`${b.tag(u)}의 사중구활: 체력 비율이 ${mine < theirs ? '낮아' : '높아'} 아군 전체 ${text}`);
      }) },
    { id: 'hyeolmaek', name: '혈맥상통', type: 'passive',
      desc: '3합부터 자신이 무기 피해를 줄 때마다 체력 비율이 가장 낮은 아군이 그 피해의 20%를 회복',
      setup: (b, u) => u.hooks.dealt.push((dmg, kind) => {
        if (b.round < 3 || kind !== 'ext') return;
        const t = b.allies(u).sort((x, y) => x.hp / x.maxHp - y.hp / y.maxHp)[0];
        b.healFlat(u, t, dmg * 0.2, '혈맥상통');
      }) },
    { id: 'gwangpung', name: '광풍쾌검', type: 'passive',
      desc: '무기 피해 치명타 확률 +25% (치명타 시 1.5배)',
      setup: (b, u) => b.addMod(u, 'crit', 0.25, Infinity, '광풍쾌검') },
    // ---- 출시 무인 50명 무공 (수치는 임시, 밸런스 때 시뮬레이터로 정함) ----
    { id: 'sorimgon', name: '소림곤법', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 무기 피해 130%, 2합 동안 자신이 받는 무기 피해 -15%',
      cast: (b, u, t) => { b.damage(u, t, 'ext', 1.3, '소림곤법'); b.addMod(u, 'takenExt', -0.15, 2, '소림곤법'); } },
    { id: 'yeonhwarak', name: '연화락', type: 'active', rate: 35,
      desc: '적 2명에게 무기 피해 80%, 2합 동안 그 적들이 받는 피해 +15%',
      cast: (b, u) => b.pickEnemies(u, 2).forEach(t => { b.damage(u, t, 'ext', 0.8, '연화락'); b.addMod(t, 'taken', 0.15, 2, '연화락'); }) },
    { id: 'chwigwon', name: '취권', type: 'passive',
      desc: '회피 +20%, 회피하면 50% 확률로 반격 (무기 피해 80%)',
      setup: (b, u) => {
        b.addMod(u, 'evade', 0.2, Infinity, '취권');
        u.hooks.evaded.push(src => { if (!src.dead && b.rand() < 0.5) b.damage(u, src, 'ext', 0.8, '취권', { counter: true }); });
      } },
    { id: 'dapseol', name: '답설무흔', type: 'aura',
      desc: '1~3합 동안 자신 경공 +20%, 회피 +15%',
      setup: (b, u) => { b.addMod(u, 'spd', 0.2, 3, '답설무흔'); b.addMod(u, 'evade', 0.15, 3, '답설무흔'); } },
    { id: 'tongbae', name: '통배권', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 무기 피해 120%, 2합 동안 대상 호신 -20%',
      cast: (b, u, t) => { b.damage(u, t, 'ext', 1.2, '통배권'); b.addMod(t, 'def', -0.2, 2, '통배권'); } },
    { id: 'sipgubong', name: '십구봉검', type: 'chase', rate: 35,
      desc: '평타 후 같은 적에게 무기 피해 80% 2회, 이 공격의 치명타 확률 +20%',
      cast: (b, u, t) => {
        b.addMod(u, 'crit', 0.2, Infinity, '십구봉검');
        b.damage(u, t, 'ext', 0.8, '십구봉검');
        b.damage(u, t, 'ext', 0.8, '십구봉검');
        u.mods = u.mods.filter(m => m.label !== '십구봉검');
      } },
    { id: 'cheonsa', name: '천사검', type: 'active', rate: 35,
      desc: '적 1명에게 내공 피해 140%, 자신에게 걸린 상태이상 1개 해제',
      cast: (b, u) => { b.pickEnemies(u, 1).forEach(t => b.damage(u, t, 'int', 1.4, '천사검')); b.cleanse(u, u, '천사검'); } },
    { id: 'nodo', name: '노도검', type: 'active', rate: 30,
      desc: '적 전체에게 무기 피해 70%',
      cast: (b, u) => b.pickEnemies(u, 3).forEach(t => b.damage(u, t, 'ext', 0.7, '노도검')) },
    { id: 'dogeom', name: '도검쌍행', type: 'active', rate: 35,
      desc: '적 2명에게 무기 피해 90%, 자신 외공 +8% (3번까지 쌓임)',
      cast: (b, u) => {
        b.pickEnemies(u, 2).forEach(t => b.damage(u, t, 'ext', 0.9, '도검쌍행'));
        b.addMod(u, 'ext', 0.08 * b.stack(u, '도검쌍행', 3), Infinity, '도검쌍행');
      } },
    { id: 'yukhap', name: '육합대창', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 무기 피해 130%, 대상이 주장이면 180%',
      cast: (b, u, t) => b.damage(u, t, 'ext', t.pos === 0 ? 1.8 : 1.3, '육합대창') },
    { id: 'bunggwon', name: '붕권', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 무기 피해 120%, 대상이 내상 상태면 180%',
      cast: (b, u, t) => b.damage(u, t, 'ext', t.status.injury ? 1.8 : 1.2, '붕권') },
    { id: 'chilseong', name: '칠성검', type: 'aura',
      desc: '1~3합 동안 아군 전체 받는 피해 -12%',
      setup: (b, u) => b.allies(u).forEach(a => b.addMod(a, 'taken', -0.12, 3, '칠성검')) },
    { id: 'namak', name: '남악검', type: 'active', rate: 35,
      desc: '적 1명에게 무기 피해 130% + 출혈(2합)',
      cast: (b, u) => b.pickEnemies(u, 1).forEach(t => {
        b.damage(u, t, 'ext', 1.3, '남악검');
        b.addStatus(u, t, 'bleed', 2, { kind: 'ext', coef: 0.4 });
      }) },
    { id: 'jungak', name: '중악검', type: 'active', rate: 35, prep: true,
      desc: '1합 기를 모은 뒤 적 2명에게 무기 피해 160%',
      cast: (b, u) => b.pickEnemies(u, 2).forEach(t => b.damage(u, t, 'ext', 1.6, '중악검')) },
    { id: 'bugak', name: '북악검', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 무기 피해 110%, 자신 호신 +6% (4번까지 쌓임)',
      cast: (b, u, t) => { b.damage(u, t, 'ext', 1.1, '북악검'); b.addMod(u, 'def', 0.06 * b.stack(u, '북악검', 4), Infinity, '북악검'); } },
    { id: 'cheolsa', name: '철사장', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 무기 피해 120% + 내상(2합)',
      cast: (b, u, t) => { b.damage(u, t, 'ext', 1.2, '철사장'); b.addStatus(u, t, 'injury', 2); } },
    { id: 'mosan', name: '모산부록', type: 'active', rate: 30,
      desc: '적 1명에게 내공 피해 100% + 혼란(1합)',
      cast: (b, u) => b.pickEnemies(u, 1).forEach(t => { b.damage(u, t, 'int', 1.0, '모산부록'); b.addStatus(u, t, 'confuse', 1); }) },
    { id: 'ilbu', name: '일부당관', type: 'aura',
      desc: '1~2합 동안 자신에게 도발, 도발 중 평타에 맞으면 30% 확률로 반격 (무기 피해 70%)',
      setup: (b, u) => {
        b.addStatus(u, u, 'taunt', 2);
        u.hooks.damaged.push((src, info) => {
          if (info.normal && u.status.taunt && !src.dead && b.rand() < 0.3) b.damage(u, src, 'ext', 0.7, '일부당관', { counter: true });
        });
      } },
    { id: 'sujeon', name: '수전', type: 'active', rate: 35,
      desc: '적 1명에게 무기 피해 100% + 봉인(1합)',
      cast: (b, u) => b.pickEnemies(u, 1).forEach(t => { b.damage(u, t, 'ext', 1.0, '수전'); b.addStatus(u, t, 'silence', 1); }) },
    { id: 'mihon', name: '미혼향', type: 'active', rate: 35,
      desc: '적 2명에게 중독(3합, 매 합 내공 피해 40%), 이미 중독된 적은 혼란(1합)',
      cast: (b, u) => b.pickEnemies(u, 2).forEach(t => {
        if (t.status.poison) b.addStatus(u, t, 'confuse', 1);
        else b.addStatus(u, t, 'poison', 3, { kind: 'int', coef: 0.4 });
      }) },
    { id: 'juwi', name: '주위상', type: 'passive',
      desc: '체력이 40% 아래로 처음 떨어지면 2합 동안 회피 +40%',
      setup: (b, u) => u.hooks.damaged.push(() => {
        if (u.stacks['주위상'] || u.hp / u.maxHp >= 0.4) return;
        b.stack(u, '주위상', 1);
        b.addMod(u, 'evade', 0.4, 2, '주위상');
        b.log(`${b.tag(u)}의 주위상: 2합 동안 회피 +40%`);
      }) },
    { id: 'sampan', name: '삼판부', type: 'active', rate: 35,
      desc: '적 1명에게 무기 피해, 1~3번째 발동은 180%, 그 뒤엔 80%',
      cast: (b, u) => b.pickEnemies(u, 1).forEach(t => b.damage(u, t, 'ext', u.rec.procs.sampan <= 3 ? 1.8 : 0.8, '삼판부')) },
    { id: 'yeonju', name: '연주전', type: 'active', rate: 35,
      desc: '무작위 적에게 3회 무기 피해 60%',
      cast: (b, u) => { for (let i = 0; i < 3; i++) b.pickEnemies(u, 1).forEach(t => b.damage(u, t, 'ext', 0.6, '연주전')); } },
    { id: 'nangri', name: '낭리백조', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 무기 피해 110%, 2합 동안 대상 경공 -20%',
      cast: (b, u, t) => { b.damage(u, t, 'ext', 1.1, '낭리백조'); b.addMod(t, 'spd', -0.2, 2, '낭리백조'); } },
    { id: 'biyeon', name: '비연도', type: 'aura',
      desc: '1합 시작 때 무작위 적 2명에게 무기 피해 60%, 1~3합 동안 자신 경공 +20%',
      setup: (b, u) => {
        b.addMod(u, 'spd', 0.2, 3, '비연도');
        u.hooks.roundStart.push(() => { if (b.round === 1) b.pickEnemies(u, 2).forEach(t => b.damage(u, t, 'ext', 0.6, '비연도')); });
      } },
    { id: 'jegalno', name: '제갈노', type: 'active', rate: 35,
      desc: '적 1명에게 3회 무기 피해 50%',
      cast: (b, u) => b.pickEnemies(u, 1).forEach(t => { for (let i = 0; i < 3; i++) b.damage(u, t, 'ext', 0.5, '제갈노'); }) },
    { id: 'dogung', name: '도궁비현', type: 'active', rate: 35,
      desc: '체력 비율이 가장 낮은 적 1명에게 무기 피해 160%',
      cast: (b, u) => { const t = b.lowestHpFoe(u); if (t) b.damage(u, t, 'ext', 1.6, '도궁비현'); } },
    { id: 'maehwa', name: '매화침', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 중독(3합, 매 합 내공 피해 40%), 이미 중독이면 중독 피해가 바로 한 번 터짐',
      cast: (b, u, t) => { if (t.status.poison) b.burstDot(t, 'poison'); else b.addStatus(u, t, 'poison', 3, { kind: 'int', coef: 0.4 }); } },
    { id: 'jamyeong', name: '잠영보', type: 'aura',
      desc: '1~2합 동안 은신 (적이 노리지 못함), 처음 주는 피해 +30%',
      setup: (b, u) => {
        b.addStatus(u, u, 'stealth', 2);
        b.addMod(u, 'dealt', 0.3, Infinity, '잠영보');
        u.hooks.dealt.push(() => { u.mods = u.mods.filter(m => m.label !== '잠영보'); });
      } },
    { id: 'wolha', name: '월하독작', type: 'passive',
      desc: '매 합 시작 시 쓰러진 아군 1명마다 자신 주는 피해 +15%',
      setup: (b, u) => u.hooks.roundStart.push(() => {
        const down = b.units.filter(a => a.side === u.side && a.dead).length;
        if (down) b.addMod(u, 'dealt', 0.15 * down, Infinity, '월하독작');
      }) },
    { id: 'poseung', name: '포승술', type: 'chase', rate: 35,
      desc: '평타 후 같은 적 무장해제(1합) + 2합 동안 경공 -20%',
      cast: (b, u, t) => { b.addStatus(u, t, 'disarm', 1); b.addMod(t, 'spd', -0.2, 2, '포승술'); } },
    { id: 'changnyong', name: '창룡출해', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 무기 피해 110%, 다른 적 1명에게도 60%',
      cast: (b, u, t) => {
        b.damage(u, t, 'ext', 1.1, '창룡출해');
        const others = b.targets(u).filter(x => x !== t);
        if (others.length) b.damage(u, b.weightedPick(others), 'ext', 0.6, '창룡출해');
      } },
    { id: 'wonang', name: '원앙검', type: 'active', rate: 35,
      desc: '적 2명에게 무기 피해 100%',
      cast: (b, u) => b.pickEnemies(u, 2).forEach(t => b.damage(u, t, 'ext', 1.0, '원앙검')) },
    { id: 'maengho', name: '맹호하산', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 무기 피해 110%, 대상 체력 비율이 70% 이상이면 170%',
      cast: (b, u, t) => b.damage(u, t, 'ext', t.hp / t.maxHp >= 0.7 ? 1.7 : 1.1, '맹호하산') },
    { id: 'hojo', name: '호조수', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 무기 피해 100% + 출혈(2합)',
      cast: (b, u, t) => { b.damage(u, t, 'ext', 1.0, '호조수'); b.addStatus(u, t, 'bleed', 2, { kind: 'ext', coef: 0.4 }); } },
    { id: 'deungpae', name: '등패술', type: 'passive',
      desc: '받는 무기 피해 -15%, 평타에 맞으면 25% 확률로 공격자 무장해제(1합)',
      setup: (b, u) => {
        b.addMod(u, 'takenExt', -0.15, Infinity, '등패술');
        u.hooks.damaged.push((src, info) => { if (info.normal && !src.dead && b.rand() < 0.25) b.addStatus(u, src, 'disarm', 1); });
      } },
    { id: 'yuseong', name: '유성추', type: 'active', rate: 35,
      desc: '적 1명에게 무기 피해 130%, 40% 확률로 점혈(1합)',
      cast: (b, u) => b.pickEnemies(u, 1).forEach(t => { b.damage(u, t, 'ext', 1.3, '유성추'); if (b.rand() < 0.4) b.addStatus(u, t, 'stun', 1); }) },
    { id: 'juhwa', name: '주화입마', type: 'passive',
      desc: '받는 피해 +10%, 매 합 시작 시 잃은 체력 비율만큼 주는 피해 증가 (최대 +40%)',
      setup: (b, u) => {
        b.addMod(u, 'taken', 0.1, Infinity, '주화입마 대가');
        u.hooks.roundStart.push(() => b.addMod(u, 'dealt', 0.4 * (1 - u.hp / u.maxHp), Infinity, '주화입마'));
      } },
    { id: 'idok', name: '이독공독', type: 'active', rate: 40,
      desc: '체력 비율이 가장 낮은 아군 회복 (계수 80%) + 적 1명 중독(2합, 매 합 내공 피해 40%)',
      cast: (b, u) => {
        b.heal(u, b.allies(u).sort((x, y) => x.hp / x.maxHp - y.hp / y.maxHp)[0], 0.8, '이독공독');
        b.pickEnemies(u, 1).forEach(t => b.addStatus(u, t, 'poison', 2, { kind: 'int', coef: 0.4 }));
      } },
    { id: 'gujeol', name: '구절편', type: 'active', rate: 35,
      desc: '적 2명에게 무기 피해 80%, 그중 1명 무장해제(1합)',
      cast: (b, u) => {
        const ts = b.pickEnemies(u, 2);
        ts.forEach(t => b.damage(u, t, 'ext', 0.8, '구절편'));
        if (ts.length) b.addStatus(u, ts[0], 'disarm', 1);
      } },
    { id: 'cheonmaryeong', name: '천마령', type: 'aura',
      desc: '1~3합 동안 아군 전체 경공 +15%',
      setup: (b, u) => b.allies(u).forEach(a => b.addMod(a, 'spd', 0.15, 3, '천마령')) },
    { id: 'hopyo', name: '호표', type: 'passive',
      desc: '아군 주장이 받는 피해의 25%를 대신 받고, 대신 받을 때 30% 확률로 반격 (무기 피해 80%). 자신이 주장이면 효과 없음',
      setup: (b, u) => {
        const leader = b.allies(u).find(a => a.pos === 0);
        if (!leader || leader === u) return;
        b.addStatus(u, leader, 'guard', CFG.rounds);
        if (leader.status.guard && !leader.status.guard.by) Object.assign(leader.status.guard, { by: u, ratio: 0.25 });
        u.hooks.guarded.push(src => { if (!src.dead && b.rand() < 0.3) b.damage(u, src, 'ext', 0.8, '호표', { counter: true }); });
      } },
    { id: 'jinwon', name: '진원창', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 무기 피해 110%, 1합 동안 체력 비율이 가장 낮은 아군 받는 피해 -15%',
      cast: (b, u, t) => {
        b.damage(u, t, 'ext', 1.1, '진원창');
        const a = b.allies(u).sort((x, y) => x.hp / x.maxHp - y.hp / y.maxHp)[0];
        if (a) b.addMod(a, 'taken', -0.15, 1, '진원창');
      } },
    { id: 'sain', name: '사인선사마', type: 'active', rate: 35,
      desc: '적 주장에게 무기 피해 120% + 2합 동안 주는 피해 -15%',
      cast: (b, u) => { const t = b.leaderTarget(u); if (t) { b.damage(u, t, 'ext', 1.2, '사인선사마'); b.addMod(t, 'dealt', -0.15, 2, '사인선사마'); } } },
    { id: 'mumyeong', name: '무명검', type: 'aura',
      desc: '준비 합에 적 1명을 표적으로 정해, 그 적에게 주는 피해 +25%',
      setup: (b, u) => {
        const [t] = b.pickEnemies(u, 1);
        if (!t) return;
        u.mark = { target: t, bonus: 0.25 };
        b.log(`${b.tag(u)}의 무명검: ${b.tag(t)}를 표적으로 정함`);
      } },
    { id: 'gyeokgong', name: '격공점혈', type: 'active', rate: 35,
      desc: '적 2명에게 각각 50% 확률로 점혈(1합)',
      cast: (b, u) => b.pickEnemies(u, 2).forEach(t => { if (b.rand() < 0.5) b.addStatus(u, t, 'stun', 1); }) },
    { id: 'sipmyeon', name: '십면매복', type: 'active', rate: 30,
      desc: '적 전체에게 내공 피해 60%, 각각 25% 확률로 혼란(1합)',
      cast: (b, u) => b.pickEnemies(u, 3).forEach(t => { b.damage(u, t, 'int', 0.6, '십면매복'); if (b.rand() < 0.25) b.addStatus(u, t, 'confuse', 1); }) },
    { id: 'pojeong', name: '포정해우', type: 'chase', rate: 40,
      desc: '평타 후 같은 적에게 무기 피해 120%, 대상 호신의 40%를 무시',
      cast: (b, u, t) => b.damage(u, t, 'ext', 1.2, '포정해우', { pierce: 0.4 }) },
    { id: 'sunsu', name: '순수추주', type: 'passive',
      desc: '받은 무기 피해의 25%를 공격자에게 되돌림',
      setup: (b, u) => u.hooks.damaged.push((src, info) => {
        if (info.kind === 'ext' && src.side !== u.side && !src.dead) b.flatDamage(u, src, info.dmg * 0.25, '순수추주');
      }) },
    { id: 'sinnong', name: '신농백초', type: 'active', rate: 40,
      desc: '체력 비율이 가장 낮은 아군의 상태이상 1개 해제 + 회복 (계수 100%)',
      cast: (b, u) => {
        const a = b.allies(u).sort((x, y) => x.hp / x.maxHp - y.hp / y.maxHp)[0];
        b.cleanse(u, a, '신농백초');
        b.heal(u, a, 1.0, '신농백초');
      } },
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
    { id: 'namgung', name: '남궁휘', title: '창궁검룡', faction: '정파', hp: 3000, ext: 195, int: 130, def: 140, spd: 170, apt: 'SABBA', skill: 'changgung' },
    { id: 'dang', name: '당소소', title: '암기천후', faction: '정파', hp: 2700, ext: 185, int: 140, def: 100, spd: 185, apt: 'ASBAB', skill: 'mancheon' },
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
    { id: 'moyong', name: '모용상', title: '모용세가주', faction: '정파', hp: 3200, ext: 130, int: 180, def: 160, spd: 130, apt: 'BACSB', skill: 'hyeonmu' },
    { id: 'jeokyeom', name: '적염후', title: '주작당주', faction: '마교', hp: 3000, ext: 170, int: 160, def: 130, spd: 140, apt: 'BCBAS', skill: 'jujak' },
    { id: 'paeng', name: '팽무혁', title: '하북도왕', faction: '정파', hp: 3100, ext: 215, int: 90, def: 140, spd: 160, apt: 'SBABC', skill: 'jikdo' },
    { id: 'sahyeol', name: '사혈귀', title: '혈수당주', faction: '마교', hp: 2900, ext: 200, int: 100, def: 120, spd: 175, apt: 'BSACA', skill: 'talmyeong' },
    { id: 'jangsam', name: '장삼', title: '금나귀수', faction: '사파', hp: 3000, ext: 180, int: 120, def: 140, spd: 165, apt: 'ABSBA', skill: 'geumna' },
    { id: 'cheolwi', name: '철위강', title: '호위대장', faction: '정파', hp: 3800, ext: 140, int: 100, def: 210, spd: 90, apt: 'BBSAA', skill: 'howi' },
    { id: 'moyongyeon', name: '모용연', title: '두전성이 전인', faction: '정파', hp: 3000, ext: 120, int: 195, def: 140, spd: 150, apt: 'ABBSA', skill: 'dujeon' },
    { id: 'dokgo', name: '독고패', title: '낭인검귀', faction: '사파', hp: 3100, ext: 205, int: 110, def: 130, spd: 155, apt: 'SABCB', skill: 'sajung' },
    { id: 'baengni', name: '백리청', title: '의협', faction: '정파', hp: 3100, ext: 190, int: 130, def: 140, spd: 145, apt: 'ASBBA', skill: 'hyeolmaek' },
    { id: 'jegal', name: '제갈운', title: '신의', faction: '정파', hp: 2800, ext: 80, int: 190, def: 120, spd: 140, apt: 'ABASB', skill: 'hoechun' },
    // 출시 무인 50명 (스탯은 역할별 임시값, 밸런스 때 시뮬레이터로 정함)
    { id: 'jimyeong', name: '지명', title: '소림 지자배 승려', faction: '정파', hp: 3400, ext: 145, int: 105, def: 195, spd: 100, apt: 'BCSBA', skill: 'sorimgon' },
    { id: 'wonsosam', name: '원소삼', title: '삼결 걸개', faction: '정파', hp: 3000, ext: 170, int: 140, def: 135, spd: 150, apt: 'CSBAB', skill: 'yeonhwarak' },
    { id: 'danorang', name: '단오랑', title: '취권 여개', faction: '정파', hp: 2900, ext: 190, int: 105, def: 115, spd: 180, apt: 'ABCSB', skill: 'chwigwon' },
    { id: 'mokunbi', name: '목운비', title: '곤륜신행', faction: '정파', hp: 2700, ext: 180, int: 115, def: 105, spd: 190, apt: 'CSBAB', skill: 'dapseol' },
    { id: 'dojungak', name: '도중악', title: '공동철권', faction: '정파', hp: 3100, ext: 195, int: 110, def: 145, spd: 140, apt: 'BCSBA', skill: 'tongbae' },
    { id: 'yeopcheong', name: '엽청', title: '점창쾌검', faction: '정파', hp: 2900, ext: 190, int: 105, def: 115, spd: 180, apt: 'SBABC', skill: 'sipgubong' },
    { id: 'haunhak', name: '하운학', title: '청성검객', faction: '정파', hp: 2700, ext: 85, int: 200, def: 105, spd: 160, apt: 'ABCSB', skill: 'cheonsa' },
    { id: 'seokhaerang', name: '석해랑', title: '해남검수', faction: '정파', hp: 3100, ext: 195, int: 110, def: 145, spd: 140, apt: 'ABCSB', skill: 'nodo' },
    { id: 'hwangboyeol', name: '황보열', title: '황보세가 소가주', faction: '정파', hp: 3360, ext: 210, int: 110, def: 160, spd: 140, apt: 'SBABC', skill: 'dogeom' },
    { id: 'akjinmyeong', name: '악진명', title: '악가창수', faction: '정파', hp: 3000, ext: 190, int: 115, def: 140, spd: 145, apt: 'SBABC', skill: 'yukhap' },
    { id: 'eonseoyeon', name: '언서연', title: '언가권녀', faction: '정파', hp: 3100, ext: 195, int: 110, def: 145, spd: 140, apt: 'BCSBA', skill: 'bunggwon' },
    { id: 'cheongyangja', name: '청양자', title: '전진도사', faction: '정파', hp: 3000, ext: 105, int: 180, def: 130, spd: 140, apt: 'SBABC', skill: 'chilseong' },
    { id: 'yusocheong', name: '유소청', title: '형산검녀', faction: '정파', hp: 2900, ext: 165, int: 145, def: 130, spd: 155, apt: 'BABCS', skill: 'namak' },
    { id: 'gwakjin', name: '곽진', title: '숭산검수', faction: '정파', hp: 3100, ext: 195, int: 110, def: 145, spd: 140, apt: 'BCSBA', skill: 'jungak' },
    { id: 'wijagyeong', name: '위자경', title: '항산제자', faction: '정파', hp: 3600, ext: 155, int: 95, def: 205, spd: 90, apt: 'BCSBA', skill: 'bugak' },
    { id: 'notaegyeong', name: '노태경', title: '태산권사', faction: '정파', hp: 3000, ext: 190, int: 115, def: 140, spd: 145, apt: 'BCSBA', skill: 'cheolsa' },
    { id: 'jangbuun', name: '장부운', title: '모산도사', faction: '정파', hp: 2800, ext: 90, int: 195, def: 110, spd: 155, apt: 'ABCSB', skill: 'mosan' },
    { id: 'maenggwan', name: '맹관', title: '검각수문검', faction: '정파', hp: 3600, ext: 155, int: 95, def: 205, spd: 90, apt: 'SBABC', skill: 'ilbu' },
    { id: 'soyuk', name: '소육', title: '하오문 정보꾼', faction: '사파', hp: 2700, ext: 180, int: 115, def: 105, spd: 190, apt: 'CSBAB', skill: 'sujeon' },
    { id: 'hongmae', name: '홍매', title: '기루 주인', faction: '사파', hp: 2800, ext: 90, int: 195, def: 110, spd: 155, apt: 'ABCSB', skill: 'mihon' },
    { id: 'noesam', name: '뇌삼', title: '객잔 점소이', faction: '사파', hp: 2900, ext: 190, int: 105, def: 115, spd: 180, apt: 'CSBAB', skill: 'juwi' },
    { id: 'udaeho', name: '우대호', title: '녹림 도부수', faction: '사파', hp: 3000, ext: 190, int: 115, def: 140, spd: 145, apt: 'BABCS', skill: 'sampan' },
    { id: 'jinryeong', name: '진령', title: '녹림 궁수', faction: '사파', hp: 2800, ext: 185, int: 110, def: 110, spd: 185, apt: 'CSBAB', skill: 'yeonju' },
    { id: 'isam', name: '이삼', title: '장강수귀', faction: '사파', hp: 3100, ext: 175, int: 135, def: 140, spd: 145, apt: 'ABCSB', skill: 'nangri' },
    { id: 'suyeon', name: '수연', title: '장강비연', faction: '사파', hp: 2700, ext: 180, int: 115, def: 105, spd: 190, apt: 'SBABC', skill: 'biyeon' },
    { id: 'gangil', name: '강일', title: '철노 수적', faction: '사파', hp: 3000, ext: 170, int: 140, def: 135, spd: 150, apt: 'CSBAB', skill: 'jegalno' },
    { id: 'chilho', name: '칠호', title: '살막 칠호', faction: '사파', hp: 2900, ext: 190, int: 105, def: 115, spd: 180, apt: 'SBABC', skill: 'dogung' },
    { id: 'heukjeop', name: '흑접', title: '살막 흑접', faction: '사파', hp: 2700, ext: 180, int: 115, def: 105, spd: 190, apt: 'ABCSB', skill: 'maehwa' },
    { id: 'muyeong', name: '무영', title: '살막 무영', faction: '사파', hp: 2940, ext: 195, int: 115, def: 115, spd: 195, apt: 'SBABC', skill: 'jamyeong' },
    { id: 'gowol', name: '고월', title: '월하낭인', faction: '사파', hp: 3200, ext: 200, int: 105, def: 150, spd: 135, apt: 'BABCS', skill: 'wolha' },
    { id: 'jobaek', name: '조백', title: '현상 사냥꾼', faction: '사파', hp: 2900, ext: 165, int: 145, def: 130, spd: 155, apt: 'BCSBA', skill: 'poseung' },
    { id: 'yongcheonho', name: '용천호', title: '청룡당 창수', faction: '마교', hp: 3100, ext: 195, int: 110, def: 145, spd: 140, apt: 'CSBAB', skill: 'changnyong' },
    { id: 'cheongrin', name: '청린', title: '청룡당 쌍검녀', faction: '마교', hp: 2900, ext: 190, int: 105, def: 115, spd: 180, apt: 'CSBAB', skill: 'wonang' },
    { id: 'baekcheolsan', name: '백철산', title: '백호당 도객', faction: '마교', hp: 3000, ext: 190, int: 115, def: 140, spd: 145, apt: 'SBABC', skill: 'maengho' },
    { id: 'baeksogyo', name: '백소교', title: '백호당 조법녀', faction: '마교', hp: 2800, ext: 185, int: 110, def: 110, spd: 185, apt: 'SBABC', skill: 'hojo' },
    { id: 'gugap', name: '구갑', title: '현무당 방패수', faction: '마교', hp: 3600, ext: 155, int: 95, def: 205, spd: 90, apt: 'BCSBA', skill: 'deungpae' },
    { id: 'dunsan', name: '둔산', title: '현무당 철퇴수', faction: '마교', hp: 3400, ext: 145, int: 105, def: 195, spd: 100, apt: 'BCSBA', skill: 'yuseong' },
    { id: 'gwaksam', name: '곽삼', title: '천마 광도', faction: '마교', hp: 3100, ext: 195, int: 110, def: 145, spd: 140, apt: 'BABCS', skill: 'juhwa' },
    { id: 'yuhwa', name: '유화', title: '마교 약녀', faction: '마교', hp: 3000, ext: 105, int: 180, def: 130, spd: 140, apt: 'ABCSB', skill: 'idok' },
    { id: 'yeonbi', name: '연비', title: '소마녀', faction: '마교', hp: 2900, ext: 165, int: 145, def: 130, spd: 155, apt: 'BABCS', skill: 'gujeol' },
    { id: 'heukui', name: '흑의사자', title: '천마령 전령', faction: '마교', hp: 2900, ext: 100, int: 185, def: 125, spd: 145, apt: 'ABCSB', skill: 'cheonmaryeong' },
    { id: 'jangwiwon', name: '장위원', title: '진원표국 표두', faction: '신비', hp: 3780, ext: 165, int: 100, def: 215, spd: 95, apt: 'BCSBA', skill: 'hopyo' },
    { id: 'jeonsoho', name: '전소호', title: '표사', faction: '신비', hp: 2900, ext: 165, int: 145, def: 130, spd: 155, apt: 'CSBAB', skill: 'jinwon' },
    { id: 'haryeong', name: '하령', title: '여표사', faction: '신비', hp: 2800, ext: 185, int: 110, def: 110, spd: 185, apt: 'CSBAB', skill: 'sain' },
    { id: 'bokmyeon', name: '복면객', title: '정체불명의 복면객', faction: '신비', hp: 3200, ext: 200, int: 105, def: 150, spd: 135, apt: 'SBABC', skill: 'mumyeong' },
    { id: 'hoeui', name: '회의집사', title: '회의 집사', faction: '신비', hp: 2700, ext: 85, int: 200, def: 105, spd: 160, apt: 'ABCSB', skill: 'gyeokgong' },
    { id: 'eunmyeon', name: '은면여인', title: '은가면 여인', faction: '신비', hp: 2800, ext: 90, int: 195, def: 110, spd: 155, apt: 'BABCS', skill: 'sipmyeon' },
    { id: 'wangdaega', name: '왕대가', title: '객잔 숙수', faction: '신비', hp: 3200, ext: 200, int: 105, def: 150, spd: 135, apt: 'BABCS', skill: 'pojeong' },
    { id: 'nocho', name: '노초', title: '나루터 뱃사공', faction: '신비', hp: 3400, ext: 145, int: 105, def: 195, spd: 100, apt: 'ABCSB', skill: 'sunsu' },
    { id: 'sana', name: '산아', title: '약초꾼 소녀', faction: '신비', hp: 2900, ext: 100, int: 185, def: 125, spd: 145, apt: 'CSBAB', skill: 'sinnong' },
  ];
  const UNIT = Object.fromEntries(UNITS.map(u => [u.id, u]));

  // ---------- 편성 검증 ----------
  // team = { element: '금', slots: [{ unit, skills: [id, id], gear? } x3] } (slots[0] = 주장)
  // gear = 장비 합계 { ext, int, def, spd, crit, critDmg, skillDmg, dmgReduce, evade, ctrlHit, ctrlRes } (없는 값은 0)
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
        const g = s.gear || {};
        const aptMult = APT_MULT[d.apt[ei]];
        const mult = aptMult * (sameFaction ? 1 + CFG.factionBonus : 1);
        const u = {
          side, pos, def: d, name: d.name, maxHp: d.hp, hp: d.hp, dead: false,
          base: { ext: (d.ext + (g.ext || 0)) * mult, int: (d.int + (g.int || 0)) * mult, def: (d.def + (g.def || 0)) * mult, spd: (d.spd + (g.spd || 0)) * mult },
          apt: d.apt[ei],
          skills: [d.skill, ...s.skills.filter(Boolean)].map(id => SKILL[id]),
          status: {}, mods: [], immune: new Set(), prep: null,
          hooks: { roundStart: [], damaged: [], dealt: [], evaded: [], guarded: [] },
          stacks: {}, mark: null,
          rec: { dealt: 0, healed: 0, taken: 0, procs: {}, bySkill: {} },
        };
        // 장비 특수효과는 전투 내내 걸린 효과로 둔다. 받는 피해 감소는 taken에 더한다.
        for (const k of Object.keys(GEAR_EFFECTS)) if (g[k]) this.addMod(u, k === 'dmgReduce' ? 'taken' : k, k === 'dmgReduce' ? -g[k] : g[k], Infinity, '장비');
        return u;
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

    // 노릴 수 있는 적. 은신한 적은 빼고, 모두 은신했으면 전부.
    targets(u) {
      const foes = this.foes(u);
      const seen = foes.filter(f => !f.status.stealth);
      return seen.length ? seen : foes;
    }

    // n명 고르기. 3 이상이면 적 전체. 혼란 상태면 단일 대상이 적·아군 중 무작위.
    pickEnemies(u, n) {
      const foes = this.targets(u);
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
      const foes = this.targets(u);
      return foes.find(f => f.pos === 0) || this.weightedPick(foes);
    }

    // 체력 비율이 가장 낮은 적. 같으면 앞자리. 혼란 상태면 무작위.
    lowestHpFoe(u) {
      if (u.status.confuse) return this.pickEnemies(u, 1)[0];
      return this.targets(u).reduce((best, f) => !best || f.hp / f.maxHp < best.hp / best.maxHp ? f : best, null);
    }

    attackTarget(u) {
      if (u.status.confuse) return this.pickEnemies(u, 1)[0];
      const foes = this.targets(u);
      const taunter = foes.find(f => f.status.taunt);
      return taunter || this.weightedPick(foes);
    }

    // --- 효과 ---
    addMod(u, key, value, rounds, label) {
      u.mods = u.mods.filter(m => m.label !== label || m.key !== key); // 같은 출처는 갱신
      u.mods.push({ key, value, rounds, label, since: this.turnSeq });
    }

    // 같은 출처 효과를 max번까지 쌓는다. 쌓인 횟수를 돌려준다.
    stack(u, label, max) {
      u.stacks[label] = Math.min(max, (u.stacks[label] || 0) + 1);
      return u.stacks[label];
    }

    // 상태이상 1개 해제 (청심결과 같은 순서)
    cleanse(src, a, label) {
      const k = DEBUFFS.find(k => a.status[k]);
      if (!k) return;
      delete a.status[k];
      this.log(`${this.tag(src)}의 ${label}: ${this.tag(a)}의 ${STATUS_NAMES[k]} 해제`);
    }

    addStatus(src, dst, key, rounds, dot, reflected) {
      if (dst.dead) return;
      if (dst.immune.has(key)) { this.log(`${this.tag(dst)}: ${STATUS_NAMES[key]} 면역`); return; }
      if (dst.status[key]) return; // 같은 종류는 중첩되지 않음
      if (CONTROLS.includes(key) && src.side !== dst.side) { // 제어 저항: (저항 - 공격자 제어 적중) 확률로 막음
        const resist = this.mod(dst, 'ctrlRes') - this.mod(src, 'ctrlHit');
        if (resist > 0 && this.rand() < resist) { this.log(`${this.tag(dst)}: ${STATUS_NAMES[key]} 저항`); return; }
      }
      const entry = { rounds, since: this.turnSeq };
      if (dot) Object.assign(entry, { tick: Math.round(this.rawDamage(src, dst, dot.kind, dot.coef, true)), src, label: this.acting });
      dst.status[key] = entry;
      this.log(`${this.tag(dst)}: ${STATUS_NAMES[key]} (${rounds}합)`);
      if (!reflected && CONTROLS.includes(key) && src.side !== dst.side && !src.dead) {
        const r = this.allies(dst).find(a => a.reflect);
        if (r && !src.status[key]) { this.log(`${this.tag(r)}의 두전성이: ${STATUS_NAMES[key]}를 되돌림`); this.addStatus(r, src, key, rounds, null, true); }
      }
    }

    // skill = 무공으로 주는 피해 (평타 아님). 장비의 무공 피해 증가가 붙는다.
    // pierce = 방어(호신·내공)를 무시하는 비율
    rawDamage(src, dst, kind, coef, skill, pierce = 0) {
      const atk = this.stat(src, kind === 'ext' ? 'ext' : 'int');
      const dfn = this.stat(dst, kind === 'ext' ? 'def' : 'int') * (1 - pierce);
      let dmg = (CFG.baseDmg + Math.max(0, atk - dfn) * CFG.statK) * coef;
      const eA = this.teams[src.side].element, eB = this.teams[dst.side].element;
      if (src.side !== dst.side) {
        if (BEATS[eA] === eB) dmg *= 1 + CFG.counterBonus + this.mod(src, 'counter');
        else if (BEATS[eB] === eA) dmg *= 1 - CFG.counterBonus;
      }
      dmg *= 0.6 + 0.4 * src.hp / src.maxHp; // 체력 보정
      dmg *= Math.max(0, 1 + this.mod(src, 'dealt'));
      if (src.mark && src.mark.target === dst) dmg *= 1 + src.mark.bonus; // 표적
      if (skill) dmg *= 1 + this.mod(src, 'skillDmg');
      dmg *= Math.max(0.1, 1 + this.mod(dst, 'taken') + this.mod(dst, kind === 'ext' ? 'takenExt' : 'takenInt'));
      return dmg;
    }

    damage(src, dst, kind, coef, label, opts = {}) {
      if (dst.dead || this.over) return 0;
      if (src.side !== dst.side) { // 회피: 지속 피해는 피하지 못함
        const evade = this.mod(dst, 'evade');
        if (evade > 0 && this.rand() < evade) {
          this.log(`${this.tag(src)}의 ${label} → ${this.tag(dst)} 회피`);
          if (!opts.counter) dst.hooks.evaded.forEach(h => h(src));
          return 0;
        }
      }
      let dmg = this.rawDamage(src, dst, kind, coef, label !== '평타', opts.pierce || 0) * (0.9 + 0.2 * this.rand());
      let crit = false;
      if (kind === 'ext' && this.rand() < this.mod(src, 'crit')) { dmg *= CFG.critMult + this.mod(src, 'critDmg'); crit = true; }
      dmg = Math.max(1, Math.round(dmg));
      const g = dst.status.guard; // 호위: 피해 일부를 호위하는 무인이 대신 받음
      if (g && src.side !== dst.side && !g.by.dead) {
        const share = Math.round(dmg * g.ratio);
        if (share > 0) {
          dmg -= share;
          this.applyLoss(g.by, share, src, label);
          this.log(`${this.tag(g.by)}의 호위: ${this.tag(dst)} 대신 ${share} 피해 (남은 체력 ${g.by.hp})`);
          if (!g.by.dead && !opts.counter) g.by.hooks.guarded.forEach(h => h(src));
        }
      }
      this.applyLoss(dst, dmg, src, label);
      this.log(`${this.tag(src)}의 ${label}${crit ? ' (치명타)' : ''} → ${this.tag(dst)} ${dmg} 피해 (${kind === 'ext' ? '무기' : '내공'}, 남은 체력 ${dst.hp})`);
      if (!dst.dead && !opts.counter) dst.hooks.damaged.forEach(h => h(src, { normal: !!opts.normal, kind, dmg }));
      if (src.side !== dst.side && !src.dead) src.hooks.dealt.forEach(h => h(dmg, kind));
      return dmg;
    }

    // 정해진 양을 그대로 주는 피해 (회피·치명타·호위 없음). 받은 피해 되돌리기에 쓴다.
    flatDamage(src, dst, amt, label) {
      if (dst.dead || this.over) return;
      amt = Math.max(1, Math.round(amt));
      this.applyLoss(dst, amt, src, label);
      this.log(`${this.tag(src)}의 ${label} → ${this.tag(dst)} ${amt} 피해 (되돌림, 남은 체력 ${dst.hp})`);
    }

    // 걸려 있는 지속 피해를 지금 한 번 더 입힌다.
    burstDot(dst, key) {
      const st = dst.status[key];
      if (!st || dst.dead) return;
      this.applyLoss(dst, st.tick, st.src, st.label);
      this.log(`${this.tag(dst)}: ${STATUS_NAMES[key]} 터짐 ${st.tick} 피해 (남은 체력 ${dst.hp})`);
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

    // 계산된 양을 그대로 회복 (난수 없음). 내상이면 반감.
    healFlat(src, dst, amt, label) {
      if (dst.dead || this.over) return;
      if (dst.status.injury) amt *= 0.5;
      amt = Math.min(Math.round(amt), dst.maxHp - dst.hp);
      if (amt <= 0) return;
      dst.hp += amt;
      src.rec.healed += amt;
      this.record(src, label, 'healed', amt);
      this.log(`${this.tag(src)}의 ${label} → ${this.tag(dst)} ${amt} 회복${dst.status.injury ? ' (내상으로 반감)' : ''}`);
    }

    teamRatio(side) {
      const us = this.units.filter(u => u.side === side);
      return us.reduce((s, u) => s + u.hp, 0) / us.reduce((s, u) => s + u.maxHp, 0);
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
        const a = this.teamRatio('A'), b = this.teamRatio('B');
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

  const api = { CFG, POS_NAMES, ELEMENTS, BEATS, APT_MULT, TYPE_NAMES, STATUS_NAMES, GEAR_EFFECTS, SKILLS, SKILL, UNITS, UNIT, validateTeam, Battle, simulate };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.WuxiaSim = api;
})(typeof self !== 'undefined' ? self : this);
