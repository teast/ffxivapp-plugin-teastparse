using System;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.Linq;
using Dapper;
using FFXIVAPP.IPluginInterface;
using FFXIVAPP.IPluginInterface.Events;
using FFXIVAPP.Plugin.TeastParse;
using FFXIVAPP.Plugin.TeastParse.Actors;
using FFXIVAPP.Plugin.TeastParse.ChatParse;
using FFXIVAPP.Plugin.TeastParse.Models;
using FFXIVAPP.Plugin.TeastParse.Repositories;
using FluentAssertions;
using Moq;
using NLog;
using NLog.Targets;
using Sharlayan.Core;
using TechTalk.SpecFlow;

namespace Tests.Steps
{
    [Binding]
    public sealed class BattleSteps
    {
        private readonly ConcurrentDictionary<uint, ActorItem> _players = new ConcurrentDictionary<uint, ActorItem>();
        private readonly ConcurrentDictionary<uint, ActorItem> _monsters = new ConcurrentDictionary<uint, ActorItem>();
        private readonly ConcurrentDictionary<uint, ActorItem> _npc = new ConcurrentDictionary<uint, ActorItem>();
        private GameLanguageEnum _language = GameLanguageEnum.English;
        private readonly EventSubscriber _event;
        private readonly Mock<IPluginHost> _pluginHost;

        private readonly ScenarioContext _scenarioContext;
        private Repository _repository;

        private readonly Random _random = new Random();
        private readonly ParserIoc _ioc;

        public BattleSteps(ScenarioContext scenarioContext)
        {
            // Output any exception so it is easier to find probelms in a test
            //Log.Logger = new LoggerConfiguration()
            //.MinimumLevel.Warning()
            //.WriteTo.Console().CreateLogger();

            // Output any exception so it is easier to find probelms in a test
            var config = new NLog.Config.LoggingConfiguration();
            config.AddRule(LogLevel.Info, LogLevel.Fatal, new ConsoleTarget("logconsole"));
            NLog.LogManager.Configuration = config;
            NLog.LogManager.ThrowExceptions = true;

            _scenarioContext = scenarioContext;

            SqlMapper.AddTypeHandler(new ActionModelHandler());

            var connectionString = string.Format("FullUri=file:{0}?mode=memory&cache=shared", Guid.NewGuid().ToString("N"));
            var connection = new SQLiteConnection(connectionString);
            _scenarioContext["connection"] = connection;
            connection.Open();
            _repository = new Repository(connectionString);

            _ioc = new ParserIoc();
            _ioc.Singelton<IRepository>(() => _repository);
            _pluginHost = new Mock<IPluginHost>();
            _event = new EventSubscriber(_ioc.Get<IChatFacade>(), _ioc.Get<IActorItemHelper>());
            _event.Subscribe(_pluginHost.Object);

            // Make sure we bind our Actor collection to the parser
            _pluginHost.Raise(_ => _.PCItemsUpdated += null, new ActorItemsEvent(this, _players));
            _pluginHost.Raise(_ => _.NPCItemsUpdated += null, new ActorItemsEvent(this, _npc));
            _pluginHost.Raise(_ => _.MonsterItemsUpdated += null, new ActorItemsEvent(this, _monsters));
        }

        [AfterScenario]
        public void AfterScenario()
        {
            _repository.CloseConnection();
            _repository.Dispose();
            ((SQLiteConnection)_scenarioContext["connection"]).Close();
            ((SQLiteConnection)_scenarioContext["connection"]).Dispose();
        }

        [Given("Player with name (.*)")]
        public void givenPlayerWithName(string name)
        {
            uint thirtyBits = (uint)_random.Next(1 << 30);
            uint twoBits = (uint)_random.Next(1 << 2);
            uint fullRange = (thirtyBits << 2) | twoBits;
            var result = _players.TryAdd(fullRange, new ActorItem
            {
                Name = string.Join(" ", name.Split(' ').Select(s => s.First().ToString().ToUpper() + s.Substring(1)))
            });

            if (!result)
                throw new Exception($"Was not able to add player \"{name}\" to the fake ConcurrentDictionary");
        }

        [Given("(.*) is you")]
        public void PlayerIsYou(string name)
        {
            var fullName = string.Join(" ", name.Split(' ').Select(s => s.First().ToString().ToUpper() + s.Substring(1)));

            _pluginHost.Raise(_ => _.CurrentPlayerUpdated += null, new CurrentPlayerEvent(this, new CurrentPlayer
            {
                Name = fullName
            }));
        }

        [Given("(.*) is he")]
        public void PlayerIsHe(string name)
        {
            var fullName = string.Join(" ", name.Split(' ').Select(s => s.First().ToString().ToUpper() + s.Substring(1)));

            var player = _players.First(p => p.Value.Name == fullName);
            player.Value.Sex = Sharlayan.Core.Enums.Actor.Sex.Male;
        }

        [Given("(.*) is she")]
        public void PlayerIsShe(string name)
        {
            var fullName = string.Join(" ", name.Split(' ').Select(s => s.First().ToString().ToUpper() + s.Substring(1)));
            var player = _players.First(p => p.Value.Name == fullName);
            player.Value.Sex = Sharlayan.Core.Enums.Actor.Sex.Female;
        }

        [Given("Monster with name (.*)")]
        public void GivenMonsterWithName(string name)
        {
            uint thirtyBits = (uint)_random.Next(1 << 30);
            uint twoBits = (uint)_random.Next(1 << 2);
            uint fullRange = (thirtyBits << 2) | twoBits;
            var result = _monsters.TryAdd(fullRange, new ActorItem
            {
                Name = string.Join(" ", name.Split(' ').Select(s => s.First().ToString().ToUpper() + s.Substring(1)))
            });

            if (!result)
                throw new Exception($"Was not able to add monster \"{name}\" to the fake ConcurrentDictionary");
        }

        [Given("(.*) chat")]
        public void GivenChatLanguage(GameLanguageEnum language)
        {
            _language = language;
        }

        [When("chat with code: (.*) and line: (.*)")]
        public void WhenAutoAttack(string code, string line)
        {
            _pluginHost.Raise(_ => _.ChatLogItemReceived += null, new ChatLogItemEvent(this, new ChatLogItem
            {
                Line = line,
                Code = code
            }));
        }

        [When("multiple chat lines")]
        public void WhenMultipleLines(Table table)
        {
            for (int i = 0; i < table.RowCount; i++)
            {
                var line = table.Rows[i]["line"].Trim('"');

                _pluginHost.Raise(_ => _.ChatLogItemReceived += null, new ChatLogItemEvent(this, new ChatLogItem
                {
                    Line = line,
                    Code = table.Rows[i]["code"]
                }));
                /*
                                _event.HandleLine(new ChatLogItem
                                {
                                    Line = line,
                                    Code = table.Rows[i]["code"]
                                });
                */
            }
        }

        [Then("Action (.*) with damage (.*), critical hit: (.*), blocked: (.*), parry: (.*), direct hit: (.*), modifier: (.*), should have been stored for player (.*) against (.*)")]
        public void ThenActionUsed(string action, ulong damage, bool crit, bool blocked, bool parry, bool direct, string modifier, string source, string target)
        {
            var data = ((SQLiteConnection)_scenarioContext["connection"]).Query<DamageModel>("SELECT * FROM Damage");
            data.Should().ContainSingle(m =>
                        DateTime.Parse(m.OccurredUtc) >= DateTime.UtcNow.AddMinutes(-5) && DateTime.Parse(m.OccurredUtc) <= DateTime.UtcNow &&
                        m.Damage == damage &&
                        m.Source == source &&
                        m.Target == target &&
                        m.Critical == crit && m.Blocked == blocked && m.Parried == parry && m.DirectHit == direct &&
                        (string.IsNullOrEmpty(modifier) || m.Modifier == modifier) &&
                        m.Action.Name == action);
        }

        [Then("Damage of (.*) with critical hit: (.*), blocked: (.*), parry: (.*), direct hit: (.*), modifier: (.*), should be stored for player (.*) against (.*)")]
        public void ThenCheckStoredDamage(ulong damage, bool crit, bool blocked, bool parry, bool direct, string modifier, string source, string target)
        {
            var data = ((SQLiteConnection)_scenarioContext["connection"]).Query<DamageModel>("SELECT * FROM Damage");
            data.Should().ContainSingle(m =>
                        DateTime.Parse(m.OccurredUtc) >= DateTime.UtcNow.AddMinutes(-5) && DateTime.Parse(m.OccurredUtc) <= DateTime.UtcNow &&
                        m.Damage == damage &&
                        m.Source == source &&
                        m.Critical == crit && m.Blocked == blocked && m.Parried == parry && m.DirectHit == direct &&
                        (string.IsNullOrEmpty(modifier) || m.Modifier == modifier) &&
                        m.Target == target);
        }

        [Then("No damage made by (.*).")]
        public void ThenCheckMonsterDamage(string source)
        {
            if (source == "[none]") source = "";
            var data = ((SQLiteConnection)_scenarioContext["connection"]).Query<DamageModel>("SELECT * FROM Damage");
            data.Should().NotContain(m => m.Source == source);
        }

        [Then("Damage of (.*) should be stored for (.*) against (.*).")]
        public void ThenCheckMonsterDamage(ulong damage, string source, string target)
        {
            if (source == "[none]") source = "";
            var data = ((SQLiteConnection)_scenarioContext["connection"]).Query<DamageModel>("SELECT * FROM Damage");
            data.Should().ContainSingle(m =>
                        DateTime.Parse(m.OccurredUtc) >= DateTime.UtcNow.AddMinutes(-5) && DateTime.Parse(m.OccurredUtc) <= DateTime.UtcNow &&
                        m.Damage == damage &&
                        m.Source == source &&
                        m.Target == target);
        }

        [Then("Cure of (.*) should be stored for (.*) on (.*).")]
        public void ThenCheckPlayercure(ulong cure, string source, string target)
        {
            var data = ((SQLiteConnection)_scenarioContext["connection"]).Query<CureModel>("SELECT * FROM Cure");
            data.Should().ContainSingle(m =>
                        DateTime.Parse(m.OccurredUtc) >= DateTime.UtcNow.AddMinutes(-5) && DateTime.Parse(m.OccurredUtc) <= DateTime.UtcNow &&
                        m.Cure == cure &&
                        m.Source == source &&
                        m.Target == target);
        }
    }
}