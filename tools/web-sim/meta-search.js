// 최강 덱 탐색과 메타 다양성 점검
//   node tools/web-sim/meta-search.js [시드=1] [반복=3] [오르기 횟수=12]
// 1) 언덕 오르기: 무작위 덱에서 시작해 무인·추출 무공·속성·자리를 하나씩 바꿔 보며 상대 묶음에 대한 승률이 오르면 받아들인다.
// 2) 찾은 덱끼리 상성표를 만들고 내시 균형(서로 최선으로 고를 때의 덱 비율)을 가상 대전(fictitious play)으로 푼다.
// 3) 균형 비율로 섞인 상대를 이기는 덱을 다시 찾아 넣는다 (double oracle). 더 못 찾으면 그 균형이 메타다.
const S = require('./engine.js');
const B = require('./balance.js');

const ownSkill = new Map(B.POOL.map(u => [u.id, u.skill]));

function toTeam(d) {
  return { element: d.element, slots: d.units.map((u, i) => ({ unit: u, skills: d.ex[i] })) };
}
const key = d => d.units.join(',') + '|' + d.ex.map(x => [...x].sort().join('+')).join(',') + '|' + d.element;

function randomDeck(rand) {
  const t = B.randomTeam(rand, B.POOL);
  return { units: t.slots.map(s => s.unit), ex: t.slots.map(s => [...s.skills]), element: t.element }; // 속성은 balance.js처럼 적성 합이 좋은 것
}

const pick = (rand, xs) => xs[Math.floor(rand() * xs.length)];
function freeSkills(d) {
  const used = new Set([...d.units.map(u => ownSkill.get(u)), ...d.ex.flat()]);
  return B.EXTRACTABLE.filter(s => !used.has(s.id)).map(s => s.id);
}

function mutate(d, rand) {
  const n = { units: [...d.units], ex: d.ex.map(x => [...x]), element: d.element };
  const r = rand();
  if (r < 0.3) {          // 무인 바꾸기 (그 무인의 추출 무공 칸은 그대로)
    const i = Math.floor(rand() * 3);
    const u = pick(rand, B.POOL.filter(u => !n.units.includes(u.id))).id;
    n.units[i] = u;
    const own = ownSkill.get(u);
    n.ex = n.ex.map(x => x.map(s => s === own ? null : s));
    for (const x of n.ex) for (let k = 0; k < 2; k++) if (!x[k]) x[k] = pick(rand, freeSkills(n));
  } else if (r < 0.7) {   // 추출 무공 하나 바꾸기
    const i = Math.floor(rand() * 3), k = Math.floor(rand() * 2);
    n.ex[i][k] = pick(rand, freeSkills(n));
  } else if (r < 0.85) {  // 속성
    n.element = pick(rand, S.ELEMENTS.filter(e => e !== d.element));
  } else {                // 자리 바꾸기 (무인과 그 무공창이 같이 움직임)
    const i = Math.floor(rand() * 3), j = (i + 1 + Math.floor(rand() * 2)) % 3;
    [n.units[i], n.units[j]] = [n.units[j], n.units[i]];
    [n.ex[i], n.ex[j]] = [n.ex[j], n.ex[i]];
  }
  return n;
}

// opps: [{ deck, w }] 가중치 합 1. 판 수를 가중치대로 나눠 돌린다 (같은 시드 = 같은 조건 비교)
function score(d, opps, battles, seed) {
  const team = toTeam(d);
  let w = 0, tot = 0;
  opps.forEach((o, oi) => {
    const k = Math.max(1, Math.round(o.w * battles)), ot = toTeam(o.deck);
    for (let i = 0; i < k; i++) {
      const r = new S.Battle(team, ot, { seed: seed + oi * 1000 + i }).run();
      w += r.winner === 'A' ? 1 : r.winner ? 0 : 0.5;
      tot++;
    }
  });
  return w / tot;
}

function climb(rand, opps, steps, battles, seed) {
  let cur = randomDeck(rand), cs = score(cur, opps, battles, seed);
  for (let s = 0; s < steps; s++) {
    const n = mutate(cur, rand), ns = score(n, opps, battles, seed);
    if (ns >= cs) { cur = n; cs = ns; }
  }
  return { deck: cur, fit: cs };
}

// 대칭 상성표 M[i][j] = i가 j를 이길 확률 (A·B 자리를 바꿔 한 번씩)
function matrix(decks, battles, seed, M = []) {
  for (let i = 0; i < decks.length; i++) {
    M[i] = M[i] || [];
    for (let j = 0; j < decks.length; j++) {
      if (M[i][j] !== undefined) continue;
      if (i === j) { M[i][j] = 0.5; continue; }
      let w = 0;
      for (let k = 0; k < battles; k++) {
        const [a, b, mine] = k % 2 ? [decks[j], decks[i], 'B'] : [decks[i], decks[j], 'A'];
        const r = new S.Battle(toTeam(a), toTeam(b), { seed: seed + k }).run();
        w += r.winner === mine ? 1 : r.winner ? 0 : 0.5;
      }
      M[i][j] = w / battles;
      (M[j] = M[j] || [])[i] = 1 - M[i][j];
    }
  }
  return M;
}

// 대칭 영합 게임의 균형: 가상 대전 (매번 상대 누적 비율에 대한 최선을 고른다)
function nash(M, iters = 200000) {
  const n = M.length, count = new Array(n).fill(0), payoff = new Array(n).fill(0);
  let best = 0;
  for (let t = 0; t < iters; t++) {
    count[best]++;
    for (let i = 0; i < n; i++) payoff[i] += M[i][best] - 0.5;
    best = payoff.indexOf(Math.max(...payoff));
  }
  return count.map(c => c / iters);
}

function describe(d) {
  return d.units.map((u, i) => `${i ? '부장' : '주장'} ${S.UNIT[u].name}(${S.SKILL[ownSkill.get(u)].name} + ${d.ex[i].map(s => S.SKILL[s].name).join('·')})`).join(' / ') + ` · ${d.element}`;
}

if (require.main === module) {
  const seed = +(process.argv[2] || 1), rounds = +(process.argv[3] || 3), climbs = +(process.argv[4] || 12);
  const STEPS = 400, BATTLES = 300, rand = B.mulberry32(seed);
  const t0 = Date.now(), log = (...a) => console.log(`[${((Date.now() - t0) / 1000).toFixed(0)}s]`, ...a);

  // 0회차: 무작위 편성 300개를 상대로
  const field = Array.from({ length: BATTLES }, () => ({ deck: randomDeck(rand), w: 1 / BATTLES }));
  let pool = [], seen = new Set();
  const add = d => { const k = key(d); if (!seen.has(k)) { seen.add(k); pool.push(d); return true; } return false; };
  for (let c = 0; c < climbs; c++) {
    const r = climb(rand, field, STEPS, BATTLES, seed * 7919 + c);
    add(r.deck);
    log(`무작위 상대 오르기 ${c + 1}/${climbs}: 승률 ${(r.fit * 100).toFixed(1)}%`);
  }
  let M = matrix(pool, 200, seed * 104729), mix = nash(M);

  for (let it = 1; it <= rounds; it++) {
    const opps = pool.map((d, i) => ({ deck: d, w: mix[i] })).filter(o => o.w > 0.01);
    let added = 0;
    for (let c = 0; c < Math.ceil(climbs / 2); c++) {
      const r = climb(rand, opps, STEPS, BATTLES, seed * 31337 + it * 100 + c);
      const fresh = score(r.deck, opps, 1000, seed * 99991 + it * 100 + c); // 다른 시드로 다시 잼
      log(`${it}회차 균형 상대 오르기 ${c + 1}: 오를 때 ${(r.fit * 100).toFixed(1)}%, 새 시드 ${(fresh * 100).toFixed(1)}%`);
      if (fresh > 0.53 && add(r.deck)) added++;
    }
    M = matrix(pool, 200, seed * 104729, M);
    mix = nash(M);
    log(`${it}회차: 새 덱 ${added}개, 덱 ${pool.length}개`);
    if (!added) break;
  }

  // 결과
  const order = mix.map((w, i) => i).sort((a, b) => mix[b] - mix[a]);
  const support = order.filter(i => mix[i] > 0.01);
  const vsField = pool.map(d => score(d, field, 1000, seed * 15485863));
  console.log(`\n균형에 든 덱 ${support.length}개 (덱 ${pool.length}개 중)`);
  support.forEach(i => {
    const beats = pool.filter((_, j) => j !== i && M[i][j] > 0.5).length;
    console.log(`  ${(mix[i] * 100).toFixed(1)}%  무작위 상대 ${(vsField[i] * 100).toFixed(1)}%  덱 ${pool.length - 1}개 중 ${beats}개에 우세  ${describe(pool[i])}`);
  });
  console.log('\n균형 덱끼리 상성 (행이 열을 이길 확률 %)');
  support.forEach(i => console.log('  ' + support.map(j => (M[i][j] * 100).toFixed(0).padStart(3)).join(' ')));
  const worst = Math.max(...pool.map((_, i) => Math.min(...pool.map((_, j) => i === j ? 1 : M[i][j]))));
  console.log(`\n모든 덱을 이기는 덱: ${worst > 0.5 ? '있음' : '없음'} (가장 약한 상성이 가장 높은 덱의 그 상성 ${(worst * 100).toFixed(1)}%)`);
  const freq = {};
  support.forEach(i => pool[i].units.forEach(u => { freq[u] = (freq[u] || 0) + mix[i]; }));
  console.log('균형 비중 높은 무인: ' + Object.entries(freq).sort((a, b) => b[1] - a[1]).slice(0, 8)
    .map(([u, w]) => `${S.UNIT[u].name} ${(w * 100).toFixed(0)}%`).join(' · '));
  require('fs').writeFileSync(process.env.META_OUT || '/dev/null', JSON.stringify({ pool, mix, M, vsField }));
}

module.exports = { toTeam, randomDeck, mutate, score, climb, matrix, nash, describe };
