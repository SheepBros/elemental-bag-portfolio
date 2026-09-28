using System;
using System.Collections.Generic;

namespace ElementalBackHero
{
    /// <summary>
    /// Provides read-only access to Luban generated tables. Copy generated row values into runtime state before mutation.
    /// </summary>
    public sealed class LubanGameDataTables : IGameDataTables
    {
        public LubanGameDataTables(IGameDataByteLoader byteLoader)
        {
            if (byteLoader == null)
            {
                throw new ArgumentNullException(nameof(byteLoader));
            }

            Tables = new Tables(byteLoader.Load);
            ValidateContentIdBandsOrThrow(Tables);
        }

        public Tables Tables { get; }

        public static void ValidateIdBandOrThrow(
            string tableName,
            IEnumerable<int> ids,
            int minimumId,
            int maximumId)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name is required.", nameof(tableName));
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            if (minimumId <= 0 || maximumId < minimumId)
                throw new ArgumentOutOfRangeException(nameof(minimumId));

            foreach (int id in ids)
            {
                if (id < minimumId || id > maximumId)
                {
                    throw new InvalidOperationException(
                        $"{tableName} row {id} is outside its content ID band "
                        + $"{minimumId}~{maximumId}.");
                }
            }
        }

        private static void ValidateContentIdBandsOrThrow(Tables tables)
        {
            ValidateIdBandOrThrow("RuneData", tables.TbRuneData.DataMap.Keys, 10001, 19999);
            ValidateIdBandOrThrow("SpellData", tables.TbSpellData.DataMap.Keys, 20001, 29999);
            ValidateIdBandOrThrow("RelicData", tables.TbRelicData.DataMap.Keys, 30001, 39999);
            ValidateIdBandOrThrow("EnemyData", tables.TbEnemyData.DataMap.Keys, 50001, 59999);
            ValidateIdBandOrThrow("EncounterData", tables.TbEncounterData.DataMap.Keys, 60001, 69999);
            ValidateIdBandOrThrow("EnemyActionPatternData",
                tables.TbEnemyActionPatternData.DataMap.Keys, 210001, 219999);
            ValidateIdBandOrThrow("BattleModifierData",
                tables.TbBattleModifierData.DataMap.Keys, 220001, 229999);
            ValidateIdBandOrThrow("EnemyActionData",
                tables.TbEnemyActionData.DataMap.Keys, 230001, 239999);
            ValidateIdBandOrThrow("GameEffectData",
                tables.TbGameEffectData.DataMap.Keys, 240001, 249999);
            ValidateIdBandOrThrow("EffectGroupData",
                tables.TbEffectGroupData.DataMap.Keys, 250001, 259999);
        }
    }
}
