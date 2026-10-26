# martial-tactics

무협 세계관 3인 팀 자동 전투 카드배틀 (Unity 6.3 LTS, 6000.3.27f1).

Unity Hub → Add → 이 폴더를 열면 된다. 처음 열 때 Unity가 만드는 `ProjectSettings/`, `Packages/`, `.meta` 파일은 커밋한다.

## 전투 엔진

- 웹 시뮬레이터: `tools/web-sim/` (규칙 실험용, `node tools/web-sim/test.js`로 규칙 테스트)
- Unity용 C# 엔진: `Assets/Scripts/Battle/` (Unity 의존 없음)
- 두 엔진이 같은 시드에서 같은 결과를 내는지 확인:
  ```
  node tools/web-sim/make-golden.js     # 웹 엔진을 고쳤으면 골든 다시 생성
  dotnet run --project Tests/BattleParity
  ```

## 게임 기본 틀 (기능 확인용)

- 게임 진행 로직: `Assets/Scripts/Game/` (Unity 의존 없음) — 뽑기, 승급(자유 스탯), 무공 추출·장착, 덱 편성, 전투 연결
- 화면: `Assets/Scripts/App/GameApp.cs` (IMGUI) — 씬에 아무것도 없어도 Play하면 스스로 생긴다
  - 처음 → 뽑기 / 무인 목록 → 무인 창 / 덱 편성 / 전투(한 합씩 기록 → 결과)
  - 진행 상황은 `Application.persistentDataPath/save.json`에 저장, 처음 화면에서 초기화
- 아직 기획이 안 정해져 임시로 둔 것 (`GameState`에 `임시` 주석):
  - 무인 등급: 전부 3성 / 뽑기 확률: 균등 / 전투 상대: 무작위 3명 + 무작위 속성, 추가 무공 없음
  - 레벨은 성장치가 정해지면 추가
- 로직 테스트:
  ```
  dotnet run --project Tests/GameLogic
  ```
