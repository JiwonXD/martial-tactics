// C# 전투 엔진이 웹 시뮬레이터와 같은 결과를 내는지 보는 기준값(골든)을 만든다.
//   node make-golden.js                 → Tests/BattleParity/golden.json 생성
//   node make-golden.js --log <대전> <시드>  → 그 판의 전투 기록 출력 (C# 쪽과 비교할 때)
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const S = require('./engine.js');

const PRESETS = {
  jeong: { element: '금', slots: [
    { unit: 'baek', skills: ['bukdu', 'cheongsim'] }, { unit: 'seol', skills: ['jeomhyeol', 'naesang'] }, { unit: 'hye', skills: ['saja', 'bantan'] }] },
  ma: { element: '화', slots: [
    { unit: 'dok', skills: ['jujak', 'seophon'] }, { unit: 'hyeol', skills: ['gwangpung', 'sangong'] }, { unit: 'cheol', skills: ['hoechun', 'bantan'] }] },
  mix: { element: '목', slots: [
    { unit: 'namgung', skills: ['cheongryong', 'gwangpung'] }, { unit: 'dang', skills: ['jeomhyeol', 'sangong'] }, { unit: 'nameless', skills: ['saja', 'bantan'] }] },
  dol: { element: '금', slots: [
    { unit: 'paeng', skills: ['bukdu', 'gwangpung'] }, { unit: 'sahyeol', skills: ['hyeolma', 'changgung'] }, { unit: 'jangsam', skills: ['naesang', 'ildo'] }] },
  ref: { element: '수', slots: [
    { unit: 'moyongyeon', skills: ['hyeonmu', 'hoechun'] }, { unit: 'baengni', skills: ['changgung', 'bantan'] }, { unit: 'dokgo', skills: ['saja', 'naesang'] }] },
};

// 무작위 편성: 무인 3명, 무공창 2·3칸은 팀에서 아직 안 쓴 무공 (가끔 비움)
function randomTeam(rand) {
  const pick = arr => arr[Math.floor(rand() * arr.length)];
  const units = [];
  while (units.length < 3) { const u = pick(S.UNITS); if (!units.includes(u)) units.push(u); }
  const used = units.map(u => u.skill);
  const slots = units.map(u => ({ unit: u.id, skills: [0, 1].map(() => {
    if (rand() < 0.1) return null;
    const s = pick(S.SKILLS.filter(x => !used.includes(x.id)));
    used.push(s.id);
    return s.id;
  }) }));
  return { element: pick(S.ELEMENTS), slots };
}

function matchups() {
  const list = [];
  const keys = Object.keys(PRESETS);
  for (const a of keys) for (const b of keys) if (a !== b) list.push({ name: `${a}-${b}`, a: PRESETS[a], b: PRESETS[b] });
  // 무작위 편성의 난수는 전투 난수와 따로 둔다
  let seed = 20261022;
  const rand = () => { seed = seed + 0x6D2B79F5 | 0; let t = Math.imul(seed ^ seed >>> 15, 1 | seed); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };
  for (let i = 0; list.length < 80; i++) {
    const a = randomTeam(rand), b = randomTeam(rand);
    if (S.validateTeam(a).length || S.validateTeam(b).length) throw new Error('무작위 편성 오류');
    list.push({ name: `random${i}`, a, b });
  }
  // 장비: 스탯과 특수효과 7종이 모두 나오도록 크게 준 시험용 값 (밸런스 수치 아님)
  const withGear = (team, gears) => ({ ...team, slots: team.slots.map((s, i) => ({ ...s, gear: gears[i] })) });
  const GEAR = {
    jeong: [{ ext: 30, int: 40, crit: 0.2, critDmg: 0.3, ctrlRes: 0.5 }, { int: 30, spd: 20, skillDmg: 0.2, ctrlHit: 0.2 }, { def: 50, dmgReduce: 0.1, evade: 0.2 }],
    ma: [{ int: 40, ctrlHit: 0.3, evade: 0.15 }, { ext: 50, crit: 0.1, critDmg: 0.5 }, { def: 40, ctrlRes: 0.4, dmgReduce: 0.15 }],
    dol: [{ ext: 40, spd: 30, skillDmg: 0.25, evade: 0.1 }, { ext: 30, crit: 0.15 }, { def: 30, ctrlRes: 0.3 }],
    ref: [{ int: 30, ctrlRes: 0.6, dmgReduce: 0.2 }, { ext: 20, evade: 0.25 }, { int: 20, skillDmg: 0.15, ctrlHit: 0.25 }],
  };
  const geared = Object.fromEntries(Object.entries(GEAR).map(([k, g]) => [k, withGear(PRESETS[k], g)]));
  for (const [a, b] of [['jeong', 'ma'], ['ma', 'jeong'], ['dol', 'ref'], ['ref', 'dol']]) {
    list.push({ name: `gear-${a}-${b}`, a: geared[a], b: geared[b] });
    list.push({ name: `gear-${a}-plain-${b}`, a: geared[a], b: PRESETS[b] });
  }
  return list;
}

const logText = lines => lines.map(l => `${l.round}\t${l.text}`).join('\n');
const sha = s => crypto.createHash('sha256').update(s, 'utf8').digest('hex');

const BATTLE_SEEDS = 12, SIM_N = 60, SIM_SEED = 1000;

if (process.argv[2] === '--log') {
  const m = matchups().find(x => x.name === process.argv[3]);
  if (!m) throw new Error('없는 대전: ' + process.argv[3]);
  process.stdout.write(logText(new S.Battle(m.a, m.b, { seed: +process.argv[4], log: true }).run().log) + '\n');
  return;
}

const seen = {};
const out = { battleSeeds: BATTLE_SEEDS, simN: SIM_N, simSeed: SIM_SEED, matchups: [] };
for (const m of matchups()) {
  const battles = [];
  for (let seed = 1; seed <= BATTLE_SEEDS; seed++) {
    const r = new S.Battle(m.a, m.b, { seed, log: true }).run();
    const text = logText(r.log);
    for (const k of ['두전성이', '의 호위', '추가타', '사중구활', '혈맥상통', '면역', '반감', '준비가 끊김', '쓰러짐', '치명타', '되돌림', '청심결', '반탄경', '회피', '저항', '은신', '(되돌림', '터짐', '표적', '주위상', '호표', '취권'])
      if (text.includes(k)) seen[k] = (seen[k] || 0) + 1;
    battles.push({ seed, winner: r.winner, reason: r.reason, rounds: r.rounds, hp: r.units.map(u => u.hp), log: sha(text) });
  }
  const sim = S.simulate(m.a, m.b, SIM_N, { seed: SIM_SEED });
  out.matchups.push({ name: m.name, a: m.a, b: m.b, battles, sim: {
    A: sim.A, B: sim.B, draw: sim.draw, byHp: sim.byHp, rounds: sim.rounds,
    units: sim.units.map(u => ({ dealt: u.dealt, healed: u.healed, taken: u.taken, deaths: u.deaths, procs: u.procs, bySkill: u.bySkill })),
  } });
}

const used = new Set(out.matchups.flatMap(m => [m.a, m.b].flatMap(t => t.slots.flatMap(s => [S.UNIT[s.unit].skill, ...s.skills]))));
const gearUsed = new Set(out.matchups.flatMap(m => [m.a, m.b].flatMap(t => t.slots.flatMap(s => Object.keys(s.gear || {})))));
const gearMissing = [...Object.keys(S.GEAR_EFFECTS), 'ext', 'int', 'def', 'spd'].filter(k => !gearUsed.has(k));
if (gearMissing.length) throw new Error('골든에 안 들어간 장비 값: ' + gearMissing.join(', '));
const missing = S.SKILLS.filter(s => !used.has(s.id)).map(s => s.id);
if (missing.length) throw new Error('골든에 안 들어간 무공: ' + missing.join(', '));

const file = path.join(__dirname, '../../Tests/BattleParity/golden.json');
fs.writeFileSync(file, JSON.stringify(out) + '\n');
console.log(`${out.matchups.length}개 대전, 판별 기록 ${out.matchups.length * BATTLE_SEEDS}판, 집계 ${out.matchups.length * SIM_N}판 → ${path.relative(process.cwd(), file)}`);
console.log('기록에 나온 판 수:', JSON.stringify(seen));
