// SPDX-FileCopyrightText: 2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: BSL-1.0

#r "nuget: TruePath, 1.10.0"
#r "nuget: TruePath.SystemIo, 1.10.0"
#r "nuget: MedallionShell, 1.6.2"

open System
open System.Text.Json
open Medallion.Shell
open TruePath
open TruePath.SystemIo

let tagFolder = "tdlib"

let args = fsi.CommandLineArgs |> Array.skip 1
let whatIf =
    match args with
    | [| "--what-if"; "false" |] -> false
    | [| "--what-if"; "true" |] -> true
    | _ ->
        printfn "Required arguments:\n--what-if <true|false> - true to preview changes, false to apply them immediately."
        exit 1

let repositoryRoot = AbsolutePath(__SOURCE_DIRECTORY__) / ".."
let releasesJson = repositoryRoot / "data" / "releases.json"

[<CLIMutable>]
type ReleaseMetadata = {
    Tag: string
    Commit: string
}

let releases =
    releasesJson.ReadAllText()
    |> JsonSerializer.Deserialize<ReleaseMetadata[]>

let existingTags =
    let command = Command.Run("git", "tag", "--list", $"{tagFolder}/*").Result
    if not command.Success then
        failwithf $"git tag failed with exit code {command.ExitCode}."

    command.StandardOutput.Split("\n", StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
    |> Set.ofSeq

let getRevisionForTag(tag: string) =
    let command = Command.Run("git", "rev-parse", tag).Result
    if not command.Success then
        failwithf $"git rev-parse failed with exit code {command.ExitCode}."
    command.StandardOutput.Trim()

let tagsToExist = releases |> Seq.map (fun release -> $"{tagFolder}/{release.Tag}", release.Commit) |> Map.ofSeq

let tagsToChange =
    // tags with releases but that don't correspond to correct hashes
    existingTags
    |> Seq.filter tagsToExist.ContainsKey
    |> Seq.filter(fun tag ->
        let existingRevision = getRevisionForTag tag
        let requiredRevision = tagsToExist[tag]
        not <| String.Equals(existingRevision, requiredRevision, StringComparison.OrdinalIgnoreCase)
    )
    |> Seq.toArray

let tagsToRemove =
    // tags without releases
    existingTags |> Seq.filter(fun tag -> not <| tagsToExist.ContainsKey tag) |> Seq.toArray

let tagsToAdd =
    tagsToExist.Keys |> Seq.filter(not << existingTags.Contains) |> Seq.toArray

printfn $"Change report: needed to add {tagsToAdd.Length} tags, remove {tagsToRemove.Length} tags, change {tagsToChange.Length} tags."

if tagsToChange.Length > 0 || tagsToRemove.Length > 0 then
    failwithf $"Assertion failed: it is required to remove tags [{tagsToRemove}] and change tags [{tagsToChange}]."

for tag in tagsToAdd do
    let commit = tagsToExist[tag]
    printfn $"Executing git tag {tag} {commit}…"
    if not whatIf then
        let command = Command.Run("git", "tag", tag, commit).Result
        if not command.Success then failwithf $"Cannot create tag, exit code {command.ExitCode}. Standard error: \n{command.StandardError}"

if tagsToAdd.Length > 0 then
    if whatIf then
        printfn $"About to create {tagsToAdd.Length} tags."
    else
        printfn $"Successfully created {tagsToAdd.Length} tags."
        let command = Command.Run("git", "push", "--tags").Result
        if not command.Success then failwithf $"Cannot push tags, exit code {command.ExitCode}."
        printfn $"%s{command.StandardOutput}"
