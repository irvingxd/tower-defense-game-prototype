# Tower branches (design proposal)

At the level 4 → 5 milestone you pick one of two **branches**; at 9 → 10 one of two **specialisations**
within it. 3 towers × 2 × 2 = **12 end-forms**. The milestone's ×2 / ×3 damage and cost rules stay; branches
reshape *how* the damage is dealt. Damage per gold stays roughly flat (±15%) — each form is stronger in
its niche and weaker outside it, never simply better.

![Tower branch previews](tower-branches.png)

Numbers are multipliers on the unbranched tower at the same level.

## Ballista — all-rounder
| Form | Level | Look | Stats | Excels / Weak |
|---|---|---|---|---|
| **Sniper** | 5 | Tall round tower, pennant | Damage ×2, fire rate ×0.5, range +1.2, 20% crit for ×2 | Bosses, armour (big hits) / crowds |
| ↳ **Deadeye** | 10 | Taller, ember ballista, twin flags | Crit 35% for ×3; executes non-boss creeps under 12% HP | Elites, bosses / swarms |
| ↳ **Piercer** | 10 | Castle siege-ballista on top | Bolts pierce a line of 3 (100 / 70 / 50%) | Lines of ground creeps / flyers spread out |
| **Repeater** | 5 | Turret on round tower | Fire rate ×3, damage ×0.4, range −0.2; Slowing/Splitting proc per hit | Flyers, fast creeps, slow uptime / armour (tiny hits) |
| ↳ **Stormbringer** | 10 | Square tower, twin turrets | Fires at 2 targets at once | Waves of small creeps / armour |
| ↳ **Skyhunter** | 10 | Blue turret, two pennants | ×2 vs flyers, +0.8 range vs flyers | Air themes / ground bosses |

## Cannon — crowds and armour
| Form | Level | Look | Stats | Excels / Weak |
|---|---|---|---|---|
| **Mortar** | 5 | Squat tower, oversized cannon | Splash ×1.6, damage ×1.5, fire rate ×0.6, range +0.8, **ground only** | Packs, armour / flyers |
| ↳ **Bombard** | 10 | Ember cannon, banner | Shells leave burning ground for 2 s (25% of the hit per second) | Swarms, regen / fast creeps |
| ↳ **Earthshaker** | 10 | Rubble around the base | Stuns 0.6 s (bosses: 30% slow instead), 4 s per-creep cooldown | Controlling packs / bosses |
| **Flak** | 5 | Twin blue cannons | ×1.8 vs flyers, fire rate ×1.3, ground damage ×0.8 | Air waves / armoured ground |
| ↳ **Skyburst** | 10 | Blue turret, wide flag | ×2.5 vs flyers, splash ×2 against air | Flying swarms / ground |
| ↳ **Hailstorm** | 10 | Three cannons | 3 shells per volley at 3 targets (60% each) | Mixed waves / lone bosses |

## Catapult — bosses and tanks
| Form | Level | Look | Stats | Excels / Weak |
|---|---|---|---|---|
| **Trebuchet** | 5 | Castle trebuchet on a base | Siege bonus ×1.75 → ×2.5, range +1.5, fire rate ×0.8 | Bosses, tanks / flyers, fast |
| ↳ **Titan** | 10 | Trebuchet on scaffolding, flag | +2% of target's max HP per hit vs bosses & tanks | Late bosses / everything small |
| ↳ **Wallbreaker** | 10 | Ember trebuchet, ram, rubble | Hits shred 20% armour for 5 s (stacks to 60%) | Armoured waves, helps all towers / flyers |
| **Frost Engine** | 5 | Blue crystal tower | Splash slows 25% for 2 s (bosses half), hits flyers at 60%, damage ×0.7, loses siege bonus | Control, air coverage / raw damage |
| ↳ **Glacier** | 10 | Taller, crystals at the base | Every 3rd shot freezes 1 s (bosses: 40% slow) | Packs, fast creeps / bosses |
| ↳ **Blizzard** | 10 | On snow, ice crystals | 20% slow aura in range + chilled creeps take +10% damage | Supporting other towers / alone |

## How it plays
- Upgrading 4 → 5 or 9 → 10 opens a **choice** with both forms' previews and stats; the tower rebuilds into the chosen look.
- The choice is permanent for that tower (sell to change). Research still applies (e.g. Slowing on a Repeater).
- The AI picks branches with its three-wave look-ahead, the same way it picks towers today.

## Assets used
All from kits already in the repo: Tower Defense Kit (turret, crystals, rocks, snow tiles, wood scaffolding) and
Castle Kit (siege ballista, trebuchet, ram, flags). Preview rendered with `-- --sheet branches <out.png>`.
