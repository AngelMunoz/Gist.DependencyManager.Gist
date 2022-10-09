#r "nuget: FsMake, 0.6.1"

open System
open System.IO

open FsMake

let args = fsi.CommandLineArgs
let root = __SOURCE_DIRECTORY__

let clean =
    Step.create "clean" {
        try
            Directory.Delete($"{root}/dist", true)
        with _ ->
            ()

        do! Cmd.createWithArgs "dotnet" [ "clean"; "src"; "-v"; "m" ] |> Cmd.run
    }

let restore =
    Step.create "restore" { do! Cmd.createWithArgs "dotnet" [ "restore"; "src" ] |> Cmd.run }

let build =
    Step.create "build" { do! Cmd.createWithArgs "dotnet" [ "build"; "src"; "--no-restore" ] |> Cmd.run }

let publish =
    Step.create "publish" {
        do!
            Cmd.createWithArgs "dotnet" [ "publish"; "src"; "-o"; "./dist"; "--no-restore" ]
            |> Cmd.run
    }

let runTestScript =
    Step.create "test:unit" {
        do!
            Cmd.createWithArgs "dotnet" [ "fsi"; "--compilertool:./dist"; "test.fsx" ]
            |> Cmd.run
    }


Pipelines.create {
    let! build =
        Pipeline.create "build" {
            run clean
            run restore
            run build
        }

    let! publish = Pipeline.createFrom build "publish" { run publish }

    let! testScript = Pipeline.createFrom publish "test" { run runTestScript }

    default_pipeline testScript
}
|> Pipelines.runWithArgsAndExit args
