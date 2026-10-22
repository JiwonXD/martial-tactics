# martial-tactics

무협 세계관 3인 팀 자동 전투 카드배틀 (Unity).

## 전투 엔진

- 웹 시뮬레이터: `tools/web-sim/` (규칙 실험용, `node tools/web-sim/test.js`로 규칙 테스트)
- Unity용 C# 엔진: `Assets/Scripts/Battle/` (Unity 의존 없음)
- 두 엔진이 같은 시드에서 같은 결과를 내는지 확인:
  ```
  node tools/web-sim/make-golden.js     # 웹 엔진을 고쳤으면 골든 다시 생성
  dotnet run --project Tests/BattleParity
  ```
