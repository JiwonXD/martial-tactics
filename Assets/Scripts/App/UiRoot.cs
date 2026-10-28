using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_2023_1_OR_NEWER
using UnityEngine.TextCore.Text;
#endif

namespace MartialTactics.App
{
    /// <summary>
    /// UI Toolkit 화면을 띄우는 곳. 씬 없이 코드로 PanelSettings를 만들고, 390×844(세로 폰) 기준으로 맞춘다.
    /// 확정된 화면(무인 상세, 전투, 결과)만 여기서 그리고, 나머지는 아직 GameApp의 IMGUI 화면이다.
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
            settings.screenMatchMode = PanelScreenMatchMode.Shrink;

            var go = new GameObject("UI");
            go.SetActive(false); // panelSettings를 넣은 뒤 켜야 패널이 생긴다
            go.transform.SetParent(parent, false);
            doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            go.SetActive(true);

            root = doc.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/App"));
            root.AddToClassList("app-root");
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
        static void ApplyKoreanFont(VisualElement root)
        {
#if UNITY_2023_1_OR_NEWER
            foreach (var family in new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans KR", "Noto Sans CJK KR" })
            {
                var font = FontAsset.CreateFontAsset(family, "Regular");
                if (font == null) continue;
                root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(font));
                return;
            }
#endif
        }
    }
}
