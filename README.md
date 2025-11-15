<!--
SPDX-FileCopyrightText: 2024-2025 Friedrich von Never <friedrich@fornever.me>

SPDX-License-Identifier: BSL-1.0
-->

tdlib-versioned [![Status Aquana][status-aquana]][andivionian-status-classifier] [![Latest release][release-badge]][releases]
===============
This repository contains a fork of Telegram's [TDLib][tdlib], but with intermediate version tags attached.

The TDLib team has stated their official position that they [don't want people to rely on the intermediate versions][tdlib-position], and want people to use the latest commit from `master` instead when possible:

> It would be wrong to tag these commits. Telegram is developed continuously and those aren't the commits you are looking for.
>
> The TDLib version is increased the same day a Telegram update is released to denote significant changes in the TDLib API. This happens along with push of source code with all new features inplemented. After that there can be a lot of improvements and bug fixes before the next release. Therefore the exact commit, in which the version was changed, isn't recommended for usage to anyone. In fact, if you want to use the most stable TDLib 1.X.Y, you need to search for the first commit with that version and message "Update/Increase layer to ..." and use the commit just before that.
>
> But everyone should tend to use as new TDLib version is possible. Telegram updates frequently and new features aren't available in old TDLib versions. Moreover, due to server-side changes or introduction of new features some previously working features can be broken.
>
> So, basically, each time a version is stable enough and can be marked with a tag, there is a newer version with the latest features, which should be used instead. Most of the time, the current master version is the TDLib version, which should be used.

This is not always practical, though. Meaning we, the community, should act on our own on this. While we recommend you to listen to the TDLib team whenever possible, for the cases when it is _not_ possible, feel free to rely on this repository, its releases, and tags.

Some other related discussions are:
- [#913: Git tags for minor versions][tdlib-913],
- [#1627: git tag patch versions][tdlib-1627],
- [#1790: Put a 1.7.10 tag][tdlib-1790],
- [#2696: Could you please make the tag regularly after every update?][tdlib-2696]

Versioning Policy
-----------------
The general idea here is to have reasonable steps between versions. To avoid confusion, we aim to completely agree with TDLib official versioning, but provide more detailed version information (all minor and patch versions, or more).

Currently, since the main TDLib repository has a `VERSION` field in their build files, we track compatibility by this file. We reserve the right to adjust this scheme in the future as necessary (e.g., if the upstream stops updating this file).

Currently, any commit pushed to the upstream TDLib that increments the version number in the `CMakeLists.txt` is considered as **the commit introducing this new version** and should be tagged. Any conflicts will be resolved on a case-by-case-basis.

We may add additional `<revision>` numbers after TDLib's main version, resulting in the notation `<major>.<minor>.<build>.<revsion>`, as necessary (e.g., when TDLib adds a build or security fixes after a tagged commit).

Usage
-----
### How to Get Code of a Particular Version of TDLib
> [!IMPORTANT]
> Note that the code in this repository is not endorsed by the TDLib team in any way.
>
> If you are looking to check for the authenticity of the version information, please see the next section after this one.
>
> We recommend always double-checking the authenticity of the code you receive from any third-party sources.

The simplest way is to clone this repository and check out a versioned tag. For example, if you are looking for sources of v1.8.0, you can run the following shell commands:
```console
$ git clone https://github.com/ForNeVeR/tdlib-versioned.git
$ cd tdlib-versioned
$ git checkout tdlib/v1.8.0
```

All the tags have the form of `tdlib/v*`.

### How to Check The Authenticity of Code
All the versioned tags in this repository correspond to the code in [the official TDLib repository][tdlib]. Meaning that you can check if the commit annotated by a version in this repository is contained in the main repository. Here's an example of how you can do that:
```console
$ git clone git@github.com:tdlib/td.git
$ cd td
$ git ls-remote --tags https://github.com/ForNeVeR/tdlib-versioned.git
[…]
b3ab664a18f8611f4dfcd3054717504271eeaa7a        refs/tags/tdlib/v1.8.0
$ git show b3ab664a18f8611f4dfcd3054717504271eeaa7a
[…]
```

In this example, you clone the main repository of TDLib, retrieve the version information on 1.8.0 from this repository via `git ls-remote`, and then use `git show` to verify that a commit with this id indeed exists in the main TDLib repository.

In fact, you don't need tdlib-versioned repository at all; you can only use it for `git ls-remote` calls, and then rely on the code in the main TDLib repository.

### How to Subscribe to Updates
For ease of subscription, this repository will publish any new tag in the [Releases][releases] section.

Log in to GitHub and use the **Watch** control in the top right corner of the page, choose **Custom** set of subscription options, and check the box to be notified of **Releases** only (or any other options as you need).

### How to Compare Releases
The [Releases][releases] section allows comparing any two tags, so you can use that if you want to see what exactly has been changed between releases.

For example, here's [a link comparing the releases 1.8.55 and 1.8.56][release-comparison-example].

### Release Information
The [`releases.json`][releases.json] file contains a list of the following objects for each release:
```json
[
    {
        "Tag": "v1.0.0",
        "Commit": "71d03f39c364367a8a7c51f783a41099297de826",
        "Date": "2018-12-31T22:04:05.0000000\u002B03:00",
        "Source": "tag",
        "Comment": "optional"
    }
]
```
where
- `Tag` is the name of the release tag in this repository (for tag with actual name `tdlib/v1.0.0`, `Tag` field will contain `v1.0.0`);
- `Commit` is the commit hash for this release;
- `Date` is an ISO-formatted commit date (optional — for information only);
- `Source` is either:
  - `"tag"` for releases officially created by tags in the upstream repostitory,
  - `"derived-from-commit-data"` for tags that are automatically derived from the contents of the `CMakeLists.txt`,
  - or `"manual"` for tags created manually by the tdlib-versioned maintainers;
- `Comment` is an optional field containing a comment from the maintainers.

Documentation
-------------
- [Contributor Guide][docs.contributing]

License
-------
The project is distributed under the terms of [the Boost Software License 1.0][docs.license].

The license indication in the project's sources is compliant with the [REUSE specification v3.3][reuse.spec].

Note that this message only concerns the contents of the `main` branch. The `tdlib` branch is daily synchronized with the TDLib upstream. While currently using the same BSL-1.0 license, it doesn't make any promises of REUSE compatibility or keeping the current licensing.

[andivionian-status-classifier]: https://andivionian.fornever.me/v1/#status-aquana-
[docs.contributing]: CONTRIBUTING.md
[docs.license]: LICENSE.txt
[release-badge]: https://img.shields.io/github/v/release/ForNeVeR/tdlib-versioned
[release-comparison-example]: https://github.com/ForNeVeR/tdlib-versioned/compare/tdlib/v1.8.55...tdlib/v1.8.56
[releases.json]: data/releases.json
[releases]: https://github.com/ForNeVeR/tdlib-versioned/releases
[reuse.spec]: https://reuse.software/spec-3.3/
[reuse]: https://reuse.software/
[status-aquana]: https://img.shields.io/badge/status-aquana-yellowgreen.svg
[tdlib-1627]: https://github.com/tdlib/td/issues/1627
[tdlib-1790]: https://github.com/tdlib/td/issues/1790
[tdlib-2696]: https://github.com/tdlib/td/issues/2696
[tdlib-913]: https://github.com/tdlib/td/issues/913
[tdlib-position]: https://github.com/tdlib/td/issues/1627#issuecomment-885993462
[tdlib]: https://github.com/tdlib/td
