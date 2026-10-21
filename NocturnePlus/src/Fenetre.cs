// Nocturne Plus - fenetre principale (theme Nocturne, navigation laterale).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NocturnePlus
{
    class Fenetre : Form
    {
        readonly Installation inst; readonly string fichierReglages;
        Reglages r;
        Partie partie;
        List<Ecrans.Info> ecrans;

        // navigation
        readonly List<Onglet> onglets = new List<Onglet>();
        readonly List<Panel> pages = new List<Panel>();

        // page Play
        Carte cUltra, cStable, c43, c169;
        RadioButton rbPlein, rbFenetre;
        ComboBox cbEcran;
        Label lblResume;
        // page Graphics
        ComboBox cbSortie, cbInterne, cbAniso, cbAA;
        NumericUpDown nuFps;
        CheckBox chkVsync, chkMip, chkTex, chkLisse2D, chkDecors;
        BoutonN btPack;
        // page Sound
        ComboBox cbSon; CheckBox chkMuet;
        // page Advanced
        RadioButton rbMono, rbEpingle, rbMulti;
        NumericUpDown nuCoeur, nuDelai;
        CheckBox chkDiag;
        Panel lstCorrectifs;   // une case themee par correctif (Tag = Correctif)
        TextBox txtDepot;
        // page Log
        TextBox txtJournal;
        // barre du bas / en-tete
        BoutonN btLancer;
        Label lblEtat;
        LinkLabel lnkMaj; Label lblVer;
        MiseAJour.Resultat maj;
        bool chargement;

        public Fenetre(Installation inst, string fichierReglages, bool lancerDirect)
        {
            this.inst = inst; this.fichierReglages = fichierReglages;
            r = Reglages.Charger(fichierReglages);
            Text = "Nocturne Plus";
            Font = Theme.Texte;
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            ClientSize = new Size(820, 570);
            BackColor = Theme.Fond; ForeColor = Theme.Parchemin;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Construire();
            Theme.Habiller(this);
            Charger();
            Journal.Ligne += l => { if (IsHandleCreated) BeginInvoke(new Action(() => { txtJournal.AppendText(l + "\r\n"); })); };
            Shown += (s, e) => VerifierMaj();
            if (lancerDirect) Shown += (s, e) => Lancer();
        }

        // ------------------------------------------------------------------ dessin de l'en-tete

        const int HautEntete = 96, LargeNav = 172, HautBas = 78;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var ent = new Rectangle(0, 0, ClientSize.Width, HautEntete);
            using (var b = new LinearGradientBrush(ent, Color.FromArgb(8, 6, 7), Color.FromArgb(58, 8, 14), 0f)) g.FillRectangle(b, ent);
            // lune pale derriere le titre
            using (var b = new SolidBrush(Color.FromArgb(28, 222, 210, 190))) g.FillEllipse(b, ClientSize.Width - 300, -60, 150, 150);
            using (var b = new SolidBrush(Color.FromArgb(255, 30, 10, 13))) g.FillEllipse(b, ClientSize.Width - 268, -72, 150, 150);
            TextRenderer.DrawText(g, "NOCTURNE", Theme.Titre, new Point(22, 14), Theme.Parchemin);
            var t = TextRenderer.MeasureText("NOCTURNE", Theme.Titre);
            TextRenderer.DrawText(g, "PLUS", Theme.Titre, new Point(22 + t.Width - 8, 14), Theme.SangVif);
            TextRenderer.DrawText(g, "Spookhouse field launcher  ·  Terminal Reality, 1999", Theme.SousTitre, new Point(26, 62), Theme.Gris);
            using (var p = new Pen(Theme.Sang, 2)) g.DrawLine(p, 0, HautEntete - 1, ClientSize.Width, HautEntete - 1);
            // colonne de navigation et barre du bas
            using (var b = new SolidBrush(Theme.Panneau))
            {
                g.FillRectangle(b, 0, HautEntete, LargeNav, ClientSize.Height - HautEntete);
                g.FillRectangle(b, LargeNav, ClientSize.Height - HautBas, ClientSize.Width - LargeNav, HautBas);
            }
            using (var p = new Pen(Theme.Bordure)) g.DrawLine(p, LargeNav, ClientSize.Height - HautBas, ClientSize.Width, ClientSize.Height - HautBas);
            base.OnPaint(e);
        }

        // ------------------------------------------------------------------ construction

        static Label Etiquette(Control parent, string t, int x, int y, Font f = null, Color? c = null)
        {
            var l = new Label { Text = t, Left = x, Top = y, AutoSize = true, BackColor = Color.Transparent, ForeColor = c ?? Theme.Parchemin, Font = f ?? Theme.Texte };
            parent.Controls.Add(l); return l;
        }
        static Label Note(Control parent, string t, int x, int y, int w, int h = 34)
        {
            var l = new Label { Text = t, Left = x, Top = y, Width = w, Height = h, ForeColor = Theme.Gris, Font = Theme.Petit, BackColor = Color.Transparent };
            parent.Controls.Add(l); return l;
        }
        static ComboBox Liste(Control parent, int x, int y, int w)
        {
            var c = new ComboBox { Left = x, Top = y, Width = w, DropDownStyle = ComboBoxStyle.DropDownList };
            parent.Controls.Add(c); return c;
        }
        Panel Page(string titre)
        {
            var p = new Panel { Left = LargeNav + 18, Top = HautEntete + 12, Width = ClientSize.Width - LargeNav - 34, Height = ClientSize.Height - HautEntete - HautBas - 20, BackColor = Theme.Fond, Visible = false };
            Etiquette(p, titre, 0, 0, Theme.Section, Theme.Parchemin);
            Controls.Add(p); pages.Add(p);
            var o = new Onglet { Text = titre, Left = 0, Top = HautEntete + 14 + onglets.Count * 42, Width = LargeNav, BackColor = Theme.Panneau };
            int i = onglets.Count; o.Click += (s, e) => Afficher(i);
            Controls.Add(o); onglets.Add(o);
            return p;
        }
        public void AfficherPage(int i) { Afficher(Math.Max(0, Math.Min(pages.Count - 1, i))); Refresh(); }

        void Afficher(int i)
        {
            for (int k = 0; k < pages.Count; k++) { pages[k].Visible = k == i; onglets[k].Actif = k == i; }
        }

        void Construire()
        {
            // --- en-tete : version et mise a jour
            lblVer = new Label { Text = "v" + MiseAJour.VersionTexte, AutoSize = true, ForeColor = Theme.Gris, BackColor = Color.Transparent, Font = Theme.Petit };
            lblVer.Location = new Point(ClientSize.Width - 230, 16);
            lnkMaj = new LinkLabel
            {
                Text = "Checking for updates…", AutoSize = true, BackColor = Color.Transparent, Font = Theme.Petit,
                LinkColor = Theme.Gris, ActiveLinkColor = Theme.SangVif, VisitedLinkColor = Theme.Gris, LinkBehavior = LinkBehavior.HoverUnderline,
                Location = new Point(ClientSize.Width - 230, 36)
            };
            lnkMaj.LinkClicked += (s, e) => { if (maj != null && maj.Etat != MiseAJour.Etat.Verification) try { Process.Start(maj.Page); } catch { } else VerifierMaj(); };
            Controls.Add(lblVer); Controls.Add(lnkMaj);

            // --- Play
            var pJ = Page("Play");
            Etiquette(pJ, "Renderer", 0, 34, Theme.Petit, Theme.Or);
            cUltra = new Carte { Text = "Ultra HD", Description = "Hardware 3D (dgVoodoo 2), rendered at your screen resolution. Recommended.", Left = 0, Top = 54, Width = 300, Height = 78 };
            cStable = new Carte { Text = "Stable", Description = "Software rendering, 640x480 upscaled (cnc-ddraw). The safe fallback.", Left = 312, Top = 54, Width = 300, Height = 78 };
            cUltra.Choisie += (s, e) => ChoisirMode(Mode.Ultra);
            cStable.Choisie += (s, e) => ChoisirMode(Mode.Stable);
            Etiquette(pJ, "Aspect ratio", 0, 146, Theme.Petit, Theme.Or);
            c43 = new Carte { Text = "4:3  Original", Description = "The game as designed, with black bars on the sides.", Left = 0, Top = 166, Width = 300, Height = 78 };
            c169 = new Carte { Text = "16:9  Widescreen", Description = "Full screen with a wider field of view (Ultra HD only).", Left = 312, Top = 166, Width = 300, Height = 78 };
            c43.Choisie += (s, e) => ChoisirFormat(false);
            c169.Choisie += (s, e) => ChoisirFormat(true);
            pJ.Controls.AddRange(new Control[] { cUltra, cStable, c43, c169 });
            Etiquette(pJ, "Monitor", 0, 262, Theme.Petit, Theme.Or);
            cbEcran = Liste(pJ, 0, 282, 300);
            cbEcran.SelectedIndexChanged += (s, e) => { RemplirSorties(); MajResume(); };
            Etiquette(pJ, "Window", 312, 262, Theme.Petit, Theme.Or);
            rbFenetre = new RondN { Text = "Borderless (easy Alt+Tab)", Left = 312, Top = 280, Width = 200, BackColor = Color.Transparent };
            rbPlein = new RondN { Text = "Fullscreen", Left = 516, Top = 280, Width = 100, BackColor = Color.Transparent };
            rbFenetre.CheckedChanged += (s, e) => MajResume();
            pJ.Controls.Add(rbFenetre); pJ.Controls.Add(rbPlein);
            lblResume = Note(pJ, "", 0, 322, 612, 40);
            lblResume.ForeColor = Theme.Parchemin;

            // --- Graphics
            var pG = Page("Graphics");
            Etiquette(pG, "3D rendered at", 0, 40); cbSortie = Liste(pG, 190, 37, 300);
            cbSortie.SelectedIndexChanged += (s, e) => MajResume();
            Etiquette(pG, "Game resolution", 0, 74); cbInterne = Liste(pG, 190, 71, 300);
            cbInterne.Items.AddRange(new object[] { "640x480", "854x480", "800x600", "1024x768", "1280x960", "1280x1024" });
            cbInterne.SelectedIndexChanged += (s, e) => { if (!chargement) { MajCartes(); RemplirSorties(); MajResume(); } };
            Note(pG, "640x480 = 4:3, 854x480 = 16:9. Taller resolutions only sharpen menus and are experimental.", 190, 98, 400);
            Etiquette(pG, "Frame rate limit", 0, 138);
            nuFps = new NumericUpDown { Left = 190, Top = 135, Width = 64, Minimum = 30, Maximum = 240, Value = 60 }; pG.Controls.Add(nuFps);
            nuFps.ValueChanged += (s, e) => MajResume();
            chkVsync = new CaseN { Text = "Vertical sync", Left = 270, Top = 137, Width = 160, BackColor = Color.Transparent }; pG.Controls.Add(chkVsync);
            Note(pG, "Keep 60: the cape and cloth physics follow the frame rate.", 190, 162, 400, 20);
            Etiquette(pG, "Anisotropic filtering", 0, 196); cbAniso = Liste(pG, 190, 193, 140);
            cbAniso.Items.AddRange(new object[] { "Game default", "2x", "4x", "8x", "16x" });
            Etiquette(pG, "Antialiasing", 0, 230); cbAA = Liste(pG, 190, 227, 140);
            cbAA.Items.AddRange(new object[] { "off", "2x", "4x", "8x" });
            Note(pG, "If antialiasing breaks menus or text, set it back to off.", 340, 230, 270, 34);
            chkMip = new CaseN { Text = "Auto-generated mipmaps (cleaner distant textures)", Left = 0, Top = 268, Width = 500, BackColor = Color.Transparent }; pG.Controls.Add(chkMip);
            chkTex = new CaseN { Text = "Larger texture cache (RENDER.INI)", Left = 0, Top = 294, Width = 500, BackColor = Color.Transparent }; pG.Controls.Add(chkTex);
            chkLisse2D = new CaseN { Text = "Smooth menus, text and HUD (instead of big pixels)", Left = 0, Top = 320, Width = 500, BackColor = Color.Transparent }; pG.Controls.Add(chkLisse2D);
            chkDecors = new CaseN { Text = "AI-remastered backgrounds and HD textures", Left = 0, Top = 346, Width = 330, BackColor = Color.Transparent }; pG.Controls.Add(chkDecors);
            btPack = new BoutonN(false) { Text = "Generate HD pack…", Left = 340, Top = 342, Width = 150, Height = 26 };
            btPack.Click += (s, e) => GenererPack();
            pG.Controls.Add(btPack);
            new ToolTip().SetToolTip(btPack, "Builds the HD backgrounds and textures from your own game files with Real-ESRGAN (downloaded once, ~45 MB).");

            // --- Sound
            var pS = Page("Sound");
            Etiquette(pS, "Device", 0, 40); cbSon = Liste(pS, 90, 37, 420);
            chkMuet = new CaseN { Text = "Mute", Left = 90, Top = 72, Width = 200, BackColor = Color.Transparent }; pS.Controls.Add(chkMuet);
            Note(pS, "Standard Windows DirectSound. DSOAL (dsound.dll) stays disabled: it crashed the menu.", 90, 104, 420);

            // --- Advanced
            var pA = Page("Advanced");
            Etiquette(pA, "Processor", 0, 34, Theme.Petit, Theme.Or);
            rbEpingle = new RondN { Text = "Game on one core, rendering free on the others (recommended)", Left = 0, Top = 52, Width = 600, BackColor = Color.Transparent };
            rbMono = new RondN { Text = "Whole game on one core (safe fallback)", Left = 0, Top = 76, Width = 600, BackColor = Color.Transparent };
            rbMulti = new RondN { Text = "No restriction (needs the engine clock fix)", Left = 0, Top = 100, Width = 600, BackColor = Color.Transparent };
            pA.Controls.AddRange(new Control[] { rbEpingle, rbMono, rbMulti });
            Etiquette(pA, "Game core: CPU", 0, 130); nuCoeur = new NumericUpDown { Left = 110, Top = 127, Width = 52, Minimum = 0, Maximum = Environment.ProcessorCount - 1 }; pA.Controls.Add(nuCoeur);
            Etiquette(pA, "Watchdog", 0, 164, Theme.Petit, Theme.Or);
            Etiquette(pA, "Stop a frozen game after", 0, 186); nuDelai = new NumericUpDown { Left = 170, Top = 183, Width = 60, Minimum = 0, Maximum = 600 }; pA.Controls.Add(nuDelai);
            Etiquette(pA, "s (0 = never)", 236, 186, Theme.Petit, Theme.Gris);
            chkDiag = new CaseN { Text = "Log where each thread is stuck", Left = 330, Top = 184, Width = 280, BackColor = Color.Transparent }; pA.Controls.Add(chkDiag);
            Etiquette(pA, "Executable patches (verified, reversible)", 0, 218, Theme.Petit, Theme.Or);
            lstCorrectifs = new Panel { Left = 0, Top = 236, Width = 612, Height = 60, BackColor = Theme.Fond }; pA.Controls.Add(lstCorrectifs);
            Etiquette(pA, "Update repository", 0, 306, Theme.Petit, Theme.Or);
            txtDepot = new TextBox { Left = 120, Top = 304, Width = 200 }; pA.Controls.Add(txtDepot);
            var btJ = new BoutonN(false) { Text = "Open log", Left = 330, Top = 300, Width = 86, Height = 28 };
            btJ.Click += (s, e) => { try { Process.Start("notepad.exe", "\"" + Journal.Fichier + "\""); } catch { } };
            var btD = new BoutonN(false) { Text = "Game folder", Left = 422, Top = 300, Width = 96, Height = 28 };
            btD.Click += (s, e) => Process.Start("explorer.exe", "\"" + inst.Jeu + "\"");
            var btR = new BoutonN(false) { Text = "Repair", Left = 524, Top = 300, Width = 88, Height = 28 };
            btR.Click += (s, e) => Reparer();
            pA.Controls.AddRange(new Control[] { btJ, btD, btR });

            // --- Log
            var pL = Page("Log");
            txtJournal = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = false, Left = 0, Top = 32, Width = pL.Width, Height = pL.Height - 32, Font = new Font("Consolas", 8.25f) };
            pL.Controls.Add(txtJournal);

            // --- barre du bas
            btLancer = new BoutonN(true) { Text = "LAUNCH", Width = 190, Height = 50 };
            btLancer.Location = new Point(ClientSize.Width - btLancer.Width - 16, ClientSize.Height - HautBas + 14);
            btLancer.Click += (s, e) => Lancer();
            var btDebloquer = new BoutonN(false) { Text = "Release mouse/screen", Width = 160, Height = 30 };
            btDebloquer.Location = new Point(btLancer.Left - btDebloquer.Width - 12, ClientSize.Height - HautBas + 24);
            btDebloquer.Click += (s, e) =>
            {
                Ecrans.RemettreAffichage();
                // copie locale : le jeu peut se fermer pendant que la question est affichee
                var p = partie;
                if (p != null && MessageBox.Show(this, "Also stop the running game?", Text, MessageBoxButtons.YesNo) == DialogResult.Yes) p.Tuer();
                Journal.Note("Souris et affichage remis a zero");
            };
            lblEtat = new Label { Left = LargeNav + 18, Top = ClientSize.Height - HautBas + 18, Width = btDebloquer.Left - LargeNav - 30, Height = 44, ForeColor = Theme.Gris, BackColor = Theme.Panneau, Font = Theme.Petit, Text = "Ready." };
            Controls.AddRange(new Control[] { btLancer, btDebloquer, lblEtat });
            AcceptButton = btLancer;
            var tip = new ToolTip();
            tip.SetToolTip(btR, "Restores the known-good setup: Stable mode, cnc-ddraw, original executable.");
            tip.SetToolTip(btDebloquer, "Releases the mouse and restores the desktop resolution if the game kept them.");
            tip.SetToolTip(txtDepot, "GitHub repository checked for new versions (owner/name).");

            Afficher(0);
        }

        // ------------------------------------------------------------------ logique des choix

        void ChoisirMode(Mode m)
        {
            r.Mode = m; cUltra.Choix = m == Mode.Ultra; cStable.Choix = m == Mode.Stable;
            c169.Enabled = m == Mode.Ultra;
            if (m == Mode.Stable && EstLarge()) ChoisirFormat(false);
            MajResume();
        }

        void ChoisirFormat(bool large)
        {
            if (large && !cUltra.Choix) { lblEtat.Text = "16:9 needs Ultra HD."; return; }
            cbInterne.SelectedItem = large ? "854x480" : "640x480";
            MajCartes(); RemplirSorties(); MajResume();
        }

        bool EstLarge()
        {
            int w, h; return Reglages.LireRes((string)cbInterne.SelectedItem ?? "640x480", out w, out h) && w * 3 > h * 4;
        }

        void MajCartes()
        {
            bool l = EstLarge(); c169.Choix = l; c43.Choix = !l;
        }

        void MajResume()
        {
            if (chargement || cbEcran == null) return;
            LireControles();
            var e = cbEcran.SelectedItem as Ecrans.Info;
            string s;
            if (r.Mode == Mode.Ultra)
                s = string.Format("Hardware 3D at {0} · {1} · {2} FPS · {3}", Installation.ResolutionUltra(r, e).Replace("h:", "").Replace(", v:", "x"),
                    EstLarge() ? "16:9" : "4:3", r.FpsMax, r.PleinEcran ? "fullscreen" : "borderless window");
            else
                s = string.Format("Software 640x480, upscaled · {0} FPS · {1}", r.FpsMax, r.PleinEcran ? "fullscreen" : "borderless window");
            lblResume.Text = s;
        }

        // ------------------------------------------------------------------ mise a jour

        // version et etat de mise a jour alignes a droite de l'en-tete
        void CalerMaj()
        {
            lnkMaj.Left = ClientSize.Width - lnkMaj.Width - 18;
            lblVer.Left = ClientSize.Width - lblVer.Width - 18;
        }

        void VerifierMaj()
        {
            lnkMaj.Text = "Checking for updates…"; lnkMaj.LinkColor = Theme.Gris; CalerMaj();
            maj = new MiseAJour.Resultat { Etat = MiseAJour.Etat.Verification };
            MiseAJour.Verifier(r.Depot, res =>
            {
                maj = res;
                switch (res.Etat)
                {
                    case MiseAJour.Etat.AJour: lnkMaj.Text = "● Up to date"; lnkMaj.LinkColor = Color.FromArgb(120, 170, 110); break;
                    case MiseAJour.Etat.Disponible: lnkMaj.Text = "● Update available: " + res.Derniere + " (open)"; lnkMaj.LinkColor = Theme.Or; break;
                    default: lnkMaj.Text = "○ Update check unavailable (" + res.Detail + ")"; lnkMaj.LinkColor = Theme.Gris; break;
                }
                CalerMaj();
                Journal.Note("Mise a jour : {0} (lanceur v{1}, depot {2}, dernier {3})", res.Etat, MiseAJour.VersionTexte, r.Depot, res.Derniere ?? "-");
            });
        }

        // ------------------------------------------------------------------ reglages <-> controles

        void Charger()
        {
            chargement = true;
            ecrans = Ecrans.Lister();
            foreach (var e in ecrans) cbEcran.Items.Add(e);
            int ie = ecrans.FindIndex(e => e.Peripherique == r.Ecran);
            if (ie < 0) ie = Math.Max(0, ecrans.FindIndex(e => e.Principal));
            if (cbEcran.Items.Count > 0) cbEcran.SelectedIndex = ie;

            (r.PleinEcran ? rbPlein : rbFenetre).Checked = true;
            nuFps.Value = Math.Max(nuFps.Minimum, Math.Min(nuFps.Maximum, r.FpsMax));
            chkVsync.Checked = r.VSync;
            cbInterne.SelectedItem = r.ResInterne; if (cbInterne.SelectedIndex < 0) cbInterne.SelectedIndex = 0;
            RemplirSorties();
            cbAniso.SelectedIndex = r.Anisotrope >= 16 ? 4 : r.Anisotrope >= 8 ? 3 : r.Anisotrope >= 4 ? 2 : r.Anisotrope >= 2 ? 1 : 0;
            cbAA.SelectedItem = r.Antialiasing; if (cbAA.SelectedIndex < 0) cbAA.SelectedIndex = 0;
            chkMip.Checked = r.MipmapsAuto; chkTex.Checked = r.TexturesMax; chkLisse2D.Checked = r.Lissage2D;
            chkDecors.Enabled = inst.PackDecorsDisponible; chkDecors.Checked = r.DecorsHD && chkDecors.Enabled;

            cbSon.Items.Add("(don't change)");
            foreach (var p in Son.Peripheriques()) cbSon.Items.Add(p);
            cbSon.SelectedItem = r.PeripheriqueSon; if (cbSon.SelectedIndex < 0) cbSon.SelectedIndex = 0;
            chkMuet.Checked = r.SonCoupe;

            (r.Coeurs == Coeurs.MonoCoeur ? rbMono : r.Coeurs == Coeurs.JeuEpingle ? rbEpingle : rbMulti).Checked = true;
            nuCoeur.Value = Math.Min(nuCoeur.Maximum, r.CoeurJeu);
            nuDelai.Value = Math.Min(nuDelai.Maximum, r.DelaiGel);
            chkDiag.Checked = r.DiagGel;
            txtDepot.Text = r.Depot;

            // la camera 16:9 est geree automatiquement par le choix du format : pas de case pour elle
            var actifs = new HashSet<string>((r.Correctifs ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            // alttab3d : obsolete depuis le correctif d'horloge, garde dans le code mais masque
            foreach (var c in Installation.Correctifs.Where(c => c.Id != Installation.IdCamera169 && c.Id != Installation.IdFichiersLibres && c.Id != "alttab3d"))
            {
                string etat; try { etat = inst.Etat(c); } catch (Exception ex) { etat = ex.Message; }
                var el = new ElementCorrectif { C = c, Etat = etat };
                lstCorrectifs.Controls.Add(new CaseN { Text = el.ToString(), Tag = c, Checked = actifs.Contains(c.Id), Left = 0, Top = lstCorrectifs.Controls.Count * 24, Width = 612, Height = 22 });
            }
            try { if (File.Exists(Journal.Fichier)) txtJournal.Text = string.Join("\r\n", File.ReadAllLines(Journal.Fichier).Reverse().Take(200).Reverse().ToArray()) + "\r\n"; } catch { }
            chargement = false;
            ChoisirMode(r.Mode);
            MajCartes();
            MajResume();
        }

        class ElementCorrectif
        {
            public Correctif C; public string Etat;
            public override string ToString() { return C.Description + "  [" + (Etat == "applique" ? "applied" : Etat == "absent" ? "not applied" : Etat) + "]"; }
        }

        void RemplirSorties()
        {
            var e = cbEcran.SelectedItem as Ecrans.Info;
            string choix = cbSortie.SelectedIndex > 0 ? (string)cbSortie.SelectedItem : r.SortieUltra;
            cbSortie.Items.Clear();
            cbSortie.Items.Add(e != null ? string.Format("Auto (native height, {0})", EstLarge() ? "16:9" : "4:3") : "Auto");
            if (e != null)
            {
                foreach (var m in Ecrans.Modes(e.Peripherique)) { int w, h; if (Reglages.LireRes(m, out w, out h) && h >= 720) cbSortie.Items.Add(m); }
                // sur-echantillonnage : rendu plus grand que l'ecran puis reduit (le plus net, le plus lourd)
                cbSortie.Items.Add(string.Format("{0}x{1}", e.H * 4 / 3 * 2, e.H * 2));
            }
            cbSortie.SelectedItem = choix; if (cbSortie.SelectedIndex < 0) cbSortie.SelectedIndex = 0;
        }

        void LireControles()
        {
            r.Mode = cUltra.Choix ? Mode.Ultra : Mode.Stable;
            var e = cbEcran.SelectedItem as Ecrans.Info; r.Ecran = e != null ? e.Peripherique : "";
            r.PleinEcran = rbPlein.Checked;
            r.Etirer169 = false;
            r.FpsMax = (int)nuFps.Value; r.VSync = chkVsync.Checked;
            r.SortieUltra = cbSortie.SelectedIndex <= 0 ? "auto" : (string)cbSortie.SelectedItem;
            r.ResInterne = r.Mode == Mode.Stable ? "640x480" : (string)cbInterne.SelectedItem ?? "640x480";
            r.Anisotrope = new[] { 0, 2, 4, 8, 16 }[Math.Max(0, cbAniso.SelectedIndex)];
            r.Antialiasing = (string)cbAA.SelectedItem ?? "off";
            r.MipmapsAuto = chkMip.Checked; r.TexturesMax = chkTex.Checked; r.Lissage2D = chkLisse2D.Checked; r.DecorsHD = chkDecors.Checked || !chkDecors.Enabled && r.DecorsHD;
            r.PeripheriqueSon = cbSon.SelectedIndex <= 0 ? "" : (string)cbSon.SelectedItem;
            r.SonCoupe = chkMuet.Checked;
            r.Coeurs = rbMono.Checked ? Coeurs.MonoCoeur : rbEpingle.Checked ? Coeurs.JeuEpingle : Coeurs.MultiCoeur;
            r.CoeurJeu = (int)nuCoeur.Value; r.DelaiGel = (int)nuDelai.Value; r.DiagGel = chkDiag.Checked;
            r.Correctifs = string.Join(",", lstCorrectifs.Controls.OfType<CaseN>().Where(x => x.Checked).Select(x => ((Correctif)x.Tag).Id).ToArray());
            r.Depot = string.IsNullOrWhiteSpace(txtDepot.Text) ? MiseAJour.DepotParDefaut : txtDepot.Text.Trim();
        }

        // ------------------------------------------------------------------ actions

        bool Confirmer(string t) { return MessageBox.Show(this, t, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes; }

        void Lancer()
        {
            if (partie != null) { MessageBox.Show(this, "The game is already running.", Text); return; }
            LireControles();
            try { r.Enregistrer(fichierReglages); } catch { }

            if (Process.GetProcessesByName("nocturne").Length > 0)
            {
                MessageBox.Show(this, "An instance of nocturne.exe is already running (maybe frozen and invisible). Close it first; if it won't close, restart the PC.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (Natif.IsUserAnAdmin() && !Confirmer("The launcher is running as administrator. The game would also run as administrator, which is not recommended. Continue anyway?")) return;
            // le gel multi-coeur venait de l'horloge du moteur : sans ce correctif, on previent
            if (r.Coeurs != Coeurs.MonoCoeur && !r.Correctifs.Split(',').Contains("horloge") &&
                !Confirmer("Without the \"Engine clock\" patch, the 1999 engine freezes outside single-core mode (claws screen or after ~30 s), and a freeze may require restarting the PC.\n\nLaunch anyway?")) return;
            int rw, rh; Reglages.LireRes(r.ResInterne, out rw, out rh);
            if (r.Mode == Mode.Ultra && rh > 480 &&
                !Confirmer("Game resolution " + r.ResInterne + " in Ultra mode is experimental. If the game shows \"Unable to lock hold buffer\", go back to 640x480.\n\nContinue?")) return;

            var e = cbEcran.SelectedItem as Ecrans.Info;
            Journal.Note("=== Lancement : mode {0} ===", r.Mode);
            foreach (var kv in r.EnPaires()) Journal.Note("  {0} = {1}", kv.Key, kv.Value);
            if (e != null) Journal.Note("  ecran : {0} {1}", e.Peripherique, e);
            if (r.Mode == Mode.Ultra) Journal.Note("  rendu 3D force par dgVoodoo : {0}", Installation.ResolutionUltra(r, e));
            try
            {
                lblEtat.Text = "Preparing the game folder…"; lblEtat.Refresh();
                inst.PreparerReserve();
                inst.InstallerWrapper(r.Mode, s => { lblEtat.Text = s; lblEtat.Refresh(); });
                inst.EcrireIni(r, e);
                inst.InstallerDecors(r.DecorsHD);
                var actifs = inst.CorrectifsActifs(r);
                foreach (var c in Installation.Correctifs) inst.Appliquer(c, actifs.Contains(c.Id));
                if (inst.DossierSauvegarde != null) Journal.Note("Fichiers d'avant le lancement sauvegardes dans {0}", inst.DossierSauvegarde.Substring(inst.Jeu.Length + 1));

                partie = new Partie(inst, r);
                partie.FenetreVisible += h => BeginInvoke(new Action(() => { WindowState = FormWindowState.Minimized; }));
                partie.Termine += bilan => BeginInvoke(new Action(() =>
                {
                    partie = null; btLancer.Enabled = true; btLancer.Text = "LAUNCH";
                    lblEtat.Text = "Game closed. Details in the Log page.";
                    WindowState = FormWindowState.Normal; Activate();
                }));
                partie.Demarrer();
                btLancer.Enabled = false; btLancer.Text = "RUNNING…"; lblEtat.Text = "The game is running. The watchdog is on.";
            }
            catch (Exception ex)
            {
                partie = null;
                Journal.Note("ERREUR : {0}", ex.Message);
                lblEtat.Text = "Launch failed: see the message.";
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void GenererPack()
        {
            if (partie != null) { MessageBox.Show(this, "Close the game first.", Text); return; }
            if (MessageBox.Show(this,
                "Nocturne Plus will build the HD pack from your own game files:\n\n" +
                "• downloads Real-ESRGAN once (~45 MB, official xinntao release, BSD licence);\n" +
                "• upscales about 1,600 backgrounds and 1,100 textures with your graphics card (15 to 30 minutes);\n" +
                "• the .POD archives are never modified; an existing pack is kept aside.\n\nStart now?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            btPack.Enabled = btLancer.Enabled = false;
            Action<string> statut = s => BeginInvoke(new Action(() => { lblEtat.Text = s; }));
            new System.Threading.Thread(() =>
            {
                string fin;
                try { fin = string.Format("HD pack ready: {0} images. Tick the box to use it.", inst.GenererPackHD(statut)); }
                catch (Exception ex) { Journal.Note("ERREUR pack HD : {0}", ex); fin = "HD pack failed: " + ex.Message; }
                BeginInvoke(new Action(() =>
                {
                    lblEtat.Text = fin; btPack.Enabled = btLancer.Enabled = true;
                    chkDecors.Enabled = inst.PackDecorsDisponible; if (chkDecors.Enabled) chkDecors.Checked = true;
                }));
            }) { IsBackground = true, Name = "pack-hd" }.Start();
        }

        void Reparer()
        {
            if (partie != null) { MessageBox.Show(this, "Close the game first.", Text); return; }
            if (!Confirmer("Restore the known-good setup (Stable mode, cnc-ddraw, original executable)?\nReplaced files are backed up, nothing is deleted.")) return;
            try
            {
                inst.Reparer(cbEcran.SelectedItem as Ecrans.Info);
                r = new Reglages { Ecran = r.Ecran, PeripheriqueSon = r.PeripheriqueSon, Depot = r.Depot };
                r.Enregistrer(fichierReglages);
                lstCorrectifs.Controls.Clear(); cbEcran.Items.Clear(); cbSon.Items.Clear(); cbSortie.Items.Clear();
                Charger();
                lblEtat.Text = "Known-good setup restored.";
                MessageBox.Show(this, "Known-good setup restored.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { Journal.Note("ERREUR reparation : {0}", ex.Message); MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
