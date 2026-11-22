using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

public class CombatRPG : Form
{
    [DllImport("winmm.dll")]
    private static extern long mciSendString(string command, System.Text.StringBuilder returnValue, int returnLength, IntPtr winHandle);

    int volumeMusique = 800; // De 0 a 1000
    string langue = "FR"; // "FR" ou "EN"
    bool musiqueLancee = false;

    int pvJoueur = 40, pvMaxJoueur = 40;
    int pvEnnemi = 120, pvMaxEnnemi = 120;
    int soins = 4, pacifisme = 0;

    // Etats : TITRE, PARAMETRES, MENU, MESSAGE, ESQUIVE, FIN
    string etat = "TITRE";
    string bulleEnnemi = "", humeurEnnemi = "NORMAL", prochaineAction = "";
    string messageDialogue = "";
    int timerTransition = 0, timerEsquive = 0, invulnerable = 0, offsetX = 0, offsetY = 0;
    double compteurRespiration = 0;

    Rectangle boiteArene = new Rectangle(30, 175, 420, 145);
    RectangleF ame = new RectangleF(232, 237, 16, 16);
    float vitesseAme = 4.5f;
    bool gauche, droite, haut, bas;

    class Projectile { public float x, y, vx, vy; }
    List<Projectile> projectiles = new List<Projectile>();
    Random rnd = new Random();

    // Boutons Titre & Parametres
    Button btnJouer, btnParametres, btnVolMoins, btnVolPlus, btnLangue, btnRetour;
    // Boutons Combat
    Button btnAttaquer, btnParler, btnSoin, btnRecommencer;
    Timer boucle;

    public CombatRPG()
    {
        this.Text = "Combat RPG";
        this.BackColor = Color.Black;
        this.FormBorderStyle = FormBorderStyle.None;
        this.WindowState = FormWindowState.Maximized;
        this.DoubleBuffered = true;
        this.KeyPreview = true;

        // Creation des boutons du menu Titre
        btnJouer = CreerBouton("", 200, Color.Lime);
        btnParametres = CreerBouton("", 200, Color.White);

        // Creation des boutons Parametres
        btnVolMoins = CreerBouton("[ - ]", 65, Color.Orange);
        btnVolPlus = CreerBouton("[ + ]", 65, Color.Orange);
        btnLangue = CreerBouton("", 240, Color.Cyan);
        btnRetour = CreerBouton("", 180, Color.White);

        // Creation des boutons de Combat
        btnAttaquer = CreerBouton("", 125, Color.Orange);
        btnParler = CreerBouton("", 125, Color.Orange);
        btnSoin = CreerBouton("", 125, Color.Orange);
        btnRecommencer = CreerBouton("", 170, Color.Cyan);

        btnJouer.Click += (s, e) => DemarrerCombat();
        btnParametres.Click += (s, e) => OuvrirParametres();
        btnVolMoins.Click += (s, e) => ChangerVolume(-100);
        btnVolPlus.Click += (s, e) => ChangerVolume(100);
        btnLangue.Click += (s, e) => ChangerLangue();
        btnRetour.Click += (s, e) => OuvrirTitre();

        btnAttaquer.Click += (s, e) => ActionAttaquer();
        btnParler.Click += (s, e) => ActionParler();
        btnSoin.Click += (s, e) => ActionSoin();
        btnRecommencer.Click += (s, e) => RecommencerJeu();

        this.Controls.AddRange(new Control[] {
            btnJouer, btnParametres, btnVolMoins, btnVolPlus, btnLangue, btnRetour,
            btnAttaquer, btnParler, btnSoin, btnRecommencer
        });

        this.Resize += (s, e) => CentrerInterface();

        this.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Escape) this.Close();
            if (e.KeyCode == Keys.Left) gauche = true;
            if (e.KeyCode == Keys.Right) droite = true;
            if (e.KeyCode == Keys.Up) haut = true;
            if (e.KeyCode == Keys.Down) bas = true;
        };
        this.KeyUp += (s, e) => {
            if (e.KeyCode == Keys.Left) gauche = false;
            if (e.KeyCode == Keys.Right) droite = false;
            if (e.KeyCode == Keys.Up) haut = false;
            if (e.KeyCode == Keys.Down) bas = false;
        };

        OuvrirTitre();

        boucle = new Timer();
        boucle.Interval = 16;
        boucle.Tick += (s, e) => MettreAJour();
        boucle.Start();
    }
    void LancerMusique()
    {
        if (!musiqueLancee) {
            string chemin = System.IO.Path.Combine(Application.StartupPath, "music.mp3");
            mciSendString("open \"" + chemin + "\" type mpegvideo alias bgm", null, 0, IntPtr.Zero);
            mciSendString("play bgm repeat", null, 0, IntPtr.Zero);
            musiqueLancee = true;
        }
        mciSendString("setaudio bgm volume to " + volumeMusique, null, 0, IntPtr.Zero);
    }

    void ChangerVolume(int delta)
    {
        volumeMusique = Math.Max(0, Math.Min(1000, volumeMusique + delta));
        if (musiqueLancee)
            mciSendString("setaudio bgm volume to " + volumeMusique, null, 0, IntPtr.Zero);
        this.Focus();
    }

    void ChangerLangue()
    {
        langue = (langue == "FR") ? "EN" : "FR";
        ActualiserTextesBoutons();
        this.Focus();
    }

    string T(string fr, string en)
    {
        return (langue == "FR") ? fr : en;
    }

    void ActualiserTextesBoutons()
    {
        btnJouer.Text = T("[ JOUER ]", "[ PLAY ]");
        btnParametres.Text = T("[ PARAMETRES ]", "[ SETTINGS ]");
        btnLangue.Text = T("LANGUE : FRANCAIS", "LANGUAGE : ENGLISH");
        btnRetour.Text = T("[ RETOUR ]", "[ BACK ]");
        btnAttaquer.Text = T("[ ATTAQUER ]", "[ FIGHT ]");
        btnParler.Text = T("[ PARLER ]", "[ ACT ]");
        btnSoin.Text = T("[ SOIN (" + soins + ") ]", "[ HEAL (" + soins + ") ]");
        btnRecommencer.Text = T("[ RECOMMENCER ]", "[ RETRY ]");
    }

    void CacherTousLesBoutons()
    {
        foreach (Control c in this.Controls) c.Visible = false;
    }

    void OuvrirTitre()
    {
        etat = "TITRE";
        CacherTousLesBoutons();
        ActualiserTextesBoutons();
        btnJouer.Visible = btnParametres.Visible = true;
        this.Focus();
    }

    void OuvrirParametres()
    {
        etat = "PARAMETRES";
        CacherTousLesBoutons();
        ActualiserTextesBoutons();
        btnVolMoins.Visible = btnVolPlus.Visible = btnLangue.Visible = btnRetour.Visible = true;
        this.Focus();
    }

    void DemarrerCombat()
    {
        LancerMusique();
        RecommencerJeu();
    }

    void CentrerInterface()
    {
        offsetX = (this.ClientSize.Width - 480) / 2;
        offsetY = (this.ClientSize.Height - 430) / 2;

        btnJouer.Location = new Point(offsetX + 140, offsetY + 230);
        btnParametres.Location = new Point(offsetX + 140, offsetY + 285);

        btnVolMoins.Location = new Point(offsetX + 120, offsetY + 165);
        btnVolPlus.Location = new Point(offsetX + 295, offsetY + 165);
        btnLangue.Location = new Point(offsetX + 120, offsetY + 240);
        btnRetour.Location = new Point(offsetX + 150, offsetY + 330);

        btnAttaquer.Location = new Point(offsetX + 35, offsetY + 370);
        btnParler.Location = new Point(offsetX + 180, offsetY + 370);
        btnSoin.Location = new Point(offsetX + 325, offsetY + 370);
        btnRecommencer.Location = new Point(offsetX + 155, offsetY + 370);
    }

    Button CreerBouton(string texte, int largeur, Color couleur)
    {
        Button b = new Button();
        b.Text = texte;
        b.Size = new Size(largeur, 40);
        b.BackColor = Color.Black;
        b.ForeColor = couleur;
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = couleur;
        b.FlatAppearance.BorderSize = 2;
        b.Font = new Font("Courier New", 10, FontStyle.Bold);
        b.TabStop = false;
        return b;
    }

    void ActiverBoutons(bool actif)
    {
        btnAttaquer.Enabled = actif;
        btnParler.Enabled = actif;
        btnSoin.Enabled = actif && soins > 0;
        if (actif) this.Focus();
    }

    void AfficherFinDePartie(string msg)
    {
        messageDialogue = msg;
        etat = "FIN";
        CacherTousLesBoutons();
        btnRecommencer.Visible = btnRecommencer.Enabled = true;
    }

    void RecommencerJeu()
    {
        pvJoueur = pvMaxJoueur; pvEnnemi = pvMaxEnnemi;
        soins = 4; pacifisme = 0; invulnerable = 0;
        projectiles.Clear();
        humeurEnnemi = "NORMAL";
        messageDialogue = T("* Un Squelette au manteau bleu bloque le passage !", "* A Skeleton in a blue coat blocks the way!");
        boiteArene = new Rectangle(30, 175, 420, 145);
        CacherTousLesBoutons();
        ActualiserTextesBoutons();
        btnAttaquer.Visible = btnParler.Visible = btnSoin.Visible = true;
        etat = "MENU";
        ActiverBoutons(true);
    }
    void ActionAttaquer()
    {
        ActiverBoutons(false);
        int degats = rnd.Next(15, 22);
        pvEnnemi = Math.Max(0, pvEnnemi - degats);
        humeurEnnemi = "BLESSE";
        messageDialogue = T("* Vous frappez le Squelette ! -" + degats + " PV.", "* You hit the Skeleton! -" + degats + " HP.");
        etat = "MESSAGE";
        timerTransition = 70;
        prochaineAction = (pvEnnemi <= 0) ? "VICTOIRE_FORCE" : "TOUR_ENNEMI";
        bulleEnnemi = T("Tu vas passer un mauvais quart d'heure...", "You're gonna have a bad time...");
    }

    void ActionParler()
    {
        ActiverBoutons(false);
        pacifisme++;
        etat = "MESSAGE";
        timerTransition = 80;
        if (pacifisme == 1) {
            humeurEnnemi = "CONTENT";
            messageDialogue = T("* Vous dites que sa fourrure a l'air tres douce.", "* You say his fur hood looks very soft.");
            bulleEnnemi = T("Heh, merci. C'est du 100% synthetique.", "Heh, thanks. It's 100% synthetic.");
            prochaineAction = "TOUR_ENNEMI";
        } else if (pacifisme == 2) {
            humeurEnnemi = "NORMAL";
            messageDialogue = T("* Vous lui proposez un chocolat chaud.", "* You offer him a hot chocolate.");
            bulleEnnemi = T("Un dernier test et on y va !", "One last test and we're good!");
            prochaineAction = "TOUR_ENNEMI";
        } else {
            humeurEnnemi = "CONTENT";
            messageDialogue = T("* Le Squelette met les mains dans ses poches et sourit !", "* The Skeleton puts his hands in his pockets and smiles!");
            prochaineAction = "VICTOIRE_PAIX";
        }
    }

    void ActionSoin()
    {
        if (soins <= 0) return;
        ActiverBoutons(false);
        soins--;
        ActualiserTextesBoutons();
        pvJoueur = Math.Min(pvMaxJoueur, pvJoueur + 20);
        messageDialogue = T("* Vous mangez un Pixel Sucre. +20 PV !", "* You eat a Sweet Pixel. +20 HP!");
        bulleEnnemi = T("Tu reprends des forces ?", "Getting your strength back?");
        etat = "MESSAGE";
        timerTransition = 70;
        prochaineAction = "TOUR_ENNEMI";
    }

    void MettreAJour()
    {
        if (!(etat == "FIN" && pvEnnemi <= 0)) compteurRespiration += 0.08;

        if (etat == "MESSAGE") {
            if (--timerTransition <= 0) {
                if (prochaineAction == "VICTOIRE_FORCE") {
                    humeurEnnemi = "KO";
                    AfficherFinDePartie(T("* Le Squelette s'effondre... VICTOIRE !", "* The Skeleton collapses... VICTORY!"));
                } else if (prochaineAction == "VICTOIRE_PAIX") {
                    AfficherFinDePartie(T("* Vous etes devenus amis. VICTOIRE !", "* You became friends. VICTORY!"));
                } else {
                    if (humeurEnnemi != "BLESSE") humeurEnnemi = "NORMAL";
                    boiteArene = new Rectangle(130, 175, 220, 145);
                    ame.X = 232; ame.Y = 237;
                    timerEsquive = 260;
                    etat = "ESQUIVE";
                }
            }
        } else if (etat == "ESQUIVE") {
            if (gauche && ame.X > boiteArene.Left + 4) ame.X -= vitesseAme;
            if (droite && ame.X < boiteArene.Right - 20) ame.X += vitesseAme;
            if (haut && ame.Y > boiteArene.Top + 4) ame.Y -= vitesseAme;
            if (bas && ame.Y < boiteArene.Bottom - 20) ame.Y += vitesseAme;
            if (invulnerable > 0) invulnerable--;

            if (timerEsquive % 14 == 0) {
                Projectile p = new Projectile();
                if (rnd.Next(2) == 0) {
                    p.x = rnd.Next(boiteArene.Left + 5, boiteArene.Right - 15);
                    p.y = boiteArene.Top;
                    p.vy = 2.8f + (float)rnd.NextDouble() * 2f;
                } else {
                    p.x = boiteArene.Left;
                    p.y = rnd.Next(boiteArene.Top + 5, boiteArene.Bottom - 15);
                    p.vx = 2.8f + (float)rnd.NextDouble() * 2f;
                }
                projectiles.Add(p);
            }

            for (int i = projectiles.Count - 1; i >= 0; i--) {
                Projectile p = projectiles[i];
                p.x += p.vx; p.y += p.vy;
                if (invulnerable == 0 && ame.IntersectsWith(new RectangleF(p.x, p.y, 10, 10))) {
                    pvJoueur = Math.Max(0, pvJoueur - 4);
                    invulnerable = 45;
                    if (pvJoueur <= 0) {
                        projectiles.Clear();
                        boiteArene = new Rectangle(30, 175, 420, 145);
                        AfficherFinDePartie(T("* Votre ame s'est brisee... DEFAITE.", "* Your soul shattered... GAME OVER."));
                        this.Invalidate();
                        return;
                    }
                }
                if (p.x > boiteArene.Right || p.y > boiteArene.Bottom) projectiles.RemoveAt(i);
            }

            if (--timerEsquive <= 0) {
                projectiles.Clear();
                humeurEnnemi = "NORMAL";
                boiteArene = new Rectangle(30, 175, 420, 145);
                messageDialogue = T("* Le Squelette garde les mains dans ses poches.", "* The Skeleton keeps his hands in his pockets.");
                etat = "MENU";
                ActiverBoutons(true);
            }
        }
        this.Invalidate();
    }
    void DessinerSquelette(Graphics g, int x, int y, int respY, int respLarg)
    {
        Brush manteauBleu = new SolidBrush(Color.RoyalBlue);
        Pen contourBlanc = new Pen(Color.White, 2);

        g.FillRectangle(Brushes.DimGray, x - 22, y + 72, 18, 22);
        g.FillRectangle(Brushes.DimGray, x + 4, y + 72, 18, 22);
        g.FillEllipse(Brushes.LightPink, x - 26, y + 92, 22, 10);
        g.FillEllipse(Brushes.LightPink, x + 4, y + 92, 22, 10);

        int largM = 76 + (respLarg * 2), hautM = 44 - respY, posYM = y + 32 + respY;
        g.FillRectangle(manteauBleu, x - (largM / 2), posYM, largM, hautM);
        g.DrawRectangle(contourBlanc, x - (largM / 2), posYM, largM, hautM);
        g.FillRectangle(Brushes.White, x - 12, posYM + 4, 24, hautM - 4);

        g.FillRectangle(manteauBleu, x - (largM / 2) - 12, posYM + 4, 12, 32);
        g.DrawRectangle(contourBlanc, x - (largM / 2) - 12, posYM + 4, 12, 32);
        g.FillRectangle(manteauBleu, x + (largM / 2), posYM + 4, 12, 32);
        g.DrawRectangle(contourBlanc, x + (largM / 2), posYM + 4, 12, 32);

        g.FillEllipse(Brushes.WhiteSmoke, x - 44 - respLarg, posYM - 10, 88 + (respLarg * 2), 22);
        g.DrawEllipse(new Pen(Color.LightGray, 2), x - 44 - respLarg, posYM - 10, 88 + (respLarg * 2), 22);

        int posYC = y - 12 + (respY / 2);
        g.FillEllipse(Brushes.White, x - 24, posYC, 48, 38);
        g.FillRectangle(Brushes.White, x - 16, posYC + 26, 32, 16);

        if (humeurEnnemi == "KO") {
            g.DrawString("X   X", new Font("Courier New", 10, FontStyle.Bold), Brushes.Black, x - 19, posYC + 11);
        } else {
            g.FillEllipse(Brushes.Black, x - 15, posYC + 11, 11, 11);
            g.FillEllipse(Brushes.Black, x + 4, posYC + 11, 11, 11);
            g.FillEllipse(Brushes.White, x - 11, posYC + 14, 4, 4);
            if (humeurEnnemi == "BLESSE") g.FillEllipse(Brushes.Cyan, x + 6, posYC + 12, 7, 7);
            else g.FillEllipse(Brushes.White, x + 7, posYC + 14, 4, 4);
        }

        g.FillPolygon(Brushes.Black, new Point[] { new Point(x, posYC + 22), new Point(x - 3, posYC + 27), new Point(x + 3, posYC + 27) });
        g.DrawArc(new Pen(Color.Black, 2), x - 13, posYC + 25, 26, 12, 10, 160);
        g.DrawLine(Pens.Black, x - 6, posYC + 31, x - 6, posYC + 36);
        g.DrawLine(Pens.Black, x, posYC + 32, x, posYC + 37);
        g.DrawLine(Pens.Black, x + 6, posYC + 31, x + 6, posYC + 36);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Font pTitre = new Font("Courier New", 24, FontStyle.Bold);
        Font pNormale = new Font("Courier New", 12, FontStyle.Bold);
        Font pPetite = new Font("Courier New", 9, FontStyle.Bold);

        g.DrawString(T("[ECHAP] Quitter", "[ESC] Quit"), pPetite, Brushes.DimGray, 10, 10);
        g.TranslateTransform(offsetX, offsetY);

        int respY = (int)(Math.Sin(compteurRespiration) * 3);
        int respL = (int)(Math.Cos(compteurRespiration) * 2);

        if (etat == "TITRE") {
            g.DrawString("SKELETON DUEL", pTitre, Brushes.White, 105, 45);
            DessinerSquelette(g, 240, 105, respY, respL);
            return;
        }

        if (etat == "PARAMETRES") {
            g.DrawString(T("PARAMETRES", "SETTINGS"), pTitre, Brushes.White, 130, 50);
            g.DrawString(T("VOLUME MUSIQUE", "MUSIC VOLUME"), pNormale, Brushes.Orange, 165, 130);
            g.DrawString((volumeMusique / 10) + " %", pNormale, Brushes.White, 215, 175);
            return;
        }

        DessinerSquelette(g, 240, 28, respY, respL);

        g.FillRectangle(Brushes.DimGray, 160, 145, 160, 10);
        g.FillRectangle(Brushes.Lime, 160, 145, (int)(160.0 * pvEnnemi / pvMaxEnnemi), 10);

        if (etat == "ESQUIVE") {
            g.FillRectangle(Brushes.White, 315, 25, 145, 65);
            g.DrawString(bulleEnnemi, pPetite, Brushes.Black, new RectangleF(320, 30, 135, 55));
        }

        g.DrawRectangle(new Pen(Color.White, 4), boiteArene);

        if (etat == "MENU" || etat == "MESSAGE" || etat == "FIN") {
            g.DrawString(messageDialogue, pNormale, Brushes.White, new RectangleF(45, 190, 390, 120));
        } else if (etat == "ESQUIVE") {
            if (invulnerable == 0 || invulnerable % 4 < 2) g.FillRectangle(Brushes.Red, ame);
            foreach (Projectile p in projectiles) g.FillRectangle(Brushes.White, p.x, p.y, 10, 10);
        }

        g.DrawString(T("VOUS   PV", "YOU    HP"), pNormale, Brushes.White, 30, 338);
        g.FillRectangle(Brushes.Red, 140, 338, 120, 18);
        g.FillRectangle(Brushes.Yellow, 140, 338, (int)(120.0 * pvJoueur / pvMaxJoueur), 18);
        g.DrawString(pvJoueur + " / " + pvMaxJoueur, pNormale, Brushes.White, 275, 338);
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new CombatRPG());
    }
}
