namespace Gist.DependencyManager.Gist.Github


open System.Collections.Concurrent
open System.Collections.Generic
open System.Net
open System.Text.Json.Serialization
open System.Text.RegularExpressions

open FsHttp

type GistFile =
    { filename: string
      ``type``: string
      language: string
      raw_url: string
      size: int64
      truncated: bool
      content: string }

[<CLIMutable>]
type GistInfo =
    { url: string
      id: string
      files: Map<string, GistFile>
      [<JsonExtensionData>]
      extras: Dictionary<string, obj> }

[<Struct>]
type FetchGistError =
    | NotFound
    | GithubError of statusCode: HttpStatusCode
    | SerializationError of error: string

type GistContents =
    | All
    | Files of string array

module Gist =

    [<Literal>]
    let User_Agent = "Gist.DependencyManager.Gist.Github"

    [<Literal>]
    let Gist_Api_Url = "https://api.github.com/gists"


    let GistCache = lazy ConcurrentDictionary<string, GistInfo>()


    let fetchGist (key: string) (token: string option) =
        let ctx =
            match token with
            | None ->
                http {
                    GET $"{Gist_Api_Url}/{key}"
                    UserAgent User_Agent
                    Accept "application/vnd.github+json"
                }
            | Some token ->
                http {
                    GET $"{Gist_Api_Url}/{key}"
                    UserAgent User_Agent
                    Accept "application/vnd.github+json"
                    AuthorizationBearer token
                }

        ctx |> Request.send |> Response.assertOk |> Response.deserializeJson<GistInfo>

    let fetchGistFile (url: string) (token: string option) =
        let ctx =
            match token with
            | None ->
                http {
                    GET url
                    UserAgent User_Agent
                }
            | Some token ->
                http {
                    GET url
                    UserAgent User_Agent
                    AuthorizationBearer token
                }

        ctx |> Request.send |> Response.assertOk |> Response.toText

type Github =

    static member ClearCache() = Gist.GistCache.Value.Clear()

    static member fetchGistData(id: string, ?revision: string, ?token: string) : Result<GistInfo, FetchGistError> =
        let revision =
            let revision = defaultArg revision ""

            if System.String.IsNullOrWhiteSpace revision then
                ""
            else
                $"/{revision}"

        let key = $"{id}{revision}"

        match Gist.GistCache.Value.TryGetValue(key) with
        | true, value -> Ok value
        | false, _ ->
            try
                let info = Gist.fetchGist key token

                Gist.GistCache.Value.TryAdd(key, info) |> ignore
                Ok info
            with
            | :? StatusCodeExpectedxception as ex ->
                match ex.Data0.actual with
                | HttpStatusCode.NotFound -> Error NotFound
                | code -> Error(GithubError code)
            | ex -> Error(SerializationError ex.Message)


    static member fetchFile(file: GistFile, ?token: string) : Result<string, FetchGistError> =
        try
            if not file.truncated then
                Ok file.content
            else
                Gist.fetchGistFile file.raw_url token |> Ok
        with
        | :? StatusCodeExpectedxception as ex ->
            match ex.Data0.actual with
            | HttpStatusCode.NotFound -> Error NotFound
            | code -> Error(GithubError code)
        | ex -> Error(SerializationError ex.Message)

    static member fetchAllGistFiles
        (
            gist: GistInfo,
            ?token: string
        ) : GistInfo * (GistFile * string) array * FetchGistError array =
        let ops =
            gist.files
            |> Map.toList
            |> List.map (fun (_, file) -> file, Github.fetchFile (file, ?token = token))

        let results = ResizeArray()
        let errors = ResizeArray()

        for key, value in ops do
            match value with
            | Ok file -> results.Add(key, file)
            | Error err -> errors.Add(err)

        gist, results |> Seq.toArray, errors |> Seq.toArray

    static member fetchSelectFiles
        (
            gist: GistInfo,
            files: string array,
            ?token: string
        ) : GistInfo * (GistFile * string) array * FetchGistError array =
        let files =
            files
            |> Array.choose (fun file -> gist.files |> Map.tryFind file)
            |> Array.map (fun file -> file, Github.fetchFile (file, ?token = token))

        let results = ResizeArray()
        let errors = ResizeArray()

        for key, value in files do
            match value with
            | Ok file -> results.Add(key, file)
            | Error err -> errors.Add(err)

        gist, results |> Seq.toArray, errors |> Seq.toArray

module Github =
    let GistIdRegex = Regex(@"^[0-9A-Fa-f]{32,}$")

    let (|IsGistId|NotGistId|) (value: string) =
        let trimmed = value.Trim()

        if GistIdRegex.IsMatch(trimmed) then
            IsGistId trimmed
        else
            NotGistId

    let getRevision (line: string) =
        if not (line.StartsWith("revision=") || line.StartsWith("Revision=")) then
            None
        else
            line.Substring(9).Trim() |> Some

    let getFiles (line: string) =
        if not (line.StartsWith("files=") || line.StartsWith("Files=")) then
            None
        else
            line.Substring(6).Trim().Split(";") |> Array.map (fun s -> s.Trim()) |> Some

    let getGhToken (line: string) =
        if
            not (
                line.StartsWith("ghtoken=")
                || line.StartsWith("GhToken=")
                || line.StartsWith("Ghtoken=")
            )
        then
            None
        else
            line.Substring(8).Trim() |> Some

    let getId (line: string) =
        match line with
        | IsGistId line -> Some line
        | _ -> None

    let extractOptionsFromString (rline: string) =
        let split = rline.Split(",")
        let gistId = split |> Array.tryPick getId
        let files = split |> Array.tryPick getFiles

        let token = split |> Array.tryPick getGhToken

        let revision = split |> Array.tryPick getRevision

        (gistId, revision, files, token)
