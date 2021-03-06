using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FFXIVAPP.Plugin.TeastParse.Actors;
using FFXIVAPP.Plugin.TeastParse.Factories;
using FFXIVAPP.Plugin.TeastParse.Models;
using FFXIVAPP.Plugin.TeastParse.RegularExpressions;
using FFXIVAPP.Plugin.TeastParse.Repositories;
using NLog;
using Sharlayan.Core;

namespace FFXIVAPP.Plugin.TeastParse.ChatParse
{
    internal class BeneficialParse : BaseChatParse
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IActorModelCollection _actors;
        private readonly ITimelineCollection _timeline;
        private readonly IBeneficialFactory _BeneficialFactory;

        /// <summary>
        /// Contains latest found actions (<see cref="ActionParse" /> for actual parsing of actions)
        /// </summary>
        private readonly IActionCollection _actions;
        private readonly IParseClock _clock;

        protected override Dictionary<ChatcodeType, ChatcodeTypeHandler> Handlers { get; }

        protected override List<ChatCodes> Codes { get; }

        public BeneficialParse(List<ChatCodes> codes, IActorModelCollection actors, ITimelineCollection timeline, IBeneficialFactory BeneficialFactory, IActionCollection actions, IParseClock clock, IRepository repository) : base(repository)
        {
            _actors = actors;
            _timeline = timeline;
            _BeneficialFactory = BeneficialFactory;
            _actions = actions;
            _clock = clock;
            Codes = codes.Where(c => c.Type == ChatcodeType.Beneficial).ToList();
            Handlers = new Dictionary<ChatcodeType, ChatcodeTypeHandler>
            {
                {ChatcodeType.Beneficial, _handleBeneficial}
            };
        }

        private void HandleBeneficial(ChatCodes activeCode, Group group, Match match, ChatLogItem item)
        {
            if (match.Groups["loses"].Success)
            {
                HandleBeneficialLose(activeCode, group, match, item);
                return;
            }

            var (model, target) = ToModel(match, item, group);
            target?.Beneficials?.Add(model);
        }

        private void HandleBeneficialLose(ChatCodes activeCode, Group group, Match match, ChatLogItem item)
        {
            var (model, target) = ToModel(match, item, group);
            if (target?.Beneficials == null || target.Beneficials.Count == 0)
                return;
            var exist = target.Beneficials.FirstOrDefault(_ => _.Name == model.Name);
            if (exist == null)
                return;

            exist.IsActive = false;
            //target.Beneficials.Remove(exist);
        }

        /// <summary>
        /// Converts given regex match to an <see cref="DamageModel" />
        /// </summary>
        /// <param name="r">regex match</param>
        /// <param name="item">the actual chat log item</param>
        /// <param name="group">chatcodes group</param>
        /// <returns>an <see cref="DamaModel" /> based on input parameters</returns>
        private (BeneficialModel model, ActorModel target) ToModel(Match r, ChatLogItem item, Group group)
        {
            var target = r.Groups["target"].Value;
            var status = r.Groups["status"].Value;
            var action = "";
            var source = "";
            var code = item.Code;

            if (_actions.TryGet(group.Subject, out var la))
            {
                source = la.Name;
                action = la.Action;
            }

            source = CleanName(source);
            target = CleanName(target);

            //var actorSource = string.IsNullOrEmpty(source) ? null : _actors.GetModel(source, group.Subject);
            var actorTarget = string.IsNullOrEmpty(target) ? null : _actors.GetModel(target, group.Subject, group.Direction);

            var model = _BeneficialFactory.GetModel(status, item.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss"), _clock.UtcNow,
                                                    source, target, code, group.Direction.ToString(), group.Subject.ToString(),
                                                    _actions.Factory);

            return (model, actorTarget);
        }

        private ChatcodeTypeHandler _handleBeneficial => new ChatcodeTypeHandler(
            ChatcodeType.Beneficial,
            new RegExDictionary(
                RegExDictionary.DamagePlayerAction,
                RegExDictionary.BeneficialPlayer,
                RegExDictionary.BeneficialLosePlayer,
                RegExDictionary.BeneficialLoseMonster
            ),
            HandleBeneficial,
            new RegExDictionary(
                RegExDictionary.MiscMonkFormChange
            )
        );
    }
}