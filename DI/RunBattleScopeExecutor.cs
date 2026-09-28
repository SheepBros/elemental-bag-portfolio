using System;
using Cysharp.Threading.Tasks;

namespace ElementalBackHero
{
    public sealed class RunBattleScopeExecutor : IRunBattleExecutor
    {
        private readonly IBattleContextFactory _battleContextFactory;

        private readonly IRunBattleScopeRunner _scopeRunner;

        public RunBattleScopeExecutor(IBattleContextFactory battleContextFactory,
            IRunBattleScopeRunner scopeRunner)
        {
            _battleContextFactory =
                battleContextFactory ?? throw new ArgumentNullException(nameof(battleContextFactory));
            _scopeRunner = scopeRunner ?? throw new ArgumentNullException(nameof(scopeRunner));
        }

        public async UniTask<BattleResultSummary> RunBattleAsync(RunBattleRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            IBattleContext battleContext = _battleContextFactory.Create(request);
            return await _scopeRunner.RunBattleAsync(request, battleContext);
        }
    }
}
