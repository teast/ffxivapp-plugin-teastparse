using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using Dapper;
using FFXIVAPP.Common.Utilities;
using FFXIVAPP.Plugin.TeastParse.Actors;
using FFXIVAPP.Plugin.TeastParse.Models;
using NLog;

namespace FFXIVAPP.Plugin.TeastParse.Repositories
{
    /// <summary>
    /// Represents an CRUD repository for db access
    /// </summary>
    public interface IRepository : IDisposable
    {
        void AddDamage(DamageModel model);
        void AddCure(CureModel model);
        void AddActor(ActorModel model);
        void AddTimeline(TimelineModel model);
        void CloseTimeline(string name, DateTime endUtc);
        void AddChatLog(ChatLogLine line);

        IEnumerable<ChatLogLine> GetChatLogs();
        ChatLogLine GetChatLog(int id);
    }

    /// <summary>
    /// Implemets the actual CRUD repository
    /// </summary>
    public class Repository : RepositoryReadOnly, IRepository
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<string> _addedActors;
        private bool _disposed;

        public Repository(string connectionString) : base(connectionString)
        {
            _disposed = false;
            _addedActors = new List<string>();
        }

        public override void AddDamage(DamageModel model)
        {
            if (!Connect())
                return;

            const string query = @"
                INSERT INTO Damage
                (
                    OccurredUtc,
                    Timestamp,
                    Source,
                    Target,
                    Damage,
                    Modifier,
                    Action,
                    Critical,
                    DirectHit,
                    Blocked,
                    Parried,
                    InitDmg,
                    EndTimeUtc,
                    Subject,
                    Direction,
                    ChatCode,
                    IsDetrimental,
                    IsCombo,
                    Potency
                )
                VALUES
                (
                    @OccurredUtc,
                    @Timestamp,
                    @Source,
                    @Target,
                    @Damage,
                    @Modifier,
                    @ActionName,
                    @Critical,
                    @DirectHit,
                    @Blocked,
                    @Parried,
                    @InitDmg,
                    @EndTimeUtc,
                    @Subject,
                    @Direction,
                    @ChatCode,
                    @IsDetrimental,
                    @IsCombo,
                    @Potency
                );
            ";

            try
            {
                if (_connection.Execute(query, model) != 1)
                    Logging.Log(Logger, $"Problem storing damage information in database.");
            }
            catch (Exception ex)
            {
                Logging.Log(Logger, $"{nameof(Repository)}.{nameof(AddDamage)}: Unhandled exception", ex);
            }
        }

        public override void AddCure(CureModel model)
        {
            if (!Connect())
                return;

            const string query = @"
                INSERT INTO Cure
                (
                    OccurredUtc,
                    Timestamp,
                    Source,
                    Target,
                    Cure,
                    Modifier,
                    Action,
                    Critical,
                    Subject,
                    Direction,
                    ChatCode,
                    Actions
                )
                VALUES
                (
                    @OccurredUtc,
                    @Timestamp,
                    @Source,
                    @Target,
                    @Cure,
                    @Modifier,
                    @Action,
                    @Critical,
                    @Subject,
                    @Direction,
                    @ChatCode,
                    @Actions
                );
            ";

            try
            {
                if (_connection.Execute(query, model) != 1)
                    Logging.Log(Logger, $"Problem storing cure information in database.");
            }
            catch (Exception ex)
            {
                Logging.Log(Logger, $"{nameof(Repository)}.{nameof(AddCure)}: Unhandled exception", ex);
            }
        }

        public override void AddActor(ActorModel model)
        {
            if (_addedActors.Contains(model.Name))
                return;

            if (!Connect())
                return;

            const string query = @"
                INSERT INTO Actor
                (
                    ActorType,
                    Name,
                    Level,
                    Job
                )
                VALUES
                (
                    @ActorType,
                    @Name,
                    @Level,
                    @Job
                );
            ";

            try
            {
                if (_connection.Execute(query, model) != 1)
                    Logging.Log(Logger, $"Problem storing actor information in database.");
                _addedActors.Add(model.Name);
            }
            catch (Exception ex)
            {
                Logging.Log(Logger, $"{nameof(Repository)}.{nameof(AddActor)}: Unhandled exception", ex);
            }
        }

        public override void AddTimeline(TimelineModel model)
        {
            if (!Connect())
                return;

            const string query = @"
                INSERT INTO Timeline
                (
                    Name,
                    StartUtc,
                    EndUtc
                )
                VALUES
                (
                    @Name,
                    @StartUtc,
                    @EndUtc
                );
            ";

            try
            {
                if (_connection.Execute(query, model) != 1)
                    Logging.Log(Logger, $"Problem storing timeline information in database.");
            }
            catch (Exception ex)
            {
                Logging.Log(Logger, $"{nameof(Repository)}.{nameof(AddTimeline)}: Unhandled exception", ex);
            }
        }

        public override void CloseTimeline(string name, DateTime endUtc)
        {
            if (!Connect())
                return;

            const string query = @"UPDATE Timeline Set EndUtc = @EndUtc WHERE Name = @Name AND EndUtc IS NULL;";

            try
            {
                if (_connection.Execute(query, new { Name = name, EndUtc = endUtc }) != 1)
                    Logging.Log(Logger, $"Problem updating timeline information in database.");
            }
            catch (Exception ex)
            {
                Logging.Log(Logger, $"{nameof(Repository)}.{nameof(CloseTimeline)}: Unhandled exception", ex);
            }
        }

        public override void AddChatLog(ChatLogLine line)
        {
            if (!Connect())
                return;

            const string query = @"
                INSERT INTO ChatLog
                (
                    OccurredUtc,
                    Timestamp,
                    ChatCode,
                    ChatLine
                )
                VALUES
                (
                    @OccurredUtc,
                    @Timestamp,
                    @ChatCode,
                    @ChatLine
                );
            ";

            try
            {
                if (_connection.Execute(query, line) != 1)
                    Logging.Log(Logger, $"Problem inserting chatline information into database.");
            }
            catch (Exception ex)
            {
                Logging.Log(Logger, $"{nameof(Repository)}.{nameof(CloseTimeline)}: Unhandled exception", ex);
            }
        }


        public override void CloseConnection()
        {
            if (_disposed)
                return;
            if (_connection != null && _connection.State == ConnectionState.Open)
                _connection.Close();
        }

        public override void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_connection != null && _connection.State == ConnectionState.Open)
                _connection.Close();
            _connection = null;
        }

        protected override bool Connect()
        {
            if (_disposed)
                return false;

            if (_connection == null)
                _connection = new SQLiteConnection(_connectionString);

            if (_connection.State == ConnectionState.Open)
                return true;
            _connection.Open();
            CreateDatabase();

            return true;
        }

        private void CreateDatabase()
        {
            if (_connection.QueryFirst<int>(@"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name;", new { name = "Damage" }) > 0)
                return;

            const string tblDamage = @"
                CREATE TABLE Damage
                (
                    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    OccurredUtc     TEXT NOT NULL,
                    Timestamp       TEXT,
                    Source          TEXT,
                    Target          TEXT,
                    Damage          INT,
                    Modifier        TEXT,
                    Action          TEXT,
                    Critical        INT,
                    DirectHit       INT,
                    Blocked         INT,
                    Parried         INT,
                    InitDmg         INT,
                    EndTimeUtc      TEXT,
                    Subject         TEXT,
                    Direction       TEXT,
                    ChatCode        TEXT,
                    IsDetrimental   INT,
                    IsCombo         INT,
                    Potency         INT
                );
            ";

            const string tblActor = @"
                CREATE TABLE Actor
                (
                    ActorType       TEXT NOT NULL,
                    Name            TEXT NOT NULL,
                    Level           INT,
                    Job             TEXT
                );
            ";

            const string tblTimeline = @"
                CREATE TABLE Timeline
                (
                    Name        TEXT NOT NULL,
                    StartUtc    TEXT NOT NULL,
                    EndUtc      TEXT
                );
            ";

            const string tblCure = @"
                CREATE TABLE Cure
                (
                    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    OccurredUtc TEXT NOT NULL,
                    Timestamp   TEXT,
                    Source      TEXT,
                    Target      TEXT,
                    Cure        INT,
                    Modifier    TEXT,
                    Action      TEXT,
                    Critical    INT,
                    Subject     TEXT,
                    Direction   TEXT,
                    ChatCode    TEXT,
                    Actions     TEXT
                );
            ";

            const string tblChatLog = @"
                CREATE TABLE ChatLog
                (
                    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    OccurredUtc TEXT NOT NULL,
                    Timestamp   TEXT,
                    ChatCode    TEXT,
                    ChatLine    TEXT
                );
            ";

            _connection.Execute(tblDamage);
            _connection.Execute(tblActor);
            _connection.Execute(tblTimeline);
            _connection.Execute(tblCure);
            _connection.Execute(tblChatLog);
        }
    }

    /// <summary>
    /// Implements an "read-only" version of the repository
    /// </summary>
    public class RepositoryReadOnly : IRepository
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private bool _disposed;

        protected readonly string _connectionString;
        protected SQLiteConnection _connection;

        public RepositoryReadOnly(string connectionString)
        {
            _disposed = false;
            _connectionString = connectionString;
        }

        public virtual void AddActor(ActorModel model)
        {
        }

        public virtual void AddChatLog(ChatLogLine line)
        {
        }

        public virtual void AddCure(CureModel model)
        {
        }

        public virtual void AddDamage(DamageModel model)
        {
        }

        public virtual void AddTimeline(TimelineModel model)
        {
        }

        public virtual void CloseTimeline(string name, DateTime endUtc)
        {
        }

        public virtual IEnumerable<ChatLogLine> GetChatLogs()
        {
            if (!Connect())
                return null;

            return _connection.Query<ChatLogLine>("SELECT * FROM ChatLog ORDER BY ID ASC");
        }

        public virtual ChatLogLine GetChatLog(int id)
        {
            if (!Connect())
                return null;

            return _connection.Query<ChatLogLine>("SELECT * FROM ChatLog WHERE Id = @id", new { Id = id }).FirstOrDefault();
        }

        public virtual void CloseConnection()
        {
            if (_disposed)
                return;
            if (_connection != null && _connection.State == ConnectionState.Open)
                _connection.Close();
        }

        public virtual void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_connection != null && _connection.State == ConnectionState.Open)
                _connection.Close();
            _connection = null;
        }

        protected virtual bool Connect()
        {
            if (_disposed)
                return false;

            if (_connection == null)
                _connection = new SQLiteConnection(_connectionString);

            if (_connection.State == ConnectionState.Open)
                return true;

            _connection.Open();
            return true;
        }
    }
}