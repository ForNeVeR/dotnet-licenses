<!--
SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>

SPDX-License-Identifier: MIT
-->

Changelog
=========
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html). See [the README file][docs.readme] for more details on how it is versioned.

## [Unreleased]
### Added
- Support the standard `--help` and `--version` command-line options.
- Add configuration file support, the file path being passed through the command line.

  Configuration file options available in this version: `inputs`, `overrides`, `lock_file`, and `package`.
- License lock file support for the packaged files.
- Supported commands: `print` (default) and `generate-lock`.
- Warnings and non-zero exit codes on duplicate and unused overrides.
- First version of the lock file format.

## [0.0.0]
This is the first version of the tool. It does nothing but prints a message to the console, mostly prepared to bootstrap the automated publishing process.

[docs.readme]: README.md

[0.0.0]: https://github.com/ForNeVeR/dotnet-licenses/releases/tag/v0.0.0
[Unreleased]: https://github.com/ForNeVeR/dotnet-licenses/compare/v0.0.0...HEAD
