using Cysharp.Threading.Tasks;

namespace ElementalBackHero
{
    public interface IRunBattleExecutor
    {
        UniTask<BattleResultSummary> RunBattleAsync(RunBattleRequest request);
    }
}
