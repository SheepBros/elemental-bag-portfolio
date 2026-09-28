using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace ElementalBackHero
{
    public sealed class PrefabRunBattleScopeHandle : IRunBattleScopeHandle
    {
        private readonly GameObject _productionBattleRootPrefab;

        private readonly Transform _instantiateParent;

        private readonly IBattleContext _battleContext;
        private readonly BattleTutorialController _guidance;
        private readonly Func<BattleResultSummary, UniTask> _resultCommitted;

        private readonly LifetimeScope _parentLifetimeScope;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly ReproCommandJournalRecorder _commandJournal;
#endif

        private readonly Camera _worldCamera;

        private readonly RunForegroundCanvasSurface _runForegroundSurface;

        private readonly Transform _backgroundShakeTarget;

        private GameObject _instance;

        private bool _hasRun;
        private readonly ScreenTransitionPresentation _transition;
        private readonly Func<BattleEntryBackgroundPose> _beginBackgroundPose;
        private BattleEntryBackgroundPose _backgroundPose;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private IBattleInteractionApplicationService _interactionService;
#endif

        private bool _runForegroundSuspended;

        public PrefabRunBattleScopeHandle(GameObject productionBattleRootPrefab, Transform instantiateParent,
            IBattleContext battleContext, LifetimeScope parentLifetimeScope = null,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ReproCommandJournalRecorder commandJournal = null,
#endif
            Camera worldCamera = null,
            RunForegroundCanvasSurface runForegroundSurface = null,
            Transform backgroundShakeTarget = null, ScreenTransitionPresentation transition = null,
            Func<BattleEntryBackgroundPose> beginBackgroundPose = null,
            BattleTutorialController guidance = null, Func<BattleResultSummary, UniTask> resultCommitted = null)
        {
            _guidance = guidance; _resultCommitted = resultCommitted;
            _productionBattleRootPrefab = productionBattleRootPrefab ??
                                          throw new ArgumentNullException(nameof(productionBattleRootPrefab));
            _instantiateParent = instantiateParent;
            _battleContext = battleContext ?? throw new ArgumentNullException(nameof(battleContext));
            _parentLifetimeScope = parentLifetimeScope;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _commandJournal = commandJournal;
#endif
            _worldCamera = worldCamera;
            _runForegroundSurface = runForegroundSurface;
            _backgroundShakeTarget = backgroundShakeTarget;
            _transition = transition;
            _beginBackgroundPose = beginBackgroundPose;
        }

        public async UniTask<BattleResultSummary> RunBattleAsync(RunBattleRequest request)
        {
            return await RunBattleAsync(request, CancellationToken.None);
        }

        public async UniTask<BattleResultSummary> RunBattleAsync(RunBattleRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (_hasRun)
            {
                throw new InvalidOperationException("Prefab run battle scope handle can only run once.");
            }

            _hasRun = true;
            if (_transition != null) await _transition.CoverAsync(cancellationToken);
            BattleLifetimeScope battleScope = BuildBattleScope(
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                CreateProductionTraceRecorder(),
#endif
                headless: false);

            IBattleApplicationService applicationService =
                battleScope.Container.Resolve<IBattleApplicationService>();
            BattleResultSummary result = await applicationService.RunBattleAsync(request, cancellationToken);
            if (_resultCommitted != null) await _resultCommitted(result);
            return await HoldCompletedBattleAsync(result, cancellationToken);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public async UniTask<IBattleInteractionApplicationService> StartInteractionAsync(
            RunBattleRequest request,
            CancellationToken cancellationToken)
        {
            return await StartInteractionAsync(request, cancellationToken, headless: true);
        }

        public async UniTask<IBattleInteractionApplicationService> StartInteractiveInteractionAsync(
            RunBattleRequest request,
            CancellationToken cancellationToken)
        {
            return await StartInteractionAsync(request, cancellationToken, headless: false);
        }

        private async UniTask<IBattleInteractionApplicationService> StartInteractionAsync(
            RunBattleRequest request,
            CancellationToken cancellationToken,
            bool headless)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (_hasRun)
            {
                throw new InvalidOperationException(
                    "Prefab run battle scope handle can only run once.");
            }

            _hasRun = true;
            if (!headless && _transition != null) await _transition.CoverAsync(cancellationToken);
            BattleExecutionTraceRecorder traceRecorder = new();
            BattleLifetimeScope battleScope = BuildBattleScope(
                traceRecorder, headless);
            traceRecorder.Record(
                "BattleCreated",
                request.EncounterId?.ToString() ?? "0",
                $"seed={request.BattleSeed}");
            _interactionService =
                battleScope.Container.Resolve<IBattleInteractionApplicationService>();
            await _interactionService.InitializeAsync(request, cancellationToken);
            if (!headless)
            {
                await _interactionService.WaitForPresentationInputAsync(
                    cancellationToken);
            }
            return _interactionService;
        }

        public async UniTask<RestoredBattleScopeRun> StartRestoredBattleAsync(
            RunBattleRequest request,
            ReproCheckpoint checkpoint,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            cancellationToken.ThrowIfCancellationRequested();
            if (_hasRun)
            {
                throw new InvalidOperationException(
                    "Prefab run battle scope handle can only run once.");
            }

            _hasRun = true;
            if (_transition != null) await _transition.CoverAsync(cancellationToken);
            BattleLifetimeScope battleScope = BuildBattleScope(
                CreateProductionTraceRecorder(),
                headless: false, restored: true);
            IBattleRestoredApplicationService service =
                battleScope.Container.Resolve<IBattleRestoredApplicationService>();
            RestoredBattleScopeRun restored = await service.StartAsync(request, checkpoint, cancellationToken);
            return new RestoredBattleScopeRun(HoldRestoredAsync(restored.Completion, cancellationToken));
        }
#endif

        public async UniTask CleanupAsync()
        {
            if (_instance == null)
            {
                _backgroundPose?.Dispose();
                RestoreRunForeground();
                return;
            }
            try
            {
                // A failed runtime may also need cleanup. Keep it covered before destroying visuals.
                if (_transition != null && !_transition.IsBusy)
                    await _transition.CoverAsync(CancellationToken.None);
                try
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (_interactionService != null)
                    {
                        await _interactionService.StopAsync();
                    }
#endif
                }
                finally
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    _interactionService?.Dispose();
                    _interactionService = null;
#endif
                }
            }
            finally
            {
                try
                {
                    GameObject instance = _instance;
                    _instance = null;
                    if (Application.isPlaying)
                    {
                        Object.Destroy(instance);
                        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                    }
                    else
                    {
                        Object.DestroyImmediate(instance);
                    }
                }
                finally
                {
                    _backgroundPose?.Dispose();
                    RestoreRunForeground();
                }
            }
        }

        private async UniTask<BattleResultSummary> HoldCompletedBattleAsync(BattleResultSummary result, CancellationToken token)
        {
            if (result.ResultType == BattleCompletionResultType.Victory)
            {
                using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, _instance.GetCancellationTokenOnDestroy());
                await UniTask.Delay(TimeSpan.FromSeconds(1.5), ignoreTimeScale: true, cancellationToken: lifetime.Token);
            }
            
            if (_transition != null)
            {
                await _transition.CoverAsync(token);
            }
            return result;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private async UniTask<BattleResultSummary> HoldRestoredAsync(UniTask<BattleResultSummary> completion, CancellationToken token)
            => await HoldCompletedBattleAsync(await completion, token);
#endif

        private BattleLifetimeScope BuildBattleScope(
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BattleExecutionTraceRecorder traceRecorder,
#endif
            bool headless, bool restored = false)
        {
            _instance = InstantiateWithManualLifetimeScopeBuild();
            try
            {
                BattleContextParentLifetimeScope parentScope =
                    _instance.GetComponent<BattleContextParentLifetimeScope>();
                if (parentScope == null)
                {
                    throw new InvalidOperationException(
                        "Production battle root prefab must have BattleContextParentLifetimeScope on the root object.");
                }

                BattleLifetimeScope battleScope = FindSingleBattleLifetimeScope(_instance);
                if (headless)
                {
                    battleScope.ConfigureHeadlessInteraction();
                }
                else
                {
                    if (_worldCamera == null)
                    {
                        throw new InvalidOperationException(
                            "Interactive production Battle requires a scene-owned world camera.");
                    }
                    _backgroundPose = _beginBackgroundPose?.Invoke();
                    battleScope.ConfigurePresentationCamera(
                        _worldCamera, _backgroundShakeTarget, _transition, _backgroundPose, restored);
                }
                
                if (_guidance != null)
                {
                    battleScope.ConfigureGuidance(_guidance, _worldCamera);
                }
                parentScope.SetBattleContext(_battleContext);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                parentScope.SetBattleExecutionTraceRecorder(traceRecorder);
                parentScope.SetCommandJournal(_guidance == null ? _commandJournal : null);
#endif
                if (_parentLifetimeScope != null)
                {
                    parentScope.parentReference.Object = _parentLifetimeScope;
                }

                battleScope.parentReference.Object = parentScope;
                if (!headless && _runForegroundSurface != null)
                {
                    _runForegroundSurface.EnterBattle();
                    _runForegroundSuspended = true;
                }

                parentScope.Build();
                battleScope.Build();
                return battleScope;
            }
            catch
            {
                _backgroundPose?.Dispose();
                RestoreRunForeground();
                DestroyFailedInstance();
                throw;
            }
        }

        private void DestroyFailedInstance()
        {
            GameObject instance = _instance;
            _instance = null;
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(instance);
            }
            else
            {
                Object.DestroyImmediate(instance);
            }
        }

        private void RestoreRunForeground()
        {
            if (!_runForegroundSuspended)
            {
                return;
            }

            _runForegroundSuspended = false;
            if (_runForegroundSurface != null)
            {
                _runForegroundSurface.ExitBattle();
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static BattleExecutionTraceRecorder CreateProductionTraceRecorder()
        {
            return ReproDiagnosticsOptions.IsDiagnosticsBuild
                ? new BattleExecutionTraceRecorder(
                    capacity: ReproDiagnosticsOptions.DefaultTraceTailCapacity)
                : BattleExecutionTraceRecorder.Disabled;
        }
#endif

        private GameObject InstantiateWithManualLifetimeScopeBuild()
        {
            GameObject inactiveHost = new("ProductionBattleRootInstantiateHost");
            inactiveHost.SetActive(false);
            if (_instantiateParent != null)
            {
                inactiveHost.transform.SetParent(_instantiateParent, false);
            }

            GameObject instance = Object.Instantiate(_productionBattleRootPrefab, inactiveHost.transform);
            DisableAutoRun(instance);
            instance.transform.SetParent(_instantiateParent, false);

            if (Application.isPlaying)
            {
                Object.Destroy(inactiveHost);
            }
            else
            {
                Object.DestroyImmediate(inactiveHost);
            }

            return instance;
        }

        private static void DisableAutoRun(GameObject root)
        {
            LifetimeScope[] scopes = root.GetComponentsInChildren<LifetimeScope>(true);
            for (int i = 0; i < scopes.Length; i++)
            {
                scopes[i].autoRun = false;
            }
        }

        private static BattleLifetimeScope FindSingleBattleLifetimeScope(GameObject root)
        {
            BattleLifetimeScope[] scopes = root.GetComponentsInChildren<BattleLifetimeScope>(true);
            if (scopes.Length == 0)
            {
                throw new InvalidOperationException(
                    "Production battle root prefab must contain a child BattleLifetimeScope.");
            }

            if (scopes.Length > 1)
            {
                throw new InvalidOperationException(
                    "Production battle root prefab must contain exactly one BattleLifetimeScope.");
            }

            return scopes[0];
        }
    }
}
