using Strange_Universe.Game.Entities;
using Strange_Universe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Strange_Universe.Game.Components;
namespace Strange_Universe.Game.EventSystem
{
    public enum EventType
    {
        SpawnEvent,
        StandardEvent
    }

    public class EventController
    {
        private IWorldContext _owner;
        private float? _standardEventThreshold = null;
        private float _timeSinceLastEvent = 0f;
        private Random _random;
        private int _minEventThreshold = 60;
        private int _maxEventThreshold = 61;
        public EventController(IWorldContext owner)
        {
            _random = new Random(ProceduralHelpers.SeedHash($"{owner.SystemId}:Events"));
            _owner = owner;
        }
        public void Update(float deltaTime)
        {
            if (_owner == null) return;

            _timeSinceLastEvent += deltaTime;
            if (_standardEventThreshold is null)
            {
                SpawnEvent();
                _timeSinceLastEvent = 0f;
                _standardEventThreshold = _random.Next(_minEventThreshold, _maxEventThreshold);
            }
            else if (_timeSinceLastEvent >= _standardEventThreshold)
            {
                StandardEvent();

                // Set a new random threshold for the next event
                _timeSinceLastEvent = 0f;
                _standardEventThreshold = _random.Next(_minEventThreshold, _maxEventThreshold);
            }

        }
        /// <summary>A generator for the next event, drawn from the controller's own seeded stream.</summary>
        private Random NextEventRandom() => new Random(_random.Next());

        /// <summary>
        /// Share of system spawns (percent) taken by <see cref="NpcTestEvent"/>: always 100 in test mode, otherwise the
        /// event's own <see cref="NpcTestEvent.EventProbability"/>. Set it to override (tests do).
        /// </summary>
        public double? NpcTestProbabilityOverride { get; set; }

        private double NpcTestProbability =>
            NpcTestProbabilityOverride ?? (_owner.TestMode ? 100 : NpcTestEvent.EventProbability);

        public void SpawnEvent()
        {
            if (_owner == null)
            {
                return;
            }
            SystemEvent spawnEvent = null;
            switch ((int)(_random.NextDouble() * 100))
            {
                // The NPC test event comes first, so it takes its share of the rolls before the others. In test mode that
                // share is everything: every system spawn is the test event.
                case int n when (n < NpcTestProbability):
                    spawnEvent = new NpcTestEvent { random = NextEventRandom() };
                    break;
                case int n when (n < DefendedSystemSpawn.EventProbability):
                    _owner.PostMessage($"A Defended System has been discovered! {n}/{DefendedSystemSpawn.EventProbability}");
                    spawnEvent = new DefendedSystemSpawn { random = NextEventRandom() };
                    break;

                case int n when (n < ScoutedSystemSpawn.EventProbability):
                    _owner.PostMessage($"A Scouted System has been discovered! {n}/{ScoutedSystemSpawn.EventProbability}");
                    spawnEvent = new ScoutedSystemSpawn { random = NextEventRandom() };
                    break;
            }
            if (spawnEvent is not null)
            {
                spawnEvent.ExcuteEvent(_owner);
            }

        }

        public void StandardEvent()
        {
            if (_owner == null) return;
            SystemEvent spawnEvent = null;
            switch ((int)(_random.NextDouble() * 100))
            {
                case int n when (n < MerchantMissionEvent.EventProbability):
                    spawnEvent = new MerchantMissionEvent { random = NextEventRandom() };
                    break;
            }
            if (spawnEvent is not null)
            {
                spawnEvent.ExcuteEvent(_owner);
            }
        }
    }
}
