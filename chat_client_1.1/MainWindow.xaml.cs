using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace chat_client_1._1
{
    public partial class MainWindow : Window
    {
        private System.Windows.Threading.DispatcherTimer searchTimer;
        private bool isUserTyping;
        private TcpClient tcpClient;
        private NetworkStream stream;
        private const string serverIp = "128.116.150.217";
        private const int serverPort = 10688;
        private string clientId = Properties.Settings.Default.ClientID; // Replace with your own client ID
        private string selectedUserId;

        public MainWindow()
        {
            InitializeComponent();

            searchTimer = new System.Windows.Threading.DispatcherTimer();
            searchTimer.Interval = TimeSpan.FromSeconds(0.5);
            searchTimer.Tick += SearchTimer_Tick;

            // Ascolta l'evento TextChanged per segnalare che l'utente sta scrivendo
            SearchTextBox.TextChanged += SearchTextBox_TextChanged;

            // Stabilisce la connessione TCP con il server
            EstablishConnection();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Invia un messaggio di disconnessione al server prima di chiudere l'applicazione
            string disconnectMessage = "DISCONNECT";
            SendMessage(disconnectMessage);

            // Chiudi la connessione TCP
            stream?.Close();
            tcpClient?.Close();
        }

        private void EstablishConnection()
        {
            try
            {
                tcpClient = new TcpClient();
                tcpClient.Connect(serverIp, serverPort);
                stream = tcpClient.GetStream();

                // Invia il client ID al server
                SendMessage(clientId);
            }
            catch (Exception ex)
            {
                // Gestione degli errori
                Console.WriteLine("Si è verificato un errore durante la connessione al server: " + ex.Message);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Imposta il flag che indica che l'utente sta scrivendo
            isUserTyping = true;

            // Riavvia il timer ad ogni modifica del testo
            searchTimer.Stop();
            searchTimer.Start();
        }

        private async void SearchTimer_Tick(object sender, EventArgs e)
        {
            searchTimer.Stop();

            // Se l'utente ha smesso di scrivere, esegui la ricerca
            if (isUserTyping)
            {
                isUserTyping = false;

                string searchTerm = SearchTextBox.Text.Trim();

                // Eseguire la query sul database locale per cercare gli utenti corrispondenti al termine di ricerca
                List<User> localSearchResults = SearchUsersInLocalDatabase(searchTerm);

                // Eseguire la ricerca nel database del server tramite un'API
                List<User> serverSearchResults = await SearchUsersInServerDatabase(searchTerm);

                // Unire i risultati ottenuti da entrambe le ricerche
                List<User> searchResults = localSearchResults.Concat(serverSearchResults).ToList();

                // Visualizzare i risultati nella UserListView
                UserListView.ItemsSource = searchResults;
            }
        }

        private List<User> SearchUsersInLocalDatabase(string searchTerm)
        {
            List<User> searchResults = new List<User>();

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection("Data Source=chat.db"))
                {
                    connection.Open();

                    string query = "SELECT * FROM user WHERE name LIKE @searchTerm OR surname LIKE @searchTerm";
                    using (SQLiteCommand command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@searchTerm", "%" + searchTerm + "%");

                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string userId = reader.GetString(0);
                                string name = reader.GetString(1);
                                string surname = reader.GetString(2);

                                User user = new User(userId, name, surname);
                                searchResults.Add(user);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Gestione degli errori
                Console.WriteLine("Si è verificato un errore durante la ricerca nel database locale: " + ex.Message);
            }

            return searchResults;
        }

        private async Task<List<User>> SearchUsersInServerDatabase(string searchTerm)
        {
            List<User> searchResults = new List<User>();

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string apiUrl = "https://chat.jolly.vm.iacca.ml/api/get_user.php?input=" + searchTerm;
                    HttpResponseMessage response = await client.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        List<UserApiResponse> apiResponse = JsonConvert.DeserializeObject<List<UserApiResponse>>(json);

                        foreach (UserApiResponse apiUser in apiResponse)
                        {
                            User user = new User(apiUser.user_id, apiUser.name, apiUser.surname);
                            searchResults.Add(user);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Gestione degli errori
                Console.WriteLine("Si è verificato un errore durante la ricerca nel database del server: " + ex.Message);
            }

            return searchResults;
        }

        private void UserListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UserListView.SelectedItem is User selectedUser)
            {
                selectedUserId = selectedUser.UserId;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedUserId))
            {
                MessageBox.Show("Selezionare un utente prima di inviare un messaggio.");
                return;
            }

            string messageText = TextBox.Text;

            if (string.IsNullOrEmpty(messageText))
            {
                MessageBox.Show("Inserire un messaggio prima di inviare.");
                return;
            }

            // Invia il messaggio al server
            SendMessage($"{selectedUserId} {clientId} {messageText}");

            // Pulisce il campo di testo del messaggio dopo l'invio
            TextBox.Text = string.Empty;
        }

        private void SendMessage(string message)
        {
            try
            {
                byte[] data = Encoding.ASCII.GetBytes(message);
                stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                // Gestione degli errori
                Console.WriteLine("Si è verificato un errore durante l'invio del messaggio: " + ex.Message);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            // Chiudi la connessione TCP quando si chiude la finestra
            stream?.Close();
            tcpClient?.Close();
        }
    }

    internal class User
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }

        public User(string userId, string name, string surname)
        {
            UserId = userId;
            Name = name;
            Surname = surname;
        }

        public override string ToString()
        {
            return $"{Name} {Surname}";
        }
    }

    internal class UserApiResponse
    {
        public string user_id { get; set; }
        public string name { get; set; }
        public string surname { get; set; }
    }
}
