# Contributing

Thanks for helping improve MBS Terminal. The project is intentionally small, so the best contributions are focused, practical, and easy to review.

## Before You Start

- Check existing issues and pull requests to avoid duplicate work.
- Open an issue first for large changes, installer behavior changes, or anything that affects a user's existing terminal profile.
- Keep pull requests scoped to one improvement or fix.

## Local Setup

Use Windows with Windows PowerShell 5.1 or later.

```powershell
git clone https://github.com/mbs047/MBS-Terminal.git
cd MBS-Terminal
```

Run the installer from the repository root:

```powershell
.\install.ps1
```

To test dependency installation behavior:

```powershell
.\install.ps1 -InstallDependencies
```

To rebuild the launcher executable:

```powershell
.\build-installer.ps1
```

## Contribution Checklist

- Test installer changes on a Windows machine or clearly document what was not tested.
- Keep generated backups, logs, and temporary files out of commits.
- Update `README.md` when setup steps, behavior, or requirements change.
- Include screenshots when visual terminal changes are part of the pull request.

## Style Notes

- Prefer clear PowerShell over clever shortcuts.
- Keep scripts safe for user machines: check paths before writing, keep backups where practical, and avoid destructive defaults.
- Use ASCII text unless a file already uses another character set for a clear reason.
