// 밸런스 심화 측정. balance.js(팀 승률 집계)를 넘어서, 대체 실험·운 분해·최강 덱 탐색·페이싱·민감도·성장 격차를 잰다.
//   node tools/web-sim/balance-lab.js [항목...] [--out 폴더]
//   항목: pacing luck skills units search sensitivity faction growth (없으면 전부)
// 모든 판은 시드를 정해 두고 돌려서, 같은 명령이면 같은 결과가 나온다.
// 승률에는 95% 신뢰구간을 붙인다. 대체 실험은 같은 상대·같은 시드로 바꾸기 전과 후를 붙여서(짝지은 비교) 잡음을 줄인다.
const fs = require('fs');
const path = require('path');
const S = require('./engine.js');
const B = require('./balance.js');

const POOL = S.UNITS.filter(u => B.stars(u.id) <= 5); // 뽑기 풀 (6성 제외)
const POOL_IDS = new Set(POOL.map(u => u.id));
// 추출할 수 있는 무공 = 뽑기 풀 무인의 무공
const EXTRACTABLE = S.SKILLS.filter(s => POOL.some(u => u.skill === s.id));

// ---------- 통계 ----------
const Z = 1.96;
function wilson(w, n) { // 승률 w/n의 95% 신뢰구간
  if (!n) return [0, 1];
  const p = w / n, d = 1 + Z * Z / n, c = p + Z * Z / (2 * n), h = Z * Math.sqrt(p * (1 - p) / n + Z * Z / (4 * n * n));
  return [(c - h) / d, (c + h) / d];
}
function meanCI(xs) { // 평균과 95% 신뢰구간 (정규 근사)
  const n = xs.length, m = xs.reduce((s, x) => s + x, 0) / n;
  const sd = Math.sqrt(xs.reduce((s, x) => s + (x - m) ** 2, 0) / Math.max(1, n - 1));
  return { mean: m, lo: m - Z * sd / Math.sqrt(n), hi: m + Z * sd / Math.sqrt(n), n };
}
const pct = x => (x * 100).toFixed(1);
const score = (r, side) => r.winner === side ? 1 : r.winner ? 0 : 0.5;

// ---------- 편성 ----------
function population(n, seed) {
  const rand = B.mulberry32(seed);
  return Array.from({ length: n }, () => B.randomTeam(rand, POOL));
}
const clone = t => JSON.parse(JSON.stringify(t));
const teamSkills = t => t.slots.flatMap(s => [S.UNIT[s.unit].skill, ...s.skills.filter(Boolean)]);
const valid = t => S.validateTeam(t).length === 0;

// 속성은 balance.js와 같이 세 무인 적성 합이 가장 좋은 것 (주장이 진법을 들고 있으면 그 속성)
function bestElement(t, rand) {
  const lead = S.UNIT[t.slots[0].unit];
  const f = [lead.skill, ...t.slots[0].skills].map(id => id && S.SKILL[id]).find(s => s && s.type === 'formation');
  if (f) return f.element;
  let best = null, bestScore = -1;
  S.ELEMENTS.forEach((e, ei) => {
    const sc = t.slots.reduce((s, x) => s + B.APT_SCORE[S.UNIT[x.unit].apt[ei]], 0) + rand() * 0.5;
    if (sc > bestScore) { best = e; bestScore = sc; }
  });
  return best;
}

// 팀 t가 상대 목록과 붙은 점수(승 1, 무 0.5). 상대 j와는 항상 시드 base+j, 앞뒤(A/B)도 번갈아.
function vsPanel(t, panel, base) {
  let s = 0;
  for (let j = 0; j < panel.length; j++) {
    if (j % 2 === 0) s += score(new S.Battle(t, panel[j], { seed: base + j }).run(), 'A');
    else s += score(new S.Battle(panel[j], t, { seed: base + j }).run(), 'B');
  }
  return s / panel.length;
}

// ---------- 1. 페이싱 ----------
function pacing(pop, seed) {
  const n = pop.length;
  let leader = 0, hp = 0, draw = 0, rounds = 0;
  const dist = Array(9).fill(0);
  const procs = { active: [], chase: [] }; // 무공 하나가 한 판에 발동한 횟수
  for (let i = 0; i + 1 < n; i += 2) {
    const r = new S.Battle(pop[i], pop[i + 1], { seed: seed + i }).run();
    if (r.reason === 'leader') leader++; else if (r.reason === 'hp') hp++; else draw++;
    rounds += r.rounds; dist[r.rounds]++;
    for (const u of r.units) for (const s of u.skills) if (s.type === 'active' || s.type === 'chase') procs[s.type].push(u.rec.procs[s.id] || 0);
  }
  const games = Math.floor(n / 2);
  return {
    games, leaderRate: leader / games, leaderCI: wilson(leader, games), hpRate: hp / games, drawRate: draw / games,
    avgRounds: rounds / games, roundDist: dist.slice(1).map((c, i) => ({ rounds: i + 1, share: c / games })),
    activeProcs: meanCI(procs.active), chaseProcs: meanCI(procs.chase),
  };
}

// ---------- 2. 운과 편성 ----------
// 대전 m개를 각각 k번(시드만 바꿔) 돌려 승률 p를 구한다. 결과의 분산 = Var(p)(편성) + E[p(1-p)](운).
function luck(pop, matchups, k, seed) {
  const ps = [];
  for (let i = 0; i < matchups; i++) {
    const a = pop[(2 * i) % pop.length], b = pop[(2 * i + 1) % pop.length];
    let s = 0;
    for (let j = 0; j < k; j++) s += score(new S.Battle(a, b, { seed: seed + i * 1000 + j }).run(), 'A');
    ps.push(s / k);
  }
  const m = ps.reduce((s, p) => s + p, 0) / ps.length;
  const varP = ps.reduce((s, p) => s + (p - m) ** 2, 0) / (ps.length - 1);
  const noise = ps.reduce((s, p) => s + p * (1 - p), 0) / ps.length * k / (k - 1); // 운 (표본 보정)
  const comp = Math.max(0, varP - noise / k); // 표본 p의 분산에서 추정 잡음을 뺀 진짜 편성 분산
  const bands = { coin: ps.filter(p => p >= 0.4 && p <= 0.6).length / ps.length, decisive: ps.filter(p => p <= 0.1 || p >= 0.9).length / ps.length };
  return { matchups, k, compositionShare: comp / (comp + noise), bands };
}

// ---------- 3·4. 대체 실험 ----------
// 기준 팀 T에서 한 가지만 바꾼 T'을 만들고, 같은 상대·같은 시드로 둘을 붙여 점수 차를 본다.
function pairedDelta(pairs, panel, base) {
  const d = pairs.map(([t, t2]) => vsPanel(t2, panel, base) - vsPanel(t, panel, base));
  return meanCI(d);
}

// 무공 s를 추출 칸 하나에 넣는다 (빼는 무공은 무작위). 자리 규칙: 진법·두전성이는 주장, 호위결·호표는 부장.
function insertSkill(t, s, rand) {
  if (teamSkills(t).includes(s.id)) return null;
  let pos;
  if (s.type === 'formation' || B.LEADER_ONLY.has(s.id)) pos = 0;
  else if (B.NOT_LEADER.has(s.id)) pos = 1 + Math.floor(rand() * 2);
  else pos = Math.floor(rand() * 3);
  const k = Math.floor(rand() * 2);
  const before = clone(t), after = clone(t);
  after.slots[pos].skills[k] = s.id;
  if (s.type === 'formation') before.element = after.element = s.element; // 속성 차이는 비교에서 뺀다
  if (!valid(after) || !valid(before)) return null;
  return [before, after];
}

// 무인 u를 한 자리에 넣는다 (그 자리 무공 칸은 그대로). 속성은 바꾼 뒤 다시 고른다.
function insertUnit(t, u, rand) {
  if (t.slots.some(s => s.unit === u.id) || teamSkills(t).includes(u.skill)) return null;
  const own = S.SKILL[u.skill];
  let pos;
  if (own.type === 'formation' || B.LEADER_ONLY.has(own.id)) pos = 0;
  else if (B.NOT_LEADER.has(own.id)) pos = 1 + Math.floor(rand() * 2);
  else pos = Math.floor(rand() * 3);
  const before = clone(t), after = clone(t);
  after.slots[pos].unit = u.id;
  after.element = bestElement(after, rand);
  if (!valid(after)) return null;
  return [before, after];
}

function swapStudy(kind, pop, panel, perItem, seed) {
  const rand = B.mulberry32(seed);
  const items = kind === 'skill' ? EXTRACTABLE : POOL;
  return items.map((it, idx) => {
    const pairs = [];
    for (let tries = 0; pairs.length < perItem && tries < perItem * 20; tries++) {
      const t = pop[Math.floor(rand() * pop.length)];
      const p = kind === 'skill' ? insertSkill(t, it, rand) : insertUnit(t, it, rand);
      if (p) pairs.push(p);
    }
    const d = pairedDelta(pairs, panel, seed + 100000 * (idx + 1));
    return kind === 'skill'
      ? { id: it.id, name: it.name, type: it.type, owner: POOL.find(u => u.skill === it.id).name, ...d }
      : { id: it.id, name: it.name, stars: B.stars(it.id), faction: it.faction, skill: S.SKILL[it.skill].name, ...d };
  });
}

// ---------- 5. 최강 덱 탐색 (언덕 오르기) ----------
function mutate(t, rand) {
  const x = clone(t), r = rand();
  if (r < 0.35) { // 무인 바꾸기
    const pos = Math.floor(rand() * 3), u = POOL[Math.floor(rand() * POOL.length)];
    x.slots[pos].unit = u.id;
  } else if (r < 0.75) { // 추출 무공 바꾸기
    const pos = Math.floor(rand() * 3), k = Math.floor(rand() * 2), s = EXTRACTABLE[Math.floor(rand() * EXTRACTABLE.length)];
    x.slots[pos].skills[k] = s.id;
  } else if (r < 0.9) { // 자리 바꾸기
    const i = Math.floor(rand() * 3), j = (i + 1 + Math.floor(rand() * 2)) % 3;
    [x.slots[i], x.slots[j]] = [x.slots[j], x.slots[i]];
  } else x.element = S.ELEMENTS[Math.floor(rand() * 5)];
  return valid(x) ? x : null;
}

function search(starts, steps, panelSize, holdout, seed) {
  const rand = B.mulberry32(seed);
  const panel = population(panelSize, seed + 1), test = population(holdout, seed + 2);
  const runs = [];
  for (let s = 0; s < starts; s++) {
    let cur = B.randomTeam(rand, POOL), curScore = vsPanel(cur, panel, seed + 10);
    for (let i = 0; i < steps; i++) {
      const nx = mutate(cur, rand);
      if (!nx) continue;
      const sc = vsPanel(nx, panel, seed + 10);
      if (sc >= curScore) { cur = nx; curScore = sc; }
    }
    // 탐색에 쓴 상대로 잰 점수는 부풀려져 있으니, 처음 보는 상대로 다시 잰다
    const w = vsPanel(cur, test, seed + 20) * holdout;
    runs.push({ team: cur, searchScore: curScore, holdout: w / holdout, ci: wilson(w, holdout) });
  }
  return runs.sort((a, b) => b.holdout - a.holdout);
}
const describe = t => `${t.element} | ` + t.slots.map(s => `${S.UNIT[s.unit].name}(${B.stars(s.unit)}성)[${[S.UNIT[s.unit].skill, ...s.skills].map(id => S.SKILL[id].name).join('·')}]`).join(' / ');

// ---------- 6. 상수 민감도 ----------
function sensitivity(pop, seed) {
  const keys = ['baseDmg', 'statK', 'counterBonus', 'critMult', 'factionBonus', 'healBase'];
  const orig = { ...S.CFG }, out = [];
  const run = label => {
    const p = pacing(pop.slice(0, 2000), seed), l = luck(pop, 120, 30, seed + 7);
    out.push({ label, leaderRate: p.leaderRate, avgRounds: p.avgRounds, compositionShare: l.compositionShare });
  };
  run('기준');
  for (const k of keys) for (const f of [0.8, 1.2]) {
    S.CFG[k] = orig[k] * f; run(`${k} ×${f}`); S.CFG[k] = orig[k];
  }
  return out;
}

// ---------- 7. 같은 세력 보너스 ----------
// 같은 세력 3명 팀을 만들어, 보너스가 있을 때와 0일 때 같은 상대에게 얼마나 이기는지
function faction(panel, n, seed) {
  const rand = B.mulberry32(seed), teams = [];
  const byFaction = {};
  POOL.forEach(u => (byFaction[u.faction] = byFaction[u.faction] || []).push(u));
  for (let tries = 0; teams.length < n && tries < n * 50; tries++) {
    const fs_ = Object.keys(byFaction), g = byFaction[fs_[Math.floor(rand() * fs_.length)]];
    if (g.length < 3) continue;
    const t = B.randomTeam(rand, g);
    if (valid(t)) teams.push(t);
  }
  const on = teams.map(t => vsPanel(t, panel, seed + 5));
  const orig = S.CFG.factionBonus; S.CFG.factionBonus = 0;
  const off = teams.map(t => vsPanel(t, panel, seed + 5));
  S.CFG.factionBonus = orig;
  return { teams: teams.length, on: meanCI(on), off: meanCI(off), delta: meanCI(on.map((x, i) => x - off[i])) };
}

// ---------- 8. 성장 격차 (승급 자유 스탯) ----------
// 무인마다 주력 스탯(외공·내공 중 큰 쪽)에 +p를 찍었을 때, 같은 팀이 같은 상대에게 얼마나 더 이기는지.
// 장비 입력(gear)을 빌려 스탯을 더한다: 엔진에서 장비 스탯은 적성 배수 전에 더해진다. 승급 스탯도 같다고 가정 [제안].
function growth(pop, panel, n, seed) {
  const base = pop.slice(0, n);
  const boost = (t, p) => { const x = clone(t); x.slots.forEach(s => { const u = S.UNIT[s.unit]; s.gear = u.ext >= u.int ? { ext: p } : { int: p }; }); return x; };
  return [10, 30, 50].map(p => ({ points: p, ...meanCI(base.map(t => vsPanel(boost(t, p), panel, seed) - vsPanel(t, panel, seed))) }));
}

// ---------- 실행 ----------
function toCSV(rows, cols) {
  return cols.join(',') + '\n' + rows.map(r => cols.map(c => {
    const v = r[c]; return typeof v === 'number' ? +v.toFixed(4) : `"${String(v).replace(/"/g, '""')}"`;
  }).join(',')).join('\n') + '\n';
}

if (require.main === module) {
  const args = process.argv.slice(2);
  const oi = args.indexOf('--out');
  const outDir = oi >= 0 ? args.splice(oi, 2)[1] : null;
  const want = new Set(args.length ? args : ['pacing', 'luck', 'skills', 'units', 'search', 'sensitivity', 'faction', 'growth']);
  const pop = population(4000, 1), panel = population(60, 2);
  const result = {};
  const t0 = Date.now(), lap = name => console.error(`${name} 끝 (${((Date.now() - t0) / 1000).toFixed(0)}초)`);
  const save = (name, rows, cols) => { if (outDir) { fs.mkdirSync(outDir, { recursive: true }); fs.writeFileSync(path.join(outDir, name), toCSV(rows, cols)); } };

  if (want.has('pacing')) {
    const p = result.pacing = pacing(pop, 11);
    console.log(`\n[페이싱] ${p.games}판: 주장 격파 ${pct(p.leaderRate)}% (${pct(p.leaderCI[0])}~${pct(p.leaderCI[1])}), 체력 비율 ${pct(p.hpRate)}%, 무승부 ${pct(p.drawRate)}%, 평균 ${p.avgRounds.toFixed(2)}합`);
    console.log(`  합 분포: ${p.roundDist.map(d => `${d.rounds}합 ${pct(d.share)}%`).join(', ')}`);
    console.log(`  무공 하나의 판당 발동: 발동형 ${p.activeProcs.mean.toFixed(2)}회, 연계형 ${p.chaseProcs.mean.toFixed(2)}회`);
    save('pacing-rounds.csv', p.roundDist, ['rounds', 'share']);
    lap('페이싱');
  }
  if (want.has('luck')) {
    const l = result.luck = luck(pop, 300, 60, 21);
    console.log(`\n[운과 편성] 대전 ${l.matchups}개 × ${l.k}판: 승패 분산 중 편성 몫 ${pct(l.compositionShare)}%, 운 몫 ${pct(1 - l.compositionShare)}%`);
    console.log(`  승률 40~60%인 대전 ${pct(l.bands.coin)}%, 10% 이하·90% 이상(거의 정해진) 대전 ${pct(l.bands.decisive)}%`);
    lap('운과 편성');
  }
  if (want.has('skills')) {
    const rows = result.skills = swapStudy('skill', pop, panel, 60, 31).sort((a, b) => b.mean - a.mean);
    console.log(`\n[무공 대체 실험] 추출 칸 하나를 이 무공으로 바꿨을 때 승률 변화 (평균 무공 대비, 95% 구간)`);
    rows.forEach(r => console.log(`  ${(r.mean >= 0 ? '+' : '') + pct(r.mean).padStart(5)}%p (${pct(r.lo)}~${pct(r.hi)})  ${r.name} [${r.type}] ← ${r.owner}`));
    save('skills.csv', rows, ['id', 'name', 'type', 'owner', 'mean', 'lo', 'hi', 'n']);
    lap('무공');
  }
  if (want.has('units')) {
    const rows = result.units = swapStudy('unit', pop, panel, 60, 41).sort((a, b) => b.mean - a.mean);
    console.log(`\n[무인 대체 실험] 한 자리를 이 무인으로 바꿨을 때 승률 변화 (95% 구간)`);
    rows.forEach(r => console.log(`  ${(r.mean >= 0 ? '+' : '') + pct(r.mean).padStart(5)}%p (${pct(r.lo)}~${pct(r.hi)})  ${r.stars}성 ${r.name} ${r.faction} [${r.skill}]`));
    save('units.csv', rows, ['id', 'name', 'stars', 'faction', 'skill', 'mean', 'lo', 'hi', 'n']);
    lap('무인');
  }
  if (want.has('search')) {
    const runs = result.search = search(8, 250, 120, 1000, 51);
    console.log(`\n[최강 덱 탐색] 무작위 출발 ${runs.length}번 × 250걸음, 처음 보는 상대 1000팀에게 다시 잰 승률`);
    runs.forEach(r => console.log(`  ${pct(r.holdout)}% (${pct(r.ci[0])}~${pct(r.ci[1])})  ${describe(r.team)}`));
    save('search.csv', runs.map(r => ({ holdout: r.holdout, lo: r.ci[0], hi: r.ci[1], team: describe(r.team) })), ['holdout', 'lo', 'hi', 'team']);
    lap('탐색');
  }
  if (want.has('sensitivity')) {
    const rows = result.sensitivity = sensitivity(pop, 61);
    console.log(`\n[상수 민감도] 하나씩 ±20%`);
    rows.forEach(r => console.log(`  ${r.label.padEnd(18)} 주장 격파 ${pct(r.leaderRate)}%  평균 ${r.avgRounds.toFixed(2)}합  편성 몫 ${pct(r.compositionShare)}%`));
    save('sensitivity.csv', rows, ['label', 'leaderRate', 'avgRounds', 'compositionShare']);
    lap('민감도');
  }
  if (want.has('faction')) {
    const f = result.faction = faction(panel, 120, 71);
    console.log(`\n[같은 세력 보너스] 같은 세력 팀 ${f.teams}개: 보너스 있음 ${pct(f.on.mean)}%, 없음 ${pct(f.off.mean)}%, 차이 ${pct(f.delta.mean)}%p (${pct(f.delta.lo)}~${pct(f.delta.hi)})`);
    lap('세력');
  }
  if (want.has('growth')) {
    const rows = result.growth = growth(pop, panel, 150, 81);
    console.log(`\n[승급 스탯 격차] 세 무인 모두 주력 스탯에 +p`);
    rows.forEach(r => console.log(`  +${r.points}: 승률 ${(r.mean >= 0 ? '+' : '') + pct(r.mean)}%p (${pct(r.lo)}~${pct(r.hi)})`));
    lap('성장');
  }
  if (outDir) fs.writeFileSync(path.join(outDir, 'result.json'), JSON.stringify(result, (k, v) => (k === 'team' ? describe(v) : v), 1));
}

module.exports = { wilson, meanCI, population, pacing, luck, swapStudy, search, sensitivity, faction, growth };
