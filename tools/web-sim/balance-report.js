// 밸런스 점검: 측정을 믿을 수 있는지, 팀원 효과를 뺀 기여도, 전투 속도를 본다.
//   node tools/web-sim/balance-report.js [시드당 판 수=100000] [시드=11,12,13]
// 1) 거울 대전(같은 팀끼리)으로 A·B 자리 편향, 시드별 등급 평균, 무인별 95% 신뢰구간(Wilson)
// 2) 로지스틱 회귀: 승패 ~ (A팀 무인·추출 무공) - (B팀 무인·추출 무공). 상수항 = 자리 편향.
//    계수는 '평균 무인(무공) 대신 넣었을 때 승률 변화'로 바꿔 보여 준다.
// 3) 끝난 합 분포와 끝난 방식(주장 격파 / 8합 후 체력 비율 / 무승부)
const S = require('./engine.js');
const B = require('./balance.js');

const pct = (x, d = 1) => (x * 100).toFixed(d);
const wilson = (w, n, z = 1.96) => {
  const p = w / n, d = 1 + z * z / n, c = (p + z * z / (2 * n)) / d;
  const h = z * Math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / d;
  return [c - h, c + h];
};
const sig = x => 1 / (1 + Math.exp(-x));

function mirror(n, seed) {
  const rand = B.mulberry32(seed);
  let w = 0;
  for (let i = 0; i < n; i++) {
    const t = B.randomTeam(rand, B.POOL);
    const r = new S.Battle(t, t, { seed: seed + i }).run();
    w += r.winner === 'A' ? 1 : r.winner ? 0 : 0.5;
  }
  return w / n;
}

// 대칭 행렬 연립방정식 (가우스-조르당). inv=true면 역행렬도 돌려준다.
function solve(H, g) {
  const n = g.length, A = H.map((row, i) => [...row, g[i], ...Array.from({ length: n }, (_, j) => +(i === j))]);
  for (let c = 0; c < n; c++) {
    let p = c;
    for (let r = c + 1; r < n; r++) if (Math.abs(A[r][c]) > Math.abs(A[p][c])) p = r;
    [A[c], A[p]] = [A[p], A[c]];
    const v = A[c][c];
    for (let k = c; k < A[c].length; k++) A[c][k] /= v;
    for (let r = 0; r < n; r++) if (r !== c && A[r][c]) {
      const f = A[r][c];
      for (let k = c; k < A[r].length; k++) A[r][k] -= f * A[c][k];
    }
  }
  return { x: A.map(r => r[n]), inv: A.map(r => r.slice(n + 1)) };
}

// rows: [{ idx: [특성 번호], val: [+1/-1], y }]. 0번 특성 = 상수항
function logistic(rows, k, lambda = 1, iters = 8) {
  let beta = new Array(k).fill(0), inv;
  for (let it = 0; it < iters; it++) {
    const H = Array.from({ length: k }, () => new Float64Array(k)), g = new Float64Array(k);
    for (const { idx, val, y } of rows) {
      let z = 0;
      for (let i = 0; i < idx.length; i++) z += beta[idx[i]] * val[i];
      const p = sig(z), w = p * (1 - p);
      for (let i = 0; i < idx.length; i++) {
        g[idx[i]] += (y - p) * val[i];
        for (let j = 0; j < idx.length; j++) H[idx[i]][idx[j]] += w * val[i] * val[j];
      }
    }
    for (let i = 0; i < k; i++) { H[i][i] += i ? lambda : 1e-6; g[i] -= i ? lambda * beta[i] : 0; }
    const r = solve(H.map(a => Array.from(a)), Array.from(g));
    beta = beta.map((b, i) => b + r.x[i]);
    inv = r.inv;
  }
  return { beta, inv };
}

// 묶음 [a, b) 안에서 '계수 - 묶음 평균'의 표준오차 (팀 인원이 늘 같아서 계수 하나만의 오차는 크게 나온다)
function contrastSe(inv, a, b) {
  const n = b - a, rowMean = i => { let s = 0; for (let j = a; j < b; j++) s += inv[i][j]; return s / n; };
  let all = 0;
  for (let i = a; i < b; i++) all += rowMean(i);
  all /= n;
  return i => Math.sqrt(Math.max(0, inv[i][i] - 2 * rowMean(i) + all));
}

if (require.main === module) {
  const n = +(process.argv[2] || 100000);
  const seeds = (process.argv[3] || '11,12,13').split(',').map(Number);
  const units = B.POOL, skills = B.EXTRACTABLE;
  const ui = new Map(units.map((u, i) => [u.id, 1 + i])), si = new Map(skills.map((s, i) => [s.id, 1 + units.length + i]));
  const K = 1 + units.length + skills.length;
  const rows = [], ends = { leader: 0, hp: 0, draw: 0 }, byRound = new Array(9).fill(0);

  console.log(`== 1. 측정 신뢰도 (시드당 ${n}판) ==`);
  const m = mirror(20000, 999);
  console.log(`거울 대전 2만 판: A 승률 ${pct(m)}% (50%면 자리 편향 없음)`);
  const perSeed = [];
  for (const seed of seeds) {
    const res = B.measure(n, seed, r => {
      const idx = [0], val = [1];
      for (const [team, sgn] of [[r.a, 1], [r.b, -1]]) for (const s of team.slots) {
        idx.push(ui.get(s.unit)); val.push(sgn);
        for (const k of s.skills) { idx.push(si.get(k)); val.push(sgn); }
      }
      rows.push({ idx, val, y: r.winner === 'A' ? 1 : r.winner ? 0 : 0.5 });
      ends[r.reason]++;
      byRound[r.rounds]++;
    });
    perSeed.push(res);
    const g = st => {
      const xs = units.filter(u => B.stars(u.id) === st).map(u => res.unit[u.id]);
      return xs.reduce((s, x) => s + x.w, 0) / xs.reduce((s, x) => s + x.n, 0);
    };
    console.log(`시드 ${seed}: 5성 ${pct(g(5))}% · 4성 ${pct(g(4))}% · 3성 ${pct(g(3))}%`);
  }

  // 시드를 합친 무인별 승률과 신뢰구간
  const tot = {}, totS = {};
  for (const res of perSeed) {
    for (const [k, r] of Object.entries(res.unit)) { const t = tot[k] || (tot[k] = { n: 0, w: 0 }); t.n += r.n; t.w += r.w; }
    for (const [k, r] of Object.entries(res.skill)) { const t = totS[k] || (totS[k] = { n: 0, w: 0 }); t.n += r.n; t.w += r.w; }
  }

  console.log(`\n== 2. 팀원 효과를 뺀 기여도 (로지스틱 회귀, ${rows.length}판) ==`);
  const { beta, inv } = logistic(rows, K);
  const seU = contrastSe(inv, 1, 1 + units.length), seS = contrastSe(inv, 1 + units.length, K);
  console.log(`상수항(자리 편향): ${beta[0].toFixed(3)} → A 승률 ${pct(sig(beta[0]))}%`);
  const mean = (a, b) => beta.slice(a, b).reduce((s, x) => s + x, 0) / (b - a);
  const mu = mean(1, 1 + units.length), ms = mean(1 + units.length, K);
  const urows = units.map(u => {
    const b = beta[ui.get(u.id)], t = tot[u.id], [lo, hi] = wilson(t.w, t.n);
    return { u, eff: sig(b - mu) - 0.5, se: seU(ui.get(u.id)) / 4, wr: t.w / t.n, lo, hi };
  });
  for (const st of [5, 4, 3]) {
    const g = urows.filter(r => B.stars(r.u.id) === st).sort((x, y) => y.eff - x.eff);
    console.log(`\n${st}성 (기여도 = 평균 무인 대신 넣었을 때 승률 변화, ±는 95% 오차)`);
    g.forEach(r => console.log(`  ${(r.eff >= 0 ? '+' : '') + pct(r.eff)}%p ±${pct(1.96 * r.se)}  원승률 ${pct(r.wr)}% [${pct(r.lo)}~${pct(r.hi)}]  ${r.u.name}  (${S.SKILL[r.u.skill].name})`));
  }
  const srows = skills.map(s => ({ s, eff: sig(beta[si.get(s.id)] - ms) - 0.5, se: seS(si.get(s.id)) / 4, t: totS[s.id] }))
    .filter(r => r.t).sort((x, y) => y.eff - x.eff);
  console.log('\n추출 무공 (평균 무공 대신 꼈을 때 승률 변화)');
  srows.forEach(r => console.log(`  ${(r.eff >= 0 ? '+' : '') + pct(r.eff)}%p ±${pct(1.96 * r.se)}  원승률 ${pct(r.t.w / r.t.n)}%  ${r.s.name}`));

  console.log(`\n== 3. 전투 속도 (${rows.length}판) ==`);
  const N = rows.length;
  console.log(`주장 격파로 끝남 ${pct(ends.leader / N)}% · 8합 후 체력 비율 ${pct(ends.hp / N)}% · 무승부 ${pct(ends.draw / N)}%`);
  console.log('끝난 합: ' + byRound.map((c, r) => r ? `${r}합 ${pct(c / N)}%` : '').filter(Boolean).join(' · '));
}

module.exports = { wilson, logistic, mirror };
