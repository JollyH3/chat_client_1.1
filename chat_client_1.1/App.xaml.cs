using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace chat_client_1._1
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DatabaseManager databaseManager = new DatabaseManager();
            databaseManager.CreateDatabase();

            Application_Startup();
        }

        private void Application_Startup()
        {
            // Controlla se l'utente è già registrato
            bool isRegistered = !string.IsNullOrEmpty(chat_client_1._1.Properties.Settings.Default.ClientID);

            if (!isRegistered)
            {
                // L'utente non è ancora registrato, apri la finestra di registrazione
                RegistrationWindow registrationWindow = new RegistrationWindow();
                if (registrationWindow.ShowDialog() != true)
                {
                    // L'utente ha annullato la registrazione o si è verificato un errore
                    Shutdown();
                    return;
                }
            }

            // Mostra la finestra principale
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
