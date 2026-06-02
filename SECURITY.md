# Security Policy

MBS Terminal installs shell configuration, Windows Terminal settings, prompt configuration, and local helper scripts. Treat changes to installer behavior as security-sensitive because they run on a contributor's machine.

## Supported Versions

The `main` branch is the supported version. Older snapshots are not patched separately.

## Reporting a Vulnerability

If GitHub private vulnerability reporting is enabled for this repository, please use it. If it is not available, open a public issue that asks for a secure contact path without including exploit details, secrets, or sensitive logs.

Please include:

- The affected file or feature.
- Steps to reproduce, if safe to share privately.
- The expected and actual behavior.
- Any impact you believe the issue may have.

## Safety Expectations

- Do not post access tokens, private paths, or personal profile contents in public issues.
- Do not include proof-of-concept code that modifies a user's profile or terminal settings without clear warnings.
- Prefer small, reviewable security fixes.
- Use a feature branch and pull request for repository changes; direct commits to `main` are blocked locally by the checked-in Git hook when repository guards are enabled.
