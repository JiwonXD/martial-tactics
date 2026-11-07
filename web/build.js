// 웹게임 배포 폴더 만들기: web/ 의 화면 파일 + 전투 엔진(tools/web-sim/engine.js)을 web/dist/ 에 모은다.
//   node web/build.js   → web/dist/index.html 을 브라우저로 열거나 정적 서버로 띄운다
const fs = require('fs');
const path = require('path');
const out = path.join(__dirname, 'dist');
fs.rmSync(out, { recursive: true, force: true });
fs.mkdirSync(out);
for (const f of ['index.html', 'style.css', 'icons.js', 'game.js', 'app.js']) fs.copyFileSync(path.join(__dirname, f), path.join(out, f));
fs.copyFileSync(path.join(__dirname, '..', 'tools', 'web-sim', 'engine.js'), path.join(out, 'engine.js'));
fs.writeFileSync(path.join(out, '.nojekyll'), '');
console.log('web/dist 만듦');
