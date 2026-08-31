using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace WojnyMagazynoweWPF
{
    public partial class MainWindow : Window
    {
        private Player me;
        private GameServer server;
        private GameClient client;

        private int selectedBidAmount = 0;
        private string currentWarehouseType = "drwal";
        private int currentHighestBid = 100;

        public MainWindow()
        {
            InitializeComponent();
            txtName.Text = "Gracz" + new Random().Next(10, 99);
        }

        // ==========================================
        // WŁASNE, WYŚRODKOWANE OKNO KOMUNIKATU
        // ==========================================
        private void ShowGameDialog(string title, string message)
        {
            var dlg = new Window
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                ShowInTaskbar = false
            };

            var border = new Border
            {
                Background = (Brush)FindResource("BrushPanel"),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(28),
                Width = 340,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#505565"))
            };
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, Opacity = 0.5, BlurRadius = 25, ShadowDepth = 6 };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("BrushGold"),
                Margin = new Thickness(0, 0, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("BrushText"),
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            });

            var okBtn = new Button { Content = "OK", Height = 40, Style = (Style)FindResource("GoodButton") };
            okBtn.Click += (s, e) => dlg.Close();
            stack.Children.Add(okBtn);

            border.Child = stack;
            dlg.Content = border;
            dlg.Show();
            dlg.Activate();
        }

        private void LoadAuctionImage(string fileName)
        {
            try
            {
                string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", fileName);
                if (System.IO.File.Exists(fullPath))
                {
                    imgAuctionItem.Source = new BitmapImage(new Uri(fullPath, UriKind.Absolute));
                }
            }
            catch { }
        }

        // ==========================================
        // NAWIGACJA PO PANELACH
        // ==========================================
        private void SwitchPanel(Grid panelToLocate)
        {
            pnlMenu.Visibility = Visibility.Hidden;
            pnlPlayerMenu.Visibility = Visibility.Hidden;
            pnlAuction.Visibility = Visibility.Hidden;
            pnlWarehouseList.Visibility = Visibility.Hidden;
            pnlStorage.Visibility = Visibility.Hidden;
            pnlShop.Visibility = Visibility.Hidden;

            panelToLocate.Visibility = Visibility.Visible;
            UpdateBalanceLabels();
        }

        private void UpdateBalanceLabels()
        {
            if (me != null)
            {
                lblBalance.Text = $"Twój budżet: {me.Balance}$";
                lblShopBalance.Text = $"Twój budżet: {me.Balance}$";
                lblPlayerName.Text = me.Name;
                lblPlayerBalance.Text = $"posiadane $ : {me.Balance}$";
            }
        }

        // ==========================================
        // MENU I LOGOWANIE
        // ==========================================
        private void BtnHost_Click(object sender, RoutedEventArgs e)
        {
            server = new GameServer();
            server.Start();
            ConnectToServer(txtName.Text, txtIp.Text);
        }

        private void BtnJoin_Click(object sender, RoutedEventArgs e)
        {
            ConnectToServer(txtName.Text, txtIp.Text);
        }

        private void ConnectToServer(string name, string ip)
        {
            me = new Player(name);
            UpdateBalanceLabels();

            client = new GameClient();
            client.OnMessageReceived = ProcessNetworkMessage;
            try
            {
                client.Connect(ip);
                SwitchPanel(pnlPlayerMenu);
            }
            catch (Exception)
            {
                ShowGameDialog("Błąd połączenia", "Nie można połączyć z IP: " + ip);
            }
        }

        // ==========================================
        // PANEL GRACZA (Menu po zalogowaniu)
        // ==========================================
        private void BtnGoToAuction_Click(object sender, RoutedEventArgs e)
        {
            // Losujemy tylko nazwę obrazka
            string selectedImage = GetRandomImageName();

            // Wysyłamy informację do serwera RAZEM Z NAZWĄ OBRAZKA (zostanie on odesłany do wszystkich)
            client.Send($"START_AUCTION|{selectedImage}");
        }

     

        private string lastImagePath = "";

        private string GetRandomImageName()
        {
            try
            {
                string folderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");

                if (System.IO.Directory.Exists(folderPath))
                {
                    string[] files = System.IO.Directory.GetFiles(folderPath);
                    List<string> imageFiles = new List<string>();

                    foreach (var file in files)
                    {
                        if (file.EndsWith(".png") || file.EndsWith(".jpg") || file.EndsWith(".jpeg"))
                        {
                            // MEGA WAŻNE: Zapisujemy tylko nazwę pliku (np. "magazyn1.png"), a nie całą ścieżkę z dysku
                            imageFiles.Add(System.IO.Path.GetFileName(file));
                        }
                    }

                    if (imageFiles.Count > 0)
                    {
                        Random rnd = new Random();
                        string selectedFile = "";

                        do
                        {
                            selectedFile = imageFiles[rnd.Next(imageFiles.Count)];
                        }
                        while (selectedFile == lastImagePath && imageFiles.Count > 1);

                        lastImagePath = selectedFile;
                        return selectedFile; // Zwracamy wylosowaną nazwę do wysłania
                    }
                }
            }
            catch { }
            return "";
        }

        private void BtnGoToStorage_Click(object sender, RoutedEventArgs e)
        {
            SwitchPanel(pnlWarehouseList);
            RefreshContainerList();
        }

        private void RefreshContainerList()
        {
            stackContainers.Children.Clear();

            if (me.Containers.Count == 0)
            {
                stackContainers.Children.Add(new TextBlock
                {
                    Text = "Brak magazynów. Wygraj aukcję, aby go zdobyć!",
                    Foreground = (Brush)FindResource("BrushSubText"),
                    FontSize = 15,
                    Margin = new Thickness(0, 10, 0, 0)
                });
                return;
            }

            for (int i = 0; i < me.Containers.Count; i++)
            {
                Container c = me.Containers[i];
                Button btn = new Button
                {
                    Content = $"📦 Kontener {i + 1}\n({c.ItemCount} przedmiotów)",
                    Height = 60,
                    Width = 180,
                    Margin = new Thickness(10),
                    Style = (Style)FindResource("DangerButton")
                };
                btn.Click += (s, e) => OpenContainer(c);
                stackContainers.Children.Add(btn);
            }
        }

        private void OpenContainer(Container c)
        {
            me.Containers.Remove(c);
            lblStorageTitle.Text = $"Zbadaj magazyn ({c.WarehouseType}) — kliknij pudełka ze znakiem zapytania";
            GenerateHiddenItems(c.ItemCount, c.WarehouseType);
            SwitchPanel(pnlStorage);
        }

        // ==========================================
        // LOGIKA AUKCJI
        // ==========================================
        private void BtnPass_Click(object sender, RoutedEventArgs e)
        {
            client.Send($"PASS|{me.Name}");
        }

        private void BtnAddAmount_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null || btn.Content == null) return;

            // Bezpieczne konwertowanie (usuwamy +, spacje)
            string cleanText = btn.Content.ToString().Replace("+", "").Trim();

            if (int.TryParse(cleanText, out int value))
            {
                selectedBidAmount += value;
                lblSelectedAmount.Text = $"Wybrana kwota: {selectedBidAmount}$";
            }
        }

        private void BtnResetBid_Click(object sender, RoutedEventArgs e)
        {
            selectedBidAmount = 0;
            lblSelectedAmount.Text = "Wybrana kwota: 0$";
        }

        private void BtnStartBid_Click(object sender, RoutedEventArgs e)
        {
            if (selectedBidAmount <= 0) return;
            int newTotalBid = currentHighestBid + selectedBidAmount;

            if (me.Balance >= newTotalBid)
            {
                client.Send($"BID|{newTotalBid}|{me.Name}");
                BtnResetBid_Click(null, null);
            }
            else
            {
                ShowGameDialog("Za mało środków", "Nie masz tyle pieniędzy w budżecie!");
            }
        }

        private void ProcessNetworkMessage(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                string[] parts = msg.Split('|');

                if (parts[0] == "START_AUCTION")
                {
                    if (parts.Length > 4 && !string.IsNullOrEmpty(parts[4]))
                    {
                        LoadAuctionImage(parts[4]);
                    }

                    if (parts.Length > 5 && !string.IsNullOrEmpty(parts[5]))
                    {
                        currentWarehouseType = parts[5];
                    }

                    // Odczytanie początkowej licytacji z wiadomości
                    if (parts.Length > 1 && int.TryParse(parts[1], out int startBid))
                    {
                        currentHighestBid = startBid;
                        lblCurrentBid.Text = $"{currentHighestBid}$";
                    }

                    // Wyświetlenie "Aukcja rozpoczęta"
                    if (parts.Length > 3)
                    {
                        listHistory.Items.Clear();
                        string[] historyItems = parts[3].Split(';');
                        foreach (var h in historyItems)
                        {
                            if (!string.IsNullOrWhiteSpace(h)) listHistory.Items.Add(h);
                        }
                    }

                    switch (currentWarehouseType)
                    {
                        case "drwal": lblAuctionHint.Text = "Magazyn do licytowania — podpowiedź: należał do drwala"; break;
                        case "górnik": lblAuctionHint.Text = "Magazyn do licytowania — podpowiedź: należał do górnika"; break;
                        case "mechanik": lblAuctionHint.Text = "Magazyn do licytowania — podpowiedź: należał do mechanika"; break;
                        case "gracz_komputerowy": lblAuctionHint.Text = "Magazyn do licytowania — podpowiedź: należał do gracza komputerowego"; break;
                        case "kucharz": lblAuctionHint.Text = "Magazyn do licytowania — podpowiedź: należał do kucharza"; break;
                        case "rybak": lblAuctionHint.Text = "Magazyn do licytowania — podpowiedź: należał do rybaka"; break;
                        case "ogrodnik": lblAuctionHint.Text = "Magazyn do licytowania — podpowiedź: należał do ogrodnika"; break;
                        case "lekarz": lblAuctionHint.Text = "Magazyn do licytowania — podpowiedź: należał do lekarza"; break;
                        case "zolnierz": lblAuctionHint.Text = "Magazyn do licytowania — podpowiedź: należał do żołnierza"; break;
                        case "elektryk": lblAuctionHint.Text = "Magazyn do licytowania — podpowiedź: należał do elektryka"; break;
                        case "fotograf": lblAuctionHint.Text = "Magazyn do licytowania — podpowiedź: należał do fotografa"; break;
                        case "muzyk_dj": lblAuctionHint.Text = "Magazyn do licytowania — podpowiedź: należał do muzyka/DJ-a"; break;
                        case "wlamywacz": lblAuctionHint.Text = "Magazyn do licytowania — podpowiedź: należał do włamywacza"; break;
                    }

                    SwitchPanel(pnlAuction);
                }
                else if (parts[0] == "STATE")
                {
                    // Ktoś (lub my) zalicytował - odświeżamy widok
                    currentHighestBid = int.Parse(parts[1]);
                    lblCurrentBid.Text = $"{currentHighestBid}$";

                    listHistory.Items.Clear();
                    string[] historyItems = parts[3].Split(';');
                    foreach (var h in historyItems)
                    {
                        if (!string.IsNullOrWhiteSpace(h)) listHistory.Items.Add(h);
                    }
                }
                else if (parts[0] == "END_AUCTION")
                {
                    string winner = parts[1];
                    int finalPrice = int.Parse(parts[2]);
                    string passer = parts.Length > 3 ? parts[3] : null;

                    if (winner == "Brak")
                    {
                        ShowGameDialog("Koniec Aukcji", "Nikt nie złożył oferty! Magazyn przepadł, nikt go nie wygrał.");
                    }
                    else if (winner == me.Name)
                    {
                        me.Balance -= finalPrice;
                        me.Containers.Add(new Container(new Random().Next(3, 7), currentWarehouseType));
                        ShowGameDialog("Zwycięstwo!", $"Wygrałeś aukcję za {finalPrice}$!\nNowy magazyn czeka na otwarcie.");
                    }
                    else if (passer == me.Name)
                    {
                        ShowGameDialog("Koniec Aukcji", $"Spasowałeś. Aukcję wygrał: {winner} za {finalPrice}$");
                    }
                    else
                    {
                        ShowGameDialog("Koniec Aukcji", $"Koniec! Zwycięzca: {winner} za {finalPrice}$");
                    }

                    SwitchPanel(pnlPlayerMenu);
                }
            });
        }

        // ==========================================
        // MAGAZYN (ZBIERANIE)
        // ==========================================
        private string activeWarehouseType = "drwal";

        private void GenerateHiddenItems(int itemsCount, string warehouseType)
        {
            activeWarehouseType = warehouseType;
            canvasStorage.Children.Clear();
            Random rnd = new Random();

            for (int i = 0; i < itemsCount; i++)
            {
                Button hiddenBox = new Button
                {
                    Content = "❓",
                    Width = 64,
                    Height = 64,
                    Background = new SolidColorBrush(Color.FromRgb(0x5B, 0x8C, 0xFF)),
                    Foreground = Brushes.White,
                    FontSize = 24,
                    FontWeight = FontWeights.Bold,
                    Opacity = 0
                };

                Canvas.SetLeft(hiddenBox, rnd.Next(10, 620));
                Canvas.SetTop(hiddenBox, rnd.Next(10, 260));

                hiddenBox.Click += HiddenBox_Click;
                canvasStorage.Children.Add(hiddenBox);

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)) { BeginTime = TimeSpan.FromMilliseconds(i * 80) };
                hiddenBox.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        private void HiddenBox_Click(object sender, RoutedEventArgs e)
        {
            Button box = sender as Button;
            box.IsEnabled = false;

            Random rnd = new Random();
            Item foundItem = null;

            switch (activeWarehouseType)
            {
                case "drwal":
                    {
                        int t = rnd.Next(1, 6);
                        if (t == 1) foundItem = new Item("🪓 Stara Siekiera", 100, 300);
                        else if (t == 2) foundItem = new Item("🪵 Kloc drewna", 50, 150);
                        else if (t == 3) foundItem = new Item("🧥 Flanelowa koszula", 80, 200);
                        else if (t == 4) foundItem = new Item("🪚 Piła ręczna", 60, 180);
                        else foundItem = new Item("🥾 Buty robocze", 70, 220);
                        break;
                    }
                case "górnik":
                    {
                        int t = rnd.Next(1, 6);
                        if (t == 1) foundItem = new Item("⛑️ Stary Kask", 150, 400);
                        else if (t == 2) foundItem = new Item("💎 Bryłka węgla z diamentem", 800, 2500);
                        else if (t == 3) foundItem = new Item("🔦 Kopalniana lampa", 300, 700);
                        else if (t == 4) foundItem = new Item("⛏️ Kilof górniczy", 100, 350);
                        else foundItem = new Item("🎒 Plecak górniczy", 60, 180);
                        break;
                    }
                case "mechanik":
                    {
                        int t = rnd.Next(1, 6);
                        if (t == 1) foundItem = new Item("🔧 Zestaw kluczy", 200, 500);
                        else if (t == 2) foundItem = new Item("🛞 Stara opona", 100, 250);
                        else if (t == 3) foundItem = new Item("🛢️ Kanister z olejem", 40, 120);
                        else if (t == 4) foundItem = new Item("🧰 Skrzynka narzędziowa", 150, 400);
                        else foundItem = new Item("🔋 Akumulator samochodowy", 100, 300);
                        break;
                    }
                case "gracz_komputerowy":
                    {
                        int t = rnd.Next(1, 6);
                        if (t == 1) foundItem = new Item("🎮 Karta graficzna RTX", 1200, 4500);
                        else if (t == 2) foundItem = new Item("🧠 Kość RAM 32GB", 300, 700);
                        else if (t == 3) foundItem = new Item("⌨️ Klawiatura Razer", 250, 600);
                        else if (t == 4) foundItem = new Item("🔊 Głośniki gamingowe", 150, 400);
                        else foundItem = new Item("🖱️ Mysz gamingowa", 100, 300);
                        break;
                    }
                case "kucharz":
                    {
                        int t = rnd.Next(1, 6);
                        if (t == 1) foundItem = new Item("🔪 Nóż szefa kuchni", 100, 350);
                        else if (t == 2) foundItem = new Item("🍳 Żeliwna patelnia", 80, 250);
                        else if (t == 3) foundItem = new Item("🧂 Zestaw przypraw", 30, 90);
                        else if (t == 4) foundItem = new Item("👨‍🍳 Fartuch kuchenny", 40, 100);
                        else foundItem = new Item("📖 Zeszyt z przepisami", 50, 150);
                        break;
                    }
                case "rybak":
                    {
                        int t = rnd.Next(1, 6);
                        if (t == 1) foundItem = new Item("🎣 Wędka spinningowa", 150, 500);
                        else if (t == 2) foundItem = new Item("🪱 Zestaw przynęt", 20, 60);
                        else if (t == 3) foundItem = new Item("👢 Wodery rybackie", 100, 300);
                        else if (t == 4) foundItem = new Item("🧊 Przenośna lodówka", 60, 180);
                        else foundItem = new Item("🎣 Kołowrotek wędkarski", 120, 400);
                        break;
                    }
                case "ogrodnik":
                    {
                        int t = rnd.Next(1, 6);
                        if (t == 1) foundItem = new Item("✂️ Sekator ogrodniczy", 40, 120);
                        else if (t == 2) foundItem = new Item("🌱 Worek nasion", 15, 50);
                        else if (t == 3) foundItem = new Item("🧤 Rękawice robocze", 20, 60);
                        else if (t == 4) foundItem = new Item("🪴 Doniczka ceramiczna", 30, 90);
                        else foundItem = new Item("🚿 Konewka ogrodowa", 25, 70);
                        break;
                    }
                case "lekarz":
                    {
                        int t = rnd.Next(1, 6);
                        if (t == 1) foundItem = new Item("🩺 Stetoskop", 100, 300);
                        else if (t == 2) foundItem = new Item("💊 Apteczka pierwszej pomocy", 60, 180);
                        else if (t == 3) foundItem = new Item("🥼 Fartuch lekarski", 50, 150);
                        else if (t == 4) foundItem = new Item("📋 Karta pacjenta", 20, 60);
                        else foundItem = new Item("💉 Zestaw strzykawek", 30, 90);
                        break;
                    }
                case "zolnierz":
                    {
                        int t = rnd.Next(1, 6);
                        if (t == 1) foundItem = new Item("🪖 Hełm bojowy", 200, 600);
                        else if (t == 2) foundItem = new Item("🦺 Kamizelka kuloodporna", 500, 1500);
                        else if (t == 3) foundItem = new Item("🍶 Manierka wojskowa", 20, 60);
                        else if (t == 4) foundItem = new Item("🔪 Nóż wojskowy", 80, 250);
                        else foundItem = new Item("🎒 Plecak taktyczny", 100, 300);
                        break;
                    }
                case "elektryk":
                    {
                        int t = rnd.Next(1, 6);
                        if (t == 1) foundItem = new Item("🔧 Miernik uniwersalny", 100, 350);
                        else if (t == 2) foundItem = new Item("🪛 Zestaw śrubokrętów", 40, 120);
                        else if (t == 3) foundItem = new Item("🔌 Rolka kabla", 30, 90);
                        else if (t == 4) foundItem = new Item("🧯 Gaśnica proszkowa", 60, 180);
                        else foundItem = new Item("💡 Zestaw żarówek LED", 20, 60);
                        break;
                    }
                case "fotograf":
                    {
                        int t = rnd.Next(1, 6);
                        if (t == 1) foundItem = new Item("📷 Aparat bezlusterkowy", 1500, 5000);
                        else if (t == 2) foundItem = new Item("🔭 Obiektyw 50mm", 400, 1200);
                        else if (t == 3) foundItem = new Item("🎥 Statyw fotograficzny", 80, 250);
                        else if (t == 4) foundItem = new Item("💾 Karta pamięci 128GB", 40, 100);
                        else foundItem = new Item("💡 Lampa błyskowa", 100, 300);
                        break;
                    }
                case "muzyk_dj":
                    {
                        int t = rnd.Next(1, 6);
                        if (t == 1) foundItem = new Item("🎧 Słuchawki DJ", 200, 600);
                        else if (t == 2) foundItem = new Item("🎛️ Kontroler MIDI", 500, 1800);
                        else if (t == 3) foundItem = new Item("🎸 Gitara elektryczna", 800, 2500);
                        else if (t == 4) foundItem = new Item("🎤 Mikrofon studyjny", 150, 450);
                        else foundItem = new Item("💿 Płyta winylowa", 30, 90);
                        break;
                    }
                default:
                    {
                        int t = rnd.Next(1, 6);
                        if (t == 1) foundItem = new Item("🗝️ Zestaw wytrychów", 100, 400);
                        else if (t == 2) foundItem = new Item("🔦 Latarka taktyczna", 30, 90);
                        else if (t == 3) foundItem = new Item("🧤 Rękawiczki bez śladów", 15, 40);
                        else if (t == 4) foundItem = new Item("🎭 Czarna maska", 20, 60);
                        else foundItem = new Item("📡 Skaner częstotliwości", 200, 600);
                        break;
                    }
            }

            me.Inventory.Add(foundItem);
            RefreshShopList();

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, a) => box.Visibility = Visibility.Collapsed;
            box.BeginAnimation(UIElement.OpacityProperty, fadeOut);

            ShowGameDialog("Znaleziono przedmiot!", $"Znalazłeś: {foundItem.Name}\n(Wartość: {foundItem.Value}$)");
        }

        private void BtnLeaveStorage_Click(object sender, RoutedEventArgs e)
        {
            SwitchPanel(pnlWarehouseList);
            RefreshContainerList();
        }

        // ==========================================
        // SKLEP (SPRZEDAŻ)
        // ==========================================
        private void BtnGoToShop_Click(object sender, RoutedEventArgs e)
        {
            SwitchPanel(pnlShop);
        }

        private void BtnBackToAuction_Click(object sender, RoutedEventArgs e)
        {
            SwitchPanel(pnlPlayerMenu);
        }

        private void RefreshShopList()
        {
            listInventory.Items.Clear();
            if (me.Inventory.Count == 0)
            {
                listInventory.Items.Add("(pusto — wygraj aukcję, żeby coś zdobyć)");
                return;
            }
            foreach (var item in me.Inventory)
            {
                listInventory.Items.Add($"{item.Name}  —  wartość: {item.Value}$");
            }
        }

        private void BtnSellAll_Click(object sender, RoutedEventArgs e)
        {
            if (me.Inventory.Count == 0)
            {
                ShowGameDialog("Pusty magazyn", "Nie masz nic do sprzedania!");
                return;
            }

            int totalEarned = 0;
            foreach (var item in me.Inventory)
            {
                me.Balance += item.Value;
                totalEarned += item.Value;
            }
            me.Inventory.Clear();
            UpdateBalanceLabels();
            RefreshShopList();

            ShowGameDialog("Sukces!", $"Zarobiłeś {totalEarned}$ ze sprzedaży wszystkich przedmiotów!");
        }
    }

    // ==========================================
    // KLASY OBIEKTOWE I SIECIOWE
    // ==========================================
    public class Item
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public Item(string name, int min, int max)
        {
            Name = name;
            Value = new Random().Next(min, max + 1);
        }
    }

    public class Container
    {
        public int ItemCount { get; set; }
        public string WarehouseType { get; set; }

        public Container(int itemCount, string type)
        {
            ItemCount = itemCount;
            WarehouseType = type;
        }
    }

    public class Player
    {
        public string Name { get; set; }
        public int Balance { get; set; }
        public List<Item> Inventory { get; set; }
        public List<Container> Containers { get; set; }
        public Player(string name)
        {
            Name = name;
            Balance = 10000;
            Inventory = new List<Item>();
            Containers = new List<Container>();
        }
    }

    public class GameServer
    {
        private TcpListener listener;
        private List<TcpClient> clients = new List<TcpClient>();
        public bool IsRunning = false;

        public int CurrentBid = 100;
        public string HighestBidder = "Brak";
        private List<string> history = new List<string>();

        public void Start()
        {
            listener = new TcpListener(IPAddress.Any, 8888);
            listener.Start();
            IsRunning = true;

            new Thread(() =>
            {
                while (IsRunning && clients.Count < 4)
                {
                    try
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        clients.Add(client);
                        new Thread(() => HandleClient(client)).Start();
                    }
                    catch { }
                }
            })
            { IsBackground = true }.Start();
        }

        private void HandleClient(TcpClient client)
        {
            StreamReader reader = new StreamReader(client.GetStream());
            while (IsRunning)
            {
                try
                {
                    string msg = reader.ReadLine();
                    if (msg != null) ProcessMessage(msg);
                }
                catch { break; }
            }
        }

        private void ProcessMessage(string msg)
        {
            string[] parts = msg.Split('|');
            if (parts[0] == "BID")
            {
                int bidAmount = int.Parse(parts[1]);
                string bidderName = parts[2];
                if (bidAmount > CurrentBid)
                {
                    CurrentBid = bidAmount;
                    HighestBidder = bidderName;

                    history.Insert(0, $"{bidderName} dał {bidAmount}$");
                    if (history.Count > 5) history.RemoveAt(5);

                    string histStr = string.Join(";", history);
                    Broadcast($"STATE|{CurrentBid}|{HighestBidder}|{histStr}");
                }
            }
            else if (parts[0] == "START_AUCTION")
            {
                string imageName = parts.Length > 1 ? parts[1] : "";
                Random rnd = new Random();
                int randomStartBid = rnd.Next(2, 11) * 50;

                string[] types = { "drwal", "górnik", "mechanik", "gracz_komputerowy", "kucharz",
                    "rybak", "ogrodnik", "lekarz", "zolnierz", "elektryk",
                    "fotograf", "muzyk_dj", "wlamywacz" };

                string selectedType = types[rnd.Next(types.Length)];

                CurrentBid = randomStartBid;
                HighestBidder = "Brak";
                history.Clear();
                history.Insert(0, $"Aukcja rozpoczęta ({CurrentBid}$)");

                Broadcast($"START_AUCTION|{CurrentBid}|{HighestBidder}|{string.Join(";", history)}|{imageName}|{selectedType}");
            }
            else if (parts[0] == "PASS")
            {
                string passer = parts[1];
                Broadcast($"END_AUCTION|{HighestBidder}|{CurrentBid}|{passer}");
            }
            else if (parts[0] == "END_AUCTION")
            {
                Broadcast($"END_AUCTION|{HighestBidder}|{CurrentBid}");
            }
        }

        public void Broadcast(string msg)
        {
            foreach (var c in clients)
            {
                try
                {
                    StreamWriter writer = new StreamWriter(c.GetStream()) { AutoFlush = true };
                    writer.WriteLine(msg);
                }
                catch { }
            }
        }
    }

    public class GameClient
    {
        private TcpClient client;
        private StreamWriter writer;
        private StreamReader reader;
        public Action<string> OnMessageReceived;

        public void Connect(string ip)
        {
            client = new TcpClient(ip, 8888);
            writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
            reader = new StreamReader(client.GetStream());

            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        string msg = reader.ReadLine();
                        if (msg != null) OnMessageReceived?.Invoke(msg);
                    }
                    catch { break; }
                }
            })
            { IsBackground = true }.Start();
        }

        public void Send(string msg)
        {
            writer?.WriteLine(msg);
        }
    }
}