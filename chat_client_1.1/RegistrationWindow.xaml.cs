using System;
using System.Net.Http;
using System.Text;
using System.Windows;
using Newtonsoft.Json;

namespace chat_client_1._1
{
    public partial class RegistrationWindow : Window
    {
        public string UserID { get; private set; }

        public RegistrationWindow()
        {
            InitializeComponent();
        }

        private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // Recupera i dati inseriti nella finestra
            string name = NameTextBox.Text;
            string surname = SurnameTextBox.Text;

            // Crea l'oggetto JSON da inviare all'API
            var json = JsonConvert.SerializeObject(new { name, surname });
            var data = new StringContent(json, Encoding.UTF8, "application/json");

            // Invia la richiesta all'API
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.PostAsync("https://chat.jolly.vm.iacca.ml/api/new_user.php", data);
                var content = await response.Content.ReadAsStringAsync();

                // Analizza la risposta dell'API
                dynamic result = JsonConvert.DeserializeObject(content);
                string error = result.error;
                string clientId = result.client_id;

                if (error == "0")
                {
                    // Salva il client_id nei settings dell'app
                    Properties.Settings.Default.ClientID = clientId;
                    Properties.Settings.Default.Save();

                    // Assegna l'ID utente alla proprietà UserID
                    UserID = clientId;

                    // Chiudi la finestra di registrazione
                    Close();
                }
                else
                {
                    MessageBox.Show("Si è verificato un errore durante la registrazione.");
                }
            }
        }
    }
}
