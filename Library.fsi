namespace Gist.DependencyManager.Gist

open System

module Attributes =
    /// A marker attribute to tell FCS that this assembly contains a Dependency Manager, or
    /// that a class with the attribute is a DependencyManager
    [<AttributeUsage(AttributeTargets.Assembly ||| AttributeTargets.Class, AllowMultiple = false)>]
    type DependencyManagerAttribute =
        new: unit -> DependencyManagerAttribute
        inherit Attribute

module Logger =
    val log: msg: string -> unit
    val error: msg: string -> unit
    val getLogs: unit -> string[] * string[]

/// The results of ResolveDependencies
type ResolveDependenciesResult =
    new:
        success: bool *
        stdOut: string array *
        stdError: string array *
        resolutions: seq<string> *
        sourceFiles: seq<string> *
        roots: seq<string> ->
            ResolveDependenciesResult
    /// Succeded?
    member Success: bool
    /// The resolution output log
    member StdOut: string array
    /// The resolution error log (* process stderror *)
    member StdError: string array
    /// The resolution paths
    member Resolutions: seq<string>
    /// The source code file paths
    member SourceFiles: seq<string>
    /// The roots to package directories
    member Roots: seq<string>

[<Attributes.DependencyManager>]
type GistDependencyManagerProvider =
    new: outputDir: string option -> GistDependencyManagerProvider
    member Name: string
    member Key: string
    member HelpMessages: string[]
    member ClearResultsCache: unit -> unit
    member ResolveDependencies:
        scriptDir: string *
        mainScriptName: string *
        scriptName: string *
        packageManagerTextLines: seq<string> *
        targetFramework: string ->
            ResolveDependenciesResult
