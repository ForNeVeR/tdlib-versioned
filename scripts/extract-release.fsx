// SPDX-FileCopyrightText: 2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: BSL-1.0

#r "nuget: TruePath, 1.11.0"
#r "nuget: TruePath.SystemIo, 1.10.0"
#r "nuget: Octokit, 14.0.0"

open System
open System.IO
open System.Text.Json
open Octokit
open TruePath
open TruePath.SystemIo

let repositoryOwner = "ForNeVeR"
let repositoryName = "tdlib-versioned"

let repositoryRoot = AbsolutePath(__SOURCE_DIRECTORY__) / ".."
let releasesJson = repositoryRoot / "data" / "releases.json"

[<CLIMutable>]
type ReleaseMetadata = {
    Tag: string
    Commit: string
    Source: string
    Comment: string | null
}

let existingReleasesTask = task {
    printfn "Fetching releases…"

    let client = GitHubClient(ProductHeaderValue("tdlib-versioned"))
    match Environment.GetEnvironmentVariable "GITHUB_TOKEN" |> Option.ofObj with
    | Some token ->
        printfn "Found GitHub token credentials in environment, will use."
        client.Credentials <- Credentials token
    | None -> ()

    let! result = client.Repository.Release.GetAll(repositoryOwner, repositoryName)
    printfn "Releases fetched."
    return result
}

let requiredReleases =
    releasesJson.ReadAllText()
    |> JsonSerializer.Deserialize<ReleaseMetadata[]>

let versionFromRelease release =
    if not <| release.Tag.StartsWith "v" then failwithf $"Unexpected tag name: {release.Tag}."
    Version.Parse(release.Tag.Substring 1)

let existingReleases = existingReleasesTask.Result |> Seq.map _.TagName |> Set.ofSeq
let missingReleases =
    requiredReleases
    |> Seq.filter (fun release ->
        let tag = $"tdlib/{release.Tag}"
        not (existingReleases.Contains tag)
    )
    |> Seq.sortBy versionFromRelease
    |> Seq.toArray

printfn $"{missingReleases.Length} missing releases to create."

let releaseToCreate = missingReleases |> Array.tryHead
let releaseMetadata = releaseToCreate |> Option.map(fun release ->
    let makeLatest =
        let maxVersion =
            requiredReleases
            |> Seq.map versionFromRelease
            |> Seq.max
        let currentVersion = versionFromRelease release
        currentVersion = maxVersion

    {|
        Name = release.Tag
        Body = release.Comment |> Option.ofObj |> Option.defaultValue ""
        TagName = $"tdlib/{release.Tag}"
        Commit = release.Commit
        MakeLatest = makeLatest

        // Release for a version ending with `.0` is expected to contain a `Source = "tag"`.
        // Otherwise, create it as a draft and wait until TDLib marks it properly.
        // But only for the latest version (others are expected to be filled anyway).
        IsDraft = makeLatest && release.Source = "derived-from-commit-data" && release.Tag.EndsWith ".0"
    |}
)

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

    let fromBool b = if b then "true" else "false"

    match releaseMetadata with
    | None ->
        serializeParameter "has-changes" <| fromBool false
    | Some release ->
        serializeParameter "has-changes" <| fromBool true
        serializeParameter "name" release.Name
        serializeParameter "body" release.Body
        serializeParameter "tag-name" release.TagName
        serializeParameter "commit" release.Commit
        serializeParameter "make-latest" <| fromBool release.MakeLatest
        serializeParameter "draft" <| fromBool release.IsDraft

writeResults()
