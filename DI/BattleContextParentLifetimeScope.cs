using System;
using VContainer;
using VContainer.Unity;

namespace ElementalBackHero
{
    public sealed class BattleContextParentLifetimeScope : LifetimeScope
    {
        private IBattleContext _battleContext;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private BattleExecutionTraceRecorder _battleExecutionTraceRecorder;

        private ReproCommandJournalRecorder _commandJournal;
#endif

        public void SetBattleContext(IBattleContext battleContext)
        {
            _battleContext = battleContext ?? throw new ArgumentNullException(nameof(battleContext));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void SetBattleExecutionTraceRecorder(
            BattleExecutionTraceRecorder battleExecutionTraceRecorder)
        {
            _battleExecutionTraceRecorder = battleExecutionTraceRecorder;
        }

        public void SetCommandJournal(ReproCommandJournalRecorder commandJournal)
        {
            _commandJournal = commandJournal;
        }
#endif

        protected override void Configure(IContainerBuilder builder)
        {
            if (_battleContext == null)
            {
                throw new InvalidOperationException(
                    "Battle context must be set before building the battle context parent scope.");
            }

            builder.RegisterInstance(_battleContext).As<IBattleContext>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterInstance(
                _battleExecutionTraceRecorder ?? BattleExecutionTraceRecorder.Disabled);
            builder.RegisterInstance(
                _commandJournal ?? new ReproCommandJournalRecorder());
#endif
        }
    }
}
