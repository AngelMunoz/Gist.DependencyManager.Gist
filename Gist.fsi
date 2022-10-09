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

[<Class>]
type Github =
    static member ClearCache: unit -> unit
    static member fetchGistData: id: string * ?revision: string * ?token: string -> Result<GistInfo, FetchGistError>
    static member fetchFile: file: GistFile * ?token: string -> Result<string, FetchGistError>
    static member fetchAllGistFiles:
        gist: GistInfo * ?token: string -> GistInfo * (GistFile * string) array * FetchGistError array
    static member fetchSelectFiles:
        gist: GistInfo * files: string array * ?token: string ->
            GistInfo * (GistFile * string) array * FetchGistError array

module Github =

    val extractOptionsFromString: rline: string -> string option * string option * string[] option * string option
