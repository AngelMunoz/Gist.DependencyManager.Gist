namespace Gist.DependencyManager.Gist

open System
open System.IO
open Gist.DependencyManager.Gist.Github

module Attributes =
    /// A marker attribute to tell FCS that this assembly contains a Dependency Manager, or
    /// that a class with the attribute is a DependencyManager
    [<AttributeUsage(AttributeTargets.Assembly
                     ||| AttributeTargets.Class,
                     AllowMultiple = false)>]
    type DependencyManagerAttribute() =
        inherit Attribute()

    [<assembly: DependencyManager>]
    do ()

module Logger =
    let private stdout =
        lazy ResizeArray<string>()

    let private stderr =
        lazy ResizeArray<string>()

    let log msg = stdout.Value.Add(msg)
    let error msg = stderr.Value.Add(msg)

    let getLogs () =
        (stdout.Value |> Seq.toArray, stderr.Value |> Seq.toArray)


/// The results of ResolveDependencies
type ResolveDependenciesResult
    (
        success: bool,
        stdOut: string array,
        stdError: string array,
        resolutions: string seq,
        sourceFiles: string seq,
        roots: string seq
    ) =

    /// Succeded?
    member _.Success = success

    /// The resolution output log
    member _.StdOut = stdOut

    /// The resolution error log (* process stderror *)
    member _.StdError = stdError

    /// The resolution paths
    member _.Resolutions = resolutions

    /// The source code file paths
    member _.SourceFiles = sourceFiles

    /// The roots to package directories
    member _.Roots = roots


[<Attributes.DependencyManager>]
type GistDependencyManagerProvider(outputDir: string option) =

    member val Name = "Github Gist Scripts Dependency Manager"
    member val Key = "gist"

    member val HelpMessages = [| "Gist Dependency Manager for F# scritps" |]

    member _.ClearResultsCache() = printfn "Clearing Cache"

    member _.ResolveDependencies
        (
            scriptDir: string,
            mainScriptName: string,
            scriptName: string,
            packageManagerTextLines: string seq,
            targetFramework: string
        ) : ResolveDependenciesResult =
        let outDir =
            match outputDir with
            | Some outputDir -> Path.Combine(outputDir, ".gists")
            | None -> Path.Combine(Environment.CurrentDirectory, ".gists")

        let gists =
            packageManagerTextLines
            |> Seq.choose (fun line ->
                let id, revision, files, token =
                    Github.extractOptionsFromString line

                match id with
                | None ->
                    Logger.error $"Can't process dependency: {line}"
                    None
                | Some id ->
                    match Github.fetchGistData (id, ?revision = revision, ?token = token) with
                    | Ok gist ->
                        let files = (defaultArg files Array.empty)

                        Some(
                            gist,
                            (if files.Length > 0 then
                                 GistContents.Files files
                             else
                                 GistContents.All),
                            revision,
                            token
                        )
                    | Error err ->
                        Logger.error $"Can't process dependency [{line}]: {err}"
                        None)

        let gists =
            gists
            |> Seq.map (fun (gist, contents, revision, token) ->
                let (info, files, errors) =
                    match contents with
                    | GistContents.All -> Github.fetchAllGistFiles (gist, ?token = token)
                    | GistContents.Files files -> Github.fetchSelectFiles (gist, files, ?token = token)

                info, files, errors, revision)

        let files =
            gists
            |> Seq.map (fun (gist, gistFiles, errors, revision) ->
                let revision = defaultArg revision "default"

                let directory =
                    Path.Combine(outDir, gist.id, revision)
                Logger.log $"Gist contents available under {directory}"

                Directory.CreateDirectory(directory) |> ignore

                for error in errors do
                    match error with
                    | FetchGistError.NotFound -> Logger.error $"Failed to fetch files from gist {gist.id}"
                    | FetchGistError.GithubError statusCode ->
                        Logger.error $"Failed to fetch files from gist, github sent us code %A{statusCode}"
                    | FetchGistError.SerializationError err ->
                        let title =
                            $"Serialization error for Gist: {gist.id}"

                        let body =
                            $"## Please fill any missing information\n### Automated bug Info:\nFailed to deserialize\n```{err}```\nGistUrl:{gist.url}"

                        let ghUrl =
                            $"https://github.com/AngelMunoz/Gist.DependencyManager.Gist/issues/new?title={title}&body={body}"

                        Logger.error
                            $"Fetched File but filed to deserialize information, this is likely a bug please report via the following url:\n{ghUrl}"

                let paths = ResizeArray()

                for file, content in gistFiles do
                    let path =
                        Path.Combine(directory, file.filename)

                    File.WriteAllText(path, content)
                    paths.Add(path)

                paths)

        let files =
            [ for file in files do
                  yield! file ]

        let stdout, stderr = Logger.getLogs ()
        ResolveDependenciesResult(true, stdout, stderr, List.empty, files, List.empty)
