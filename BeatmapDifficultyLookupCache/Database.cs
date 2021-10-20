using System;
using System.Threading.Tasks;
using MySqlConnector;

namespace BeatmapDifficultyLookupCache
{
    public static class Database
    {
        /// <summary>
        /// Retrieve a database connection.
        /// </summary>
        public static async Task<MySqlConnection> GetDatabaseConnection()
        {
            string host = (Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost");
            string user = (Environment.GetEnvironmentVariable("DB_USER") ?? "root");

            var connection = new MySqlConnection($"Server={host};Database=osu;User ID={user};ConnectionTimeout=5;ConnectionReset=false;Pooling=true;");
            await connection.OpenAsync();

            return connection;
        }
    }
}
