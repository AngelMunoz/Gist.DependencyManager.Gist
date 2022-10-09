namespace Gist.DependencyManager.Gist

open System
open Gist.DependencyManager.Gist.Github

module Attributes =
    /// A marker attribute to tell FCS that this assembly contains a Dependency Manager, or
    /// that a class with the attribute is a DependencyManager
    [<AttributeUsage(AttributeTargets.Assembly ||| AttributeTargets.Class, AllowMultiple = false)>]
    type DependencyManagerAttribute() =
        inherit Attribute()

    [<assembly: DependencyManager>]
    do ()

module private Logger =
    let private stdout = lazy ResizeArray<string>()

    let private stderr = lazy ResizeArray<string>()

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
        printfn $"%A{outputDir}\n%A{scriptDir}\n%A{packageManagerTextLines |> List.ofSeq}\n%A{targetFramework}"

        let gists =
            packageManagerTextLines
            |> Seq.choose (fun line ->
                match line.Split(",") with
                | [| Github.IsGistId id; Github.IsGistId revision |] ->
                    Logger.log $"Processing Gist: {id}/{revision}"
                    Some(id, Some revision)
                | [| Github.IsGistId id |] ->
                    Logger.log $"Processing Gist: {id}"
                    Some(id, None)
                | _ ->
                    Logger.error $"Can't process Gist: {line}"
                    None)
            |> Seq.map (fun (id, revision) -> Github.fetchGistData (id, ?revision = revision))
        // TODO: Write these to Disk and tell FSI where are the files located
        printfn "%A" (gists |> List.ofSeq)
        let stdout, stderr = Logger.getLogs ()
        ResolveDependenciesResult(true, stdout, stderr, List.empty, List.empty, List.empty)
