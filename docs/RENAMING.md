# RENAMING.md - the finale steps of the Taimi's Toolbench rename

The repository content was renamed from GW2 Crafting Helper /
`GW2CraftingHelper` to Taimi's Toolbench / `TaimisToolbench` in the
`taimis-toolbench-v2` branch (see the CHANGELOG 0.3.0 entry). This file
is the runbook for everything the content sweep could NOT do: steps that
touch GitHub, the local dev environment, and the owner's live Blish HUD
install. Execute top to bottom, after the rename PR merges.

(An earlier sweep of the same rename shipped as PR #200 on the
`taimis-toolbench` branch. Master moved 25 commits past it, and a rename
touching this many files does not merge across that gap, so the sweep
was re-derived from the current tree instead. Close PR #200 as
superseded and delete its branch and its `wt-rename` worktree.)

This file names the old identifiers on purpose - it is the migration
record. It is on the CI tripwire's allowlist for that reason.

---

## 1. GitHub repo rename

GitHub redirects the old URL, old remotes, and old clone/fetch/push
endpoints automatically after a rename, so nothing breaks mid-sequence.

```bash
# from any checkout of the repo
"/mnt/c/Program Files/GitHub CLI/gh.exe" repo rename TaimisToolbench -R MaximusCub/GW2CraftingHelper --yes
```

Then point the local clone (and any live worktrees) at the new URL so
nothing keeps leaning on the redirect:

```bash
cd /mnt/c/Dev/Blish/GW2CraftingHelper
git remote set-url origin https://github.com/MaximusCub/TaimisToolbench.git
git remote -v   # verify
```

Worktrees share the clone's remotes; no per-worktree action needed.
The manifest `url`, README badges, and issue-template links already
point at `MaximusCub/TaimisToolbench` - they go live the moment this
step runs.

## 2. Local folder rename and junction re-link

The clone folder renames to match. Close Visual Studio / any shells
holding handles under the folder first, and remove the rename worktrees
once their PRs are settled:

```bash
git worktree remove /mnt/c/Dev/Blish/wt-rename2   # this rename's worktree
git worktree remove /mnt/c/Dev/Blish/wt-rename    # the superseded PR #200 worktree
```

Then:

```bash
cd /mnt/c/Dev/Blish
mv GW2CraftingHelper TaimisToolbench
```

The `packages` NuGet folder is reached via junctions from worktrees.
Re-create any junction that pointed at the old folder path. For a
future worktree:

```bash
cmd.exe /c "mklink /J C:\\Dev\\Blish\\<worktree>\\packages C:\\Dev\\Blish\\TaimisToolbench\\packages"
```

Then, IN THE RENAMED CLONE, one commit that finishes the path story:

- `CLAUDE.md` line 12: `C:/Dev/Blish/GW2CraftingHelper/...` becomes
  `C:/Dev/Blish/TaimisToolbench/...`.
- `.github/workflows/tests.yml`, rename-tripwire step: remove
  `CLAUDE.md` from the allowlist (its entry is marked PENDING and
  exists only for that folder path).

Out-of-repo scripts that embed the old repo path (re-measured
2026-08-27, all under `C:\Dev\Blish\`):

- `preflight/launch-sandbox.ps1`
- `preflight/launch_m37.ps1`
- `preflight/start_blish.ps1`
- `blish-preflight-settings/settings.json` (sandbox module-state key,
  see step 4 for the shape)

Update the `C:\Dev\Blish\GW2CraftingHelper` paths (and any
`GW2CraftingHelper.bhm` module paths) in those to the new names. The
sandbox's own `blish-preflight-settings/logs/*.log` also carry the old
name; they are dated logs and need no edit.

Claude memory continuity: the project memory directory is keyed to the
opened folder path, so the rename forks it. Old:
`~/.claude/projects/-mnt-c-Dev-Blish-GW2CraftingHelper/memory/`. After
the first session in the renamed folder, leave a pointer note in BOTH
the old MEMORY.md and the new one, and copy the memory files across so
the new key starts with the full history.

## 3. Deploy the new .bhm

Build from the renamed clone:

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build C:/Dev/Blish/TaimisToolbench/TaimisToolbench.csproj -p:Platform=x64 -c Release
```

Output: `bin/x64/Release/TaimisToolbench.bhm`. If the tree was built
before the rename, delete `bin/` and `obj/` first: an incremental
OutDir keeps the pre-rename `GW2CraftingHelper.dll` beside the new one
and packs both into the `.bhm`.

## 4. Blish install migration (owner's live install)

Blish HUD must NOT be running for any of this.

Measured layout (re-measured 2026-08-27), all under
`C:\Users\lachl\Documents\Guild Wars 2\addons\blishhud\`:

- `modules\GW2CraftingHelper.bhm` - delete after the new deploy
- `modules\GW2CraftingHelper.bhm.aug25-backup` - delete
- `data\` - the module's data directory (plan.json, plan_history,
  plan_history.json, ranker.json, recipe_cache, snapshot.json,
  module_log.jsonl, status.txt). NOT keyed by namespace: the manifest
  registers the directory NAME `data` (`"directories": ["data"]`), and
  Blish creates it directly under `addons\blishhud\`. The renamed
  manifest registers the same name, so this directory carries over with
  NO migration. Do not move or copy anything here.
- `settings.json` - the ONE thing keyed by namespace. The module's
  enabled state, granted API permissions, and every module setting
  live under `Entries[Key=ModuleConfiguration].Value.Entries[Key=`
  `ModuleStates].Value.GW2CraftingHelper`. Rename that key to
  `TaimisToolbench` and everything (Enabled, UserEnabledPermissions,
  ValueOwnMaterials, homestead tiers, log settings, plan-history cap,
  snapshot interval, click volume, ...) migrates intact. The API key
  itself lives in `Gw2WebApiConfiguration.ApiKeyRepository` keyed by
  ACCOUNT name, not namespace - untouched by the rename.

Exact sequence:

```bash
B="/mnt/c/Users/lachl/Documents/Guild Wars 2/addons/blishhud"

# 4a. backup settings first
cp "$B/settings.json" "$B/settings.json.pre-rename-backup"

# 4b. rename the module-state key old namespace -> new namespace
python3 - "$B/settings.json" <<'PY'
import json, sys
p = sys.argv[1]
s = json.load(open(p, encoding="utf-8"))
mc = next(e for e in s["Entries"] if e["Key"] == "ModuleConfiguration")
ms = next(e for e in mc["Value"]["Entries"] if e["Key"] == "ModuleStates")
states = ms["Value"]
assert "GW2CraftingHelper" in states, "old key not found - already migrated?"
assert "TaimisToolbench" not in states, "new key already exists - stop and look"
states["TaimisToolbench"] = states.pop("GW2CraftingHelper")
json.dump(s, open(p, "w", encoding="utf-8"), indent=2)
print("migrated:", ", ".join(states))
PY

# 4c. deploy the new module, remove the old
cp /mnt/c/Dev/Blish/TaimisToolbench/bin/x64/Release/TaimisToolbench.bhm "$B/modules/"
rm "$B/modules/GW2CraftingHelper.bhm" "$B/modules/GW2CraftingHelper.bhm.aug25-backup"
```

Optional tidy: `settings.json` also holds an orphaned window-position
entry keyed `GW2CraftingHelper_ModalDialog_c4f19a` (the renamed code
writes `TaimisToolbench_ModalDialog_...` keys instead). Confirmed still
present on 2026-08-27. Harmless to leave; delete the entry if tidying.

Then launch Blish, confirm the module lists as "Taimi's Toolbench",
loads enabled with its API permissions intact, and that the Plan
History / Ranker / recipe cache data all survived (they live in
`data\`, which never moved).

## 5. Ship v0.3.0

Only after steps 1-4 are verified:

```bash
cd /mnt/c/Dev/Blish/TaimisToolbench
git tag v0.3.0 && git push origin v0.3.0
```

The release workflow gates on the full test suite, checks the tag
against `manifest.json` (0.3.0), extracts the CHANGELOG 0.3.0 section
(which carries the rename entry) as the release body, and publishes
`TaimisToolbench.bhm` - the first release under the new name.
