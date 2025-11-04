// SPDX-FileCopyrightText: 2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: BSL-1.0

#r "nuget: MedallionShell, 1.6.2"
#r "nuget: Fenrir.Git, 1.0.0"
#r "nuget: TruePath.SystemIo, 1.10.0"

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.RegularExpressions
open FSharp.Control
open Fenrir.Git
open Fenrir.Git.Metadata
open JetBrains.Lifetimes
open Medallion.Shell
open TruePath
open TruePath.SystemIo

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

let guessTags() = Lifetime.UsingAsync(fun lt -> task {
    printfn "Enumerating commits…"
    let dotGit = AbsolutePath(__SOURCE_DIRECTORY__) / "../.git" |> LocalPath

    let index = PackIndex(lt, dotGit)
    let cMakeVersionRegex = Regex(@"project\(TdLib VERSION (.+?) LANGUAGES CXX C\)", RegexOptions.Compiled)
    let getTdLibVersion(commit: Commit) = task {
        let commitTree = commit.Body.Tree
        let! commitTreeBody = Trees.ReadTreeBody(index, dotGit, commitTree)
        let cMakeLists = commitTreeBody |> Seq.filter(fun x -> x.Name = "CMakeLists.txt") |> Seq.exactlyOne
        let! cMakeListsContent =
            let object = Objects.GetRawObjectPath(dotGit, cMakeLists.Hash).ResolveToCurrentDirectory()
            if object.Exists()
            then object.ReadAllTextAsync()
            else task {
                let! packed = Packing.ReadPackedObject(index, cMakeLists.Hash)
                use packed = packed
                use reader = new StreamReader(packed.Stream)
                return! reader.ReadToEndAsync()
            }

        let matches = cMakeVersionRegex.Match cMakeListsContent
        if not matches.Success
        then failwithf $"Cannot match CMakeLists.txt content with version regex. Content:\n{cMakeListsContent}"

        return matches.Groups[1].Value
    }


    let branch = Refs.ReadRefs dotGit |> Seq.filter(fun x -> x.Name = "refs/remotes/origin/tdlib") |> Seq.exactlyOne
    let headCommitHash = branch.CommitObjectId
    let! headCommit = Commits.ReadCommit(index, dotGit, headCommitHash)

    let! allCommits = Commits.TraverseCommits(dotGit, headCommitHash) |> AsyncSeq.ofAsyncEnum |> AsyncSeq.toArrayAsync
    printfn $"Total commits: {allCommits.Length}."

    printfn "Discovering versions…"
    let commitsIntroducingVersions = Dictionary<string, Sha1Hash>()
    let! currentVersion = getTdLibVersion headCommit
    let mutable lastVersion = currentVersion
    let mutable lastCommit = headCommitHash

    let visitVersion newVersion =
        if Some lastVersion <> newVersion then
            match commitsIntroducingVersions.TryGetValue lastVersion with
            | true, commit -> failwithf $"Two commits introducing the same version {lastVersion}: {commit} and {lastCommit}."
            | false, _ -> commitsIntroducingVersions.Add(lastVersion, lastCommit)

    for commit in allCommits do
        let! version = getTdLibVersion commit
        visitVersion(Some version)

        lastCommit <- commit.Hash
        lastVersion <- version

    visitVersion None

    printfn $"Discovered {commitsIntroducingVersions.Count} versions."
    return commitsIntroducingVersions
})

let guessedTags = guessTags().Result

[<CLIMutable>]
type ReleaseMetadata = {
    Tag: string
    Commit: string
}

open System.Text.Json

let metaFromCommits =
    guessedTags
    |> Seq.map(fun kvp -> kvp.Key, kvp.Value.ToString())
    |> Seq.toArray

let metaFromTags = tags

printfn $"Producing result from {metaFromCommits.Length} releases derived from commits and {metaFromTags.Length} releases derived from tags."

let releases =
    let dict = Dictionary<string, string>()
    for tag, commit in metaFromCommits do dict[tag] <- commit
    for tag, commit in metaFromTags do dict[tag] <- commit

    dict
    |> Seq.map(fun kvp -> { Tag = kvp.Key; Commit = kvp.Value })
    |> Seq.sortBy(fun r ->
        if not <| r.Tag.StartsWith "v" then failwithf $"Unexpected tag name: {r.Tag}."
        Version.Parse(r.Tag.Substring 1)
    )
    |> Seq.toArray

printfn $"Resulting set: {releases.Length} releases."

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
