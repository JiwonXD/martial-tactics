using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_2023_1_OR_NEWER
using UnityEngine.TextCore.Text;
#endif

namespace MartialTactics.App
{
    /// <summary>
    /// UI Toolkit 화면을 띄우는 곳. 씬 없이 코드로 PanelSettings를 만들고, 390×844(세로 폰) 기준으로 맞춘다.
    /// </summary>
    public sealed class UiRoot
    {
        public static readonly Vector2Int Reference = new Vector2Int(390, 844);

        readonly UIDocument doc;
        VisualElement root;

        public bool Visible { get; private set; }

        public UiRoot(Transform parent)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/RuntimeTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = Reference;
            settings.screenMatchMode = PanelScreenMatchMode.Expand; // 화면 비율이 달라도 390×844보다 작아지지 않게 (남는 쪽이 넓어진다)

            var go = new GameObject("UI");
            go.SetActive(false); // panelSettings를 넣은 뒤 켜야 패널이 생긴다
            go.transform.SetParent(parent, false);
            doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            go.SetActive(true);

            root = doc.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/App"));
            root.AddToClassList("app-root");
            var paper = Art.Load("Art/hanji"); // 한지 질감. 없으면 USS의 단색
            if (paper != null) root.style.backgroundImage = new StyleBackground(paper);
            ApplyKoreanFont(root);
            Hide();
        }

        public void Show(VisualElement screen)
        {
            root.Clear();
            root.Add(screen);
            root.style.display = DisplayStyle.Flex;
            Visible = true;
        }

        public void Hide()
        {
            root.Clear();
            root.style.display = DisplayStyle.None;
            Visible = false;
        }

        // 기본 글꼴에 한글이 없을 때를 대비해 OS 한글 글꼴로 글꼴 에셋을 만든다. 출시 전에는 글꼴 파일을 프로젝트에 넣는다.
        // Play를 끌 때 이 에셋이 화면보다 먼저 파괴되면 글자 그리기가 NullReferenceException을 내서, 파괴되지 않게 표시해 둔다.
#if UNITY_2023_1_OR_NEWER
        static FontAsset koreanFont;
#endif

        static void ApplyKoreanFont(VisualElement root)
        {
#if UNITY_2023_1_OR_NEWER
            if (koreanFont == null)
            {
                // 화풍 A는 명조. Noto Serif KR은 가변 글꼴이라 이름으로 안 열리고 파일로 열면 가장 가는 굵기라 OS 명조를 쓴다. 없으면 고딕
                foreach (var family in new[] { "HCR Batang", "Batang", "AppleMyungjo", "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans KR", "Noto Sans CJK KR" })
                {
                    var font = FontAsset.CreateFontAsset(family, "Regular");
                    if (font == null) continue;
                    font.hideFlags = HideFlags.HideAndDontSave;
                    if (font.material != null) font.material.hideFlags = HideFlags.HideAndDontSave;
                    if (font.atlasTextures != null)
                        foreach (var tex in font.atlasTextures)
                            if (tex != null) tex.hideFlags = HideFlags.HideAndDontSave;
                    koreanFont = font;
                    break;
                }
            }
            if (koreanFont != null)
                root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(koreanFont));
#endif
        }
    }
}
