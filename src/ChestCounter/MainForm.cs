using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

/// <summary>
/// Ventana principal, horizontal 16:9 (592×332): total y partida actual, última partida, gráfico de 7 días,
/// estadísticas en tarjetas y los botones de acción.
/// Todo lo demás (escaneo y overlay) lo hace <see cref="Monitor"/> en segundo plano.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class MainForm : Form
{
    private readonly Monitor _monitor;
    private readonly Store _store;
    private readonly Card _badgeCard;
    private readonly Label _badge;
    private readonly Label _profileName;
    private readonly ComboBox _profile;
    private readonly Label _total;
    private readonly Label _totalCap;
    private readonly Label _current;
    private readonly Label _currentCap;
    private readonly Label _session;
    private readonly Label _statToday, _statBestDay, _statAverage, _statBestMatch, _statMatches, _statTreasures;
    private readonly Label _statTodayCap, _statBestDayCap, _statAverageCap, _statBestMatchCap, _statMatchesCap, _statTreasuresCap;
    private readonly Card _noticeCard;
    private readonly Label _notice;
    private readonly PillButton _overlayButton, _reportButton, _settingsButton;
    private readonly DailyChart _daily;
    private readonly Label _lastValue, _lastDetail;
    private MonitorState? _last;
    private string? _profileSig;
    private bool _ignoreProfileChange;
    private bool _autoCloseOffer;

    // Disposición: 592×332 (16:9). Columna izquierda (total, última partida, gráfico) y derecha (estadísticas y botones).
    private const int LeftX = 15, LeftW = 267, RightX = 297, RightW = 278;

    public MainForm(Monitor monitor, Store store)
    {
        _monitor = monitor;
        _store = store;

        Text = L.T("Contador de cofres", "Chest Counter", "상자 카운터") + " v" + AppInfo.VersionText;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = Theme.Text();
        BackColor = Theme.Bg;
        ForeColor = Theme.Ink;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(420, 236);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(592, 332);
        Icon = AppIcon.Load();

        // Cabecera: título, perfil y distintivo.
        Lbl(this, L.T("CONTADOR DE COFRES", "CHEST COUNTER", "상자 카운터"), 8.5f, FontStyle.Bold, Theme.Cyan, new Point(LeftX, 14), new Size(202, 18));
        _badgeCard = new Card { Location = new Point(484, 9), Size = new Size(96, 22), Radius = 11, BackColor = Theme.Panel, Visible = false };
        _badge = Lbl(_badgeCard, "", 8.0f, FontStyle.Bold, Theme.Mint, new Point(0, 0), new Size(96, 22), ContentAlignment.MiddleCenter);
        Controls.Add(_badgeCard);

        _profileName = Lbl(this, "", 8.0f, FontStyle.Regular, Theme.Muted, new Point(230, 11), new Size(240, 18), ContentAlignment.TopRight);
        _profile = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Panel,
            ForeColor = Theme.Ink,
            Font = Theme.Text(),
            Location = new Point(306, 8),
            Size = new Size(164, 24),
            Visible = false,
        };
        _profile.SelectedIndexChanged += (_, _) =>
        {
            if (!_ignoreProfileChange && _profile.SelectedItem is ProfileItem item)
            {
                _monitor.SetProfile(item.Key);
            }
        };
        Controls.Add(_profile);

        // Tarjeta principal: total y partida actual.
        var hero = new Card { Location = new Point(LeftX, 40), Size = new Size(LeftW, 100), Radius = 16 };
        Controls.Add(hero);
        _total = Lbl(hero, "0", 28f, FontStyle.Bold, Theme.Cyan, new Point(15, 6), new Size(120, 50));
        _totalCap = Lbl(hero, L.T("cofres del equipo", "team chests", "팀 상자"), 8.0f, FontStyle.Regular, Theme.Muted, new Point(16, 59), new Size(120, 16));
        hero.Controls.Add(new Panel { BackColor = Theme.CardEdge, Location = new Point(138, 15), Size = new Size(1, 70) });
        _current = Lbl(hero, "—", 28f, FontStyle.Bold, Theme.Amber, new Point(151, 6), new Size(110, 50));
        _currentCap = Lbl(hero, L.T("última partida", "last match", "최근 경기"), 8.0f, FontStyle.Regular, Theme.Muted, new Point(153, 59), new Size(110, 16));
        _session = Lbl(hero, "", 8.0f, FontStyle.Regular, Theme.Ink, new Point(15, 77), new Size(243, 16));

        // Última partida: cofres abiertos, con los detalles (rotos, tesoros, puesto, eliminaciones).
        var last = new Card { Location = new Point(LeftX, 149), Size = new Size(LeftW, 50), Radius = 12 };
        Lbl(last, L.T("Última partida", "Last match", "최근 경기"), 7.5f, FontStyle.Regular, Theme.Muted, new Point(13, 6), new Size(101, 13));
        _lastDetail = Lbl(last, "", 7.5f, FontStyle.Regular, Theme.Muted, new Point(101, 5), new Size(153, 15), ContentAlignment.MiddleRight);
        _lastValue = Lbl(last, "—", 11.5f, FontStyle.Bold, Theme.Amber, new Point(13, 22), new Size(243, 22));
        Controls.Add(last);

        // Gráfico de los últimos 7 días.
        var daily = new Card { Location = new Point(LeftX, 206), Size = new Size(LeftW, 120), Radius = 14 };
        Lbl(daily, L.T("Últimos 7 días", "Last 7 days", "최근 7일"), 8.0f, FontStyle.Regular, Theme.Muted, new Point(13, 7), new Size(184, 13));
        _daily = new DailyChart { Location = new Point(9, 24), Size = new Size(246, 90), BackColor = Theme.Card };
        daily.Controls.Add(_daily);
        Controls.Add(daily);

        // Rejilla de estadísticas: dos columnas, tres filas (columna derecha).
        _statToday = Stat(L.T("Hoy", "Today", "오늘"), RightX, 40, Theme.Cyan, out _statTodayCap);
        _statBestDay = Stat(L.T("Mejor día", "Best day", "최고 기록일"), RightX + 142, 40, Theme.Amber, out _statBestDayCap);
        _statAverage = Stat(L.T("Media por partida", "Average per match", "경기당 평균"), RightX, 117, Theme.Ink, out _statAverageCap);
        _statBestMatch = Stat(L.T("Mejor partida", "Best match", "최고 기록 경기"), RightX + 142, 117, Theme.Ink, out _statBestMatchCap);
        _statMatches = Stat(L.T("Histórico", "History", "전체 경기"), RightX, 196, Theme.Ink, out _statMatchesCap);
        _statTreasures = Stat(L.T("Tesoros", "Treasures", "보물"), RightX + 142, 196, Theme.Ink, out _statTreasuresCap);

        // Aviso de cierre automático: ocupa el sitio de los botones mientras está visible.
        _noticeCard = new Card { Location = new Point(RightX, 277), Size = new Size(RightW, 40), Radius = 12, BackColor = Theme.Card, Visible = false };
        _notice = Lbl(_noticeCard, "", 8.5f, FontStyle.Regular, Theme.AccentSoft, new Point(13, 0), new Size(RightW - 26, 40), ContentAlignment.MiddleLeft);
        _noticeCard.Click += (_, _) => { if (_autoCloseOffer) _monitor.KeepOpen(); };
        _notice.Click += (_, _) => { if (_autoCloseOffer) _monitor.KeepOpen(); };
        Controls.Add(_noticeCard);

        // Botones de acción.
        _overlayButton = Action(L.T("Overlay", "Overlay", "오버레이"), RightX);
        _overlayButton.Click += (_, _) => _monitor.ToggleOverlay();
        _reportButton = Action(L.T("Informe", "Report", "리포트"), RightX + 95);
        _reportButton.Click += (_, _) => Open(_store.ReportPath);
        _settingsButton = Action(L.T("Ajustes", "Settings", "설정"), RightX + 190);
        _settingsButton.Click += (_, _) =>
        {
            using var dialog = new SettingsForm(_monitor, _store);
            dialog.ShowDialog(this);
        };

        // Diseño base (592×332): al redimensionar o maximizar, todos los controles y fuentes escalan proporcionalmente.
        CaptureLayout();
        _scaleTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _scaleTimer.Tick += (_, _) => { _scaleTimer.Stop(); ApplyLayoutScale(); };

        monitor.Changed += OnChanged;
        Shown += (_, _) =>
        {
            if (_monitor.AutoMode)
            {
                WindowState = FormWindowState.Minimized; // abierto con Fortnite: el juego conserva la pantalla
            }
        };
    }

    private const int DesignW = 592, DesignH = 332;
    private readonly Dictionary<Control, LayoutEntry> _layout = new();
    private System.Windows.Forms.Timer? _scaleTimer;
    private float _lastFx = 1, _lastFy = 1;

    private sealed class LayoutEntry
    {
        public Point Location;
        public Size Size;
        public Font? Font;
        public Font? Scaled;
    }

    private void CaptureLayout()
    {
        Capture(this);
        void Capture(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                _layout[c] = new LayoutEntry { Location = c.Location, Size = c.Size, Font = c.Font };
                Capture(c);
            }
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            return;
        }
        _scaleTimer?.Stop();
        _scaleTimer?.Start();
    }

    /// <summary>Escala posiciones, tamaños y fuentes desde el diseño base. Las fuentes van en puntos: se dividen por el DPI para no contarlas dos veces.</summary>
    private void ApplyLayoutScale()
    {
        if (_layout.Count == 0 || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }
        var fx = ClientSize.Width / (float)DesignW;
        var fy = ClientSize.Height / (float)DesignH;
        if (Math.Abs(fx - _lastFx) < 0.005 && Math.Abs(fy - _lastFy) < 0.005)
        {
            return;
        }
        _lastFx = fx;
        _lastFy = fy;
        // Durante restaurar/maximizar el tamaño puede ser transitorio: sin factor válido no se tocan las fuentes.
        var k = Math.Min(fx / (DeviceDpi / 96f), fy / (DeviceDpi / 96f));
        var scaleFonts = float.IsFinite(k) && k > 0.2 && k < 10;
        foreach (var (c, l) in _layout)
        {
            c.Location = new Point((int)Math.Round(l.Location.X * fx), (int)Math.Round(l.Location.Y * fy));
            c.Size = new Size((int)Math.Round(l.Size.Width * fx), (int)Math.Round(l.Size.Height * fy));
            if (scaleFonts && l.Font != null)
            {
                var nf = new Font(l.Font.FontFamily, l.Font.SizeInPoints * k, l.Font.Style);
                l.Scaled?.Dispose();
                l.Scaled = nf;
                c.Font = nf;
            }
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.Chrome(Handle); // esquinas redondeadas de Windows 11 (el sistema las dibuja sin tocar botones ni bordes)
        if (_last != null)
        {
            Render(_last);
        }
    }

    private void OnChanged(MonitorState state)
    {
        _last = state;
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }
        try
        {
            BeginInvoke(new Action(() => Render(state)));
        }
        catch (ObjectDisposedException)
        {
            // la ventana se está cerrando
        }
        catch (InvalidOperationException)
        {
            // todavía sin ventana
        }
    }

    private void Render(MonitorState s)
    {
        var v = s.View;
        _total.Text = v.Total;
        _current.Text = v.Current ?? "—";
        _current.ForeColor = v.CurrentLive ? Theme.Mint : v.Current == null ? Theme.Muted : Theme.Amber;
        // El overlay usa textos cortos ("anterior" / "actual"); aquí hay sitio para los largos.
        _currentCap.Text = v.CurrentCaption == OverlayModel.LastMatchCaption ? L.T("última partida", "last match", "최근 경기")
            : v.CurrentLive && v.Current != "…" ? L.T("partida actual", "current match", "현재 경기")
            : v.CurrentCaption;
        _badge.Text = v.Badge ?? "";
        _badgeCard.Visible = !string.IsNullOrEmpty(v.Badge);

        // Línea de sesión: la primera fila del modelo (cofres y, detrás, partidas y media).
        var session = v.Rows.FirstOrDefault();
        var sessionChests = session != null && int.TryParse(session.Value, System.Globalization.NumberStyles.None, L.Culture, out var n)
            ? L.Count(n, "cofre", "cofres", "chest", "chests")
            : session?.Value;
        _session.Text = session == null ? "" : $"{L.T("Sesión", "Session", "이번 세션")}: {sessionChests}" + (session.Detail != null ? $"  ·  {session.Detail}" : "");

        var st = s.Stats;
        _statToday.Text = L.N(st.Today);
        // La fecha va en la línea de arriba: "1.409 · 06/11" a 15 pt no cabe en la tarjeta y se cortaba.
        _statBestDay.Text = st.BestDay is { } bestDay ? L.N(st.BestDayChests) : "—";
        _statBestDayCap.Text = st.BestDay is { } bd
            ? $"{L.T("Mejor día", "Best day", "최고 기록일")} · {bd.ToString(L.Ko ? "MM/dd" : "dd/MM", L.Culture)}"
            : L.T("Mejor día", "Best day", "최고 기록일");
        _statAverage.Text = st.Matches > 0 ? st.Average.ToString("0.0", L.Culture) : "—";
        _statBestMatch.Text = st.Matches > 0 ? L.N(st.BestMatch) : "—";
        _statMatches.Text = L.Count(st.HistoryMatches, "partida", "partidas", "match", "matches");
        _statMatchesCap.Text = st.HistorySince is { } since
            ? L.T($"Desde {since:dd/MM/yy}", $"Since {since:dd/MM/yy}", $"{since:yyyy년 M월}부터")
            : L.T("Histórico", "History", "전체 기록");
        _statTreasures.Text = $"{OverlayModel.TreasureIcon} {L.N(st.Treasures)}";
        _overlayButton.Enabled = _monitor.OverlayEnabled;
        _overlayButton.Indicator = _monitor.OverlayVisible ? Theme.Green : Theme.Red;

        if (st.LastMatch is { } lm)
        {
            _lastValue.Text = L.Count(lm.Chests, "cofre del equipo", "cofres del equipo", "team chest", "team chests");
            var parts = new List<string>();
            if (lm.Destroyed > 0) parts.Add(L.T($"{lm.Destroyed} roto(s)", $"{lm.Destroyed} destroyed"));
            if (lm.Treasures > 0) parts.Add(L.Count(lm.Treasures, "tesoro", "tesoros", "treasure", "treasures"));
            if (lm.Placement is { } place) parts.Add(lm.Players is { } players ? $"{L.Ordinal(place)} {L.T("de", "of")} {players}" : L.Ordinal(place));
            if (lm.Eliminations is { } elim) parts.Add(L.T($"{elim} elim.", $"{elim} elim."));
            _lastDetail.Text = string.Join("  ·  ", parts);
        }
        else
        {
            _lastValue.Text = "—";
            _lastDetail.Text = "";
        }
        _daily.SetDays(st.LastDays);

        // Perfil: nombre fijo con una sola cuenta, selector con varias.
        var signature = string.Join("|", s.Profiles.Select(p => p.Key));
        var several = s.Profiles.Count >= 2;
        _profile.Visible = several;
        _profileName.Visible = !several;
        _profileName.Text = s.Profiles.FirstOrDefault(p => p.Key == s.CurrentKey) is { } current
            ? $"{L.T("Perfil", "Profile")}: {current.Name}"
            : "";
        if (signature != _profileSig)
        {
            _profileSig = signature;
            _ignoreProfileChange = true;
            _profile.Items.Clear();
            foreach (var p in s.Profiles)
            {
                _profile.Items.Add(new ProfileItem(p.Key, Profiles.Describe(p)));
            }
            _ignoreProfileChange = false;
        }
        _ignoreProfileChange = true;
        for (var i = 0; i < _profile.Items.Count; i++)
        {
            if (_profile.Items[i] is ProfileItem item && item.Key == s.CurrentKey)
            {
                _profile.SelectedIndex = i;
                break;
            }
        }
        _ignoreProfileChange = false;

        // Aviso de cierre automático.
        _autoCloseOffer = s.AutoCloseOffer;
        _notice.Text = s.Notice ?? "";
        _notice.Cursor = s.AutoCloseOffer ? Cursors.Hand : Cursors.Default;
        _noticeCard.Visible = !string.IsNullOrEmpty(s.Notice);

        // Los botones ocupan el sitio del aviso de cierre automático cuando aparece.
        var notice = _noticeCard.Visible;
        _overlayButton.Visible = _reportButton.Visible = _settingsButton.Visible = !notice;
    }

    /// <summary>Tarjeta de estadística: título arriba y cifra debajo. Devuelve la etiqueta de la cifra.</summary>
    private Label Stat(string caption, int x, int y, Color valueColor, out Label captionLabel)
    {
        var card = new Card { Location = new Point(x, y), Size = new Size(136, 70), Radius = 14 };
        Controls.Add(card);
        captionLabel = Lbl(card, caption, 8.0f, FontStyle.Regular, Theme.Muted, new Point(12, 9), new Size(113, 15));
        return Lbl(card, "—", 15f, FontStyle.Bold, valueColor, new Point(12, 31), new Size(117, 28));
    }

    private PillButton Action(string text, int x)
    {
        var b = new PillButton { Text = text, Location = new Point(x, 277), Size = new Size(88, 40), Radius = 11 };
        Controls.Add(b);
        return b;
    }

    private static Label Lbl(Control parent, string text, float size, FontStyle style, Color color, Point at, Size sz, ContentAlignment align = ContentAlignment.TopLeft)
    {
        var l = new FitLabel
        {
            Text = text,
            Font = Theme.Text(size, style),
            ForeColor = color,
            BackColor = parent.BackColor,
            Location = at,
            Size = sz,
            TextAlign = align,
        };
        parent.Controls.Add(l);
        return l;
    }

    private static void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            // el informe aún no existe o no hay navegador: nada que hacer
        }
    }

    private sealed record ProfileItem(string Key, string Text)
    {
        public override string ToString() => Text;
    }
}
