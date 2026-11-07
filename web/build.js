// 웹게임 한 파일로 묶기: web/ 화면 + 전투 엔진(tools/web-sim/engine.js)을 CSS·JS 모두 안에 넣은 HTML 하나로.
//   node web/build.js
//   → web/dist/artifact.html : Claude Artifact로 올리는 파일 (게시할 때 <!doctype>·<head>가 씌워짐)
//   → web/dist/index.html    : 로컬에서 브라우저로 여는 확인용 (같은 내용에 문서 틀만 붙임)
const fs = require('fs');
const path = require('path');
const read = f => fs.readFileSync(path.join(__dirname, f), 'utf8');
// 그림(web/art/)은 data URI로 넣는다 (Artifact는 바깥 파일을 못 불러옴)
const art = f => 'data:image/jpeg;base64,' + fs.readFileSync(path.join(__dirname, 'art', f)).toString('base64');
const ids = fs.readdirSync(path.join(__dirname, 'art/portraits')).filter(f => f.endsWith('.jpg')).map(f => f.slice(0, -4)).sort();
const artScript = `window.WuxiaArt = ${JSON.stringify(ids)};`;
const artCss = `:root { --hanji: url(${art('hanji.jpg')}); --mountains: url(${art('mountains.jpg')}); }\n`
  + ids.map(id => `.portrait.u-${id} { background-image: url(${art('portraits/' + id + '.jpg')}); }`).join('\n');
const scripts = [read('../tools/web-sim/engine.js'), read('game.js'), read('icons.js'), artScript, read('app.js')];
if (scripts.some(s => s.includes('</script'))) throw new Error('스크립트 안에 </script 가 있어 한 파일로 묶을 수 없음');

const page = `<title>무협 카드배틀</title>
<meta name="theme-color" content="#1c130d">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Noto+Sans+KR:wght@400;500;700;900&family=Noto+Serif+KR:wght@700;900&display=swap" rel="stylesheet">
<style>
${artCss}
${read('style.css')}
</style>
<div id="app"></div>
${scripts.map(s => `<script>\n${s}\n</script>`).join('\n')}
`;

const out = path.join(__dirname, 'dist');
fs.rmSync(out, { recursive: true, force: true });
fs.mkdirSync(out);
fs.writeFileSync(path.join(out, 'artifact.html'), page);
fs.writeFileSync(path.join(out, 'index.html'), `<!doctype html>\n<html lang="ko">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">\n</head>\n<body>\n${page}</body>\n</html>\n`);
console.log(`web/dist 만듦 (artifact.html ${Math.round(page.length / 1024)}KB)`);
