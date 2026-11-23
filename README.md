# SEA WANDERER

The Wanderer sails into the mist on her own. Keep her crewed, fed and armed, chart the sea, and
find the Drowned Crown.

An ambient auto-explore pirate game. You never steer. One ship, the Wanderer, picks her own course
through a fogged sea. You decide what she buys, what she trades, and whether she fights or runs.
If she sinks or is boarded, the voyage is over and its save is deleted.

Art, UI and names are original. Single file, no build step. Three.js r160 comes from jsDelivr.

Older builds are kept in `backup/`:

- `flotilla-capped.html` is the fleet-of-ships version, from when the game was called Fogbound. It still reads the old `fogbound.v2` save.
- `caravan-uncapped.html` and `fleet-v1.html` are earlier builds.

## Play

| Input | Action |
|---|---|
| Drag, right-drag, two fingers | Pan (turns off follow) |
| Wheel or pinch | Zoom, out to the full chart |
| `F` | Follow the Wanderer |
| `Space` | Fit the charted sea |
| Click a ship, port or landing site | What it is |
| `U` or `Tab` | Charts & Rigging, the upgrade tree. Works in port too. |
| `C` | Crew & Ship: stats, hands, roles, relics |
| `M` | Sound |
| `Esc` | Menu. In port, `Esc` casts off. |

The simulation pauses while the menu, tree, crew panel, a port, or an encounter card is open.

### Hands-off

**Hands-off** in the menu lets the game play itself. With it on:
- Battles decide themselves ("decide for me").
- Once you have left the game alone for 6 s in port, she runs the usual errands (sell charts, repair,
  restock, hire), then fits the cheapest upgrades she can afford while keeping enough gold to restock.
- After 45 s with no input, open panels and the menu close and the camera follows her again.
- A lost voyage makes way for a new sea after 15 s, a won one after 40 s.

Any input resets those timers, so you can still step in. Turn it off and ports and battles wait for you
again. It is saved with your settings.

In headless runs, the hands-off autopilot won 6 of 7 voyages, each in 64–82 minutes.

## Desktop wallpaper (Windows)

`wallpaper/SeaWandererWallpaper.exe` runs the game as a live desktop background. It needs nothing
beyond Windows itself: Edge shows the page, and the program is built with the C# compiler that comes
with Windows (`wallpaper/build.cmd`; the exe is not committed).

- Edge opens `index.html?wallpaper` full screen with its own profile, and the program moves that window
  behind the desktop icons.
- A mouse hook passes clicks, drags and the wheel on empty desktop to the game. Clicks on desktop icons
  and right-clicks still go to the desktop. Edge drops wheel messages there, so each notch is sent as
  F13 or F14, and the game zooms on the pointer.
- While a maximised or full-screen window covers the main screen, the program hides the window and the
  game pauses.
- If Edge closes or Explorer restarts, it starts again by itself.
- The tray icon has Pause, Reload and Quit. A log is at `%LOCALAPPDATA%\SeaWandererWallpaper\wallpaper.log`.

`?wallpaper` mode in the game:
- It has its own save (`fogbound.wallpaper`) and settings, with sound off.
- Hands-off is always on.
- It draws at most 30 frames a second, and keeps the HUD clear of the taskbar and of the icon columns
  (`&left=`, `&bottom=`, both in screen pixels).

## Goods and upkeep

She carries gold and four goods. Goods share the hold: 100 at the start, 600 with every Holds node.

| Good | Used for |
|---|---|
| Gold | Upgrades, repairs, hiring, and anything at a port market |
| Food | Every hand eats each day. With none aboard she slows and hands desert. |
| Wood | Carpenters patch the hull and sails with it between fights |
| Metal | Shot: each gun fired uses a quarter. With none aboard the guns fall silent. |
| Rum | Grog. While there is rum aboard, reloading and boarding are 10% better. |

Upgrades cost gold plus goods. In port, gold also covers any goods she is short of, at that
market's prices.

Goods come from these sources:

- **Ports:** buy and sell at the market.
- **Merchants:** trade with merchantmen that hail her at sea.
- **Landings:** island landing sites hold food, wood, metal or gold, and the longboat rows a party
  ashore to fetch them.
- **Pirates:** plunder from the ones she beats.
- **Flotsam:** barrels of goods, and wrecks that pay gold.

## Ports

About 40 harbours are spread over the sea, with at least three near home. Her home port, on the
starting isle, is known from the start.

Each port makes some goods cheaply and pays well for one. Prices move as you buy and sell.

She docks on her own when she runs low on food or shot, is badly holed, or is short of hands.
Docking pauses the game on the port screen:

- **Market:** buy and sell the four goods.
- **Chart office:** sell what she has charted, or buy a rumour (250 gold) that marks the nearest
  hidden relic, compass or wreck.
- **Shipyard:** repair the hull and sails.
- **Tavern:** hire hands. Each has a role and a rarity.

Port buttons:

- **Sell charts, repair & restock** does the usual errands in one click.
- **Charts & Rigging** opens the tree.
- **Cast off** sends her back to sea.

**Merchantmen** (pale hulls, blue flags) sail between ports. One that comes alongside hails her
with two or three offers. Take the ones you want before she sails on.

## Crew

The Wanderer starts with hammocks for 8. The Quarters branch adds 4, 6, 8 and 12, for 38 in all.

Castaways, tavern hires and beaten pirates' crews sign on while there is room. Each hand rolls a
role and a rarity (common, uncommon, rare, legendary). The role gives a perk that stacks across
the crew.

| Role | Perk | Unlocked by |
|---|---|---|
| Cartographer | Chart value | start |
| Lookout | Sight, and sails spotted sooner | start |
| Navigator | Speed | start |
| Carpenter | Repairs | start |
| Forager | Landing hauls and salvage | start |
| Gunner | Gun damage and reloads | Gunnery |
| Boarder | Boarding strength | Quarters |
| Cook | Less food eaten | Articles |

## Battles

Pirates hide in the fog, and more ride out of fog banks as the voyage goes on. They are stronger
far from home and get stronger with time. From minute 15, notorious raiders appear; their names
end in "the Grim", "the Butcher" and so on. They are a good deal stronger and quicker than the
waters around them.

When black sails close in, an encounter card shows both ships side by side and gives these odds:

- **Fight:** her chance to win, to be lost, or for one side to break off.
- **Flee:** her chance to get away. If they catch her, they fire first, and the card shows her
  chance of being lost then.

The battle itself is watch-only. The two ships close, circle and trade broadsides:

- **Shot:** round shot holes the hull, chain shot tears sails (and slows the ship), and grapeshot
  cuts down crews.
- **Luck:** every battle rolls its own luck for each side, and now and then a lucky shot does
  heavy damage.
- **Breaking off:** she breaks off when badly holed, or when the other ship has the hands to board
  her.
- **The chase:** getting clear takes speed. A quicker ship fires chain at her as she runs, runs her
  down, and boards her or forces her to turn and fight.
- **Boarding:** the side with more crew strength wins the melee. Beaten pirates strike their
  colours, are sunk, or are taken by boarding. Some of their crew sign on, and she takes their
  plunder.

**Decide for me** (on the card, or under Battles in the menu) fights when the risk of losing her
is under 5%, and otherwise takes whichever choice is less likely to lose her.

Reloading the page mid-battle resumes the battle. Losing deletes the voyage and its save.

## Charts & Rigging

Eight branches. Each node costs gold plus goods and opens the next one in its branch.

- **Rigging:** speed, and better odds of getting away.
- **Crow's Nest:** sight, charting pace, spotting sails sooner.
- **Hull:** hull, armour, repairs.
- **Holds:** hold space, chart value, salvage, treasure maps.
- **Quarters:** crew capacity, boarders, rare and legendary recruits.
- **Gunnery:** guns, reload, accuracy, chain shot, grapeshot, damage.
- **Articles:** crew perks, cooks, boarding, grog.
- **Pilotage:** shoals, kelp and doldrums, then the Wraith Lantern.

## The Drowned Crown

The Crown sits inside a ring of green wraith-mist. Twelve compasses narrow a search ring around
it.

The Wraith Lantern is the last Pilotage node. It costs 150k gold, 100 metal and 50 rum, and lets
her into the mist. Once it is lit she makes for the Crown, stopping only if she runs short of food
or shot. Holding the Crown for 3 seconds wins.

## Pacing

Measured with a test bot that buys every upgrade it can afford and lets "decide for me" pick its
battles:

- **Careful play:** won 9 of 10 seeds, in 48–79 minutes. The one loss was a fight shown at 3% risk.
- **Fighting every pirate:** lost 2 of 4 seeds.
- **No rigging, hull, guns or quarters:** lost every seed tried, by minute 21–49.

## Architecture notes

- **Saves:** `fogbound.v4` in localStorage (the keys keep the old name so saves carry over). A save keeps the seed, an RLE diff of the fog, hands,
  goods, ports, landing sites, items, rivals and any battle in progress. The old `fogbound.v2`
  save is never touched.
- **Test mode:** `?test` in the URL saves to `fogbound.test` instead, so test runs stay apart
  from a real voyage.
- **The battle model:** an abstract model (range, hull, sails, crew, reload) decides every
  battle. It runs headless a few hundred times to compute the odds on the encounter card, and live
  it drives the two ships on the map.
- **Fog texture:** RGBA 1024². Display density and wraith mist. Elevation is another texture
  (R height, G kelp, B doldrums).
- **Map mode:** past a camera distance of about 330 the shaders blend into a chart with ship,
  port, beacon and item icons.
- **Debug hooks:** `window.__fog` has `step(seconds)`, `buildSea(seed)`, `nodes`, `nodeState`,
  `buyNode`, `give`, `spawnRival`, `encounter`, `decide`, `battleOdds`, `trade`, `undock`,
  `portUsual` and others. A hidden browser tab never fires `requestAnimationFrame`, so automated
  tests drive the simulation through these.

## Local development

```bash
python -m http.server 8123
```

Run it from the `Game Apps` folder, then open `http://localhost:8123/Sea%20Wanderer/`.
`.claude/launch.json` starts that server by name (`gameapps`). `file://` works too, but saves may
not.
