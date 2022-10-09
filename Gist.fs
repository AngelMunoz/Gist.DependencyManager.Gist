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

module private Gist =

    [<Literal>]
    let User_Agent = "Gist.DependencyManager.Gist.Github"

    [<Literal>]
    let Gist_Api_Url = "https://api.github.com/gists"


    let GistCache = lazy ConcurrentDictionary<string, GistInfo>()


    let fetchGist (key: string) =
        http {
            GET $"{Gist_Api_Url}/{key}"
            UserAgent User_Agent
            Accept "application/vnd.github+json"
        }
        |> Request.send
        |> Response.assertOk
        |> Response.deserializeJson<GistInfo>

    let fetchGistFile (url: string) =
        http {
            GET url
            UserAgent User_Agent
        }
        |> Request.send
        |> Response.assertOk
        |> Response.toText

type Github =

    static member ClearCache() = Gist.GistCache.Value.Clear()

    static member fetchGistData(id: string, ?revision: string) : Result<GistInfo, FetchGistError> =
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
                let info = Gist.fetchGist key

                Gist.GistCache.Value.TryAdd(key, info) |> ignore
                Ok info
            with
            | :? StatusCodeExpectedxception as ex ->
                match ex.Data0.actual with
                | HttpStatusCode.NotFound -> Error NotFound
                | code -> Error(GithubError code)
            | ex -> Error(SerializationError ex.Message)


    static member fetchFile(file: GistFile) : Result<string, FetchGistError> =
        try
            if not file.truncated then
                Ok file.content
            else
                Gist.fetchGistFile file.raw_url |> Ok
        with
        | :? StatusCodeExpectedxception as ex ->
            match ex.Data0.actual with
            | HttpStatusCode.NotFound -> Error NotFound
            | code -> Error(GithubError code)
        | ex -> Error(SerializationError ex.Message)

    static member fetchAllGistFiles(gist: GistInfo) : (string * string) array * FetchGistError array =
        let ops =
            gist.files
            |> Map.toList
            |> List.map (fun (name, file) -> name, (Github.fetchFile file))

        let results = ResizeArray()
        let errors = ResizeArray()

        for key, value in ops do
            match value with
            | Ok file -> results.Add(key, file)
            | Error err -> errors.Add(err)

        results |> Seq.toArray, errors |> Seq.toArray

module Github =
    let GistIdRegex = Regex(@"^[0-9A-Fa-f]{32,}$")

    let (|IsGistId|NotGistId|) (value: string) =
        let trimmed = value.Trim()

        if GistIdRegex.IsMatch(trimmed) then
            IsGistId trimmed
        else
            NotGistId
