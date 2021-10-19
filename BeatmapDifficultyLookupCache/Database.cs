using System;
using MySqlConnector;

namespace BeatmapDifficultyLookupCache
{
    public static class Database
    {
        /// <summary>
        /// Retrieve a database connection.
        /// </summary>
        public static MySqlConnection GetDatabaseConnection()
        {
            string host = (Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost");
            string user = (Environment.GetEnvironmentVariable("DB_USER") ?? "root");

            var connection = new MySqlConnection($"Server={host};Database=osu;User ID={user};ConnectionTimeout=5;ConnectionReset=false;Pooling=true;");
            connection.Open();

            // TODO: remove this when we have set a saner time zone server-side.
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SET time_zone = '+00:00';";
                cmd.ExecuteNonQuery();
            }

            return connection;
        }
    }
}
