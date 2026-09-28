using System;
using Cysharp.Threading.Tasks;

namespace ElementalBackHero
{
    public sealed class RunBattleScopeRunner : IRunBattleScopeRunner
    {
        private readonly IRunBattleScopeHandleFactory _handleFactory;

        public RunBattleScopeRunner(IRunBattleScopeHandleFactory handleFactory)
        {
            _handleFactory = handleFactory ?? throw new ArgumentNullException(nameof(handleFactory));
        }

        public async UniTask<BattleResultSummary> RunBattleAsync(RunBattleRequest request,
            IBattleContext battleContext)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (battleContext == null)
            {
                throw new ArgumentNullException(nameof(battleContext));
            }

            IRunBattleScopeHandle handle = _handleFactory.Create(request, battleContext);
            if (handle == null)
            {
                throw new InvalidOperationException("Run battle scope handle factory must return a handle.");
            }

            Exception runException = null;
            try
            {
                return await handle.RunBattleAsync(request);
            }
            catch (Exception exception)
            {
                runException = exception;
                throw;
            }
            finally
            {
                try
                {
                    await handle.CleanupAsync();
                }
                catch when (runException != null)
                {
                    // Preserve the battle execution failure as the primary exception.
                }
            }
        }
    }
}
