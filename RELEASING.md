# Publishing PenguinComb & creating a release

Everything below is done from your machine with `git` and (optionally) the
GitHub web UI. The repository is already set up for publishing: GPL-3.0
`LICENSE`, a two-part README, a `build` workflow (every push/PR) and a
`release` workflow (tag pushes) that attaches the Linux AppImage, the Linux
tarball and the Windows archive to the GitHub Release page.

## 0. Pre-flight (already verified)

Before the first push, confirm the repo works from a fresh checkout:

```sh
git clone --recursive <repository-url>
cd <repository>
./scripts/init-deps.sh          # applies the GH-Toolkit cross-platform patch
dotnet restore PenguinComb.sln
dotnet build PenguinComb.sln -c Release
dotnet test PenguinComb.sln -c Release
```

This exact sequence is what CI runs, and it is green.

## 1. Create the repository on GitHub

1. GitHub → **New repository**.
2. Name it (e.g. `penguincomb`), set it to **Public**.
3. **Do not** tick "Add a README", ".gitignore" or "license" — the repository
   already has all three and GitHub will detect the GPL-3.0 license from the
   committed `LICENSE`.
4. Create the repository, then from your local checkout:

```sh
git remote add origin https://github.com/<you>/penguincomb.git
git push -u origin main
```

5. Open the **Actions** tab — the `build` workflow runs on the push
   (Linux + Windows matrix) and should finish green. It restores, builds,
   tests, publishes both platforms, builds the AppImage, and fails if
   WinForms/Registry code leaks into shared projects or resource casing is
   wrong.

## 2. Create the first release

Releases are triggered by a tag matching `v*`:

```sh
# Bump the version FIRST in src/PenguinComb.App/PenguinComb.App.csproj
# (<Version>1.0.0</Version>) — the AppImage name uses it.
git tag v1.0.0
git push origin v1.0.0
```

The `release` workflow then runs on a Linux runner and:

1. Publishes `linux-x64` and `win-x64` (cross-published) and builds the
   single-file AppImage,
2. Verifies the three assets exist,
3. Creates a **GitHub Release** with them attached (auto-generated release
   notes from the commits since the previous tag).

When the workflow finishes, open the **Releases** page and polish the notes:
a short "what this is" line, the quick start (Clone Hero → Better GH3), and
a pointer that **`PenguinComb-<version>-x86_64.AppImage` is the file most
Linux users should download** (one file, no .NET/FUSE needed).

## 3. Post-release polish (recommended)

* Repository **About** section: description ("Cross-platform Guitar Hero
  custom-song toolkit — Linux & Windows"), homepage if you have one.
* **Topics**: `guitar-hero`, `gh3`, `clone-hero`, `dotnet`, `avalonia`,
  `linux`, `gpl-3-0`.
* Confirm the **GPL-3.0** license is detected on the repo page.
* Keep releases in sync: bump `<Version>` in the App csproj for every release
  (the AppImage and package names follow it), then tag `vX.Y.Z`.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `build` workflow fails on one OS | Open the failing job's log; re-run the job (Actions → Re-run). Usually environmental. |
| Release not created after tag | The `release` workflow needs the tag on the default branch push; check the workflow log for the `softprops/action-gh-release` step. `contents: write` is already granted. |
| AppImage step fails | `appimagetool` is downloaded during the build; if the download fails, re-run the job. |
| Fresh clone fails to restore/build | Run `git submodule update --init --recursive` then `./scripts/init-deps.sh` and rebuild. The GH-Toolkit submodule is pinned; do not update it without re-verifying and regenerating `patches/gh-toolkit-crossplatform.patch`. |
| Something looks like the old name | The project is PenguinComb; `Honeycomb` should only appear in attribution to the original `Honeycomb-GUI`/`Honeycomb-CLI` by AddyMills and in `legacy/`. |
