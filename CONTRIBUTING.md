# Contributing to RPMulate

Thanks for your interest in contributing to RPMulate.

RPMulate is a tool to mount disk images and simulate various drive types and speeds. This document explains how to contribute changes, report issues, and propose improvements.

## Ways to contribute

You can contribute by:

- Reporting bugs
- Suggesting features or usability improvements
- Improving documentation
- Submitting bug fixes
- Adding tests
- Refining drive simulation behavior or compatibility

## Before you start

Please:

- Check existing issues and pull requests before starting work
- Open an issue for substantial changes so the approach can be discussed first
- Keep pull requests focused on a single change whenever possible

## Development setup

Because this repository is written in C#, a typical setup includes:

- A recent .NET SDK
- An IDE or editor such as Visual Studio, Rider, or VS Code
- Git

General setup steps:

1. Fork the repository
2. Clone your fork
3. Create a feature branch for your change
4. Restore dependencies
5. Build the project
6. Run any available tests

Example workflow:

```bash
git clone https://github.com/michaelmadell/RPMulate.git
cd RPMulate
git checkout -b my-change
```

## Coding guidelines

Please aim to:

- Follow the existing code style and project structure
- Prefer clear, small, focused changes
- Use descriptive names for classes, methods, variables, and commits
- Avoid unrelated refactors in the same pull request
- Add or update comments only where they improve maintainability

## Testing

Before submitting a pull request:

- Build the project successfully
- Run relevant tests if present
- Manually verify behavior for the scenarios your change affects
- Include tests for new behavior when practical

If your change affects disk mounting, media emulation, or timing/speed simulation, include notes in the pull request describing how you validated it.

## Pull request guidelines

When opening a pull request:

- Explain what changed and why
- Link related issues when applicable
- Keep the scope limited and reviewable
- Include screenshots or logs if they help explain the change
- Note any follow-up work that is intentionally out of scope

A good pull request description includes:

- Summary of the change
- Motivation/background
- Testing performed
- Any compatibility considerations

## Reporting bugs

When filing a bug report, please include:

- A clear summary of the problem
- Steps to reproduce
- Expected behavior
- Actual behavior
- Environment details (OS, .NET version, app version)
- Example disk image or configuration details if relevant
- Logs, screenshots, or error messages when available

## Suggesting features

For feature requests, please describe:

- The problem you are trying to solve
- The proposed behavior
- Why it would be useful
- Any alternatives you considered

## Documentation contributions

Documentation improvements are welcome. If you notice unclear setup steps, missing examples, or behavior that should be documented, feel free to open a pull request.

## Code of conduct

Please be respectful and constructive in all discussions and reviews.

## Questions

If you are unsure whether a change is a good fit, open an issue first to discuss it.
