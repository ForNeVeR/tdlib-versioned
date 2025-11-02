// SPDX-FileCopyrightText: 2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: BSL-1.0

#r "nuget: MedallionShell, 1.6.2"

open System
open System.Text
open Medallion.Shell

let upstreamRepoRemote = "https://github.com/tdlib/td.git"

let command =
    Command.Run(
        "git",
        "ls-remote", "--tags", upstreamRepoRemote
    ).Result
if not command.Success then
    failwithf $"git ls-remote failed with exit code {command.ExitCode}."

let extractTagName(ref: string) =
    let prefix = "refs/tags/"
    if ref.StartsWith prefix then ref.Substring prefix.Length
    else failwithf $"Cannot parse ref: \"{ref}\"."

let tags =
    command.StandardOutput.Split("\n", StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
    |> Array.map(fun line ->
        let components = line.Split('\t', 2)
        match components with
        | [| commit; tag |] -> commit, extractTagName tag
        | _ -> failwithf $"Cannot parse git ls-remote output: \"{line}\"."
    )

[<CLIMutable>]
type ReleaseMetadata = {
    Tag: string
    Commit: string
}

open System.Text.Json
open System.IO

let releases =
    tags
    |> Seq.map(fun (commit, tag) ->
        { Tag = tag; Commit = commit }
    )
    |> Seq.sortBy _.Tag
    |> Seq.toArray

let releasesJsonPath = Path.Combine(__SOURCE_DIRECTORY__, "../data/releases.json")

let previousReleases =
    File.ReadAllText releasesJsonPath
    |> JsonSerializer.Deserialize<ReleaseMetadata[]>

let options = JsonSerializerOptions(WriteIndented = true)
let jsonString = JsonSerializer.Serialize(releases, options) + "\n"
File.WriteAllText(releasesJsonPath, jsonString)

let prevReleaseTags = previousReleases |> Seq.map _.Tag |> Set.ofSeq
let newReleaseTags = releases |> Seq.map _.Tag |> Set.ofSeq
let removedReleases = prevReleaseTags - newReleaseTags
let addedReleases = newReleaseTags - prevReleaseTags

let prevRelMap = previousReleases |> Seq.map(fun r -> r.Tag, r.Commit) |> Map.ofSeq
let newRelMap = releases |> Seq.map(fun r -> r.Tag, r.Commit) |> Map.ofSeq

let changedReleases =
    previousReleases |> Seq.map(
        fun p ->
            match Map.tryFind p.Tag newRelMap with
            | None -> None
            | Some commit when commit = p.Commit -> None
            | Some _ -> Some p.Tag
    ) |> Seq.collect Option.toArray
    |> Seq.toArray

let gitHubOutput = Environment.GetEnvironmentVariable "GITHUB_OUTPUT"
let summary =
    match removedReleases.Count, addedReleases.Count, changedReleases.Length with
    | 0, 0, 0 -> "<no changes>"
    | 0, 1, 0 -> $"Add release {Seq.exactlyOne addedReleases}"
    | r, a, c ->
        let capitalize(s: string) =
            s[0].ToString().ToUpperInvariant() + s.Substring 1
        [|
            if r > 0 then $"removed {r} releases"
            if a > 0 then $"added {a} releases"
            if c > 0 then $"changed {c} releases"
        |] |> String.concat ", " |> capitalize
let detailedDescription =
    let sb = StringBuilder()
    let appendLine x = sb.Append $"{x}\n" |> ignore
    for tag in removedReleases do
        appendLine $"- remove release {tag}"
    for tag in addedReleases do
        appendLine $"- add release {tag}"
    for tag in changedReleases do
        appendLine $"- changed release {tag} from {prevRelMap[tag]} to {newRelMap[tag]}"
    sb.ToString()

let commitMessage = summary + "\n\n" + detailedDescription

let writeResults() =
    let output = Environment.GetEnvironmentVariable "GITHUB_OUTPUT" |> Option.ofObj
    use outputStream =
        match output with
        | None ->
            printfn "No GITHUB_OUTPUT env var provided, results will be written to the stdard output."
            Console.OpenStandardOutput()
        | Some path -> File.OpenWrite path

    use writer = new StreamWriter(outputStream)
    let serializeParameter key (value: string) =
        if value.Contains("\n") then
            let delimiter = Guid.NewGuid()
            writer.Write $"{key}<<{delimiter}\n{value}\n{delimiter}\n"
        else
            writer.Write $"{key}={value}\n"
    serializeParameter "title" summary
    serializeParameter "commit-message" commitMessage
    serializeParameter "body" detailedDescription

writeResults()
