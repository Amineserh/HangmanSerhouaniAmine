using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Media;
using System.Windows.Threading;
using System.IO;
// 🔑 CORRECTION 1 : Ajout de la directive using pour BitmapImage et Uri
using System.Windows.Media.Imaging;

namespace PenduWPF
{
    // Enum pour les niveaux de difficulté
    public enum NiveauDifficulte
    {
        Facile = 1,
        Intermediaire = 2,
        Hardcore = 3
    }

    public partial class MainWindow : Window
    {
        // Propriétés de jeu
        private string MotSecret;
        private char[] MotMasqueArray;
        private int ViesRestantes;
        private NiveauDifficulte NiveauActuel;
        private readonly DispatcherTimer TimerJeu = new DispatcherTimer();
        private TimeSpan TempsRestant;
        private MediaPlayer MusiqueFondPlayer = new MediaPlayer();

        // 🔑 CORRECTION 2 : Les chemins de fichiers et indices utilisent maintenant des guillemets doubles (string)
        private readonly (int Vies, int TempsInitialSeconds, int BonusSec, int MalusSec, string Musique, string Image, string Indice, string[] Mots)[] Configs = new (int, int, int, int, string, string, string, string[])[]
        {
            // Index 0: Non utilisé (Facile = 1, etc.)
            default,
            // Index 1: Facile
            (7, 50, 5, 0, @"son\AMBForst_Foret (ID 0100)_LS.wav", @"image\beau-paysage-de-foret-d-automne.jpg", "Un mot plutôt court ou très commun.", new string[] { "XAML", "CONSOLE", "PROJET", "BTS", "RÉSEAUX", "VISUEL", "INTERFACE" }),
            // Index 2: Intermédiaire
            (5, 40, 2, 0, @"son\futuristic-funshine-lead-spark_110bpm_A_minor.wav", @"image\prise-de-vue-verticale-du-chateau-et-de-la-cathedrale-d-arundel-a-partir-d-une-belle-arche-couverte-de-feuillage-vert.jpg", "Un terme technique ou un concept de base.", new string[] { "ORDINATEUR", "ALGORITHME", "DEVELOPPEMENT", "INFORMATIQUE", "ARCHITECTURE" }),
            // Index 3: Hardcore
            (3, 15, 3, 3, @"son\hard-scream-for-metal-rage-phonk_111bpm_F_minor.wav", @"image\paysage-inspire-d-un-jeu-video-mythique-avec-une-ville-souterraine.jpg", "Un terme très spécifique ou avancé.", new string[] { "PROGRAMMATION", "APPRENTISSAGE", "CYBERSÉCURITÉ" })
        };

        // --- SON : Constantes pour les chemins de fichiers d'événements ---
        private const string SON_CLIC = @"son\CMPTKey_Souris raspberry simple clic (ID 1735)_LS.wav";
        private const string SON_VICTOIRE = @"son\victory-sound_130bpm_F_major.wav";
        private const string SON_DEFAITE = @"son\goose-animal-sound-fx.wav";

        private readonly SoundPlayer soundPlayer = new SoundPlayer();

        // Dessins ASCII (inchangé, 0 Vies Perdues à 7 Vies Perdues)
        private readonly string[] PenduDessins = new string[]
        {
            // 0 Vies Perdues (7 restantes)
            "_________  \n|/       |  \n|          \n|          \n|          \n|_________",
            // 1 Vie Perdue (6 restantes)
            "_________  \n|/       |  \n|       ( ) \n|          \n|          \n|_________",
            // 2 Vies Perdues (5 restantes)
            "_________  \n|/       |  \n|       ( ) \n|        |  \n|          \n|_________",
            // 3 Vies Perdues (4 restantes)
            "_________  \n|/       |  \n|       ( ) \n|       /|  \n|          \n|_________",
            // 4 Vies Perdues (3 restantes)
            "_________  \n|/       |  \n|       ( ) \n|       /|\\ \n|          \n|_________",
            // 5 Vies Perdues (2 restantes)
            "_________  \n|/       |  \n|       ( ) \n|       /|\\ \n|        |  \n|_________",
            // 6 Vies Perdues (1 restante)
            "_________  \n|/       |  \n|       ( ) \n|       /|\\ \n|       /|  \n|_________",
            // 7 Vies Perdues (0 restantes) - Défaite
            "_________  \n|/       |  \n|       ( ) \n|       /|\\ \n|       /|\\ \n|_________"
        };


        public MainWindow()
        {
            InitializeComponent();

            // Configuration du Timer
            TimerJeu.Interval = TimeSpan.FromSeconds(1);
            TimerJeu.Tick += TimerJeu_Tick;
        }

        // --- LOGIQUE DU TIMER ---
        private void TimerJeu_Tick(object sender, EventArgs e)
        {
            if (TempsRestant.TotalSeconds > 0)
            {
                TempsRestant = TempsRestant.Subtract(TimeSpan.FromSeconds(1));
                TimerTextBlock.Text = $"Temps: {TempsRestant.Minutes:D2}:{TempsRestant.Seconds:D2}";
            }
            else
            {
                // Temps écoulé : Défaite
                TimerJeu.Stop();
                ViesRestantes = 0; // Force la défaite
                MettreAJourAffichage();
                VerifierFinDePartie();
            }
        }

        // --- GESTION DES BOUTONS DE DIFFICULTÉ ---
        private void DifficultyButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            int niveau = int.Parse(btn.Tag.ToString());
            NiveauActuel = (NiveauDifficulte)niveau;

            InitialiserJeu();

            // Passer à l'onglet du jeu
            GameTab.IsEnabled = true;
            MainTabControl.SelectedIndex = 1;
            DifficultyTab.IsEnabled = false; // Désactiver le retour au choix de difficulté en cours de jeu
        }

        // --- LOGIQUE DU JEU ---

        private void InitialiserJeu()
        {
            var config = Configs[(int)NiveauActuel];

            // 1. Initialisation des vies
            ViesRestantes = config.Vies;

            // 2. Choisir un mot aléatoire en fonction du niveau
            Random random = new Random();
            MotSecret = config.Mots[random.Next(config.Mots.Length)].ToUpperInvariant(); // Assure la majuscule

            // 3. Initialiser le mot masqué avec des '_'
            MotMasqueArray = new string('_', MotSecret.Length).ToCharArray();

            // 4. Configurer le Timer
            TempsRestant = TimeSpan.FromSeconds(config.TempsInitialSeconds);
            TimerTextBlock.Text = $"Temps: {TempsRestant.Minutes:D2}:{TempsRestant.Seconds:D2}";
            TimerJeu.Start();

            // 5. Configurer les éléments visuels et sonores
            MettreAJourFondEtMusique(config.Image, config.Musique);
            IndiceTextBlock.Text = $"Indice: {config.Indice}";
            ResultatTextBlock.Text = "C'est parti !";
            RejouerButton.Visibility = Visibility.Collapsed;

            // 6. Mettre à jour l'affichage et les boutons
            MettreAJourAffichage();
            CreerBoutonsAlphabet();
        }

        private void MettreAJourFondEtMusique(string imagePath, string musiquePath)
        {
            // --- 1. Gestion de la Musique (pour ne pas la casser) ---
            try
            {
                MusiqueFondPlayer.Stop();
                string fullMusiquePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, musiquePath);
                MusiqueFondPlayer.Open(new Uri(fullMusiquePath, UriKind.Absolute));
                MusiqueFondPlayer.MediaEnded += (s, e) => { MusiqueFondPlayer.Position = TimeSpan.Zero; MusiqueFondPlayer.Play(); }; // Boucle
                MusiqueFondPlayer.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lecture musique : {ex.Message}");
            }

            // --- 2. Mise à jour de l'image de fond (avec débogage) ---
            string fullImagePath = "";
            try
            {
                // Chemin complet que le programme essaie de lire
                fullImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imagePath);

                // 🚨 POINT DE VÉRIFICATION 1 : Affiche le chemin complet dans la fenêtre Sortie/Output de Visual Studio
                Console.WriteLine($"Tentative de chargement de l'image à partir de : {fullImagePath}");

                // Tente de charger l'image
                BackgroundImage.ImageSource = new BitmapImage(new Uri(fullImagePath, UriKind.Absolute));
            }
            catch (Exception ex)
            {
                // 🚨 POINT DE VÉRIFICATION 2 : Si le chargement échoue, afficher l'erreur
                Console.WriteLine($"ERREUR DE CHARGEMENT D'IMAGE : {ex.Message}");
                Console.WriteLine($"Chemin d'accès au fichier échoué : {fullImagePath}");

                // Fallback visuel
                GameGrid.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x19, 0x19, 0x70));

                // Vous pouvez décommenter ceci pour avoir une alerte visible :
                // MessageBox.Show($"Échec du chargement de l'image. Vérifiez le chemin : {fullImagePath}", "Erreur Image", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MettreAJourAffichage()
        {
            MotAfficheTextBlock.Text = string.Join(" ", MotMasqueArray);

            // Affichage des vies en cœurs (selon la vie max du niveau)
            int viesMax = Configs[(int)NiveauActuel].Vies;
            // Ligne corrigée : Utilise string.Concat et Enumerable.Repeat pour répéter l'emoji (qui est une chaîne)
            string coeursAffiches = string.Concat(Enumerable.Repeat("❤️", ViesRestantes)) +
                                    string.Concat(Enumerable.Repeat("🖤", viesMax - ViesRestantes));
            StatutCoeursTextBlock.Text = $"Vies : {coeursAffiches}";

            // Mise à jour du dessin du pendu
            int viesPerdues = viesMax - ViesRestantes;

            // Sécurité pour ne pas dépasser la taille du tableau PenduDessins (qui est basé sur 7 erreurs max)
            int indexDessin = (int)Math.Min(viesPerdues, PenduDessins.Length - 1);
            PenduDessinTextBlock.Text = PenduDessins[indexDessin];

            VerifierFinDePartie();
        }

        private void VerifierFinDePartie()
        {
            bool gagne = new string(MotMasqueArray) == MotSecret;
            bool perdu = ViesRestantes <= 0 || TempsRestant.TotalSeconds <= 0;

            if (gagne || perdu)
            {
                TimerJeu.Stop();
                MusiqueFondPlayer.Stop();
                DesactiverTousBoutons();
                RejouerButton.Visibility = Visibility.Visible;
                DifficultyTab.IsEnabled = true; // Réactiver l'accès au choix de la difficulté

                if (gagne)
                {
                    ResultatTextBlock.Text = "🎉 VICTOIRE !";
                    ResultatTextBlock.Foreground = Brushes.LightGreen;
                    JouerSon(SON_VICTOIRE);
                }
                else // Défaite
                {
                    ResultatTextBlock.Text = $"💀 DÉFAITE ! Le mot était : {MotSecret}";
                    ResultatTextBlock.Foreground = Brushes.Red;
                    MotAfficheTextBlock.Text = string.Join(" ", MotSecret.ToCharArray());
                    MotAfficheTextBlock.Foreground = Brushes.Red;
                    JouerSon(SON_DEFAITE);
                }
            }
        }

        private void CreerBoutonsAlphabet()
        {
            AlphabetPanel.Children.Clear();
            for (char c = 'A'; c <= 'Z'; c++)
            {
                Button btn = new Button
                {
                    Content = c.ToString(),
                    Style = (Style)this.FindResource("LettreButtonStyle"),
                    Tag = c
                };
                btn.Click += LettreButton_Click;
                AlphabetPanel.Children.Add(btn);
            }
        }

        private void LettreButton_Click(object sender, RoutedEventArgs e)
        {
            if (TimerJeu.IsEnabled == false) return; // Ne rien faire si le jeu est fini

            JouerSon(SON_CLIC);

            Button boutonClique = (Button)sender;
            char lettre = (char)boutonClique.Tag;
            bool lettreTrouvee = false;
            var config = Configs[(int)NiveauActuel];

            for (int i = 0; i < MotSecret.Length; i++)
            {
                if (MotSecret[i] == lettre)
                {
                    MotMasqueArray[i] = lettre;
                    lettreTrouvee = true;
                }
            }

            boutonClique.IsEnabled = false;

            if (lettreTrouvee)
            {
                boutonClique.Background = Brushes.LightGreen;
                // BONUS de temps
                if (config.BonusSec > 0)
                {
                    TempsRestant = TempsRestant.Add(TimeSpan.FromSeconds(config.BonusSec));
                    ResultatTextBlock.Text = $"+{config.BonusSec} secondes !";
                }
            }
            else
            {
                ViesRestantes--;
                boutonClique.Background = Brushes.Red;
                // MALUS de temps (seulement en Hardcore dans votre config)
                if (config.MalusSec > 0)
                {
                    TempsRestant = TempsRestant.Subtract(TimeSpan.FromSeconds(config.MalusSec));
                    // S'assurer que le temps ne passe pas en négatif à cause du malus
                    if (TempsRestant.TotalSeconds < 0) TempsRestant = TimeSpan.Zero;
                    ResultatTextBlock.Text = $"-{config.MalusSec} secondes ! Attention !";
                }
                else
                {
                    ResultatTextBlock.Text = "Mauvaise lettre !";
                }
            }

            MettreAJourAffichage();
        }

        private void DesactiverTousBoutons()
        {
            foreach (UIElement element in AlphabetPanel.Children)
            {
                if (element is Button btn)
                {
                    btn.IsEnabled = false;
                }
            }
        }

        // --- GESTION DES SONS (méthode unique pour les sons d'événement) ---
        private void JouerSon(string sonPath)
        {
            try
            {
                // Utilise SoundPlayer pour les sons d'événements courts
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, sonPath);
                soundPlayer.SoundLocation = fullPath;
                soundPlayer.Load();
                soundPlayer.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la lecture du son {sonPath}: {ex.Message}");
            }
        }

        // --- BOUTON REJOUER (retour au choix de la difficulté) ---
        private void RejouerButton_Click(object sender, RoutedEventArgs e)
        {
            // Arrêter tout avant de revenir au menu
            TimerJeu.Stop();
            MusiqueFondPlayer.Stop();
            MainTabControl.SelectedIndex = 0;
            GameTab.IsEnabled = false;
            DifficultyTab.IsEnabled = true;
            ResultatTextBlock.Text = "Choisissez votre prochain défi !";
        }
    }
}
