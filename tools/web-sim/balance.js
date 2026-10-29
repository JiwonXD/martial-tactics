// 밸런스 측정: 무작위 편성으로 많은 판을 돌려 무인별·무공별 승률을 본다.
//   node tools/web-sim/balance.js [판 수=40000] [시드=1]
// 편성: 뽑기에 나오는 무인(6성 제외) 중 3명, 무공창 2·3칸은 팀에서 안 쓴 무공 중 무작위,
//       속성은 세 무인 적성 합이 가장 좋은 것 (진법 주인이 주장이면 그 진법 속성).
//       승률은 그 무인·무공이 들어간 팀의 승률.
const S = require('./engine.js');

// 등급 (C# Catalog의 Stars와 같게 둔다. 없으면 3성)
const STARS = { dok: 5, cheol: 5, gongjin: 5, guyang: 5, jeokyeom: 5, sahyeol: 5, cheongheo: 6, moyong: 6, hwangboyeol: 4, muyeong: 4, jangwiwon: 4 };
const stars = id => STARS[id] || 3;
const APT_SCORE = { S: 3, A: 2, B: 1, C: 0 };

function mulberry32(seed) {
  return () => { seed = seed + 0x6D2B79F5 | 0; let t = Math.imul(seed ^ seed >>> 15, 1 | seed); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };
}

// 자리에 따라 효과가 갈리는 무공: 진법(주장·같은 속성), 두전성이(주장), 호위(주장이 아닐 때)
const LEADER_ONLY = new Set(['dujeon']), NOT_LEADER = new Set(['howi', 'hopyo']);
const placed = s => s.type === 'formation' || LEADER_ONLY.has(s.id) || NOT_LEADER.has(s.id);

// 실제 편성처럼 자리를 잡는다: 진법·두전성이 주인은 주장, 호위 주인은 부장
function arrange(units) {
  const lead = units.findIndex(u => S.SKILL[u.skill].type === 'formation');
  const i = lead >= 0 ? lead : units.findIndex(u => LEADER_ONLY.has(u.skill));
  if (i > 0) [units[0], units[i]] = [units[i], units[0]];
  if (NOT_LEADER.has(units[0].skill)) {
    const j = units.findIndex(u => !NOT_LEADER.has(u.skill));
    if (j > 0) [units[0], units[j]] = [units[j], units[0]];
  }
  return S.SKILL[units[0].skill].type === 'formation' ? S.SKILL[units[0].skill].element : null;
}

function randomTeam(rand, pool) {
  const units = [];
  while (units.length < 3) { const u = pool[Math.floor(rand() * pool.length)]; if (!units.includes(u)) units.push(u); }
  const formationElement = arrange(units);
  const used = units.map(u => u.skill);
  const free = S.SKILLS.filter(s => !used.includes(s.id) && !placed(s)); // 자리 따라 갈리는 무공은 무작위로 안 끼움
  const slots = units.map(u => ({ unit: u.id, skills: [0, 1].map(() => {
    const i = Math.floor(rand() * free.length);
    return free.splice(i, 1)[0].id;
  }) }));
  if (formationElement) return { element: formationElement, slots };
  let best = null, bestScore = -1;
  S.ELEMENTS.forEach((e, ei) => {
    const score = units.reduce((s, u) => s + APT_SCORE[u.apt[ei]], 0) + rand() * 0.5; // 동점은 무작위
    if (score > bestScore) { best = e; bestScore = score; }
  });
  return { element: best, slots };
}

function measure(n, seed) {
  const rand = mulberry32(seed);
  const pool = S.UNITS.filter(u => stars(u.id) <= 5);
  const unit = {}, skill = {};
  const add = (map, k, win, extra) => {
    const r = map[k] || (map[k] = { n: 0, w: 0, ln: 0, lw: 0 });
    r.n++; r.w += win;
    if (extra) { r.ln++; r.lw += win; }
  };
  for (let i = 0; i < n; i++) {
    const a = randomTeam(rand, pool), b = randomTeam(rand, pool);
    const r = new S.Battle(a, b, { seed: seed + i }).run();
    for (const [team, side] of [[a, 'A'], [b, 'B']]) {
      const win = r.winner === side ? 1 : r.winner ? 0 : 0.5;
      team.slots.forEach((s, pos) => {
        add(unit, s.unit, win, pos === 0);                     // ln/lw = 주장일 때
        s.skills.forEach(id => add(skill, id, win, false));    // 추출해 낀 무공
      });
    }
  }
  return { unit, skill };
}

const pct = (w, n) => n ? (w / n * 100).toFixed(1).padStart(5) : '    -';

if (require.main === module) {
  const n = +(process.argv[2] || 40000), seed = +(process.argv[3] || 1);
  const { unit, skill } = measure(n, seed);
  const rows = S.UNITS.filter(u => unit[u.id]).map(u => ({ u, r: unit[u.id], wr: unit[u.id].w / unit[u.id].n }));
  console.log(`무작위 ${n}판 (시드 ${seed})`);
  for (const st of [5, 4, 3]) {
    const g = rows.filter(x => stars(x.u.id) === st).sort((x, y) => y.wr - x.wr);
    if (!g.length) continue;
    const avg = g.reduce((s, x) => s + x.r.w, 0) / g.reduce((s, x) => s + x.r.n, 0);
    console.log(`\n${st}성 ${g.length}명, 평균 승률 ${(avg * 100).toFixed(1)}%`);
    g.forEach(x => {
      const sk = S.SKILL[x.u.skill], kr = skill[x.u.skill] || { n: 0, w: 0 };
      console.log(`  ${pct(x.r.w, x.r.n)}%  주장 ${pct(x.r.lw, x.r.ln)}%  ${x.u.name.padEnd(5, '　')} ${x.u.faction}  무공 ${sk.name} (남이 낄 때 ${pct(kr.w, kr.n)}%)`);
    });
  }
}

module.exports = { measure, stars };
