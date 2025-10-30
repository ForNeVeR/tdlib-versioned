<!--
SPDX-FileCopyrightText: 2024-2025 Friedrich von Never <friedrich@fornever.me>

SPDX-License-Identifier: BSL-1.0
-->

tdlib-versioned [![Status Zero][status-zero]][andivionian-status-classifier]
===============
This repository contains a fork of Telegram's [TDLib][tdlib], but with version tags attached.

The TDLib team several times refused to add such tags:
- [#913: Git tags for minor versions][tdlib-913],
- [#1627: git tag patch versions][tdlib-1627],
- [#1790: Put a 1.7.10 tag][tdlib-1790],
- [#2696: Could you please make the tag regularly after every update?][tdlib-2696]

Meaning that we, the community, need to act on our own.

Versioning Policy
-----------------
The general idea here is to have reasonable steps between versions. To avoid confusion, we aim to completely agree with TDLib official versioning where it makes sense, but provide more precise versioning.

Currently, since the main TDLib repository has a `VERSION` field in their build files, we track compatibility by this file. We reserve the right to adjust this scheme in the future as necessary (e.g., if the upstream stops updating this file).

Currently, any commit pushed to the upstream TDLib that increments the version number in the `CMakeLists.txt` is considered as **the commit introducing this new version** and should be tagged. Any conflicts will be resolved on a case-by-case-basis.

License
-------
The project is distributed under the terms of [the Boost Software License 1.0][docs.license].

The license indication in the project's sources is compliant with the [REUSE specification v3.3][reuse.spec].

[andivionian-status-classifier]: https://andivionian.fornever.me/v1/#status-zero-
[docs.license]: LICENSE.txt
[reuse.spec]: https://reuse.software/spec-3.3/
[reuse]: https://reuse.software/
[status-zero]: https://img.shields.io/badge/status-zero-lightgrey.svg
[tdlib-1627]: https://github.com/tdlib/td/issues/1627
[tdlib-1790]: https://github.com/tdlib/td/issues/1790
[tdlib-2696]: https://github.com/tdlib/td/issues/2696
[tdlib-913]: https://github.com/tdlib/td/issues/913
[tdlib]: https://github.com/tdlib/td