# martial-tactics

무협 세계관 3인 팀 자동 전투 카드배틀 (Unity 6.3 LTS, 6000.3.27f1).

Unity Hub → Add → 이 폴더를 열면 된다. 처음 열 때 Unity가 만드는 `ProjectSettings/`, `Packages/`, `.meta` 파일은 커밋한다.

## 전투 엔진

- 웹 시뮬레이터: `tools/web-sim/` (규칙 실험용, `node tools/web-sim/test.js`로 규칙 테스트)
- Unity용 C# 엔진: `Assets/Scripts/Battle/` (Unity 의존 없음)
- 장비: 팀 편성의 무인마다 장비 합계(`gear` / `TeamSlot.Gear`)를 넘긴다. 스탯(외공·내공·호신·경공)은 무인 스탯에 더한 뒤 적성 배수를 곱하고, 특수효과 7종(치명타율·치명타 피해·무공 피해 증가·받는 피해 감소·회피·제어 적중·제어 저항)은 전투 내내 걸린 효과로 둔다. 웹 시뮬레이터는 무인 칸의 '장비'를 펼쳐 입력한다.
- 무인 80명(기존 30명 + 출시 무인 50명).
- 밸런스 측정: `node tools/web-sim/balance.js [판 수] [시드]` — 무작위 편성으로 무인별(전체·주장일 때)·무공별(추출해 낄 때) 승률을 등급별로 보여 준다. 1차 기준은 무작위 편성 승률 3성 49% · 4성 53% · 5성 57%.
- 밸런스 점검: `node tools/web-sim/balance-report.js [시드당 판 수] [시드들]` — 거울 대전으로 자리 편향, 시드별 등급 평균, 무인별 95% 신뢰구간, 팀원 효과를 뺀 무인·무공 기여도(로지스틱 회귀), 끝난 합·끝난 방식 분포.
- 메타 점검: `node tools/web-sim/meta-search.js [시드] [반복] [오르기 횟수]` — 언덕 오르기로 최강 덱을 찾고 덱끼리 상성표의 내시 균형을 풀어 지배 덱·쏠린 무인이 있는지 본다 (시드 하나에 4~5분, 시드마다 결과 차이가 커서 두 개 이상 돌려 공통된 것만 본다).
- 밸런스 심화 측정: `node tools/web-sim/balance-lab.js [항목...] [--out 폴더]` — 항목은 pacing(전투 길이·주장 격파 비율) · luck(운과 편성 비중) · skills/units(같은 상대·시드로 하나만 바꿔 넣은 승률 차이, 95% 신뢰구간) · search(최강덱 탐색, 처음 보는 상대로 재검증) · sensitivity(규칙 상수 ±20%) · faction(같은 세력 보너스) · growth(추가 스탯 효과). 없으면 전부, 약 3~4분.
- 두 엔진이 같은 시드에서 같은 결과를 내는지 확인:
  ```
  node tools/web-sim/make-golden.js     # 웹 엔진을 고쳤으면 골든 다시 생성
  dotnet run --project Tests/BattleParity
  ```

## 웹게임 (Unity보다 먼저 공개하는 판)

- 주소 (Claude Artifact): https://claude.ai/artifact/7qxVKQLax59aKyQC4QXshq — 비공개, 다른 사람에게는 페이지의 Share 메뉴로 공유
- 소스: `web/` — Unity 화면(`Assets/Scripts/App/`, 스타일 `App.uss`)과 진행 로직(`GameState.cs`)을 HTML/JS로 옮긴 것. 전투는 `tools/web-sim/engine.js`를 그대로 쓴다
- 진행 상황은 브라우저 localStorage에 저장 (설정 → 저장 초기화)
- 화면·규칙을 Unity 쪽에서 바꾸면 `web/`도 같이 고친다. 아직 정하지 않은 수치는 Unity와 같이 `[n]`
- 묶기: `node web/build.js` → `web/dist/artifact.html`(CSS·JS를 모두 넣은 한 파일, Artifact로 올림) · `web/dist/index.html`(로컬 확인용)
- 테스트: `node web/test.js` (등급이 C# Catalog와 같은지, 뽑기·승급·추출·장착·덱·전투)
- 배포: `main`(실제 서비스판)에 머지된 내용으로 `artifact.html`을 만들어 같은 Artifact 주소에 다시 올린다

## 게임 기본 틀 (기능 확인용)

- 게임 진행 로직: `Assets/Scripts/Game/` (Unity 의존 없음) — 뽑기, 승급(자유 스탯), 무공 추출·장착, 덱 편성, 전투 연결
- 화면: `Assets/Scripts/App/` (UI Toolkit, 스타일은 `Assets/Resources/UI/App.uss`, 화풍 A) — 씬에 아무것도 없어도 Play하면 스스로 생긴다
  - 로비(하단 바: 무인 · 성장 · 대장간 · 컨텐츠 · 뽑기) → 무인 목록 → 무인 상세 → 승급·추출 / 무공 고르기, 덱 편성 → 무인 고르기, 대전 → 전투 → 결과
  - 그림은 `Assets/Resources/Portraits/<무인 id>`(초상), `Assets/Resources/Art/lobby_bg`(배경)에서 읽고 없으면 자리 표시. 생성 그림은 확인 전까지 커밋하지 않는다
  - 진행 상황은 `Application.persistentDataPath/save.json`에 저장, 처음 화면에서 초기화
- 아직 기획이 안 정해져 임시로 둔 것 (`GameState`에 `임시` 주석):
  - 무인 등급: 전부 3성 / 뽑기 확률: 균등 / 전투 상대: 무작위 3명 + 무작위 속성, 추가 무공 없음
  - 레벨은 성장치가 정해지면 추가
- 로직 테스트:
  ```
  dotnet run --project Tests/GameLogic
  ```
