using System;
using System.Collections.Generic;

namespace ElementalBackHero
{
    public sealed class TableBackedBattleTokenDefinitionProvider : IBattleTokenDefinitionProvider
    {
        private readonly IGameDataTables _gameDataTables;

        public TableBackedBattleTokenDefinitionProvider(IGameDataTables gameDataTables)
        {
            _gameDataTables = gameDataTables ?? throw new ArgumentNullException(nameof(gameDataTables));
        }

        public BattleTokenDefinition Get(int tokenId)
        {
            if (tokenId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tokenId));
            }

            Tables tables = GetTables();
            RuneData rune = GetRune(tables, tokenId);
            return CreateDefinition(tables, rune);
        }

        public bool TryGet(int tokenId, out BattleTokenDefinition definition)
        {
            definition = null;
            if (tokenId <= 0)
            {
                return false;
            }

            Tables tables = _gameDataTables.Tables;
            RuneData rune = tables?.TbRuneData?.GetOrDefault(tokenId);
            if (rune == null)
            {
                return false;
            }

            definition = CreateDefinition(tables, rune);
            return true;
        }

        public bool TryGetResourceType(int tokenId, out ResourceType resourceType)
        {
            resourceType = ResourceType.Max;
            if (!TryGet(tokenId, out BattleTokenDefinition definition))
            {
                return false;
            }

            for (int i = 0; i < definition.Effects.Count; i++)
            {
                if (definition.Effects[i] is BattleTokenEffectGainResource gainResource)
                {
                    resourceType = gainResource.ResourceType;
                    return resourceType >= 0 && resourceType < ResourceType.Max;
                }

                if (definition.Effects[i] is BattleTokenEffectGameEffect gameEffect &&
                    gameEffect.Definition.GameEffectType == GameEffectType.GainResource &&
                    gameEffect.Definition.ResourceGrantModeType == ResourceGrantModeType.Specific)
                {
                    resourceType = gameEffect.Definition.ResourceType;
                    return resourceType >= 0 && resourceType < ResourceType.Max;
                }
            }

            return false;
        }

        public bool IsUnstableToken(int tokenId)
        {
            return TryGet(tokenId, out BattleTokenDefinition definition) && definition.IsUnstable;
        }

        private Tables GetTables()
        {
            return _gameDataTables.Tables ??
                   throw new InvalidOperationException("GameData Tables are not available.");
        }

        private static RuneData GetRune(Tables tables, int tokenId)
        {
            RuneData rune = tables.TbRuneData?.GetOrDefault(tokenId);
            return rune ?? throw new InvalidOperationException($"RuneData {tokenId} is not found.");
        }

        private static BattleTokenDefinition CreateDefinition(Tables tables, RuneData rune)
        {
            return new BattleTokenDefinition(
                rune.Id,
                rune.Key,
                rune.DisplayName,
                rune.ElementType,
                rune.RuneKindType,
                rune.IsUnstable,
                CreateEffects(tables, rune),
                rune.DrawEffectGroupId, rune.RemovalTimingType);
        }

        private static IReadOnlyList<IBattleTokenEffect> CreateEffects(Tables tables, RuneData rune)
        {
            List<IBattleTokenEffect> effects = new();
            if (rune.DrawEffectGroupId > 0)
            {
                EffectGroupData group = tables.TbEffectGroupData?.GetOrDefault(rune.DrawEffectGroupId)
                    ?? throw new InvalidOperationException(
                        $"RuneData {rune.Id} references missing EffectGroupData {rune.DrawEffectGroupId}.");
                for (int i = 0; i < group.EffectIds.Count; i++)
                {
                    int effectId = group.EffectIds[i];
                    GameEffectData effect = tables.TbGameEffectData?.GetOrDefault(effectId)
                        ?? throw new InvalidOperationException(
                            $"EffectGroupData {group.Id} references missing GameEffectData {effectId}.");
                    if (effect.EffectType == GameEffectType.SuppressNextRuneEffect)
                    {
                        effects.Add(new BattleTokenEffectSuppressNextRune());
                        continue;
                    }
                    BattleSpellEffectDefinition definition =
                        TableBackedBattleSpellDefinitionFactory.CreateEffect(effect);
                    if (!definition.IsSupportedInBattleRuntime)
                    {
                        throw new InvalidOperationException(
                            $"RuneData {rune.Id} draw effect {effect.Id} is unsupported in Battle runtime.");
                    }

                    effects.Add(new BattleTokenEffectGameEffect(definition, rune.ElementType));
                }
            }

            if (rune.UnstableValue > 0)
            {
                effects.Add(new BattleTokenEffectAddUnstable(rune.UnstableValue));
            }

            return effects.AsReadOnly();
        }

    }
}
