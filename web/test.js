// 웹게임 진행 로직 테스트: node web/test.js
const fs = require('fs');
const path = require('path');
const assert = require('assert');
const S = require('../tools/web-sim/engine.js');
const G = require('./game.js');

// 등급이 C# Catalog의 Stars와 같은지
const cs = fs.readFileSync(path.join(__dirname, '..', 'Assets', 'Scripts', 'Battle', 'Catalog.cs'), 'utf8');
const csStars = {};
for (const m of cs.matchAll(/U\("(\w+)",[^\n]*?"\w+"(?:, (\d))?\),?\s*$/gm)) csStars[m[1]] = m[2] ? +m[2] : 3;
assert.strictEqual(Object.keys(csStars).length, S.UNITS.length, 'Catalog.cs 무인 수');
for (const u of S.UNITS) assert.strictEqual(G.stars(u.id), csStars[u.id], `${u.id} 등급`);

// 고정 난수로 진행
let seed = 7;
const rand = () => ((seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648);
const g = new G.GameState(G.newData(), rand);

const draws = g.draw(60);
assert.strictEqual(draws.length, 60);
assert.ok(draws.every(d => G.stars(d.unit) <= 5), '6성은 뽑기에 안 나옴');
assert.strictEqual(g.data.draws, 60);
assert.strictEqual(g.data.units.length + g.data.units.reduce((s, u) => s + u.spare, 0), 60, '새 무인 + 중복 카드 = 뽑은 수');

// 승급: 중복 카드 1장 → 자유 스탯 10, 별 수까지만
const dup = g.data.units.find(u => u.spare > 0);
g.promote(dup);
assert.strictEqual(dup.promotions, 1);
assert.strictEqual(dup.freePoints, G.POINTS_PER_PROMOTION);
g.addStat(dup, 0);
assert.strictEqual(dup.added[0], 1);
assert.strictEqual(g.statValue(dup, 0), S.UNIT[dup.id].ext + 1);

// 추출 · 장착: 자기 무공 불가, 다른 무인 장착 중이면 옮겨 옴
const donor = g.data.units.find(u => u.spare > 0 && u !== dup);
assert.ok(donor, '중복 카드가 있는 두 번째 무인');
const sk = g.extract(donor);
assert.strictEqual(sk.skill, S.UNIT[donor.id].skill);
assert.strictEqual(g.equipError(donor, 0, sk), '자기 무공과 같습니다.');
const [a, b] = g.data.units.filter(u => u !== donor && u !== dup);
g.equip(a, 0, sk);
g.equip(b, 1, sk);
assert.strictEqual(a.equipped[0], -1, '옮겨 가면 원래 칸은 빔');
assert.strictEqual(b.equipped[1], sk.uid);

// 덱 편성: 같은 무인을 다른 자리에 넣으면 맞바꿈
g.setDeckUnit(0, a.id); g.setDeckUnit(1, b.id); g.setDeckUnit(2, dup.id);
assert.deepStrictEqual(g.validateDeck(), ['속성을 고르세요.']);
g.data.deck.element = '금';
assert.deepStrictEqual(g.validateDeck(), []);
g.setDeckUnit(0, b.id);
assert.deepStrictEqual(g.data.deck.units, [b.id, a.id, dup.id]);

// 자유 스탯은 장비 칸으로 넘겨 무인 스탯에 더한 뒤 적성 배수를 곱한다
const team = g.buildTeam();
assert.strictEqual(team.slots[2].gear.ext, 1);
const r = g.fight(g.randomOpponent());
assert.ok(['A', 'B', null].includes(r.winner));
assert.ok(r.log.length > 0 && r.log.every(l => l.hp.length === 6 && l.status.length === 6), '기록마다 체력·상태');

// 저장 데이터는 JSON 왕복 가능
const back = new G.GameState(JSON.parse(JSON.stringify(g.data)));
assert.deepStrictEqual(back.data, g.data);

console.log('웹게임 로직 테스트 통과');
