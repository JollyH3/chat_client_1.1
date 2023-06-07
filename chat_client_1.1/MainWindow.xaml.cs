using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Windows.Documents;

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

            CreateMessageTable();

            GetMyMessagesFromServer();
            

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
                Task.Run(async () =>
                {
                    await ReceiveMessagesFromServer();
                });
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
                List<Message>Messages = new List<Message>();

                selectedUserId = selectedUser.UserId;

                Messages = GetMessagesFromDatabase(selectedUserId);
                

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

            Message message = new Message
            {
                SenderId = clientId,
                ReceiverId = selectedUserId,
                Content = messageText,
                Timestamp = DateTime.Now,
            };

            InsertMessageIntoDatabase(message);


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

        //tabella per i messaggi
        private void CreateMessageTable()
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection("Data Source=chat.db"))
                {
                    connection.Open();

                    string createTableQuery = @"CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                idSender TEXT,
                idReceiver TEXT,
                message TEXT,
                timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
            )";

                    using (SQLiteCommand command = new SQLiteCommand(createTableQuery, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Gestione degli errori
                Console.WriteLine("Si è verificato un errore durante la creazione della tabella dei messaggi: " + ex.Message);
            }
        }

        //ascolto se ci sono messaggi dal server
        private async Task ReceiveMessagesFromServer()
        {
            try
            {
                byte[] buffer = new byte[1024];
                StringBuilder messageBuilder = new StringBuilder();

                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead <= 0)
                    {
                        // La connessione è stata chiusa dal server o si è verificato un errore di rete
                        break;
                    }

                    string receivedData = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    messageBuilder.Append(receivedData);

                    // Verifica se il messaggio ricevuto è completo
                    string receivedMessage = messageBuilder.ToString();
                    if (receivedMessage.EndsWith(Environment.NewLine))
                    {
                        // Rimuovi il terminatore di riga dal messaggio completo
                        receivedMessage = receivedMessage.TrimEnd(Environment.NewLine.ToCharArray());


                        // Estrai i dati dal pacchetto ricevuto
                        string[] messageData = receivedMessage.Split(',');

                        // Verifica che il pacchetto contenga tutti i dati necessari
                        if (messageData.Length >= 3)
                        {
                            string receiverId = messageData[0];
                            string senderId = messageData[1];
                            string content = messageData[2];

                            // Crea un oggetto Message
                            Message message = new Message
                            {
                                ReceiverId = receiverId,
                                SenderId = senderId,
                                Content = content,
                                Timestamp = DateTime.Now
                            };

                            // Gestisci il messaggio ricevuto
                            InsertMessageIntoDatabase(message);
                        }

                        // Resetta il buffer del messaggio
                        messageBuilder.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                // Gestione degli errori
                Console.WriteLine("Si è verificato un errore durante la ricezione dei messaggi dal server: " + ex.Message);
            }
        }

        //richiedo i messaggi dal server
        private async Task<List<Message>> GetMyMessagesFromServer()
        {
            List<Message> messages = new List<Message>();

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string apiUrl = "https://chat.jolly.vm.iacca.ml/api/my_message.php?input=" + clientId;
                    HttpResponseMessage response = await client.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        List<List<string>> apiResponse = JsonConvert.DeserializeObject<List<List<string>>>(json);

                        foreach (List<string> messageData in apiResponse)
                        {
                            string receiverId = messageData[0];
                            string senderId = messageData[1];
                            string messageText = messageData[2];

                            Message message = new Message
                            {
                                SenderId = senderId,
                                ReceiverId = receiverId,
                                Content = messageText,
                                Timestamp = DateTime.Now,
                            };

                            messages.Add(message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Gestione degli errori
                Console.WriteLine("Si è verificato un errore durante la richiesta dei messaggi: " + ex.Message);
            }

            return messages;
        }

        //inserisco i messaggi nella tabella
        private void InsertMessagesIntoDatabase(List<Message> messages)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection("Data Source=chat.db"))
                {
                    connection.Open();

                    foreach (Message message in messages)
                    {
                        string insertQuery = @"INSERT INTO messages (idSender, idReceiver, message) 
                                       VALUES (@idSender, @idReceiver, @message)";

                        using (SQLiteCommand command = new SQLiteCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@idSender", message.SenderId);
                            command.Parameters.AddWithValue("@idReceiver", message.ReceiverId);
                            command.Parameters.AddWithValue("@message", message.Content);

                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Gestione degli errori
                Console.WriteLine("Si è verificato un errore durante l'inserimento dei messaggi nel database: " + ex.Message);
            }
        }

        //inserisco un solo messaggio nel database locale
        private void InsertMessageIntoDatabase(Message message)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection("Data Source=chat.db"))
                {
                    connection.Open();

                    string insertQuery = @"INSERT INTO messages (idSender, idReceiver, message) 
                                   VALUES (@idSender, @idReceiver, @message)";

                    using (SQLiteCommand command = new SQLiteCommand(insertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@idSender", message.SenderId);
                        command.Parameters.AddWithValue("@idReceiver", message.ReceiverId);
                        command.Parameters.AddWithValue("@message", message.Content);

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Gestione degli errori
                Console.WriteLine("Si è verificato un errore durante l'inserimento del messaggio nel database: " + ex.Message);
            }
        }

        

       



        //richiedo i messaggi dal database locale
        private List<Message> GetMessagesFromDatabase(string userId)
        {
            List<Message> messages = new List<Message>();

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection("Data Source=chat.db"))
                {
                    connection.Open();

                    //seleziono solo i messaggi tra me e l'utente selezionato
                    string query = "SELECT * FROM messages WHERE (idSender = @senderId AND idReceiver = @receiverId) OR (idSender = @receiverId AND idReceiver = @senderId)";
                    using (SQLiteCommand command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@senderId", clientId);
                        command.Parameters.AddWithValue("@receiverId", userId);

                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string senderId = reader.GetString(1);
                                string receiverId = reader.GetString(2);
                                string content = reader.GetString(3);
                                DateTime timestamp = reader.GetDateTime(4);

                                Message message = new Message
                                {
                                    SenderId = senderId,
                                    ReceiverId = receiverId,
                                    Content = content,
                                    Timestamp = timestamp
                                };

                                messages.Add(message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Gestione degli errori
                Console.WriteLine("Si è verificato un errore durante la lettura dei messaggi dal database: " + ex.Message);
            }

            return messages;
        }




        internal class Message
        {
            public string SenderId { get; set; }
            public string ReceiverId { get; set; }
            public string Content { get; set; }
            public DateTime Timestamp { get; set; }
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


      


       


    
