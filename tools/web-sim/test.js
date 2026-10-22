const S = require('./engine.js');
const assert = require('assert');
const 정파 = { element: '금', slots: [
  { unit: 'baek', skills: ['bukdu', 'cheongsim'] },
  { unit: 'seol', skills: ['jeomhyeol', 'naesang'] },
  { unit: 'hye', skills: ['saja', 'bantan'] } ] };
const 마교 = { element: '화', slots: [
  { unit: 'dok', skills: ['jujak', 'seophon'] },
  { unit: 'hyeol', skills: ['gwangpung', 'sangong'] },
  { unit: 'cheol', skills: ['hoechun', 'bantan'] } ] };
const 혼합 = { element: '목', slots: [
  { unit: 'namgung', skills: ['cheongryong', 'gwangpung'] },
  { unit: 'dang', skills: ['jeomhyeol', 'sangong'] },
  { unit: 'nameless', skills: ['saja', 'bantan'] } ] };

// 1. 편성 검증
assert.deepStrictEqual(S.validateTeam(정파), []);
assert.deepStrictEqual(S.validateTeam(마교), []);
assert.deepStrictEqual(S.validateTeam(혼합), []);
assert(S.validateTeam({ ...정파, slots: [정파.slots[0], 정파.slots[0], 정파.slots[2]] }).some(e => e.includes('같은 무인')));
assert(S.validateTeam({ ...정파, slots: [{ unit: 'baek', skills: ['bukdu', 'cheongsim'] }, { unit: 'seol', skills: ['bukdu', 'naesang'] }, 정파.slots[2]] }).some(e => e.includes('같은 무공')));
// 다른 무인의 무공은 추출해서 넣을 수 있지만, 자기 무공을 또 넣으면 중복
assert.deepStrictEqual(S.validateTeam({ ...정파, slots: [{ unit: 'baek', skills: ['mandok', 'cheongsim'] }, 정파.slots[1], 정파.slots[2]] }), []);
assert(S.validateTeam({ ...정파, slots: [{ unit: 'baek', skills: ['cheongun', 'cheongsim'] }, 정파.slots[1], 정파.slots[2]] }).some(e => e.includes('같은 무공')));
// 모든 무공은 정확히 한 무인의 자기 무공
S.SKILLS.forEach(sk => assert.strictEqual(S.UNITS.filter(u => u.skill === sk.id).length, 1, sk.id));

// 2. 같은 시드면 같은 결과
const r1 = new S.Battle(정파, 마교, { seed: 7, log: true }).run();
const r2 = new S.Battle(정파, 마교, { seed: 7, log: true }).run();
assert.deepStrictEqual(r1.log, r2.log);

// 3. 진법은 주장만: 천강북두진을 부장에 끼우면 적용 로그가 없어야 함
const 진법부장 = { element: '금', slots: [
  { unit: 'baek', skills: ['cheongsim', 'jeomhyeol'] },
  { unit: 'seol', skills: ['bukdu', 'naesang'] },
  정파.slots[2] ] };
const r3 = new S.Battle(진법부장, 마교, { seed: 1, log: true }).run();
assert(!r3.log.some(l => l.text.includes('천강북두진 적용')));
assert(r1.log.some(l => l.text.includes('천강북두진 적용')));
// 진법은 팀 속성이 같을 때만: 금 진법을 토 팀에서 쓰면 적용 안 됨
const r3b = new S.Battle({ ...정파, element: '토' }, 마교, { seed: 1, log: true }).run();
assert(!r3b.log.some(l => l.text.includes('천강북두진 적용')));
// 진법마다 속성이 하나씩, 다섯 속성 모두 진법이 있음
assert.deepStrictEqual(S.SKILLS.filter(s => s.type === 'formation').map(s => s.element).sort(), [...S.ELEMENTS].sort());
// 상극 우위 피해 강화: 같은 조건에서 진법 쪽 피해가 더 큼
{ const b = new S.Battle(정파, { ...마교, element: '목' }, { seed: 1 });
  const [a, t] = [b.units[0], b.units[3]];
  const before = b.rawDamage(a, t, 'int', 1);
  S.SKILL.bukdu.setup(b, a);
  assert(Math.abs(b.rawDamage(a, t, 'int', 1) / before - (1 + 0.15 + 0.10) / 1.15 * 1.12) < 1e-9); }

// 3-2. 새 부품: 주장 지정, 조건부 추가 효과, 피해 분담
const 돌격 = { element: '금', slots: [
  { unit: 'paeng', skills: ['bukdu', 'gwangpung'] }, { unit: 'sahyeol', skills: ['hyeolma', 'changgung'] }, { unit: 'jangsam', skills: ['naesang', 'ildo'] } ] };
const 호위팀 = { element: '금', slots: [정파.slots[0], 정파.slots[1], { unit: 'cheolwi', skills: ['saja', 'bantan'] }] };
assert.deepStrictEqual(S.validateTeam(돌격), []);
assert.deepStrictEqual(S.validateTeam(호위팀), []);
let jikdo = 0, guarded = 0, talExtra = 0, geumnaStun = 0;
for (let seed = 1; seed <= 300; seed++) {
  const r = new S.Battle(돌격, 호위팀, { seed, log: true }).run();
  r.log.forEach((l, i) => {
    if (l.text.startsWith('[아] 팽무혁의 직도황룡')) { assert(l.text.includes('→ [적] 백무진') || l.text.includes('→ [아]') || r.log.slice(0, i).some(x => x.text.startsWith('[아] 팽무혁: 혼란'))); jikdo++; }
    if (l.text.startsWith('[적] 철위강의 호위')) guarded++;
    if (l.text.includes('탈명연환 추가타')) talExtra++;
    if (l.text.includes('연계 무공 금나수') && r.log.slice(i + 1, i + 4).some(x => x.text.includes(': 점혈 ('))) geumnaStun++;
  });
}
assert(jikdo > 0 && guarded > 0 && talExtra > 0 && geumnaStun > 0, JSON.stringify({ jikdo, guarded, talExtra, geumnaStun }));
// 호위 피해 분담: 30%를 철위강이 대신 받음
{ const b = new S.Battle(돌격, 호위팀, { seed: 3 });
  S.SKILL.howi.setup(b, b.units[5]);
  const [a, leader, guard] = [b.units[0], b.units[3], b.units[5]];
  const g0 = guard.hp, l0 = leader.hp;
  const dealt = b.damage(a, leader, 'int', 1, '시험');
  assert.strictEqual(l0 - leader.hp, dealt);
  assert(g0 - guard.hp > 0 && Math.abs((g0 - guard.hp) / ((l0 - leader.hp) + (g0 - guard.hp)) - 0.3) < 0.01); }

// 4. 승패 판정: 주장이 쓰러진 쪽이 진다, 무승부는 주장 둘 다 생존
let leaderChecks = 0, stunImmune = 0, hpJudged = 0;
for (let seed = 1; seed <= 3000; seed++) {
  const r = new S.Battle(정파, 마교, { seed, log: true }).run();
  const [a0, b0] = [r.units[0], r.units[3]];
  if (r.reason === 'leader') assert(a0.dead !== b0.dead);
  if (r.reason === 'hp') { assert(!a0.dead && !b0.dead && r.rounds === 8);
    const ratio = side => { const us = r.units.filter(u => u.side === side); return us.reduce((s, u) => s + u.hp, 0) / us.reduce((s, u) => s + u.maxHp, 0); };
    assert(ratio(r.winner) > ratio(r.winner === 'A' ? 'B' : 'A')); hpJudged++; }
  if (!r.winner) assert(!a0.dead && !b0.dead);
  if (r.reason === 'leader') leaderChecks++;
  assert(r.rounds <= 8);
  // 5. 금강불괴(혜법)에게는 점혈이 걸리지 않음
  assert(!r.log.some(l => l.text.startsWith('[아] 혜법: 점혈 (')));
  if (r.log.some(l => l.text === '[아] 혜법: 점혈 면역')) stunImmune++;
  // 기혈은 0 ~ 최대 사이
  r.units.forEach(u => assert(u.hp >= 0 && u.hp <= u.maxHp));
}
assert(stunImmune > 0, '점혈 면역 상황이 한 번도 안 나옴');
console.log('규칙 테스트 통과. 주장 격파로 끝난 판', leaderChecks, '/ 점혈 면역 발생', stunImmune, '/ 체력 비율로 승부', hpJudged);

// 6. 밸런스 확인
function report(label, a, b) {
  const r = S.simulate(a, b, 2000);
  console.log(`${label}: A ${(r.A / r.n * 100).toFixed(1)}% B ${(r.B / r.n * 100).toFixed(1)}% 무 ${(r.draw / r.n * 100).toFixed(1)}% 평균 ${r.avgRounds.toFixed(2)}합`);
  r.units.forEach(u => console.log(`   ${u.side} ${S.POS_NAMES[u.pos]} ${u.name}: 피해 ${Math.round(u.dealt / r.n)} 회복 ${Math.round(u.healed / r.n)} 받음 ${Math.round(u.taken / r.n)} 사망 ${(u.deaths / r.n * 100).toFixed(0)}%`));
}
report('정파 vs 마교', 정파, 마교);
report('마교 vs 정파', 마교, 정파);
report('정파 vs 혼합', 정파, 혼합);
report('마교 vs 혼합', 마교, 혼합);
report('정파 미러', 정파, 정파);
