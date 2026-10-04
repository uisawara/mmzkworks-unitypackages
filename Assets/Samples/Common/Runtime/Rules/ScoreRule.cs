using Mmzkworks.muEventHub;

namespace App.Contents
{
    public sealed class ScoreRule : IEventRule<GemCollectedEvent>
    {
        private readonly int _pointsPerGem;

        public int Score { get; private set; }

        public ScoreRule(int pointsPerGem)
        {
            _pointsPerGem = pointsPerGem;
        }

        public void Handle(in GemCollectedEvent e, IEventContext context)
        {
            var previous = Score;
            Score += _pointsPerGem;
            context.Publish(new ScoreChangedEvent(previous, Score));
        }
    }
}
