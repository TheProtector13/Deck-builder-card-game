using System.IO;
using Microsoft.Data.Sqlite;

namespace CardGame {
    internal static class DatabaseConnector {
        private static readonly SqliteConnection connection;
        public static SqliteCommand CreateCommand() => connection.CreateCommand();

        static DatabaseConnector()
        {
            try {
                connection = new SqliteConnection("Data Source=cardgame.db");
                connection.Open();
            }
            catch {
                if (File.Exists("cardgame.db")) {
                    File.Delete("cardgame.db");
                    connection = new SqliteConnection("Data Source=cardgame.db");
                    connection.Open();
                }
                else {
                    throw new SqliteException("Failed to connect to database and no existing database file to delete.", 0);
                }
            }
            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE IF NOT EXISTS SETTINGS (KEY TEXT PRIMARY KEY, VALUE real);";
            command.ExecuteNonQuery();
            command.CommandText = "CREATE TABLE IF NOT EXISTS USERNAME (NAME TEXT PRIMARY KEY);";
            command.ExecuteNonQuery();
        }

        public static float? GetSetting(string key)
        {
            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT VALUE FROM SETTINGS WHERE KEY = $key;";
            command.Parameters.AddWithValue("$key", key);
            float? value = null;
            using (SqliteDataReader reader = command.ExecuteReader()) {
                value = reader.Read() ? reader.GetFloat(0) : null;
            }
            return value;
        }

        public static void SetSetting(string key, float value)
        {
            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO SETTINGS (KEY, VALUE) VALUES ($key, $value) " +
                                  "ON CONFLICT(KEY) DO UPDATE SET VALUE = $value;";
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }

        public static string GetUsername()
        {
            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT NAME FROM USERNAME LIMIT 1;";
            string name = string.Empty;
            using (SqliteDataReader reader = command.ExecuteReader()) {
                name = reader.Read() ? reader.GetString(0) : string.Empty;
            }
            return name;
        }

        public static void SetUsername(string name)
        {
            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM USERNAME;";
            command.ExecuteNonQuery();
            command.CommandText = "INSERT INTO USERNAME (NAME) VALUES ($name);";
            command.Parameters.AddWithValue("$name", name);
            command.ExecuteNonQuery();
        }

        public static void Init() { }

        public static void CloseConnection()
        {
            connection.Close();
            connection.Dispose();
        }

    }
}
