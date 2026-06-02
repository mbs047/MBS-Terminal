# Support

Use GitHub issues for support requests, bug reports, and setup questions.

## Good Support Requests Include

- Windows version.
- Windows Terminal version.
- PowerShell version.
- Whether Starship is installed.
- The command you ran.
- The exact error message or screenshot.

## Before Opening An Issue

- On a fresh PC, run `.\MBS-Terminal-Install.cmd` from the repository root.
- Re-run the installer from the repository root.
- If the GUI executable does not open, run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install-terminal.ps1`.
- Try `.\install.ps1 -InstallDependencies` if Starship or PSReadLine is missing.
- Check whether Windows Terminal has a `settings.json` file in one of its standard locations.
- Avoid sharing private folder paths, tokens, or full profile files if they contain sensitive information.
