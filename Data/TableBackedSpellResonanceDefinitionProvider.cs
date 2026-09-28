using System;

namespace ElementalBackHero
{
    public sealed class TableBackedSpellResonanceDefinitionProvider : ISpellResonanceDefinitionProvider
    {
        private readonly Tables _tables;

        public TableBackedSpellResonanceDefinitionProvider(IGameDataTables gameDataTables)
        {
            if (gameDataTables == null)
            {
                throw new ArgumentNullException(nameof(gameDataTables));
            }

            _tables = gameDataTables.Tables ?? throw new ArgumentException(
                "Game data tables must not be null.",
                nameof(gameDataTables));
        }

        public SpellResonanceDefinitionSnapshot GetSpellResonance(int spellResonanceId)
        {
            SpellResonanceData resonance = GetRequired(
                _tables.TbSpellResonanceData.GetOrDefault(spellResonanceId),
                nameof(SpellResonanceData),
                spellResonanceId);
            SpellData spell = GetRequired(_tables.TbSpellData.GetOrDefault(resonance.SpellId),
                nameof(SpellData),
                resonance.SpellId);

            ValidateProductionEffectGroup(resonance);

            ValidateMaterialFilter(resonance, spell);

            return new SpellResonanceDefinitionSnapshot(
                resonance.Id,
                resonance.Key,
                resonance.SpellId,
                spell.ElementType,
                resonance.ResonanceLevel,
                spell.DisplayName,
                resonance.MaterialFilterType,
                resonance.MaterialElementType,
                resonance.UpgradedEffectGroupId,
                resonance.Tags,
                resonance.Description);
        }

        public bool TryGetNextResonance(int spellId, int currentResonanceLevel,
            out SpellResonanceDefinitionSnapshot resonance)
        {
            if (spellId <= 0)
            {
                resonance = null;
                return false;
            }

            int nextLevel = currentResonanceLevel + 1;
            foreach (SpellResonanceData row in _tables.TbSpellResonanceData.DataList)
            {
                if (row.SpellId == spellId
                    && row.ResonanceLevel == nextLevel
                    && row.UpgradedEffectGroupId > 0)
                {
                    resonance = GetSpellResonance(row.Id);
                    return true;
                }
            }

            resonance = null;
            return false;
        }

        private void ValidateProductionEffectGroup(SpellResonanceData resonance)
        {
            if (resonance.UpgradedEffectGroupId <= 0)
            {
                throw new InvalidOperationException(
                    $"SpellResonanceData {resonance.Id} is not production-ready: upgraded_effect_group_id must be positive.");
            }

            GetRequired(_tables.TbEffectGroupData.GetOrDefault(resonance.UpgradedEffectGroupId),
                nameof(EffectGroupData),
                resonance.UpgradedEffectGroupId);
        }

        public RuneDefinitionSnapshot GetRune(int runeId)
        {
            RuneData rune = GetRequired(
                _tables.TbRuneData.GetOrDefault(runeId),
                nameof(RuneData),
                runeId);
            return new RuneDefinitionSnapshot(
                rune.Id,
                rune.DisplayName,
                rune.ElementType,
                rune.IsUnstable,
                rune.RarityType,
                rune.RuneKindType,
                rune.DrawEffectGroupId,
                rune.ResonanceExperience,
                rune.UnstableValue,
                rune.RewardEligible);
        }

        public bool SpellExists(int spellId)
        {
            return _tables.TbSpellData.GetOrDefault(spellId) != null;
        }

        private static void ValidateMaterialFilter(SpellResonanceData resonance, SpellData spell)
        {
            switch (resonance.MaterialFilterType)
            {
                case ResonanceMaterialFilterType.AnyNonUnstable:
                    if (resonance.MaterialElementType != ElementType.None)
                    {
                        throw new InvalidOperationException(
                            $"SpellResonanceData {resonance.Id} AnyNonUnstable must use material_element_type None.");
                    }

                    break;
                case ResonanceMaterialFilterType.SameElement:
                    if (spell.ElementType == ElementType.None)
                    {
                        throw new InvalidOperationException(
                            $"SpellResonanceData {resonance.Id} SameElement requires a spell element.");
                    }

                    break;
                case ResonanceMaterialFilterType.SpecificElement:
                    if (resonance.MaterialElementType == ElementType.None)
                    {
                        throw new InvalidOperationException(
                            $"SpellResonanceData {resonance.Id} SpecificElement requires material_element_type.");
                    }

                    break;
                case ResonanceMaterialFilterType.Tag:
                    throw new InvalidOperationException(
                        $"SpellResonanceData {resonance.Id} Tag material filter is not supported in v0.");
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(resonance.MaterialFilterType),
                        resonance.MaterialFilterType,
                        "Unknown resonance material filter type.");
            }
        }

        private static T GetRequired<T>(T value, string dataTypeName, int id) where T : class
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{dataTypeName} {id} was not found.");
            }

            return value;
        }
    }
}
