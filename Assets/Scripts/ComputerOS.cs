using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

// Canvas coordinate space: 640x400
// (Same physical size as 1280x800 but each unit is 2x larger => text is 2x more readable)

public class ComputerOS : MonoBehaviour
{
    [HideInInspector]
    public InteractablePC pcController;

    private Font osFont;
    private GameObject startMenuGo;
    private Text clockText;

    // Big Brother specific
    private Transform bbPCStatusParent;
    private bool isBigBrother = false;
    private float bbRefreshTimer = 0f;

    // Window dragging
    public class WindowDragHandler : MonoBehaviour, UnityEngine.EventSystems.IBeginDragHandler, UnityEngine.EventSystems.IDragHandler
    {
        public RectTransform windowRect;
        private Vector2 dragOffset;

        public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                windowRect.parent as RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 lp);
            dragOffset = windowRect.anchoredPosition - lp;
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (windowRect == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                windowRect.parent as RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 lp);
            windowRect.anchoredPosition = lp + dragOffset;
            Vector2 pos = windowRect.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, -320f + windowRect.sizeDelta.x * 0.5f, 320f - windowRect.sizeDelta.x * 0.5f);
            pos.y = Mathf.Clamp(pos.y, -200f + windowRect.sizeDelta.y * 0.5f, 200f - windowRect.sizeDelta.y * 0.5f);
            windowRect.anchoredPosition = pos;
        }
    }

    // App windows
    private GameObject terminalWin;
    private Text terminalHistoryText;
    private InputField terminalInput;
    private string terminalLog = "";

    private GameObject notepadWin;
    private InputField notepadInput;

    private GameObject snakeWin;
    private Text snakeGridText;
    private Text snakeScoreText;
    private bool snakePlaying = false;

    private GameObject browserWin;
    private InputField browserUrlInput;
    private Transform browserContentPanel;

    private GameObject networkWin;
    private Transform networkListParent;
    private float networkRefreshTimer = 0f;

    private GameObject powerOffOverlay;

    // Snake
    private const int SW = 26, SH = 11;
    private Vector2Int snakeHead;
    private List<Vector2Int> snakeBody = new List<Vector2Int>();
    private Vector2Int snakeDir = Vector2Int.right;
    private Vector2Int snakeFood;
    private int snakeScore = 0;
    private float snakeTimer = 0f;
    private float snakeTickRate = 0.13f;

    void Awake()
    {
        try { osFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch {}
        if (osFont == null) try { osFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch {}
        if (osFont == null) { var f = Resources.FindObjectsOfTypeAll<Font>(); if (f != null && f.Length > 0) osFont = f[0]; }

        string host = pcController != null ? pcController.name : "";
        isBigBrother = (host == "Screen (2)" || host == "Screen (3)");

        BuildDesktopGUI();
    }

    void OnEnable()
    {
        if (terminalWin) terminalWin.SetActive(false);
        if (notepadWin) notepadWin.SetActive(false);
        if (snakeWin) { snakeWin.SetActive(false); snakePlaying = false; }
        if (browserWin) browserWin.SetActive(false);
        if (startMenuGo) startMenuGo.SetActive(false);
        terminalLog = "guest@roomOS:~$ type 'help' for commands.\n\n";
        if (terminalHistoryText) terminalHistoryText.text = terminalLog;
        UpdatePowerOverlay();
    }

    // ===================== LAYOUT HELPERS =====================

    private GameObject MakeGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private GameObject MakePanel(string name, Transform parent, Color col)
    {
        var go = MakeGO(name, parent);
        go.AddComponent<Image>().color = col;
        return go;
    }

    private void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        r.pivot = Vector2.one * 0.5f;
    }

    // Set rect: anchors (min/max) + size + center position
    private void SR(RectTransform r, Vector2 amin, Vector2 amax, Vector2 sz, Vector2 pos)
    {
        r.anchorMin = amin; r.anchorMax = amax;
        r.sizeDelta = sz; r.anchoredPosition = pos;
        r.pivot = Vector2.one * 0.5f;
    }

    private Button MakeBtn(string name, Transform parent, string lbl, Color col, System.Action act, out RectTransform rt)
    {
        var go = MakePanel(name, parent, col);
        rt = go.GetComponent<RectTransform>();
        var btn = go.AddComponent<Button>();
        var bc = btn.colors;
        bc.normalColor = col; bc.highlightedColor = col * 1.3f; bc.pressedColor = col * 0.7f;
        btn.colors = bc;
        btn.onClick.AddListener(() => act());
        if (!string.IsNullOrEmpty(lbl))
        {
            var tgo = MakeGO("Lbl", go.transform);
            var t = tgo.AddComponent<Text>();
            t.font = osFont; t.text = lbl; t.fontSize = 14; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            Stretch(tgo.GetComponent<RectTransform>());
        }
        return btn;
    }

    private Text MakeTxt(string name, Transform parent, string txt, int sz, Color col, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var go = MakeGO(name, parent);
        var t = go.AddComponent<Text>();
        t.font = osFont; t.text = txt; t.fontSize = sz; t.color = col; t.alignment = align;
        return t;
    }

    // ===================== DESKTOP =====================

    private void BuildDesktopGUI()
    {
        var rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(640f, 400f);

        if (isBigBrother) BuildBigBrotherDashboard();
        else BuildRegularDesktop();

        powerOffOverlay = MakePanel("PowerOff", transform, Color.black);
        Stretch(powerOffOverlay.GetComponent<RectTransform>());
        UpdatePowerOverlay();
    }

    // ===================== BIG BROTHER DASHBOARD =====================

    private void BuildBigBrotherDashboard()
    {
        // BG
        var bg = MakePanel("BB_BG", transform, new Color(0.03f, 0.05f, 0.07f));
        Stretch(bg.GetComponent<RectTransform>());

        // Scanlines
        for (int i = 0; i < 20; i++)
        {
            var ln = MakePanel("SL" + i, bg.transform, new Color(0f, 0.35f, 0.22f, 0.06f));
            SR(ln.GetComponent<RectTransform>(), new Vector2(0f, (float)i / 20f), new Vector2(1f, (float)i / 20f), new Vector2(0f, 1f), Vector2.zero);
        }

        // Header
        var hdr = MakePanel("BB_Hdr", transform, new Color(0f, 0.12f, 0.08f));
        SR(hdr.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 30f), new Vector2(0f, -15f));

        var hAcc = MakePanel("HA", hdr.transform, new Color(0f, 1f, 0.5f, 0.5f));
        SR(hAcc.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f), new Vector2(0f, 1f));

        var titleT = MakeTxt("Title", hdr.transform, "◼  BIG BROTHER — SURVEILLANCE CONTROL CENTER  ◼", 16, new Color(0f, 1f, 0.55f));
        titleT.fontStyle = FontStyle.Bold;
        SR(titleT.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0.72f, 1f), Vector2.zero, Vector2.zero);

        var clkGo = MakeGO("Clock", hdr.transform);
        clockText = clkGo.AddComponent<Text>();
        clockText.font = osFont; clockText.fontSize = 14; clockText.color = new Color(0f, 0.85f, 0.5f);
        clockText.alignment = TextAnchor.MiddleRight;
        SR(clkGo.GetComponent<RectTransform>(), new Vector2(0.72f, 0f), new Vector2(1f, 1f), new Vector2(-8f, 0f), new Vector2(-4f, 0f));

        // Left panel
        float lw = 142f, hh = 30f, bh = 14f;
        float ch = 400f - hh - bh;

        var lp = MakePanel("BB_Left", transform, new Color(0.04f, 0.09f, 0.06f));
        SR(lp.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(lw, ch), new Vector2(lw * 0.5f, -hh * 0.5f));

        var lacc = MakePanel("LAcc", lp.transform, new Color(0f, 1f, 0.5f, 0.3f));
        SR(lacc.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(-0.5f, 0f));

        MakeTxt("LTitle", lp.transform, "▶  NETWORK NODES", 12, new Color(0f, 1f, 0.55f), TextAnchor.MiddleLeft).fontStyle = FontStyle.Bold;
        SR(MakeGO("_", lp.transform).GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-8f, 14f), new Vector2(4f, -10f));
        // Redo — place title manually
        var ltGo = lp.transform.GetChild(lp.transform.childCount - 1).gameObject;
        Destroy(ltGo);
        var ltTitle = MakeGO("LTitle", lp.transform);
        var lt = ltTitle.AddComponent<Text>();
        lt.font = osFont; lt.text = "▶  NETWORK NODES"; lt.fontSize = 12; lt.color = new Color(0f, 1f, 0.55f);
        lt.alignment = TextAnchor.MiddleLeft; lt.fontStyle = FontStyle.Bold;
        SR(ltTitle.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-8f, 14f), new Vector2(4f, -8f));

        var sep = MakePanel("Sep", lp.transform, new Color(0f, 0.6f, 0.35f, 0.35f));
        SR(sep.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-8f, 1f), new Vector2(4f, -17f));

        MakeBtn("OnAll", lp.transform, "▶ ALL ON", new Color(0.08f, 0.48f, 0.18f), PowerOnAllPCs, out RectTransform onR);
        SR(onR, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(-4f, 18f), new Vector2(2f, -29f));
        var onTxt = onR.GetComponentInChildren<Text>(); if (onTxt) onTxt.fontSize = 11;

        MakeBtn("OffAll", lp.transform, "■ ALL OFF", new Color(0.48f, 0.08f, 0.08f), PowerOffAllPCs, out RectTransform offR);
        SR(offR, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-4f, 18f), new Vector2(-2f, -29f));
        var offTxt = offR.GetComponentInChildren<Text>(); if (offTxt) offTxt.fontSize = 11;

        var sep2 = MakePanel("Sep2", lp.transform, new Color(0f, 0.6f, 0.35f, 0.35f));
        SR(sep2.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-8f, 1f), new Vector2(4f, -40f));

        var pcList = MakePanel("PCList", lp.transform, Color.clear);
        SR(pcList.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(-4f, -44f), new Vector2(2f, -22f));
        bbPCStatusParent = pcList.transform;

        // Camera grid
        float gx = 640f - lw - 4f;
        var cArea = MakePanel("CamArea", transform, Color.clear);
        SR(cArea.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(-lw - 4f, ch), new Vector2(lw * 0.5f + (640f - lw) * 0.5f + 2f, -hh * 0.5f));

        // Camera grid title
        var ctGo = MakeGO("CamTitle", cArea.transform);
        var ct = ctGo.AddComponent<Text>();
        ct.font = osFont; ct.text = "▶  LIVE SURVEILLANCE FEEDS"; ct.fontSize = 12;
        ct.color = new Color(0f, 1f, 0.55f); ct.fontStyle = FontStyle.Bold; ct.alignment = TextAnchor.MiddleLeft;
        SR(ctGo.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(0f, 14f), new Vector2(6f, -8f));

        string[] camNames = {
            "CAM 01 — NORTH", "CAM 02 — EAST CORRIDOR",
            "CAM 03 — DESK ROW A", "CAM 04 — DESK ROW B",
            "CAM 05 — SOUTH EXIT", "CAM 06 — SERVER ROOM",
            "CAM 07 — HALLWAY W", "CAM 08 — MAIN AREA"
        };
        int cols = 4, rows = 2;
        float padX = 5f, padY = 5f, toph = 16f;
        float tileW = (gx - padX * (cols + 1)) / cols;
        float tileH = (ch - toph - padY * (rows + 1)) / rows;

        for (int i = 0; i < camNames.Length; i++)
        {
            int r = i / cols, c = i % cols;
            float cx2 = padX + c * (tileW + padX) + tileW * 0.5f - gx * 0.5f;
            float cy2 = -(toph + padY + r * (tileH + padY) + tileH * 0.5f) + ch * 0.5f;
            BuildCamTile(cArea.transform, camNames[i], i + 1, new Vector2(cx2, cy2), new Vector2(tileW, tileH));
        }

        // Bottom bar
        var bb = MakePanel("BottomBar", transform, new Color(0f, 0.1f, 0.07f));
        SR(bb.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, bh), new Vector2(0f, bh * 0.5f));
        var bbAcc = MakePanel("BBAcc", bb.transform, new Color(0f, 1f, 0.5f, 0.35f));
        SR(bbAcc.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), Vector2.zero);
        var stsGo = MakeGO("Sts", bb.transform);
        var sts = stsGo.AddComponent<Text>();
        sts.font = osFont; sts.text = "SYSTEM STATUS: ONLINE  |  SECURITY LEVEL: MAXIMUM  |  ALL STATIONS MONITORING";
        sts.fontSize = 10; sts.color = new Color(0f, 0.75f, 0.45f); sts.alignment = TextAnchor.MiddleCenter;
        Stretch(stsGo.GetComponent<RectTransform>());

        RefreshBBPCList();
    }

    private void BuildCamTile(Transform parent, string camName, int num, Vector2 pos, Vector2 size)
    {
        var tile = MakePanel("Cam" + num, parent, new Color(0.05f, 0.08f, 0.06f));
        SR(tile.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, pos);

        var bar = MakePanel("Bar", tile.transform, new Color(0f, 0.12f, 0.08f));
        SR(bar.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 18f), new Vector2(0f, -9f));

        var lblGo = MakeGO("Lbl", bar.transform);
        var lbl = lblGo.AddComponent<Text>();
        lbl.font = osFont; lbl.text = camName; lbl.fontSize = 9;
        lbl.color = new Color(0f, 1f, 0.6f); lbl.alignment = TextAnchor.MiddleLeft;
        SR(lblGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.75f, 1f), new Vector2(0f, 0f), new Vector2(4f, 0f));

        var recGo = MakeGO("REC", bar.transform);
        var rec = recGo.AddComponent<Text>();
        rec.font = osFont; rec.text = "● REC"; rec.fontSize = 8; rec.color = new Color(1f, 0.2f, 0.15f);
        rec.alignment = TextAnchor.MiddleRight;
        SR(recGo.GetComponent<RectTransform>(), new Vector2(0.75f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-3f, 0f));

        var feed = MakePanel("Feed", tile.transform, new Color(0.03f, 0.06f, 0.04f));
        SR(feed.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -18f), new Vector2(0f, -9f));

        var stcGo = MakeGO("Stc", feed.transform);
        var stc = stcGo.AddComponent<Text>();
        stc.font = osFont; stc.text = GenStatic((int)(size.x / 7), (int)(size.y / 10));
        stc.fontSize = 8; stc.color = new Color(0f, 0.38f, 0.22f, 0.65f); stc.alignment = TextAnchor.UpperLeft;
        Stretch(stcGo.GetComponent<RectTransform>());

        float sy = UnityEngine.Random.Range(0.2f, 0.7f);
        var sl = MakePanel("SL", feed.transform, new Color(0f, 0.9f, 0.5f, 0.07f));
        SR(sl.GetComponent<RectTransform>(), new Vector2(0f, sy), new Vector2(1f, sy), new Vector2(0f, 2f), Vector2.zero);

        if (num % 3 == 0)
        {
            float px = UnityEngine.Random.Range(0.2f, 0.75f);
            var ps = MakePanel("Person", feed.transform, new Color(0f, 0.6f, 0.35f, 0.28f));
            SR(ps.GetComponent<RectTransform>(), new Vector2(px - 0.05f, 0.1f), new Vector2(px + 0.05f, 0.8f), Vector2.zero, Vector2.zero);
        }
    }

    private string GenStatic(int cols, int rows)
    {
        rows = Mathf.Max(rows, 4); cols = Mathf.Max(cols, 8);
        var sb = new System.Text.StringBuilder();
        string chars = " .,:;|+xX#";
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int n = UnityEngine.Random.Range(0, 100);
                if (n < 50) sb.Append(' ');
                else if (n < 70) sb.Append('.');
                else if (n < 83) sb.Append(',');
                else if (n < 92) sb.Append(':');
                else sb.Append(chars[UnityEngine.Random.Range(5, chars.Length)]);
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private void RefreshBBPCList()
    {
        if (bbPCStatusParent == null) return;
        foreach (Transform ch in bbPCStatusParent) Destroy(ch.gameObject);

        var pcs = GameObject.FindObjectsByType<InteractablePC>(FindObjectsSortMode.None);
        System.Array.Sort(pcs, (a, b) => string.Compare(a.name, b.name));

        float rowH = 22f;
        float areaH = bbPCStatusParent.GetComponent<RectTransform>()?.rect.height ?? 300f;
        float startY = areaH * 0.5f - rowH * 0.5f;

        for (int i = 0; i < pcs.Length; i++)
        {
            var pc = pcs[i]; bool isSelf = pc.name == "Screen (2)" || pc.name == "Screen (3)";
            bool on = pc.isPoweredOn;
            float y = startY - i * (rowH + 2f);

            Color bg = isSelf ? new Color(0.08f, 0.22f, 0.45f) :
                (on ? new Color(0.05f, 0.3f, 0.12f) : new Color(0.3f, 0.05f, 0.05f));

            var row = MakePanel("Row" + i, bbPCStatusParent, bg);
            SR(row.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, rowH), new Vector2(-2f, y));

            var dot = MakePanel("Dot", row.transform, on ? new Color(0f, 1f, 0.4f) : new Color(1f, 0.2f, 0.2f));
            SR(dot.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(7f, 7f), new Vector2(8f, 0f));

            var nmGo = MakeGO("N", row.transform);
            var nm = nmGo.AddComponent<Text>();
            nm.font = osFont; nm.text = pc.name + (isSelf ? " (SYS)" : ""); nm.fontSize = 11;
            nm.color = Color.white; nm.alignment = TextAnchor.MiddleLeft;
            SR(nmGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.62f, 1f), Vector2.zero, new Vector2(18f, 0f));

            var stGo = MakeGO("S", row.transform);
            var st = stGo.AddComponent<Text>();
            st.font = osFont; st.text = isSelf ? "SYS" : (on ? "ON" : "OFF"); st.fontSize = 10;
            st.color = isSelf ? new Color(0.4f, 0.75f, 1f) : (on ? new Color(0f, 1f, 0.5f) : new Color(1f, 0.35f, 0.35f));
            st.alignment = TextAnchor.MiddleCenter;
            SR(stGo.GetComponent<RectTransform>(), new Vector2(0.62f, 0f), new Vector2(0.82f, 1f), Vector2.zero, Vector2.zero);

            if (!isSelf)
            {
                var cap = pc;
                MakeBtn("Tog" + i, row.transform, on ? "OFF" : "ON",
                    on ? new Color(0.5f, 0.1f, 0.1f) : new Color(0.1f, 0.45f, 0.18f),
                    () => { cap.SetPowerState(!cap.isPoweredOn); RefreshBBPCList(); },
                    out RectTransform togR);
                SR(togR, new Vector2(0.82f, 0.15f), new Vector2(1f, 0.85f), new Vector2(-4f, 0f), new Vector2(-2f, 0f));
                var tt = togR.GetComponentInChildren<Text>(); if (tt) { tt.fontSize = 10; tt.raycastTarget = false; }
            }
        }
    }

    // ===================== REGULAR DESKTOP =====================

    private void BuildRegularDesktop()
    {
        // BG
        var bg = MakePanel("DeskBG", transform, new Color(0.09f, 0.11f, 0.20f));
        Stretch(bg.GetComponent<RectTransform>());

        // Taskbar (21px tall)
        var tb = MakePanel("Taskbar", transform, new Color(0.06f, 0.07f, 0.10f));
        SR(tb.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 22f), new Vector2(0f, 11f));
        var tba = MakePanel("TBAcc", tb.transform, new Color(0.25f, 0.5f, 1f, 0.4f));
        SR(tba.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 2f), new Vector2(0f, -1f));

        MakeBtn("Start", tb.transform, "⊞ Start", new Color(0.18f, 0.38f, 0.78f), ToggleStart, out RectTransform stR);
        SR(stR, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(70f, 18f), new Vector2(42f, 0f));
        var stTxt = stR.GetComponentInChildren<Text>(); if (stTxt) stTxt.fontSize = 12;

        var clkGo = MakeGO("Clock", tb.transform);
        clockText = clkGo.AddComponent<Text>();
        clockText.font = osFont; clockText.fontSize = 14; clockText.color = Color.white;
        clockText.alignment = TextAnchor.MiddleRight;
        SR(clkGo.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(85f, 18f), new Vector2(-48f, 0f));

        // Start menu
        startMenuGo = MakePanel("StartMenu", transform, new Color(0.10f, 0.12f, 0.22f));
        startMenuGo.SetActive(false);
        SR(startMenuGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(130f, 130f), new Vector2(74f, 86f));

        var smh = MakePanel("SMH", startMenuGo.transform, new Color(0.18f, 0.38f, 0.78f));
        SR(smh.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 22f), new Vector2(0f, -11f));
        var smTitle = MakeGO("SMT", smh.transform);
        var smt = smTitle.AddComponent<Text>();
        smt.font = osFont; smt.text = "roomOS v2"; smt.fontSize = 13; smt.color = Color.white;
        smt.alignment = TextAnchor.MiddleCenter;
        Stretch(smTitle.GetComponent<RectTransform>());

        MakeBtn("Shut", startMenuGo.transform, "⏻  Shutdown", new Color(0.7f, 0.18f, 0.18f), ShutdownPC, out RectTransform shutR);
        SR(shutR, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 22f), new Vector2(6f, 15f));
        var shtxt = shutR.GetComponentInChildren<Text>(); if (shtxt) shtxt.fontSize = 12;

        // App Icons — left column
        Vector2[] iconPos = {
            new Vector2(-277f, 155f), new Vector2(-277f, 95f),
            new Vector2(-277f, 35f),  new Vector2(-277f, -25f),
            new Vector2(-277f, -85f)
        };
        MakeIcon("TermIcon", bg.transform, "Terminal",  new Color(0.08f, 0.08f, 0.08f), "▶", OpenTerminal,      iconPos[0]);
        MakeIcon("NoteIcon", bg.transform, "Notepad",   new Color(0.12f, 0.5f,  0.22f), "✎", OpenNotepad,       iconPos[1]);
        MakeIcon("SnkIcon",  bg.transform, "Snake",     new Color(0.55f, 0.3f,  0.07f), "◉", OpenSnake,          iconPos[2]);
        MakeIcon("BrwIcon",  bg.transform, "Browser",   new Color(0.07f, 0.38f, 0.62f), "◈", OpenBrowser,        iconPos[3]);
        MakeIcon("NetIcon",  bg.transform, "Network",   new Color(0.38f, 0.10f, 0.55f), "⚡", OpenNetwork,       iconPos[4]);

        // Build windows
        BuildTerminal(bg.transform);
        BuildNotepad(bg.transform);
        BuildSnake(bg.transform);
        BuildBrowser(bg.transform);
        BuildNetwork(bg.transform);
    }

    private void ToggleStart() { if (startMenuGo) startMenuGo.SetActive(!startMenuGo.activeSelf); }
    private void ShutdownPC()  { if (startMenuGo) startMenuGo.SetActive(false); if (pcController) pcController.StopInteraction(); }

    private void MakeIcon(string name, Transform parent, string label, Color col, string sym, System.Action act, Vector2 pos)
    {
        var go = MakePanel(name, parent, new Color(col.r, col.g, col.b, 0.82f));
        SR(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46f, 46f), pos);
        var btn = go.AddComponent<Button>();
        var bc = btn.colors;
        bc.normalColor = new Color(col.r, col.g, col.b, 0.82f);
        bc.highlightedColor = new Color(col.r * 1.35f, col.g * 1.35f, col.b * 1.35f, 0.95f);
        bc.pressedColor = new Color(col.r * 0.65f, col.g * 0.65f, col.b * 0.65f, 0.9f);
        btn.colors = bc;
        btn.onClick.AddListener(() => act());

        var symGo = MakeGO("Sym", go.transform);
        var st = symGo.AddComponent<Text>();
        st.font = osFont; st.text = sym; st.fontSize = 22;
        st.color = new Color(1f, 1f, 1f, 0.9f); st.alignment = TextAnchor.MiddleCenter; st.raycastTarget = false;
        SR(symGo.GetComponent<RectTransform>(), new Vector2(0f, 0.4f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        var lblGo = MakeGO("Lbl", parent);
        var lt = lblGo.AddComponent<Text>();
        lt.font = osFont; lt.text = label; lt.fontSize = 11; lt.color = Color.white;
        lt.alignment = TextAnchor.UpperCenter; lt.raycastTarget = false;
        SR(lblGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(60f, 14f), pos + new Vector2(0f, -31f));
    }

    private GameObject MakeWindow(string title, Transform parent, Vector2 sz, Vector2 pos, System.Action onClose, out Transform content)
    {
        var win = MakePanel(title + "Win", parent, new Color(0.13f, 0.16f, 0.25f));
        var wr = win.GetComponent<RectTransform>();
        SR(wr, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), sz, pos);

        var hdr = MakePanel("Hdr", win.transform, new Color(0.08f, 0.10f, 0.18f));
        SR(hdr.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 22f), new Vector2(0f, -11f));

        var ha = MakePanel("HA", hdr.transform, new Color(0.25f, 0.5f, 1f, 0.4f));
        SR(ha.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f), new Vector2(0f, 1f));

        var drag = hdr.AddComponent<WindowDragHandler>(); drag.windowRect = wr;

        var ttGo = MakeGO("Title", hdr.transform);
        var tt = ttGo.AddComponent<Text>();
        tt.font = osFont; tt.text = title; tt.fontSize = 13;
        tt.color = new Color(0.5f, 0.8f, 1f); tt.alignment = TextAnchor.MiddleLeft; tt.raycastTarget = false;
        SR(ttGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(-36f, 0f), new Vector2(12f, 0f));

        MakeBtn("Close", hdr.transform, "✕", new Color(0.8f, 0.18f, 0.18f), onClose, out RectTransform cr);
        SR(cr, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(18f, 17f), new Vector2(-11f, 0f));
        var ctx = cr.GetComponentInChildren<Text>(); if (ctx) { ctx.fontSize = 11; ctx.raycastTarget = false; }

        var cgo = MakePanel("Content", win.transform, Color.clear);
        content = cgo.transform;
        SR(cgo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(0f, -11f));

        return win;
    }

    // ===================== APP BUILDERS =====================

    private void BuildTerminal(Transform parent)
    {
        terminalWin = MakeWindow("Command Terminal", parent, new Vector2(420f, 270f), new Vector2(30f, 20f),
            () => terminalWin.SetActive(false), out Transform c);
        terminalWin.SetActive(false);

        MakePanel("TBG", c, new Color(0.02f, 0.05f, 0.02f));
        Stretch(c.parent.Find("TBG")?.GetComponent<RectTransform>() ?? c.GetComponentInChildren<RectTransform>());
        // Proper BG
        var tbg = MakePanel("BG2", c, new Color(0.02f, 0.05f, 0.02f));
        Stretch(tbg.GetComponent<RectTransform>());

        var histGo = MakeGO("Hist", c);
        terminalHistoryText = histGo.AddComponent<Text>();
        terminalHistoryText.font = osFont; terminalHistoryText.fontSize = 13;
        terminalHistoryText.color = new Color(0.2f, 1f, 0.35f); terminalHistoryText.alignment = TextAnchor.LowerLeft;
        SR(histGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(-10f, -30f), new Vector2(5f, 15f));

        var prefGo = MakeGO("Pref", c);
        var pref = prefGo.AddComponent<Text>();
        pref.font = osFont; pref.text = "guest@roomOS:~$"; pref.fontSize = 13;
        pref.color = new Color(0.2f, 1f, 0.35f); pref.alignment = TextAnchor.MiddleLeft; pref.raycastTarget = false;
        SR(prefGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(110f, 20f), new Vector2(60f, 12f));

        var inpGo = MakeGO("Inp", c);
        terminalInput = inpGo.AddComponent<InputField>();
        SR(inpGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-125f, 20f), new Vector2(120f, 12f));

        var phGo = MakeGO("PH", inpGo.transform);
        var ph = phGo.AddComponent<Text>();
        ph.font = osFont; ph.fontSize = 13; ph.color = new Color(0f, 0.5f, 0f, 0.4f); ph.raycastTarget = false;
        Stretch(phGo.GetComponent<RectTransform>());

        var inpTGo = MakeGO("IT", inpGo.transform);
        var inpT = inpTGo.AddComponent<Text>();
        inpT.font = osFont; inpT.fontSize = 13; inpT.color = new Color(0.2f, 1f, 0.35f);
        inpT.alignment = TextAnchor.MiddleLeft; inpT.raycastTarget = false;
        Stretch(inpTGo.GetComponent<RectTransform>());

        terminalInput.textComponent = inpT; terminalInput.placeholder = ph;
        terminalInput.onSubmit.AddListener(OnTermCmd);
    }

    private void BuildNotepad(Transform parent)
    {
        notepadWin = MakeWindow("Notepad", parent, new Vector2(370f, 260f), new Vector2(-20f, -10f),
            () => notepadWin.SetActive(false), out Transform c);
        notepadWin.SetActive(false);

        var inpGo = MakeGO("NoteInp", c);
        notepadInput = inpGo.AddComponent<InputField>();
        notepadInput.lineType = InputField.LineType.MultiLineNewline;
        SR(inpGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(-6f, -6f), Vector2.zero);
        inpGo.AddComponent<Image>().color = new Color(0.97f, 0.97f, 0.92f);

        var tGo = MakeGO("T", inpGo.transform);
        var t = tGo.AddComponent<Text>();
        t.font = osFont; t.fontSize = 14; t.color = new Color(0.1f, 0.1f, 0.1f);
        t.alignment = TextAnchor.UpperLeft; t.raycastTarget = false;
        Stretch(tGo.GetComponent<RectTransform>());

        var phGo = MakeGO("PH", inpGo.transform);
        var ph = phGo.AddComponent<Text>();
        ph.font = osFont; ph.fontSize = 14; ph.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        ph.text = "Start typing here..."; ph.alignment = TextAnchor.UpperLeft; ph.raycastTarget = false;
        Stretch(phGo.GetComponent<RectTransform>());

        notepadInput.textComponent = t; notepadInput.placeholder = ph;
        notepadInput.text = "=== ROOM NOTES ===\n\nAdmin credentials:\nUser: admin\nPass: escape_antigravity_2026\n\nSearch 'mainframe.local' in browser.\n";
    }

    private void BuildSnake(Transform parent)
    {
        snakeWin = MakeWindow("Snake Game", parent, new Vector2(340f, 250f), new Vector2(-30f, 0f),
            () => { snakeWin.SetActive(false); snakePlaying = false; }, out Transform c);
        snakeWin.SetActive(false);

        MakePanel("SBG", c, new Color(0.03f, 0.04f, 0.03f));
        Stretch(c.parent.Find("SBG")?.GetComponent<RectTransform>() ?? c.GetComponentInChildren<RectTransform>());
        var sbg = MakePanel("SBG2", c, new Color(0.03f, 0.04f, 0.03f));
        Stretch(sbg.GetComponent<RectTransform>());

        var hintGo = MakeGO("Hint", c);
        var hint = hintGo.AddComponent<Text>();
        hint.font = osFont; hint.text = "WASD / Arrow Keys"; hint.fontSize = 12;
        hint.color = new Color(0.4f, 0.7f, 0.4f); hint.alignment = TextAnchor.MiddleCenter;
        SR(hintGo.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 14f), new Vector2(0f, -8f));

        var gridGo = MakeGO("Grid", c);
        snakeGridText = gridGo.AddComponent<Text>();
        snakeGridText.font = osFont; snakeGridText.fontSize = 14;
        snakeGridText.color = new Color(0.15f, 0.95f, 0.25f); snakeGridText.alignment = TextAnchor.MiddleCenter;
        SR(gridGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(-8f, -44f), new Vector2(0f, 14f));

        var botRow = MakePanel("Bot", c, new Color(0.05f, 0.08f, 0.05f));
        SR(botRow.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 26f), new Vector2(0f, 13f));

        var scoGo = MakeGO("Score", botRow.transform);
        snakeScoreText = scoGo.AddComponent<Text>();
        snakeScoreText.font = osFont; snakeScoreText.text = "Score: 0"; snakeScoreText.fontSize = 14;
        snakeScoreText.color = Color.yellow; snakeScoreText.alignment = TextAnchor.MiddleLeft;
        SR(scoGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.55f, 1f), Vector2.zero, new Vector2(12f, 0f));

        MakeBtn("Play", botRow.transform, "▶ Play", new Color(0.1f, 0.5f, 0.15f), StartSnakeGame, out RectTransform playR);
        SR(playR, new Vector2(0.55f, 0.1f), new Vector2(1f, 0.9f), new Vector2(-8f, 0f), new Vector2(-4f, 0f));
        var ptxt = playR.GetComponentInChildren<Text>(); if (ptxt) { ptxt.fontSize = 13; ptxt.raycastTarget = false; }
    }

    private void BuildBrowser(Transform parent)
    {
        browserWin = MakeWindow("Web Browser", parent, new Vector2(420f, 280f), new Vector2(0f, 10f),
            () => browserWin.SetActive(false), out Transform c);
        browserWin.SetActive(false);

        var topBar = MakePanel("TopBar", c, new Color(0.10f, 0.13f, 0.22f));
        SR(topBar.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 26f), new Vector2(0f, -13f));

        MakeBtn("Home", topBar.transform, "⌂ Home", new Color(0.18f, 0.28f, 0.48f), () => LoadUrl("goggle.com"), out RectTransform homeR);
        SR(homeR, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(54f, 20f), new Vector2(34f, 0f));
        var hbt = homeR.GetComponentInChildren<Text>(); if (hbt) { hbt.fontSize = 11; hbt.raycastTarget = false; }

        var urlGo = MakeGO("UrlInp", topBar.transform);
        browserUrlInput = urlGo.AddComponent<InputField>();
        SR(urlGo.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-124f, 20f), new Vector2(110f, 0f));
        urlGo.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.13f);

        var utGo = MakeGO("T", urlGo.transform);
        var ut = utGo.AddComponent<Text>();
        ut.font = osFont; ut.fontSize = 12; ut.color = Color.white;
        ut.alignment = TextAnchor.MiddleLeft; ut.raycastTarget = false;
        Stretch(utGo.GetComponent<RectTransform>());

        var uphGo = MakeGO("PH", urlGo.transform);
        var uph = uphGo.AddComponent<Text>();
        uph.font = osFont; uph.text = "Enter URL..."; uph.fontSize = 12;
        uph.color = new Color(0.5f, 0.5f, 0.7f, 0.5f); uph.alignment = TextAnchor.MiddleLeft; uph.raycastTarget = false;
        Stretch(uphGo.GetComponent<RectTransform>());

        browserUrlInput.textComponent = ut; browserUrlInput.placeholder = uph;
        browserUrlInput.onSubmit.AddListener(LoadUrl);

        MakeBtn("Go", topBar.transform, "Go", new Color(0.18f, 0.38f, 0.7f), () => LoadUrl(browserUrlInput.text), out RectTransform goR);
        SR(goR, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(36f, 20f), new Vector2(-22f, 0f));
        var gotxt = goR.GetComponentInChildren<Text>(); if (gotxt) { gotxt.fontSize = 11; gotxt.raycastTarget = false; }

        var disp = MakePanel("Disp", c, Color.white);
        browserContentPanel = disp.transform;
        SR(disp.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -26f), new Vector2(0f, -13f));

        LoadUrl("goggle.com");
    }

    // ===================== OPEN ACTIONS =====================

    private void OpenTerminal() { if (!terminalWin) return; terminalWin.SetActive(true); terminalWin.transform.SetAsLastSibling(); terminalInput?.ActivateInputField(); }
    private void OpenNotepad()  { if (!notepadWin)  return; notepadWin.SetActive(true);  notepadWin.transform.SetAsLastSibling();  notepadInput?.ActivateInputField(); }
    private void OpenSnake()    { if (!snakeWin)    return; snakeWin.SetActive(true);    snakeWin.transform.SetAsLastSibling();    StartSnakeGame(); }
    private void OpenBrowser()  { if (!browserWin)  return; browserWin.SetActive(true);  browserWin.transform.SetAsLastSibling();  LoadUrl("goggle.com"); }
    private void OpenNetwork()  { if (!networkWin)  return; networkWin.SetActive(true);  networkWin.transform.SetAsLastSibling();  RefreshNetworkList(); }

    // ===================== TERMINAL LOGIC =====================

    private void OnTermCmd(string val)
    {
        if (string.IsNullOrEmpty(val.Trim())) { terminalInput?.ActivateInputField(); return; }
        terminalLog += "\nguest@roomOS:~$ " + val + "\n";
        string cmd = val.Trim().ToLower();
        string[] tok = cmd.Split(' ');

        switch (tok[0])
        {
            case "help": terminalLog += "Commands: help  ls  cat [f]  ping [ip]  hack  whoami  date  ifconfig  pwd  history  clear\n"; break;
            case "ls": terminalLog += "credentials.txt  note.txt  secret_project.bin  .bash_history  system.log\n"; break;
            case "pwd": terminalLog += "/home/guest\n"; break;
            case "whoami": terminalLog += "guest  (uid=1001, groups=guest,users)\n"; break;
            case "date": terminalLog += System.DateTime.Now.ToString("ddd MMM dd HH:mm:ss UTC yyyy") + "\n"; break;
            case "ifconfig":
                terminalLog += "eth0: inet 192.168.1.42  netmask 255.255.255.0\nlo:   inet 127.0.0.1\n"; break;
            case "history":
                terminalLog += "  1  ls\n  2  cat credentials.txt\n  3  ping 192.168.1.1\n  4  hack\n  5  clear\n"; break;
            case "clear": terminalLog = ""; break;
            case "cat":
                if (tok.Length < 2) { terminalLog += "Usage: cat [file]\n"; break; }
                switch (tok[1])
                {
                    case "note.txt": terminalLog += "LOG #411:\n Cabinet code on upper doors.\n Keypad on bottom level.\n"; break;
                    case "credentials.txt": terminalLog += "Username: admin\nPassword: escape_antigravity_2026\n"; break;
                    case "system.log": terminalLog += "[INFO] Boot OK.\n[WARN] Port 8080 unusual activity.\n[CRIT] Breach attempt blocked 03:17:22.\n"; break;
                    case ".bash_history": terminalLog += "ssh admin@mainframe.local\ncat /etc/passwd\nhack\n"; break;
                    case "secret_project.bin": terminalLog += "Error: Binary. Use 'hack' to extract.\n"; break;
                    default: terminalLog += "cat: " + tok[1] + ": No such file\n"; break;
                }
                break;
            case "ping":
                terminalLog += tok.Length < 2 ? "Usage: ping [ip]\n" :
                    $"PING {tok[1]}: 64 bytes, ttl=64, time=1.12ms\n64 bytes, time=0.98ms\n2 transmitted, 0% loss\n"; break;
            case "hack": StartCoroutine(HackRoutine()); break;
            default: terminalLog += tok[0] + ": not found. Type 'help'.\n"; break;
        }

        var lines = terminalLog.Split('\n');
        if (lines.Length > 16) terminalLog = string.Join("\n", lines, lines.Length - 16, 16);
        terminalHistoryText.text = terminalLog;
        terminalInput.text = "";
        terminalInput.ActivateInputField();
    }

    private IEnumerator HackRoutine()
    {
        terminalInput.interactable = false;
        string[] steps = {
            "[...] Connecting to mainframe...",
            "[...] Located core at 0x7F9B2A0",
            "[...] Injecting payload... OK",
            "[...] Bypassing firewall... OK",
            "[!!!] ACCESS GRANTED!\n    admin / escape_antigravity_2026"
        };
        foreach (var s in steps) { terminalLog += s + "\n"; terminalHistoryText.text = terminalLog; yield return new WaitForSeconds(0.6f); }
        terminalInput.interactable = true; terminalInput.ActivateInputField();
    }

    // ===================== SNAKE =====================

    private void StartSnakeGame()
    {
        snakePlaying = true; snakeScore = 0; snakeScoreText.text = "Score: 0";
        snakeDir = Vector2Int.right;
        snakeHead = new Vector2Int(SW / 2, SH / 2);
        snakeBody.Clear();
        snakeBody.Add(snakeHead);
        snakeBody.Add(snakeHead + Vector2Int.left);
        snakeBody.Add(snakeHead + Vector2Int.left * 2);
        SpawnFood(); RenderSnake();
    }

    private void SpawnFood()
    {
        for (int i = 0; i < 200; i++)
        {
            var p = new Vector2Int(UnityEngine.Random.Range(1, SW - 1), UnityEngine.Random.Range(1, SH - 1));
            if (!snakeBody.Contains(p)) { snakeFood = p; return; }
        }
        snakeFood = new Vector2Int(1, 1);
    }

    private void RenderSnake()
    {
        var sb = new System.Text.StringBuilder();
        for (int y = 0; y < SH; y++)
        {
            for (int x = 0; x < SW; x++)
            {
                var p = new Vector2Int(x, y);
                if (x == 0 || x == SW - 1 || y == 0 || y == SH - 1) sb.Append('#');
                else if (p == snakeHead) sb.Append('O');
                else if (snakeBody.Contains(p)) sb.Append('o');
                else if (p == snakeFood) sb.Append('*');
                else sb.Append(' ');
            }
            sb.Append('\n');
        }
        snakeGridText.text = sb.ToString();
    }

    private void MoveSnake()
    {
        var nh = snakeHead + snakeDir;
        if (nh.x <= 0 || nh.x >= SW - 1 || nh.y <= 0 || nh.y >= SH - 1 || snakeBody.Contains(nh))
        {
            snakePlaying = false;
            snakeGridText.text = "\n  GAME OVER!\n  Score: " + snakeScore + "\n  Press ▶ Play to retry.";
            return;
        }
        snakeHead = nh; snakeBody.Insert(0, snakeHead);
        if (snakeHead == snakeFood) { snakeScore += 10; snakeScoreText.text = "Score: " + snakeScore; SpawnFood(); }
        else snakeBody.RemoveAt(snakeBody.Count - 1);
        RenderSnake();
    }

    // ===================== BROWSER =====================

    private void LoadUrl(string url)
    {
        url = url.Trim().ToLower();
        if (browserUrlInput) browserUrlInput.text = url;
        foreach (Transform ch in browserContentPanel) Destroy(ch.gameObject);

        if (string.IsNullOrEmpty(url) || url == "goggle.com") RenderGoggle();
        else if (url == "mainframe.local") RenderMainframe();
        else if (url.Contains("goggle.com/search") || url.Contains("search")) RenderSearchResults();
        else if (url == "chalkboard.news") RenderArticle("Chalkboard News", "Chalkboard updated to 4:1 aspect ratio.\nFixed flush at X=-1.9353.");
        else if (url == "hinge.wiki") RenderArticle("Cabinet Hinges Wiki", "Cabinet doors use hinge joints.\nPivot anchor offset to back corner to prevent clipping.");
        else RenderArticle("404 Not Found", "URL '" + url + "' not found.\n\nTry:\n  goggle.com\n  mainframe.local\n  chalkboard.news");
    }

    private Text BrwTxt(string name, float ancY, string txt, int sz, Color col, bool bold = false)
    {
        var go = MakeGO(name, browserContentPanel);
        var t = go.AddComponent<Text>();
        t.font = osFont; t.text = txt; t.fontSize = sz; t.color = col;
        t.alignment = TextAnchor.MiddleCenter;
        if (bold) t.fontStyle = FontStyle.Bold;
        SR(go.GetComponent<RectTransform>(), new Vector2(0.1f, ancY), new Vector2(0.9f, ancY), new Vector2(0f, sz + 6f), Vector2.zero);
        return t;
    }

    private void RenderGoggle()
    {
        BrwTxt("Logo", 0.78f, "Goggle", 36, new Color(0.15f, 0.35f, 0.75f), true);
        BrwTxt("Tag", 0.65f, "Search the web or navigate directly", 12, new Color(0.4f, 0.4f, 0.5f));

        var sinpGo = MakeGO("SI", browserContentPanel);
        var sinp = sinpGo.AddComponent<InputField>();
        SR(sinpGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), new Vector2(260f, 26f), Vector2.zero);
        sinpGo.AddComponent<Image>().color = new Color(0.9f, 0.91f, 0.95f);
        var st = MakeGO("ST", sinpGo.transform).AddComponent<Text>();
        st.font = osFont; st.fontSize = 13; st.color = Color.black; st.alignment = TextAnchor.MiddleLeft; st.raycastTarget = false;
        Stretch(st.GetComponent<RectTransform>());
        sinp.textComponent = st;
        sinp.onSubmit.AddListener((_) => LoadUrl("goggle.com/search"));

        MakeBtn("SBtn", browserContentPanel, "🔍 Search", new Color(0.18f, 0.38f, 0.7f), () => LoadUrl("goggle.com/search"), out RectTransform sbr);
        SR(sbr, new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.38f), new Vector2(130f, 24f), Vector2.zero);
        var sbt = sbr.GetComponentInChildren<Text>(); if (sbt) { sbt.fontSize = 12; sbt.raycastTarget = false; }

        string[] ql = { "mainframe.local", "chalkboard.news", "hinge.wiki" };
        for (int i = 0; i < ql.Length; i++)
        {
            string cap = ql[i]; float y = 0.24f - i * 0.1f;
            MakeBtn("QL" + i, browserContentPanel, ql[i], new Color(0.88f, 0.9f, 0.95f), () => LoadUrl(cap), out RectTransform qr);
            SR(qr, new Vector2(0.5f, y), new Vector2(0.5f, y), new Vector2(170f, 22f), Vector2.zero);
            var qt = qr.GetComponentInChildren<Text>(); if (qt) { qt.color = new Color(0.1f, 0.3f, 0.8f); qt.fontSize = 12; qt.raycastTarget = false; }
        }
    }

    private void RenderSearchResults()
    {
        BrwTxt("Hdr", 0.87f, "Search Results", 18, new Color(0.1f, 0.1f, 0.15f), true);

        (string title, string desc, string href)[] res = {
            ("Mainframe Portal — mainframe.local", "Admin login to facility mainframe.", "mainframe.local"),
            ("Chalkboard News — chalkboard.news", "Chalkboard aspect ratio fix info.", "chalkboard.news"),
            ("Cabinet Hinges Wiki — hinge.wiki", "Physics-based hinge documentation.", "hinge.wiki"),
        };
        for (int i = 0; i < res.Length; i++)
        {
            float y = 0.68f - i * 0.22f; string cap = res[i].href;
            MakeBtn("R" + i, browserContentPanel, res[i].title, new Color(0.92f, 0.93f, 0.97f), () => LoadUrl(cap), out RectTransform rr);
            SR(rr, new Vector2(0.1f, y), new Vector2(0.9f, y), new Vector2(0f, 22f), Vector2.zero);
            var rt = rr.GetComponentInChildren<Text>(); if (rt) { rt.color = new Color(0.1f, 0.2f, 0.8f); rt.fontSize = 13; rt.alignment = TextAnchor.MiddleLeft; rt.raycastTarget = false; }
            BrwTxt("D" + i, y - 0.1f, res[i].desc, 11, new Color(0.3f, 0.3f, 0.35f));
        }
    }

    private void RenderMainframe()
    {
        var mfh = MakePanel("MFH", browserContentPanel, new Color(0.08f, 0.08f, 0.12f));
        SR(mfh.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 38f), new Vector2(0f, -19f));
        BrwTxt("MFT", 0f, "MAINFRAME SECURITY LOGIN PORTAL", 14, Color.red, true);
        // Re-parent to mfh
        var mftgo = browserContentPanel.GetChild(browserContentPanel.childCount - 1);
        mftgo.SetParent(mfh.transform, false);
        Stretch(mftgo.GetComponent<RectTransform>());

        var respGo = MakeGO("Resp", browserContentPanel);
        var resp = respGo.AddComponent<Text>();
        resp.font = osFont; resp.fontSize = 13; resp.alignment = TextAnchor.MiddleCenter;
        SR(respGo.GetComponent<RectTransform>(), new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.18f), new Vector2(0f, 24f), Vector2.zero);

        MakeBrowserField("Username", 0.65f, out InputField uInp);
        MakeBrowserField("Password", 0.48f, out InputField pInp);
        pInp.inputType = InputField.InputType.Password; pInp.asteriskChar = '●';

        MakeBtn("Sub", browserContentPanel, "ACCESS PORTAL", new Color(0.1f, 0.1f, 0.15f), () => {
            if (uInp.text.Trim().ToLower() == "admin" && pInp.text.Trim() == "escape_antigravity_2026")
            { resp.color = new Color(0f, 0.6f, 0f); resp.text = "✓ ACCESS GRANTED — Mains unlocked."; }
            else { resp.color = Color.red; resp.text = "✗ ACCESS DENIED — Attempt logged."; }
        }, out RectTransform subR);
        SR(subR, new Vector2(0.5f, 0.31f), new Vector2(0.5f, 0.31f), new Vector2(140f, 26f), Vector2.zero);
        var stxt = subR.GetComponentInChildren<Text>(); if (stxt) { stxt.fontSize = 12; stxt.raycastTarget = false; }
    }

    private void MakeBrowserField(string lbl, float ancY, out InputField field)
    {
        var lgo = MakeGO(lbl + "L", browserContentPanel);
        var lt = lgo.AddComponent<Text>();
        lt.font = osFont; lt.text = lbl + ":"; lt.fontSize = 13; lt.color = new Color(0.1f, 0.1f, 0.15f);
        lt.alignment = TextAnchor.MiddleRight;
        SR(lgo.GetComponent<RectTransform>(), new Vector2(0.15f, ancY), new Vector2(0.38f, ancY), new Vector2(0f, 24f), Vector2.zero);

        var igo = MakeGO(lbl + "I", browserContentPanel);
        field = igo.AddComponent<InputField>();
        igo.AddComponent<Image>().color = new Color(0.88f, 0.9f, 0.9f);
        SR(igo.GetComponent<RectTransform>(), new Vector2(0.40f, ancY), new Vector2(0.82f, ancY), new Vector2(0f, 24f), Vector2.zero);

        var tgo = MakeGO("T", igo.transform);
        var t = tgo.AddComponent<Text>();
        t.font = osFont; t.fontSize = 13; t.color = Color.black;
        t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
        Stretch(tgo.GetComponent<RectTransform>());
        field.textComponent = t;
    }

    private void RenderArticle(string title, string body)
    {
        BrwTxt("ATitle", 0.84f, title, 18, new Color(0.1f, 0.1f, 0.15f), true);
        var div = MakePanel("Div", browserContentPanel, new Color(0.7f, 0.7f, 0.75f));
        SR(div.GetComponent<RectTransform>(), new Vector2(0.05f, 0.73f), new Vector2(0.95f, 0.73f), new Vector2(0f, 1f), Vector2.zero);
        var bGo = MakeGO("ABody", browserContentPanel);
        var bt = bGo.AddComponent<Text>();
        bt.font = osFont; bt.text = body; bt.fontSize = 13; bt.color = new Color(0.15f, 0.15f, 0.18f);
        bt.alignment = TextAnchor.UpperLeft;
        SR(bGo.GetComponent<RectTransform>(), new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.7f), Vector2.zero, Vector2.zero);
    }

    // ===================== NETWORK CONNECTIONS =====================

    private void BuildNetwork(Transform parent)
    {
        networkWin = MakeWindow("Network Connections", parent, new Vector2(380f, 250f), new Vector2(10f, -20f),
            () => networkWin.SetActive(false), out Transform c);
        networkWin.SetActive(false);

        // Dark slate-grey premium BG with a touch of purple/indigo to match the icon
        var nbg = MakePanel("NBG", c, new Color(0.06f, 0.07f, 0.12f));
        Stretch(nbg.GetComponent<RectTransform>());

        // Active nodes label
        var infoGo = MakeGO("Info", c);
        var info = infoGo.AddComponent<Text>();
        info.font = osFont; info.text = "⚡  SCANNING LOCAL NETWORK NODES..."; info.fontSize = 11;
        info.color = new Color(0.4f, 0.85f, 1f); info.alignment = TextAnchor.MiddleLeft;
        SR(infoGo.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-24f, 16f), new Vector2(12f, -12f));

        // Glassy separator
        var sep = MakePanel("Sep", c, new Color(0.2f, 0.4f, 0.8f, 0.25f));
        SR(sep.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-24f, 1f), new Vector2(12f, -24f));

        // List container
        var listGo = MakePanel("ListParent", c, Color.clear);
        SR(listGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(-24f, -62f), new Vector2(12f, -20f));
        networkListParent = listGo.transform;

        // Glassy footer / bottom bar
        var botBar = MakePanel("BotBar", c, new Color(0.04f, 0.05f, 0.08f));
        SR(botBar.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 28f), new Vector2(0f, 14f));
        var botBarSep = MakePanel("BotBarSep", botBar.transform, new Color(0.2f, 0.4f, 0.8f, 0.25f));
        SR(botBarSep.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), Vector2.zero);

        // Refresh button
        MakeBtn("RefreshBtn", botBar.transform, "↻  Refresh Network", new Color(0.12f, 0.25f, 0.5f), RefreshNetworkList, out RectTransform refR);
        SR(refR, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(140f, 18f), Vector2.zero);
        var rtxt = refR.GetComponentInChildren<Text>(); if (rtxt) { rtxt.fontSize = 11; rtxt.raycastTarget = false; }
    }

    private void RefreshNetworkList()
    {
        if (networkListParent == null) return;
        foreach (Transform ch in networkListParent) Destroy(ch.gameObject);

        var pcs = GameObject.FindObjectsByType<InteractablePC>(FindObjectsSortMode.None);
        System.Array.Sort(pcs, (a, b) => string.Compare(a.name, b.name));

        float rowH = 22f;
        float areaH = networkListParent.GetComponent<RectTransform>()?.rect.height ?? 150f;
        float startY = areaH * 0.5f - rowH * 0.5f;

        for (int i = 0; i < pcs.Length; i++)
        {
            var pc = pcs[i];
            bool isSelf = (pcController != null && pc == pcController);
            bool on = pc.isPoweredOn;
            float y = startY - i * (rowH + 2f);

            // Sleek translucent background colors
            Color bg = isSelf ? new Color(0.15f, 0.25f, 0.45f, 0.85f) :
                (on ? new Color(0.08f, 0.24f, 0.14f, 0.75f) : new Color(0.24f, 0.08f, 0.08f, 0.75f));

            var row = MakePanel("Row" + i, networkListParent, bg);
            SR(row.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, rowH), new Vector2(0f, y));

            // Small accent border on the left of each row
            var leftBorder = MakePanel("Border", row.transform, isSelf ? new Color(0.4f, 0.7f, 1f, 0.8f) : (on ? new Color(0f, 1f, 0.5f, 0.8f) : new Color(1f, 0.3f, 0.3f, 0.8f)));
            SR(leftBorder.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(3f, 0f), new Vector2(1.5f, 0f));

            // Status indicator dot
            var dot = MakePanel("Dot", row.transform, on ? new Color(0.1f, 0.9f, 0.4f) : new Color(0.9f, 0.2f, 0.2f));
            SR(dot.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 6f), new Vector2(12f, 0f));

            // Name label
            var nmGo = MakeGO("N", row.transform);
            var nm = nmGo.AddComponent<Text>();
            nm.font = osFont; nm.text = pc.name + (isSelf ? " (LOCAL PC)" : ""); nm.fontSize = 11;
            nm.color = Color.white; nm.alignment = TextAnchor.MiddleLeft;
            SR(nmGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(24f, 0f));

            // Fake IP address based on pc index
            var ipGo = MakeGO("IP", row.transform);
            var ip = ipGo.AddComponent<Text>();
            ip.font = osFont;
            string fakeIp = "192.168.1." + (100 + i);
            ip.text = fakeIp; ip.fontSize = 10;
            ip.color = new Color(0.6f, 0.65f, 0.8f); ip.alignment = TextAnchor.MiddleCenter;
            SR(ipGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.78f, 1f), Vector2.zero, Vector2.zero);

            // Status text label
            var stGo = MakeGO("S", row.transform);
            var st = stGo.AddComponent<Text>();
            st.font = osFont; st.text = on ? "CONNECTED" : "OFFLINE"; st.fontSize = 10;
            st.color = on ? new Color(0.2f, 0.9f, 0.5f) : new Color(0.9f, 0.4f, 0.4f);
            st.alignment = TextAnchor.MiddleCenter;
            SR(stGo.GetComponent<RectTransform>(), new Vector2(0.78f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        }
    }

    // ===================== POWER =====================

    public void UpdatePowerOverlay()
    {
        if (powerOffOverlay && pcController)
        {
            powerOffOverlay.SetActive(!pcController.isPoweredOn);
            if (powerOffOverlay.activeSelf) snakePlaying = false;
        }
    }

    private void PowerOnAllPCs()
    {
        foreach (var pc in GameObject.FindObjectsByType<InteractablePC>(FindObjectsSortMode.None))
            pc.SetPowerState(true);
        RefreshBBPCList();
    }

    private void PowerOffAllPCs()
    {
        foreach (var pc in GameObject.FindObjectsByType<InteractablePC>(FindObjectsSortMode.None))
            if (pc.name != "Screen (2)" && pc.name != "Screen (3)") pc.SetPowerState(false);
        RefreshBBPCList();
    }

    // ===================== UPDATE =====================

    void Update()
    {
        if (powerOffOverlay && pcController && powerOffOverlay.activeSelf == pcController.isPoweredOn)
            UpdatePowerOverlay();

        if (clockText) clockText.text = System.DateTime.Now.ToString("HH:mm:ss");

        if (isBigBrother && bbPCStatusParent)
        {
            bbRefreshTimer += Time.deltaTime;
            if (bbRefreshTimer >= 0.5f) { bbRefreshTimer = 0f; RefreshBBPCList(); }
        }

        if (networkWin && networkWin.activeSelf && networkListParent)
        {
            networkRefreshTimer += Time.deltaTime;
            if (networkRefreshTimer >= 1.0f)
            {
                networkRefreshTimer = 0f;
                RefreshNetworkList();
            }
        }

        if (snakePlaying && snakeWin && snakeWin.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))    { if (snakeDir != Vector2Int.down)  snakeDir = Vector2Int.up; }
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))  { if (snakeDir != Vector2Int.up)    snakeDir = Vector2Int.down; }
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))  { if (snakeDir != Vector2Int.right) snakeDir = Vector2Int.left; }
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) { if (snakeDir != Vector2Int.left)  snakeDir = Vector2Int.right; }
            snakeTimer += Time.deltaTime;
            if (snakeTimer >= snakeTickRate) { snakeTimer = 0f; MoveSnake(); }
        }
    }
}
// Trigger recompile v6
