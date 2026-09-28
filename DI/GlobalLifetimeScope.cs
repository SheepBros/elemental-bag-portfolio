using System;
using ElementalBackHero;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GlobalLifetimeScope : LifetimeScope
{
    [SerializeField]
    private PopupRoot _popupRoot;

    [SerializeField]
    private PopupCatalog _popupCatalog;

    [SerializeField]
    private UiIconCatalog _uiIconCatalog;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance<IUserProgressStore>(new UserProgressStore(UserProgressService.DefaultPath()));
        builder.Register<UserProgressService>(Lifetime.Singleton);
        RegisterGameDataServices(builder);
        RegisterUiPresentationServices(builder);
        RegisterPopupServices(builder);
    }

    protected void RegisterUiPresentationServices(IContainerBuilder builder)
    {
        if (_uiIconCatalog == null)
        {
            throw new InvalidOperationException(
                "UiIconCatalog is required for UI presentation.");
        }

        _uiIconCatalog.ValidateOrThrow();
        builder.RegisterInstance<IUiIconProvider>(_uiIconCatalog);
        builder.Register<ILocalizationProvider, UnityLocalizationProvider>(
            Lifetime.Singleton);
        builder.RegisterInstance<IDisplaySettingsStore>(
            new PlayerPrefsDisplaySettingsStore());
        builder.RegisterInstance<IGameDisplayAdapter>(
            new UnityGameDisplayAdapter());
        builder.Register<IDisplaySettingsService, DisplaySettingsService>(
            Lifetime.Singleton);
    }

    protected void RegisterGameDataServices(IContainerBuilder builder)
    {
        builder.Register<IGameDataByteLoader, StreamingAssetsGameDataByteLoader>(Lifetime.Singleton);
        builder.Register<IGameDataTables, LubanGameDataTables>(Lifetime.Singleton);
    }

    protected void RegisterPopupServices(IContainerBuilder builder)
    {
        builder.RegisterInstance<IPopupService>(new PopupService(_popupRoot, _popupCatalog));
    }
}
