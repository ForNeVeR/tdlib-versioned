let licenseHeader = """
# SPDX-FileCopyrightText: 2025 Friedrich von Never <friedrich@fornever.me>
#
# SPDX-License-Identifier: BSL-1.0

# This file is auto-generated.""".Trim()

#r "nuget: Generaptor, 1.8.0"
open System

open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands

let workflows = [
    workflow "main" [
        header licenseHeader
        name "Main"

        let mainBranch = "main"
        onPushTo mainBranch
        onPullRequestTo mainBranch
        onSchedule(day = DayOfWeek.Saturday)
        onWorkflowDispatch

        job "check-encoding" [
            runsOn "ubuntu-24.04"
            step(
                name = "Check out the sources",
                usesSpec = Auto "actions/checkout"
            )
            step(
                name = "Verify encoding",
                shell = "pwsh",
                run = "Install-Module VerifyEncoding -Repository PSGallery -RequiredVersion 2.2.1 -Force && Test-Encoding"
            )
        ]

        job "check-licenses" [
            runsOn "ubuntu-24.04"
            step(
                name = "Check out the sources",
                usesSpec = Auto "actions/checkout"
            )
            step(
                name = "REUSE license check",
                usesSpec = Auto "fsfe/reuse-action"
            )
        ]

        job "check-workflows" [
            runsOn "ubuntu-24.04"

            setEnv "DOTNET_CLI_TELEMETRY_OPTOUT" "1"
            setEnv "DOTNET_NOLOGO" "1"
            setEnv "NUGET_PACKAGES" "${{ github.workspace }}/.github/nuget-packages"
            step(
                usesSpec = Auto "actions/checkout"
            )
            step(
                usesSpec = Auto "actions/setup-dotnet"
            )
            step(
                run = "dotnet fsi ./scripts/github-actions.fsx verify"
            )
        ]
    ]
]

exit <| EntryPoint.Process fsi.CommandLineArgs workflows
