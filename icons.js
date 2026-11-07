// 선 아이콘 (24×24 격자). Unity Assets/Scripts/App/Icon.cs 의 그리기를 SVG로 옮김. 색은 글자색(currentColor).
(function (root) {
  'use strict';
  const L = (pts, w) => `<polyline points="${pts}" fill="none"${w ? ` stroke-width="${w}"` : ''}/>`;
  const P = (pts, fill) => `<polygon points="${pts}"${fill ? ' fill="currentColor"' : ' fill="none"'}/>`;
  const C = (x, y, r) => `<circle cx="${x}" cy="${y}" r="${r}" fill="none"/>`;

  const SHAPES = {
    units: C(12, 7.5, 4) + '<path d="M4 21C4 16.6 7.6 14 12 14C16.4 14 20 16.6 20 21" fill="none"/>',
    growth: L('3,20 21,20') + L('5,20 10,10 13.5,15.5 15.5,12.5 19,20') + C(17, 6, 2), // 산과 해: 폐관수련
    content: L('5,4 15,14') + L('12.5,16.5 17.5,11.5') + L('15.5,15.5 19.5,19.5') + L('19,4 9,14') + L('11.5,16.5 6.5,11.5') + L('8.5,15.5 4.5,19.5'), // 엇갈린 두 검
    gacha: L('8,3 16,3') + P('7,6 17,6 18,17 15,21 9,21 6,17') + L('6.6,12 17.4,12'), // 항아리
    forge: P('3,12 21,12 18,15.5 15,15.5 16,19 18.5,19 18.5,21 5.5,21 5.5,19 8,19 9,15.5 6,15.5', true) + L('13,9 19,3', 2.6) + P('16,1.5 21.5,7 20,8.5 14.5,3', true), // 모루와 망치
    gear: C(12, 12, 3) + ['12,2 12,5', '12,19 12,22', '2,12 5,12', '19,12 22,12', '4.9,4.9 7,7', '17,17 19.1,19.1', '4.9,19.1 7,17', '17,7 19.1,4.9'].map(p => L(p)).join(''),
    close: L('6,6 18,18', 2.4) + L('18,6 6,18', 2.4),
    chest: P('3,10 21,10 21,20 3,20') + L('3,10 5,5 19,5 21,10') + P('10,13 14,13 14,16 10,16'), // 방치 보상
    back: L('15,5 8,12 15,19', 2.2),
    plus: L('12,5 12,19', 2) + L('5,12 19,12', 2),
    coin: C(12, 12, 8.5) + P('9.5,9.5 14.5,9.5 14.5,14.5 9.5,14.5'), // 골드: 네모 구멍 엽전
    ticket: P('3,7 21,7 21,10 19.5,12 21,14 21,17 3,17 3,14 4.5,12 3,10') + L('15,7.5 15,9') + L('15,11.25 15,12.75') + L('15,15 15,16.5'), // 뽑기권
    book: P('5,3 19,3 19,21 5,21') + L('8,3 8,21') + L('11,8 16,8') + L('11,11 16,11'), // 레벨업 재화 (비급)
    ore: P('4,15 8,7 14,5 20,9 19,17 11,20') + L('8,7 11,13 19,17') + L('11,13 14,5'), // 철광석
    flame: '<path d="M12 21C6 21 5 15 8 11C8.5 13 10 14 10.5 14C9.5 9 12 5 14 3C14 7 19 10 18 16C17.5 19 15 21 12 21Z" fill="none"/>', // 불씨
    pill: C(12, 13, 7) + '<path d="M8.71 11.8A3.5 3.5 0 0 1 13.2 9.71" fill="none"/>' + L('12,3 12,4.5') + L('7,4.5 7.8,5.8') + L('17,4.5 16.2,5.8'), // 단약
    clock: C(12, 12, 8.5) + L('12,7 12,12 15.5,14'),
    check: L('5,12.5 10,17.5 19,7', 2.4),
    next: L('9,5 16,12 9,19', 2.2),
    prev: L('15,5 8,12 15,19', 2.2),
    down: L('5,9 12,16 19,9', 2.2),
    up: L('5,15 12,8 19,15', 2.2),
  };

  function icon(kind, size) {
    const s = size || 24;
    const span = document.createElement('div');
    span.className = 'icon';
    span.style.width = span.style.height = s + 'px';
    span.style.flexShrink = '0';
    span.innerHTML = `<svg viewBox="0 0 24 24" width="${s}" height="${s}" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${SHAPES[kind] || ''}</svg>`;
    return span;
  }

  root.WuxiaIcon = icon;
})(typeof self !== 'undefined' ? self : this);
