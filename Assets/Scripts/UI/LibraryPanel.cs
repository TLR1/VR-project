// Assets/Scripts/Interaction/LibraryPanel.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class LibraryPanel : MonoBehaviour
{
    /*──────────── إعداد من الـInspector ────────────*/
    [Header("UI Toolkit")]
    [Tooltip("عنصر UXML الذى يمثل صفاً واحداً فى المكتبة")]
    public VisualTreeAsset itemTemplate;          // LibraryItem.uxml

    [Tooltip("id عنصر ScrollView داخل Panel_Library.uxml")]
    public string scrollViewId = "scrollView";

    [Header("Prefabs to spawn")]
    public List<GameObject> prefabs = new();      // أسقِط الـPrefabs هنا

    /*──────────── متغيرات خاصة ────────────*/
    ScrollView scroll;
    VisualElement ghost;
    bool dragging;
    GameObject pendingPrefab;

    /*────────────────────────────────────────*/
    void Awake()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        scroll = root.Q<ScrollView>(scrollViewId);

        // أنشئ صفًّا لكل Prefab
        foreach (GameObject pf in prefabs)
            CreateEntry(pf);

        // التتبع العام لحركة وإفلات الماوس
        root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        root.RegisterCallback<PointerUpEvent>(OnPointerUp);
    }

    /*──────────────── إنشاء صف ─────────────*/
    void CreateEntry(GameObject prefab)
    {
        VisualElement entry = itemTemplate.CloneTree();

        var icon = entry.Q<VisualElement>("icon");
        var label = entry.Q<Label>("label");

        label.text = prefab.name;

        /*ــ محاولة جلب صورة مصغرة ــ*/
        Sprite sprite = Resources.Load<Sprite>($"Icons/{prefab.name}");
#if UNITY_EDITOR
        if (sprite == null)
        {
            var tex = UnityEditor.AssetPreview.GetAssetPreview(prefab);
            if (tex)
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * .5f);
        }
#endif
        if (sprite)
            icon.style.backgroundImage = new StyleBackground(sprite);
        else
            icon.style.backgroundColor = new StyleColor(new Color(.35f, .35f, .35f));

        /*ــ ابدأ السحب عند PointerDown ــ*/
        entry.RegisterCallback<PointerDownEvent>(e =>
        {
            StartDrag(prefab, e.position, icon);
            e.StopPropagation();                 // منع ScrollView من استهلاك الحدث
        });

        scroll.Add(entry);
    }

    /*──────────────── بدء السحب ─────────────*/
    void StartDrag(GameObject pf, Vector2 mousePos, VisualElement sourceIcon)
    {
        dragging = true;
        pendingPrefab = pf;

        ghost = new VisualElement
        {
            name = "ghost",
            style =
            {
                width           = 64,
                height          = 64,
                position        = Position.Absolute,
                backgroundImage = sourceIcon.style.backgroundImage,
                backgroundColor = sourceIcon.style.backgroundColor
            },
            pickingMode = PickingMode.Ignore     // لا يلتقط أحداث
        };

        // ضع الـGhost فى الشجرة الجذرية
        scroll.panel.visualTree.Add(ghost);
        MoveGhost(mousePos);
    }

    /*──────────────── متابعة المؤشر ─────────*/
    void OnPointerMove(PointerMoveEvent e)
    {
        if (dragging && ghost != null)
            MoveGhost(e.position);
    }
    void MoveGhost(Vector2 mouse)
    {
        ghost.style.left = mouse.x - 32;
        ghost.style.top = mouse.y - 32;
    }

    /*──────────────── الإفلات ───────────────*/
    void OnPointerUp(PointerUpEvent e)
    {
        if (!dragging) return;

        dragging = false;
        ghost?.RemoveFromHierarchy();
        ghost = null;

        // تقاطع الشعاع مع مستوى الأرض (Y = 0)
        Ray ray = Camera.main.ScreenPointToRay(e.position);
        if (new Plane(Vector3.up, 0f).Raycast(ray, out float hit))
        {
            Vector3 pos = ray.GetPoint(hit);
            SpawnPrefab(pos);
        }
        pendingPrefab = null;
    }

    /*──────────────── Instantiate + Draggable ─────────*/
    void SpawnPrefab(Vector3 pos)
    {
        if (!pendingPrefab) return;

        GameObject go = Instantiate(pendingPrefab, pos, Quaternion.identity);

        // أضف Draggable إذا لم يكن موجودًا
        if (!go.TryGetComponent(out Draggable d))
            d = go.AddComponent<Draggable>();    // يُسجَّل تلقائياً فى SelectionManager
    }
}
