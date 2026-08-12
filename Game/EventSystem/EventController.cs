using Strange_Universe.Game.Entities;
using StrangeUniverse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Strange_Universe.Game.EventSystem
{
    public enum EventType
    {
        SpawnEvent,
        StandardEvent
    }

    public class EventController
    {
        private StarSystem _owner;
        private float? _standardEventThreshold = null;
        private float _timeSinceLastEvent = 0f;
        private Random _random;
        private int _minEventThreshold = 60;
        private int _maxEventThreshold = 61;
        public EventController(StarSystem owner)
        {
            _random = new Random(StaticHelpers.SeedHash($"{owner.SystemId}:Events"));
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
        public void SpawnEvent()
        {
            if (_owner == null)
            {
                return;
            }
            SystemEvent spawnEvent = null;
            switch ((int)(_random.NextDouble() * 100))
            {
                case int n when (n < DefendedSystemSpawn.EventProbability):
                    Launcher.ActiveUniverse.ShowTimedMessage($"A Defended System has been discovered! {n}/{DefendedSystemSpawn.EventProbability}");
                    spawnEvent = new DefendedSystemSpawn();
                    break;

                case int n when (n < ScoutedSystemSpawn.EventProbability):
                    Launcher.ActiveUniverse.ShowTimedMessage($"A Scouted System has been discovered! {n}/{ScoutedSystemSpawn.EventProbability}");
                    spawnEvent = new ScoutedSystemSpawn();
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
                    spawnEvent = new MerchantMissionEvent();
                    break;
            }
            if (spawnEvent is not null)
            {
                spawnEvent.ExcuteEvent(_owner);
            }
        }
    }
}
