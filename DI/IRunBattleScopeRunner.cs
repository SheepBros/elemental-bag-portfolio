using Cysharp.Threading.Tasks;

namespace ElementalBackHero
{
    public interface IRunBattleScopeRunner
    {
        UniTask<BattleResultSummary> RunBattleAsync(RunBattleRequest request, IBattleContext battleContext);
    }
}
