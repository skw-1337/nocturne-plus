// Nocturne Plus - theme visuel : nuit, sang seche et parchemin, comme le jeu.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NocturnePlus
{
    static class Theme
    {
        public static readonly Color Fond = Color.FromArgb(12, 10, 11);
        public static readonly Color Panneau = Color.FromArgb(22, 18, 19);
        public static readonly Color PanneauSurvol = Color.FromArgb(34, 26, 27);
        public static readonly Color Bordure = Color.FromArgb(58, 44, 44);
        public static readonly Color Sang = Color.FromArgb(150, 18, 28);
        public static readonly Color SangVif = Color.FromArgb(196, 30, 42);
        public static readonly Color Parchemin = Color.FromArgb(222, 210, 190);
        public static readonly Color Gris = Color.FromArgb(150, 138, 128);
        public static readonly Color Or = Color.FromArgb(201, 162, 90);

        public static readonly Font Titre = new Font("Palatino Linotype", 26f, FontStyle.Bold);
        public static readonly Font SousTitre = new Font("Palatino Linotype", 10f, FontStyle.Italic);
        public static readonly Font Section = new Font("Palatino Linotype", 12.5f, FontStyle.Bold);
        public static readonly Font Texte = new Font("Segoe UI", 9.5f);
        public static readonly Font Petit = new Font("Segoe UI", 8.5f);
        public static readonly Font CarteTitre = new Font("Palatino Linotype", 12f, FontStyle.Bold);
        public static readonly Font Bouton = new Font("Palatino Linotype", 15f, FontStyle.Bold);

        /// <summary>Applique les couleurs du theme aux controles standard (recursif).</summary>
        public static void Habiller(Control c)
        {
            foreach (Control x in c.Controls)
            {
                if (x is ComboBox)
                {
                    var cb = (ComboBox)x;
                    cb.FlatStyle = FlatStyle.Flat; cb.BackColor = Panneau; cb.ForeColor = Parchemin;
                    cb.DrawMode = DrawMode.OwnerDrawFixed; cb.ItemHeight = 20;
                    cb.DrawItem -= DessinerElement; cb.DrawItem += DessinerElement;
                }
                else if (x is NumericUpDown) { x.BackColor = Panneau; x.ForeColor = Parchemin; ((NumericUpDown)x).BorderStyle = BorderStyle.FixedSingle; }
                else if (x is CheckBox || x is RadioButton) { x.ForeColor = Parchemin; }
                else if (x is TextBox) { x.BackColor = Color.FromArgb(8, 7, 7); x.ForeColor = Color.FromArgb(190, 180, 165); ((TextBox)x).BorderStyle = BorderStyle.None; }
                else if (x is CheckedListBox) { x.BackColor = Panneau; x.ForeColor = Parchemin; ((CheckedListBox)x).BorderStyle = BorderStyle.None; }
                Habiller(x);
            }
        }

        static void DessinerElement(object s, DrawItemEventArgs e)
        {
            var cb = (ComboBox)s;
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var f = new SolidBrush(sel ? Sang : Panneau)) e.Graphics.FillRectangle(f, e.Bounds);
            TextRenderer.DrawText(e.Graphics, cb.Items[e.Index].ToString(), cb.Font, Rectangle.Inflate(e.Bounds, -4, 0),
                cb.Enabled ? Parchemin : Gris, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        public static GraphicsPath Arrondi(Rectangle r, int rayon)
        {
            var p = new GraphicsPath(); int d = rayon * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
    }

    /// <summary>Carte cliquable (titre + description) pour les choix principaux.</summary>
    class Carte : Control
    {
        bool choisie, survol;
        public string Description = "";
        public event EventHandler Choisie;

        public Carte()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand; BackColor = Theme.Fond;
        }

        public bool Choix
        {
            get { return choisie; }
            set { choisie = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { survol = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { survol = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { var h = Choisie; if (h != null) h(this, e); base.OnClick(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var p = Theme.Arrondi(r, 6))
            {
                Color fond = choisie ? Color.FromArgb(48, 14, 18) : survol ? Theme.PanneauSurvol : Theme.Panneau;
                using (var b = new SolidBrush(fond)) g.FillPath(b, p);
                using (var pen = new Pen(choisie ? Theme.SangVif : Theme.Bordure, choisie ? 2f : 1f)) g.DrawPath(pen, p);
            }
            // pastille de selection
            var pt = new Rectangle(14, 16, 14, 14);
            using (var pen = new Pen(choisie ? Theme.SangVif : Theme.Gris, 1.5f)) g.DrawEllipse(pen, pt);
            if (choisie) using (var b = new SolidBrush(Theme.SangVif)) g.FillEllipse(b, Rectangle.Inflate(pt, -3, -3));
            Color texte = Enabled ? Theme.Parchemin : Theme.Gris;
            TextRenderer.DrawText(g, Text, Theme.CarteTitre, new Point(36, 11), choisie ? Color.White : texte);
            TextRenderer.DrawText(g, Description, Theme.Petit, new Rectangle(36, 36, Width - 46, Height - 40), Theme.Gris,
                TextFormatFlags.WordBreak | TextFormatFlags.Left);
        }
    }

    /// <summary>Bouton plat aux couleurs du theme (principal = rouge sang).</summary>
    class BoutonN : Button
    {
        public bool Principal;
        public BoutonN(bool principal)
        {
            Principal = principal;
            FlatStyle = FlatStyle.Flat; Cursor = Cursors.Hand; UseVisualStyleBackColor = false;
            FlatAppearance.BorderSize = 1;
            FlatAppearance.BorderColor = principal ? Theme.SangVif : Theme.Bordure;
            FlatAppearance.MouseOverBackColor = principal ? Theme.SangVif : Theme.PanneauSurvol;
            FlatAppearance.MouseDownBackColor = Color.FromArgb(110, 12, 20);
            BackColor = principal ? Theme.Sang : Theme.Panneau;
            ForeColor = Theme.Parchemin;
            Font = principal ? Theme.Bouton : Theme.Texte;
        }
    }

    /// <summary>Case a cocher dessinee aux couleurs du theme (les cases "Flat" de Windows ne montrent pas leur etat sur fond sombre).</summary>
    class CaseN : CheckBox
    {
        public CaseN() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand; }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Parent != null ? Parent.BackColor : Theme.Fond)) g.FillRectangle(b, ClientRectangle);
            int y = (Height - 14) / 2; var box = new Rectangle(1, y, 14, 14);
            using (var b = new SolidBrush(Checked ? Theme.Sang : Theme.Panneau)) g.FillRectangle(b, box);
            using (var p = new Pen(Checked ? Theme.SangVif : Theme.Gris)) g.DrawRectangle(p, box);
            if (Checked) using (var p = new Pen(Color.White, 2f)) g.DrawLines(p, new[] { new Point(4, y + 7), new Point(7, y + 10), new Point(12, y + 4) });
            TextRenderer.DrawText(g, Text, Font, new Rectangle(22, 0, Width - 22, Height), Enabled ? Theme.Parchemin : Theme.Gris, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }

    /// <summary>Bouton radio dessine aux couleurs du theme.</summary>
    class RondN : RadioButton
    {
        public RondN() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand; }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Parent != null ? Parent.BackColor : Theme.Fond)) g.FillRectangle(b, ClientRectangle);
            int y = (Height - 14) / 2; var rond = new Rectangle(1, y, 14, 14);
            using (var p = new Pen(Checked ? Theme.SangVif : Theme.Gris, 1.5f)) g.DrawEllipse(p, rond);
            if (Checked) using (var b = new SolidBrush(Theme.SangVif)) g.FillEllipse(b, Rectangle.Inflate(rond, -4, -4));
            TextRenderer.DrawText(g, Text, Font, new Rectangle(22, 0, Width - 22, Height), Enabled ? (Checked ? Color.White : Theme.Parchemin) : Theme.Gris, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }

    /// <summary>Entree de navigation laterale.</summary>
    class Onglet : Control
    {
        bool actif, survol;
        public Onglet() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); Cursor = Cursors.Hand; Height = 40; }
        public bool Actif { get { return actif; } set { actif = value; Invalidate(); } }
        protected override void OnMouseEnter(EventArgs e) { survol = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { survol = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(actif ? Color.FromArgb(40, 14, 17) : survol ? Theme.PanneauSurvol : Theme.Panneau)) g.FillRectangle(b, ClientRectangle);
            if (actif) using (var b = new SolidBrush(Theme.SangVif)) g.FillRectangle(b, 0, 6, 3, Height - 12);
            TextRenderer.DrawText(g, Text, actif ? Theme.Section : Theme.Texte, new Rectangle(18, 0, Width - 18, Height),
                actif ? Color.White : Theme.Parchemin, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }
}
