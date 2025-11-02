let licenseHeader = """
# SPDX-FileCopyrightText: 2025 Friedrich von Never <friedrich@fornever.me>
#
# SPDX-License-Identifier: BSL-1.0

# This file is auto-generated.""".Trim()

#r "nuget: Generaptor, 1.9.0"

open System
open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands

let workflows = [
    let workflow (displayName: string) body =
        workflow (displayName.ToLower()) [
            header licenseHeader
            name displayName

            let mainBranch = "main"
            onPushTo mainBranch
            onPullRequestTo mainBranch
            onWorkflowDispatch

            yield! body
        ]

    let linuxSourceJob name body =
        job name [
            runsOn "ubuntu-24.04"
            step(
                name = "Check out the sources",
                usesSpec = Auto "actions/checkout"
            )
            yield! body
        ]

    let powerShell name command =
        step(
            name = name,
            shell = "pwsh",
            run = command
        )

    let withCondition condition = function
        | AddStep ({ Condition = None } as step) ->
            AddStep { step with Condition = Some condition }
        | AddStep step -> failwith $"Step {step} has a condition already: {step.Condition}."
        | x -> x

    let withManualOrScheduleCondition = withCondition "github.event_name == 'schedule' || github.event_name == 'workflow_dispatch'"

    workflow "Main" [
        onSchedule(day = DayOfWeek.Saturday)

        linuxSourceJob "check-encoding" [
            powerShell "Verify encoding"
                "Install-Module VerifyEncoding -Repository PSGallery -RequiredVersion 2.2.1 -Force && Test-Encoding"
        ]

        linuxSourceJob "check-licenses" [
            step(
                name = "REUSE license check",
                usesSpec = Auto "fsfe/reuse-action"
            )
        ]

        linuxSourceJob "check-workflows" [
            setEnv "DOTNET_CLI_TELEMETRY_OPTOUT" "1"
            setEnv "DOTNET_NOLOGO" "1"
            setEnv "NUGET_PACKAGES" "${{ github.workspace }}/.github/nuget-packages"
            step(
                name = "Set up .NET SDK",
                usesSpec = Auto "actions/setup-dotnet"
            )
            powerShell "Verify workflows"
                "dotnet fsi ./scripts/github-actions.fsx verify"
        ]

        job "push-tags" [
            runsOn "ubuntu-24.04"
            jobPermission(PermissionKind.Contents, AccessKind.Write)
            step(
                name = "Check out the sources",
                usesSpec = Auto "actions/checkout",
                options = Map.ofList [
                    "fetch-depth", "0"
                ]
            )

            powerShell "Prepare tags"
                "dotnet fsi ./scripts/update-tags.fsx --what-if ${{ github.ref != 'refs/heads/main' }}"
        ]
    ]

    workflow "Maintenance" [
        onSchedule(cron = "0 0 * * *") // every day

        linuxSourceJob "clone-upstream" [
            jobPermission(PermissionKind.Contents, AccessKind.Write)
            jobPermission(PermissionKind.PullRequests, AccessKind.Write)

            powerShell "Clone upstream repository"
                "./scripts/Update-Upstream.ps1"

            powerShell "Push new commits"
                "git push origin tdlib"
            |> withManualOrScheduleCondition

            step(
                id = "extract-metadata",
                name = "Extract metadata",
                shell = "pwsh",
                run = "dotnet fsi ./scripts/extract-metadata.fsx"
            )

            step(
                name = "Create a pull request",
                usesSpec = Auto "peter-evans/create-pull-request",
                options = Map.ofList [
                    "branch", "new-metadata"
                    "author", "TdLibVersioned automation <friedrich@fornever.me>"
                    "title", "${{ steps.extract-metadata.outputs.title }}"
                    "commit-message", "${{ steps.extract-metadata.outputs.commit-message }}"
                    "body", "${{ steps.extract-metadata.outputs.body }}"
                ]
            )
            |> withManualOrScheduleCondition
        ]
    ]

    workflow "Release" [
        onPushTags "tdlib/*"

        job "release" [
            runsOn "ubuntu-24.04"

            step(
                id = "version",
                name = "Read version from Git ref",
                shell = "pwsh",
                run = "echo \"version=$(if ($env:GITHUB_REF.StartsWith('refs/tags/v')) { $env:GITHUB_REF -replace '^refs/tags/v', '' } else { 'next' })\" >> $env:GITHUB_OUTPUT"
            )

            step(
                name = "Prepare a release",
                usesSpec = Auto "softprops/action-gh-release"
            )
            |> withManualOrScheduleCondition
        ]
    ]
]

exit <| EntryPoint.Process fsi.CommandLineArgs workflows
