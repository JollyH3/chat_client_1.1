using System.Data.SQLite;

namespace chat_client_1._1
{
    public class DatabaseManager
    {
        private string connectionString;

        public DatabaseManager()
        {
            // Imposta la stringa di connessione al database SQLite
            connectionString = "Data Source=chat.db;Version=3;";
        }

        public void CreateDatabase()
        {
            // Crea il file del database SQLite
            SQLiteConnection.CreateFile("chat.db");

            // Crea la tabella "Users"
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                string createTableQuery = "CREATE TABLE IF NOT EXISTS User (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT, Surname TEXT);";

                using (var command = new SQLiteCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        public void InsertUser(string name, string surname)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                string insertQuery = "INSERT INTO User (Name, Surname) VALUES (@Name, @Surname);";

                using (var command = new SQLiteCommand(insertQuery, connection))
                {
                    command.Parameters.AddWithValue("@Name", name);
                    command.Parameters.AddWithValue("@Surname", surname);

                    command.ExecuteNonQuery();
                }
            }
        }

        // Aggiungi altri metodi per eseguire altre operazioni sul database (es. ricerca utenti, aggiornamento, cancellazione, ecc.)
    }
}
