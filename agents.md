# Mini Healer Modding Notes

## What this install says

- This is a Unity Mono build, not IL2CPP.
- The main game logic is in `MiniHealer_Data/Managed/Assembly-CSharp.dll`.
- The install includes many Unity runtime DLLs plus `steam_api64.dll`.
- There is no obvious built-in mod loader in the game folder (`BepInEx`, `MelonLoader`, `Doorstop`, `Harmony` patch folders were not present).
- Game content is heavily stored in Unity asset files:
  - `resources.assets`
  - `sharedassets*.assets`
  - `globalgamemanagers`
  - `resources.resource`

## What can likely be modded

1. Code/logic mods
   - Likely possible through DLL injection or runtime patching.
   - Because the game uses managed assemblies, Harmony-style patches or a BepInEx Mono setup should be the most practical route.
   - This is the best path for changing gameplay rules, UI behavior, stats, drop logic, enemy tuning, and similar script-driven systems.

2. Asset/content mods
   - Likely possible by editing Unity serialized assets and bundles.
   - This is the best route for textures, icons, sprites, sounds, localization text, and other stored content.
   - Tools in this area are typically AssetStudio, UABE, or a Unity asset/bundle workflow.

3. Save/config tweaks
   - Possible, but the save location is not visible in the install folder.
   - Expect player data to live in the Unity persistent-data location under the user profile, not beside the executable.

## What looks limited

- No evidence of an official mod SDK or workshop integration in the local files.
- No source project is present, so direct rebuild-based modding is not the path here.
- If the game validates files or packs important data into large asset files, some content changes may require repacking rather than simple file replacement.

## Practical conclusion

- Yes, this game is probably moddable.
- The strongest modding paths are:
  - managed-code patching for gameplay changes
  - Unity asset editing for content changes
- The install structure strongly favors a standard Unity PC modding workflow.

## Concrete hook points found in `Assembly-CSharp.dll`

- Core game systems:
  - `DataSaver`
  - `SaveConflictManager`
  - `GlobalConfigManager`
  - `SettingViewController`
  - `LocalizationManager`
  - `SteamManager`

- Combat and progression:
  - `SkillDataController`
  - `TalentDataController`
  - `QuestDataManager`
  - `LootTableManager`
  - `BossDataController`
  - `BattleScreenUIManager`
  - `EnemyContainer`
  - `DamageData`

- UI and interaction:
  - `MenuUIManager`
  - `BattleCombatLogModalUI`
  - `ItemSelector`
  - `SkillTreeViewUIManager`
  - `SkillListUIManager`
  - `TooltipUIManager`
  - `TowerAtlasUIManager`
  - `AireneTowerShopUIManager`

## Most promising mod types

- Balance mods:
  - patch skill, talent, loot, boss, or enemy data loading
  - adjust damage/stat formulas in combat classes

- UI mods:
  - patch menu and tooltip classes
  - add overlays, filters, search, or quality-of-life panels

- Content mods:
  - edit Unity asset files for icons, sprites, text, and other serialized content

- Save-data mods:
  - inspect or patch `DataSaver` and the various `*SaveInfo` classes if you want persistence changes

## UI Quality Of Life

### What looks easiest to mod

- This game has a lot of UI already broken into dedicated manager classes, which is a good sign for QoL patching.
- The most promising targets are:
  - `MenuUIManager`
  - `TooltipUIManager`
  - `BattleCombatLogModalUI`
  - `SkillListUIManager`
  - `SkillTreeViewUIManager`
  - `ItemSelector`
  - `StatusEffectsViewer`
  - `BattleScreenUIManager`
  - `BattleSuccessUIManager`
  - `SettingViewController`
  - `ScreenManager`
  - `ScrollManager`
  - `UtilsManager`
  - `TowerAtlasUIManager`
  - `AireneTowerShopUIManager`
  - `AireneTowerInsideUIManager`
  - `AireneTowerEventsUIManager`
  - `RuneAtlasUIManager`
  - `TransmogUIManager`

### What each area is for

- `TooltipUIManager` and `StatusEffectsViewer` are the best places for richer hover info, filtered displays, or more readable stat breakdowns.
- `BattleCombatLogModalUI` is the obvious hook for combat-log filtering, search, timestamping, and compact log modes.
- `MenuUIManager`, `BattleScreenUIManager`, and `BattleSuccessUIManager` are the main entry points for general HUD behavior, screen transitions, and post-fight flow.
- `SkillListUIManager` and `SkillTreeViewUIManager` are the best candidates for search, sorting, category filters, favorites, and better comparison views.
- `ItemSelector` is useful for inventory and loot selection QoL, especially if you want bulk actions or smarter default selection.
- `SettingViewController` is where accessibility or interface toggles would most naturally live.
- `ScreenManager` and `ScrollManager` look useful for generic navigation, fade timing, scrolling, and panel behavior.
- The tower, rune, and transmog managers suggest there are already specialized UI flows that can usually be patched without touching the core combat code.

### Common QoL mod patterns

- Add a passive overlay rather than replacing the base UI if you only need extra information.
- Patch refresh methods so your changes persist when the game rebuilds a panel.
- Hook button creation or list population methods if you want search, filters, or new sort modes.
- Use existing text and tooltip components where possible instead of injecting a completely separate rendering system.
- For combat and progression views, prefer augmenting the existing data model over duplicating it.
- If you need a feature to survive scene changes, patch the manager that owns the screen rather than the temporary widget.

### What to watch for

- Some UI is likely rebuilt from coroutine-driven flows, so a one-time patch may disappear after refresh or scene transitions.
- If you modify a list, the game may reapply sorting or filtering from saved state, so patch the source list population method as well.
- Steam/cloud save synchronization does not directly affect UI mods, but UI changes that depend on save data should still tolerate older archive files.
- Many UI systems look data-driven, but some screens likely hardcode special cases, so test battle, inventory, and tower screens separately.

### Best modding approach

- For simple QoL, Harmony patches on the UI manager refresh/build methods are the safest route.
- For bigger UI additions, a BepInEx Mono setup is the most practical way to add persistent panels, overlays, or custom input handling.
- If you only need cosmetic or layout changes, a Unity asset edit may be enough for sprites, icons, or static text.

## Save Files

### Where saves live

- Player data is stored under `AppData\LocalLow\MiniHealer\MiniHealer\`, not beside the game executable.
- The main save files currently visible there are:
  - `globalPlayerData.txt`
  - `player0.txt`, `player1.txt`, and matching `_CLOUD` copies
- The game also keeps archive backups in `Archive\*.txt`.
- `steam_autocloud.vdf` suggests Steam Cloud sync is involved, so local edits may be overwritten if cloud sync is active.

### What the files look like

- The save files are plain text on disk, but they are not human-readable JSON.
- The contents are base64 text that decodes to binary data.
- That binary payload is then handled by `DataSaver.Encrypt` and `DataSaver.Decrypt`, so save editing is probably an encrypted serialization pipeline rather than a simple text format.
- `DataSaver` exposes the relevant entry points:
  - `loadData`
  - `loadRawData`
  - `saveData`
  - `deleteData`
  - `validateSavedData`

### Save containers found in `Assembly-CSharp.dll`

- Progress and account state:
  - `AscendSaveInfo`
  - `MasterySaveInfo`
  - `QuestSaveInfo`
  - `TaroCardSaveInfo`
  - `RunewordHintSaveInfo`
  - `LevelCompleteionSaveInfo`
  - `DepthBossCompletionSaveInfo`

- Skill and talent progress:
  - `SkillTreeSaveInfo`
  - `SkillUpgradeSaveInfo`
  - `TalentMasterySaveInfo`

- Inventory and materials:
  - `MaterialSaveInfo`
  - `ArtifactSaveInfo`
  - `ArtifactEquippedSaveInfo`
  - `ArtifactEquippedSaveIds`

- Depth and dungeon state:
  - `DepthSaveInfo`
  - `DepthFloorSaveInfo`
  - `DepthFloorSpawnSaveInfo`

- Artifact substructures:
  - `ArtifactSaveAttribute`
  - `ArtifactAttrUpgradeSaveInfo`
  - `MonsterModSaveAttribute`

### What each container is doing

- `SkillTreeSaveInfo` stores `skillTreeNodekey`, `currentLevel`, and `isFavorite`.
- `SkillUpgradeSaveInfo` stores `skillKey` and `numOfUpgrade`.
- `TalentMasterySaveInfo` stores `talentKey` and `currentLevel`.
- `MaterialSaveInfo` stores `Key`, `owned`, `amountUsed`, and `timeUsed`.
- `QuestSaveInfo` stores `key` and `completionStatus`.
- `TaroCardSaveInfo` stores `Key`, `owned`, and `used`.
- `MasterySaveInfo` stores mastery level, spent/unspent points, class type, and XP totals.
- `LevelCompleteionSaveInfo` stores difficulty clears, kill counts, best times, dungeon flags, and chaos modifiers.
- `DepthSaveInfo` stores the depth key, tier, completion count, chaos tier, chaos attributes, and infusion info.
- `DepthFloorSaveInfo` and `DepthFloorSpawnSaveInfo` store floor templates, room layout, spawn lists, and whether the floor is cleared.
- `ArtifactSaveInfo` is the most complex container:
  - it stores artifact identity, equipment state, rarity/quality overrides, socket and attribute lists, upgrade tracking, mutation counts, and material usage.
- `ArtifactSaveAttribute` stores attribute type, added type, trio type, tier, quality, lock state, negativity, and override bounds.
- `ArtifactAttrUpgradeSaveInfo` stores per-attribute upgrade counts.
- `ArtifactEquippedSaveInfo` and `ArtifactEquippedSaveIds` map save IDs to equipped slot positions.

### How to mod saves safely

- Best-case path: patch `DataSaver` or the game logic that builds these save objects, then let the game re-save them.
- If you want manual edits, work through the game's deserialize/serialize path instead of editing the base64 blob directly.
- Keep field names and types stable when changing save containers; the game likely depends on serialized member names for load compatibility.
- Add migration code if you introduce new fields or remove old ones, because archive and cloud copies can still contain older layouts.
- If you are changing progression values only, target the specific `*SaveInfo` class rather than the base balance classes.
- If you are changing persistence behavior, also inspect `SaveConflictManager`, since cloud/local conflict resolution can affect which save wins.

## Skill And Talent Balance

### How skill balance works

- `SkillData` is the registry layer:
  - it stores the `Skill[]` array and a `skillsMap` lookup by key.
- `Skill` is the actual balance object for each spell/ability:
  - core tuning fields include `unlockLevel`, `manaCost`, `healthCostPercentage`, `castTime`, `castTimeMulti`, `castTimePerLevel`, `baseDuration`, `durationMulti`, `tickRate`, `channelTime`, `aoeRadius`, `cooldownMulti`, `minimumCD`, `BaseUpgradeCost`, `MaxSkillUpgradeLevel`, and scaling fields like `BaseEffectNumber`, `BaseEffectMulti`, `SecondaryEffectNumber`, `SecondaryEffectMulti`, `ThirdEffectNumber`, `ThirdEffectMulti`, plus level-scaling variants.
  - it also carries requirements and restrictions such as required talents/artifacts, target rules, phase rules, effect conflicts, and whether the skill can be cast, crit, or over-heal.
  - the class has event hooks like `OnConsume`, `OnPreCast`, `OnCastStart`, `OnCast`, `OnChannelStart`, `OnChannelTick`, and `OnChannelEnd`, which means special cases are implemented as code callbacks on top of the numeric data.
- `SkillDataController` is where the game turns those values into behavior:
  - it exposes `getSkillByKey`, `getAllPLayerSkills`, `getBaseHealText`, `getHealpowerBonusText`, and a long set of `OnGetDetailedDesc_*` methods.
  - it contains a very large set of per-skill tuning fields, many named like `*_BASE_*`, `*_PER_LEVEL`, `*_COOLDOWN`, `*_DURATION`, `*_MULTI`, `*_CHANCE`, `*_RATIO`, and `*_THRESHOLD`.
  - this is strong evidence that most skill balance is data-driven through serialized controller fields, with custom methods only for description generation and special-case mechanics.

### How skill levels are saved

- Skill progress is not stored in the base `Skill` definition.
- The save layer uses:
  - `SkillTreeSaveInfo` for skill-tree node level, favorite flag, and node key
  - `SkillUpgradeSaveInfo` for direct upgrade counts per skill key
- In practice, the game reconstructs the current skill state by combining:
  - the static `Skill` definition from `SkillData`
  - the saved upgrade count from `SkillUpgradeSaveInfo`
  - the saved tree-node level and favorite state from `SkillTreeSaveInfo`
- The `Skill` object itself also has runtime fields such as `additionalLevel`, `currentCharge`, `maximumCharge`, `castTimeSpent`, `cooldownRemaining`, and `remainingTime`, but those are gameplay/session state rather than the persistent upgrade record.
- If you want to mod progression safely, patch the place where the game applies the saved upgrade counts rather than editing the base skill definition.

### How the UI shows skill data

- The UI is not showing raw save data directly.
- `SkillDataController` and the UI managers build display text from the skill object at runtime.
- The most important display helpers on `Skill` are:
  - `getDescription`
  - `getTotalManaCost`
  - `getTotalCooldownWhenUsed`
  - `getTotalCastTime`
  - `getTotalHealthCost`
  - `getAoeRadius`
  - `getAoeRadiusDiff`
  - `getEnemyCastCooldownWhenUsed`
  - `getDefaultCaster`
  - `hasRuneSocketed`
  - `get_isOffCooldown`
- `SkillDataController` also has description helpers that are clearly meant to turn numeric tuning into readable text:
  - `getBaseHealText`
  - `getHealpowerBonusText`
  - `hydrateSkillInfo`
  - many `OnGetDetailedDesc_*` methods for skill-specific description formatting
- `SkillListUIManager` is the main list population path:
  - its closures show an `addSkillstoUI` path and a `getSkillByKey` lookup path, which is consistent with the game building each skill row from the current skill definition.
- `TooltipUIManager` is the most likely place where the expanded hover text is composed for each skill, including the derived values from `SkillDataController` and the numeric fields on `Skill`.
- `SkillSetupUIMananger` is likely where equipped skill slots, selection state, and per-skill options are presented during build/loadout changes.
- The combat and status screens then reuse those same skill definitions indirectly through the battle UI, not through a separate skill model.

### What the player actually sees

- Healing and damage text are usually derived from the same underlying numeric fields that determine effect strength, scaling, and target counts.
- Cooldown text comes from the total cooldown helpers plus any minimum cooldown or cooldown multiplier logic.
- Mana cost text comes from the total mana cost helper plus any rune, talent, or cost-multiplier adjustments.
- Cast time text comes from the total cast-time helper and the skill’s current cast-time modifiers.
- Range/AOE text is derived from the radius helpers and target-behavior flags.
- Whether a skill is shown as usable, locked, off cooldown, or channeling is driven by the runtime state fields on `Skill` plus the controller/UI refresh logic.

### Practical modding takeaway

- If you change a skill’s numeric balance, the UI will usually follow automatically because the text is generated from the same `Skill` fields.
- If you want the UI to show a new metric, patch `SkillDataController` text generation or the tooltip/list refresh methods, not just the underlying skill numbers.
- If you want to change how many levels a skill can have, patch both the static skill definition and the save application code so old saves still load correctly.
- If you only change the save counters, the displayed numbers should still update as long as the controller rebuilds the skill summary from the saved upgrade counts.

### How talent balance works

- `TalentData` is the talent registry:
  - it stores the `Talent[]` array and a `talentsMap` lookup.
- `Talent` contains the progression and gating state:
  - key fields include `tier`, `maxLevel`, `m_currentLevel`, `m_currentBaseLevel`, `bonusLevel`, `baseCost`, `costsWillGrow`, `costsGrowthLevel`, `procCooldownMin`, `procCooldownMax`, `relatedElements`, `masteryAttributes`, and the event hooks for learn/proc/engage/channel/shield/death effects.
  - talents also have unlock and class gating via `Type`, `EssenceEhanceType`, `EssenceEhanceRequiredNumInvested`, and `Position`.
- `TalentDataController` is the main balance engine for talents:
  - it has `LoadTalentData`, `FindTalentByKey`, `getTalentUpgrateReq`, `getTalentDowngradePoint`, `gainMasteryExp`, `resetMasteryByClass`, and `getTalentDescriptionByTalent`.
  - it contains many `OnLearn_*`, `OnBattleProc_*`, and `OnEngage_*` methods for named talents, so some talents are pure numeric modifiers while others are hardcoded behavior changes.
  - the field list includes a very large number of talent-specific balance constants, which again points to serialized tuning data rather than balance hardcoded only in formulas.

### What this means for modding

- Simple balance tweaks are likely best done by changing serialized skill/talent values and then letting the existing controller code format and apply them.
- Bigger overhauls may need Harmony patches on `SkillDataController` and `TalentDataController` methods for:
  - custom unlock rules
  - changed upgrade costs
  - altered mastery scaling
  - rewriting individual `OnLearn_*` or `OnGetDetailedDesc_*` behaviors
- Save files appear separate from balance:
  - `SkillUpgradeSaveInfo`, `SkillTreeSaveInfo`, and `TalentMasterySaveInfo` store player progress, not the base tuning itself.

## Loot And Drop Tables

### Loot architecture

- `LootTableManager` is the main reward-rolling controller.
- `GenericLootDropItem<T>` and `GenericLootDropTable<T, U>` are the generic infrastructure:
  - each loot item has a `probabilityWeight`, a derived `probabilityPercent`, and a computed probability range.
  - `PickLootDropItem()` is the generic weighted selector.
  - `ValidateTable()` suggests the game checks that tables are internally consistent.
- `ArtifactEleDamageLootTable` is a specialized weighted table:
  - it contains `ArtifactEleDamageLootDrop` rows with `DamageElement` plus weight.
- `BossMatLootItemInfo` shows boss material tables are also data-driven:
  - each row has a material key, weight, and min/max amount.

### What `LootTableManager` controls

- Material drops:
  - `getMaterialDropNoDropChanceByType`
  - `didMaterialDropByType`
  - `getMaterialDropAmountByType`
  - `buildMaterialLootDropTableByLevel`
  - `buildTotalMaterialLootDropTableByMonsterLevel`
- Artifact drops:
  - `rollArtifactByLevelId`
  - `rollArtifactModular`
  - `rollArtifactDropRarity`
  - `rollArtifact`
  - `rollSpriteIndex`
  - `rollSpriteMaskIndex`
  - `rollAttributesByLevel`
  - `rollAttributesModular`
  - `getNumOfAttrByLevelAndRarity`
  - `getAttributeTierTableByLevel`
  - `getDepthAttributeTierTableByLevel`
  - `getAttributeTierTableByDungeonLevel`
- Depth and boss rewards:
  - `getDepthDropNoDropLootTable`
  - `rollDepthRENumOfAttributes`
  - `rollDepthNumOfRollsArtifact`
  - `getDepthRoomDropTable`
  - `getBossSpecificDropTable`
  - `rollDepthUniqueArtifacts`
  - `rollBossSpeicalMat`
- Special handling:
  - `OnRollDropNoDrop_GUARENTEED_LEGENDARY_DROP`
  - `OnAcquireArtifact_CURSED_ITEMS`
  - `OnAcquireMaterial_CURSED_ITEMS`

### Balance fields found

- Material drop tuning:
  - `MAT_BASE_DROPNODROP_CHANCE`
  - `MAT_BASE_DROP_AMOUNT_WEIGHT_ONE` through `MAT_BASE_DROP_AMOUNT_WEIGHT_FIVE`
- Artifact drop tuning:
  - `ARTIFACT_BASE_DROP_AMOUNT_WEIGHT_ONE` through `ARTIFACT_BASE_DROP_AMOUNT_WEIGHT_FOUR`
  - `ARTIFACT_BASE_DROP_RARITY_WEIGHT_UNCOMMON`
  - `ARTIFACT_BASE_DROP_RARITY_WEIGHT_RARE`
  - `ARTIFACT_BASE_DROP_RARITY_WEIGHT_EPIC`
  - `ARTIFACT_BASE_DROP_RARITY_WEIGHT_LEGENDARY`
- The controller also carries large sprite maps by artifact type, which suggests artifact visuals and slot/type selection are part of the drop pipeline.

### What this means for modding

- Material farming changes should be easiest to patch in `LootTableManager` by adjusting the base no-drop chance or amount weights.
- Artifact balance is more complex because it mixes:
  - rarity weighting
  - attribute count rolls
  - attribute tier tables
  - sprite/slot selection
- Boss and depth reward changes are likely easiest if you patch the table builders and room-specific drop methods.
- Generic table logic is reusable and probably the safest place to hook if you want to add new loot categories or rebalance all weighted tables at once.

## Runtime Item Injection Template

### Working example

- `Aegis Choir` was added by BepInEx/Harmony injection only.
- The plugin entry point lives at `tmp\LesserHealCostMod\LesserHealCostMod.cs`.
- Item-specific weapon logic lives in `tmp\LesserHealCostMod\AegisChoirMod.cs`.
- Shared artifact and reflection helpers live in `tmp\LesserHealCostMod\ModHelpers.cs` and should be reused when adding another item.
- Built plugin output is `BepInEx\plugins\MiniHealerTestPlugin.dll`.
- Build command from `tmp\LesserHealCostMod`:

```powershell
$env:DOTNET_CLI_HOME = (Resolve-Path ..\..\tmp).Path; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'; $env:DOTNET_NOLOGO = '1'; dotnet build .\LesserHealCostMod.csproj -c Release -o ..\..\BepInEx\plugins
```

### Core pattern

- Do not edit game assets or original assemblies.
- Put reusable helper code in the shared helper file, not in each item file.
- Keep each custom item in its own file so registration, loot, UI, stats, and custom effects stay grouped by item.
- Clone an existing artifact template instead of constructing a sparse `new Artifact`.
- Prefer a template of the same slot/type:
  - for a staff, clone an existing `Artifact` where `SlotType == WEAPON` and `Type == STAFF`
  - fallback to another weapon only if no same-type template exists
- Copy all non-delegate instance fields from the template, then override only the custom item fields.
- Register the item by replacing the artifact collections copy-on-write:
  - `ArtifactsData.ArtifactList`
  - `ArtifactsData.Artifacts`
  - `ArtifactsData.artifactsMap`
- Remove any old item with the same key before adding the new one, or repeated scene loads can duplicate entries.

### Required artifact fields

- Set a stable key, for example `CODEX_AEGIS_CHOIR`.
- Set display identity:
  - `ArtifactName`
  - `Key`
  - `Rarity`
  - `SlotType`
  - `Type`
  - `MutationPoolType`
- Set discover/crafting/drop behavior:
  - `HiddenItemLevel`
  - `DropRate`
  - `weight`
  - `isEquippable = true`
  - `isMutateable = true`
  - `isAugmentable = true`
  - `isDiscoverable = true`
  - `isDepth = false` unless deliberately making a depth artifact
  - `isDivine = false` unless deliberately making a divine item
  - `linkedDivineArtifactKey = string.Empty`
  - `linkedNormalArtifactKey = string.Empty`
  - `droppedBossName`
  - `droppedLevelName`
  - `PurchaseMat`
  - `PurchasePrice`
- Keep the cloned `spriteIndex` unless there is a verified valid sprite index for that artifact type.
- Set `Icon` to a safe existing icon if needed, but do not force invalid sprite indices.

### Atlas and codex safety

- Do not patch `ItemAtlasUIManager.refreshItemAtlas` or `initItemAtlas` to inject items into the UI list directly.
- Add the artifact to the underlying artifact data collections instead.
- Add an entry to `ArtifactDataController.baseArtifactSearchStringMap` for the new key.
- Missing search-map entries caused the staff section to fail loading while other weapon subtypes worked.
- Do not put literal prose into `Artifact.specialDesc` unless it is verified to be accepted as literal text.
- `specialDesc` is often localization/special-case driven; literal custom text caused tooltip error strings.
- If the item should always be visible in the atlas, patch `ArtifactDataController.isArtifactUnlocked` for the new key only.

### Tooltip field mapping

- The atlas fields are misleading:
  - `UniqueText` is the item type/subtitle line, for example `(Staff, Unique)`.
  - `DescriptionTextMeshPro` is the main stats/effects body.
  - `ExtraInfoText` is drop/crafting/acquisition information.
- Do not write stats into `UniqueText`.
- Do not overwrite `ExtraInfoText` unless intentionally replacing drop/crafting info.
- Prefer patching `OtherGameDataController.getDescriptionByArtifact` to append custom effect text so atlas and crafting screens share the same description output.
- For the custom proc text, append one line in a postfix when `artifact.Key` matches the custom item.

### Native stats and upgrade support

- Do not implement core stats only through `Artifact.OnAddCurrentBonus` or `OnGetBaseAttackDamage` if the item should show stats and support random base attribute upgrades.
- The crafting UI and random base attribute upgrade flow use `AttributesManager.getArtifactBaseAttributes`.
- Patch `AttributesManager.getArtifactBaseAttributes` for the custom item and return native `ArtifactAttribute` entries.
- For `Aegis Choir`, the working native attributes are:
  - `ArtifactAttribute.AttriubteType.INCREASE_HEALER_PHYSICAL_DAMAGE_FLAT`
  - `ArtifactAttribute.AttriubteType.INCREASE_ALL_HP_FLAT`
  - `ArtifactAttribute.AttriubteType.INCREASE_HEALER_ATT_SPD_PERCENT`
- Use `AttributesManager.ATRM.getBaseAttributByType(attributeType)` to get the base-game attribute template.
- Set:
  - `addedType = ArtifactAttribute.AddedType.BASE`
  - `T1_MIN`, `T1_MAX`
  - `T2_MIN`, `T2_MAX`
  - `T3_MIN`, `T3_MAX`
  - `quality = 100`
  - `tier = 3`
  - `isUpgradeAble = true`
- For the custom item, replace the returned base attribute list instead of appending to it. Appending preserved the cloned staff template's original stats and caused doubled base stats.
- Give each stat a real min/max range. If min and max are identical, the random upgrade button can work but the stat will not visibly improve.
- Current `Aegis Choir` ranges:
  - healer physical damage: `5000` to `6000`
  - party health: `5000` to `6000`
  - healer attack speed: `20` to `25`

### Save attributes and old-copy cleanup

- Old attempts used seeded `ArtifactSaveAttribute` entries with `AddedType.ROLL_BASE`.
- That caused duplicated/cursed tooltip lines after native base attributes were added.
- The working pattern removes custom seeded `ROLL` and `ROLL_BASE` entries for the custom base stat types.
- `ArtifactSaveInfo.AttributeUpgrade` should be initialized before upgrade logic runs.
- Patch `ArtifactSaveInfo.upgradeRandomBaseAttribute` prefix only to repair/initialize save-info state, not to add duplicate stat rows.

### Custom effects

- Keep effects like "on healer attack shield a random party member" as artifact event hooks.
- For `Aegis Choir`, only `OnAttack` is wired for custom behavior.
- Native stats should be handled through `AttributesManager.getArtifactBaseAttributes`, not through `OnAddCurrentBonus` or `OnGetBaseAttackDamage`, to avoid double stats and broken UI/upgrade behavior.
- The shield effect uses:
  - `UtilsManager.UTILM.getGenericShieldEffect(...)`
  - fallback `Character.setShielded(true, amount)` if the generic shield effect is unavailable
- Filter targets defensively:
  - non-null
  - not enemy
  - not dead

### Reusable Helpers

- Reuse `ModHelpers.GetFieldValue` and `ModHelpers.SetFieldValue` for reflection access in future item mods.
- Reuse `ModHelpers.CopyArtifactTemplate` and `ModHelpers.ReplaceArtifactCollections` when cloning/registering custom artifacts.
- Reuse `ModHelpers.AppendKeyCopy` when adding a custom item key to loot lists.
- Reuse `ModHelpers.AddOrReplaceBaseAttribute` when building native base stats for upgrade support.
- Reuse `ModHelpers.SetTextField` and `ModHelpers.LogAtlasRefreshFailure` for UI patch plumbing.
- Reuse `ReferenceEqualityComparer` when a patch needs identity-based tracking of object instances.
- Keep new item files focused on the item itself and call into shared helpers for generic plumbing.

### Loot and crafting

- Add the item to boss loot through data and/or drop-table patching:
  - copy-on-write append to guardian `DifficultyData.Loot`
  - copy-on-write append to guardian `Boss.depthLoot`
  - optional postfix on `LootTableManager.getBossSpecificDropTable`
- For atlas crafting, set `PurchaseMat` and `PurchasePrice`.
- The greater alchemy shard key found in code is `GR_ALCHEMY_SHARD`.
- `ItemAtlasUIManager.getArtifactPurchaseMat` has special fallback logic for normal guardian/depth items, so patch it for the custom key if the item must use a specific material.
- Current `Aegis Choir` atlas crafting cost is `1` greater alchemy shard.

### Common failure modes

- Staff atlas section stuck on the previous list:
  - likely an exception while building the staff list
  - check missing `baseArtifactSearchStringMap` entry first
  - avoid invalid `spriteIndex`
- Tooltip shows error text:
  - likely literal text in a field that expects a localization key
  - clear `specialDesc` and append custom text through `OtherGameDataController.getDescriptionByArtifact`
- Stats do not show in crafting:
  - the item lacks native base attributes from `AttributesManager.getArtifactBaseAttributes`
- Random base attribute upgrade button is grey:
  - `ArtifactSaveInfo.canArtifactBeOreUpgraded` found no base attributes from `AttributesManager.getArtifactBaseAttributes`
- Upgrade works but values do not change:
  - base attribute min/max values are identical
- Stats appear doubled:
  - the custom item kept cloned template base attributes and appended new ones
  - replace the base attribute list for the custom key instead
